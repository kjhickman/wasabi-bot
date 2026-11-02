import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  MessageFlags,
  type APIApplicationCommandInteractionDataBasicOption,
  type APIApplicationCommandOption,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { FunctionTool } from 'openai/resources/responses/responses';
import type { Env } from '../env.ts';
import { commands, runCommand } from './index.ts';

const JSON_TYPES: Partial<Record<ApplicationCommandOptionType, string>> = {
  [ApplicationCommandOptionType.String]: 'string',
  [ApplicationCommandOptionType.Integer]: 'integer',
  [ApplicationCommandOptionType.Number]: 'number',
  [ApplicationCommandOptionType.Boolean]: 'boolean',
};
const DISCORD_MAX_LENGTH = 6_000;

type Definition = RESTPostAPIChatInputApplicationCommandsJSONBody;
type Value = string | number | boolean | null;

// Chat can't offer commands limited to some members, or options that only Discord can
// fill in (users, channels, roles, attachments, subcommands).
export function commandTool(definition: Definition): FunctionTool | undefined {
  const options = definition.options ?? [];
  if (definition.default_member_permissions != null) return undefined;
  if (!options.every((option) => JSON_TYPES[option.type])) return undefined;
  return {
    type: 'function',
    name: definition.name,
    description: definition.description,
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
): Promise<string> {
  const definition: Definition | undefined = commands.find(
    (command) => command.definition.name === name,
  )?.definition;
  if (!definition || !commandTool(definition)) return 'Error: unknown command.';

  try {
    const args = JSON.parse(json) as Record<string, Value>;
    const options: APIApplicationCommandInteractionDataBasicOption[] = [];
    for (const option of definition.options ?? []) {
      const value = args[option.name];
      if (value == null) continue;
      // Strict mode can't enforce string lengths, so check them the way Discord would.
      if (option.type === ApplicationCommandOptionType.String && typeof value === 'string') {
        const { min_length: min = 0, max_length: max = DISCORD_MAX_LENGTH } = option;
        if (value.length < min || value.length > max) {
          return `Error: ${option.name} must be ${min} to ${max} characters long.`;
        }
      }
      options.push({
        name: option.name,
        type: option.type,
        value,
      } as APIApplicationCommandInteractionDataBasicOption);
    }
    const response = await runCommand(
      { id: '0', type: ApplicationCommandType.ChatInput, name, options },
      env,
      signal,
    );
    const content = response.data.content ?? '';
    return (response.data.flags ?? 0) & MessageFlags.Ephemeral ? `Error: ${content}` : content;
  } catch (error) {
    console.error('Chat command failed', name, error instanceof Error ? error.message : error);
    return 'Error: the command failed.';
  }
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
