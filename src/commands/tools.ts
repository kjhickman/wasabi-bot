import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  MessageFlags,
  type APIApplicationCommandInteractionDataBasicOption,
  type APIApplicationCommandOption,
  type APIAttachment,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { FunctionTool } from 'openai/resources/responses/responses';
import type { Env } from '../env.ts';
import { takeUsage, type Usage } from '../usage.ts';
import { commands, runCommand } from './index.ts';

const JSON_TYPES: Partial<Record<ApplicationCommandOptionType, string>> = {
  [ApplicationCommandOptionType.String]: 'string',
  [ApplicationCommandOptionType.Integer]: 'integer',
  [ApplicationCommandOptionType.Number]: 'number',
  [ApplicationCommandOptionType.Boolean]: 'boolean',
};
const DISCORD_MAX_LENGTH = 6_000;
const IMAGE_NOTE =
  'It uses the image attached to the last message, else the one on the message it replies to, else the most recent image in the channel.';

type Definition = RESTPostAPIChatInputApplicationCommandsJSONBody;
type Value = string | number | boolean | null;

// Chat can't offer commands limited to some members, or options that only Discord can
// fill in (users, channels, roles, subcommands). Chat fills attachment options itself
// with the image the conversation is about.
export function commandTool(definition: Definition): FunctionTool | undefined {
  const options = (definition.options ?? []).filter((option) => !isAttachment(option));
  if (definition.default_member_permissions != null) return undefined;
  if (!options.every((option) => JSON_TYPES[option.type])) return undefined;
  const usesImage = options.length < (definition.options?.length ?? 0);
  return {
    type: 'function',
    name: definition.name,
    description: usesImage ? `${definition.description} ${IMAGE_NOTE}` : definition.description,
    strict: true,
    parameters: {
      type: 'object',
      properties: Object.fromEntries(options.map((option) => [option.name, parameter(option)])),
      required: options.map((option) => option.name),
      additionalProperties: false,
    },
  };
}

export const commandTools = commands.flatMap((command) => commandTool(command.definition) ?? []);

export async function runCommandTool(
  name: string,
  json: string,
  env: Env,
  signal: AbortSignal,
  user: { id: string; sql: SqlStorage },
  image?: APIAttachment,
): Promise<{ output: string; files: File[] }> {
  const failure = (message: string) => ({ output: `Error: ${message}`, files: [] });
  const command = commands.find((command) => command.definition.name === name);
  if (!command || !commandTool(command.definition)) return failure('unknown command.');
  const definition: Definition = command.definition;

  let use: Usage | undefined;
  let succeeded = false;
  try {
    const args = JSON.parse(json) as Record<string, Value>;
    const options: APIApplicationCommandInteractionDataBasicOption[] = [];
    for (const option of definition.options ?? []) {
      if (isAttachment(option)) {
        if (image) {
          options.push({
            name: option.name,
            type: ApplicationCommandOptionType.Attachment,
            value: image.id,
          });
        }
        continue;
      }
      const value = args[option.name];
      if (value == null) continue;
      // Strict mode can't enforce string lengths, so check them the way Discord would.
      if (option.type === ApplicationCommandOptionType.String && typeof value === 'string') {
        const { min_length: min = 0, max_length: max = DISCORD_MAX_LENGTH } = option;
        if (value.length < min || value.length > max) {
          return failure(`${option.name} must be ${min} to ${max} characters long.`);
        }
      }
      options.push({
        name: option.name,
        type: option.type,
        value,
      } as APIApplicationCommandInteractionDataBasicOption);
    }
    // The chat reply already counts as a request, so only images count again here.
    if ('limit' in command && command.limit === 'image') {
      use = takeUsage(user.sql, 'image', user.id);
      if ('refused' in use) return failure(use.refused);
    }
    const response = await runCommand(
      {
        id: '0',
        type: ApplicationCommandType.ChatInput,
        name,
        options,
        ...(image && { resolved: { attachments: { [image.id]: image } } }),
      },
      env,
      signal,
    );
    const content = response.data.content ?? '';
    if ((response.data.flags ?? 0) & MessageFlags.Ephemeral) return failure(content);
    succeeded = true;
    const files = 'textOnlyInChat' in command ? [] : (response.files ?? []);
    const note = files.length ? '\nThe image is attached to your reply.' : '';
    return { output: content + note, files };
  } catch (error) {
    console.error('Chat command failed', name, error instanceof Error ? error.message : error);
    return failure('the command failed.');
  } finally {
    if (!succeeded && use && 'refund' in use) use.refund();
  }
}

function isAttachment(option: APIApplicationCommandOption) {
  return option.type === ApplicationCommandOptionType.Attachment;
}

function parameter(option: APIApplicationCommandOption) {
  const type = JSON_TYPES[option.type]!;
  const choices: Value[] | undefined =
    'choices' in option ? option.choices?.map((choice) => choice.value) : undefined;
  return {
    type: option.required ? type : [type, 'null'],
    description: option.description,
    ...(choices && { enum: option.required ? choices : [...choices, null] }),
    ...('min_value' in option && option.min_value !== undefined && { minimum: option.min_value }),
    ...('max_value' in option && option.max_value !== undefined && { maximum: option.max_value }),
  };
}
