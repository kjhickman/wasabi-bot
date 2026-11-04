import {
  ApplicationCommandOptionType,
  ApplicationCommandType,
  type APIApplicationCommandInteractionDataBasicOption,
  type APIApplicationCommandOption,
  type APIAttachment,
  type RESTPostAPIChatInputApplicationCommandsJSONBody,
} from 'discord-api-types/v10';
import type { FunctionTool } from 'openai/resources/responses/responses';
import type { Env } from '../env.ts';
import { log, preview } from '../log.ts';
import { takeUsage, type Usage } from '../usage.ts';
import { CommandError } from '../commands/command.ts';
import { commands, runCommand } from '../commands/index.ts';
import { searchStations } from '../radio/stations.ts';

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

// A tool runs a command, or one of its subcommands.
interface Entry {
  name: string;
  description: string;
  options: APIApplicationCommandOption[];
  subcommand?: string;
}

// A command with subcommands offers one tool per subcommand, named like `radio_play`.
function entries(definition: Definition): Entry[] {
  const { name, description, options = [] } = definition;
  const subcommands = options.filter(
    (option) => option.type === ApplicationCommandOptionType.Subcommand,
  );
  if (!subcommands.length) return [{ name, description, options }];
  return subcommands.map((subcommand) => ({
    name: `${name}_${subcommand.name}`,
    description: subcommand.description,
    options: subcommand.options ?? [],
    subcommand: subcommand.name,
  }));
}

function tool({ name, description, options: all }: Entry): FunctionTool | undefined {
  const options = all.filter((option) => !isAttachment(option));
  if (!options.every((option) => JSON_TYPES[option.type])) return undefined;
  const usesImage = options.length < all.length;
  return {
    type: 'function',
    name,
    description: usesImage ? `${description} ${IMAGE_NOTE}` : description,
    strict: true,
    parameters: {
      type: 'object',
      properties: Object.fromEntries(options.map((option) => [option.name, parameter(option)])),
      required: options.map((option) => option.name),
      additionalProperties: false,
    },
  };
}

// Chat can't offer commands limited to some members, or options that only Discord can
// fill in (users, channels, roles, subcommand groups). Chat fills attachment options
// itself with the image the conversation is about.
export function offers(definition: Definition) {
  if (definition.default_member_permissions != null) return [];
  return entries(definition).flatMap((entry) => {
    const offer = tool(entry);
    return offer ? [{ ...entry, tool: offer }] : [];
  });
}

const offered = commands.flatMap((command) =>
  offers(command.definition).map((entry) => ({ ...entry, command })),
);

export const commandTools = offered.map((entry) => entry.tool);

// Only chat has this: people browse stations with /radio play's autocomplete instead.
const radioSearch: FunctionTool = {
  type: 'function',
  name: 'radio_search',
  description:
    'Search internet radio stations by name or genre, most played first. When someone asks for a kind of music, search, pick the best match, and play it by passing its id as the station to radio_play.',
  strict: true,
  parameters: {
    type: 'object',
    properties: {
      query: { type: 'string', description: 'A station name or genre, like "classic rock"' },
    },
    required: ['query'],
    additionalProperties: false,
  },
};

export const chatTools = [...commandTools, radioSearch];

// How the reply's "used" note shows a command's tool: as the slash command someone would
// type. Other tools aren't noted.
export function commandLabel(name: string) {
  const entry = offered.find((entry) => entry.name === name);
  if (!entry) return undefined;
  return [entry.command.definition.name, entry.subcommand].filter(Boolean).join(' ');
}

export async function runTool(name: string, json: string, context: ToolContext) {
  if (name !== radioSearch.name) return runCommandTool(name, json, context);
  try {
    const query = (JSON.parse(json) as { query: string }).query.trim();
    const stations = await searchStations(query, context.signal);
    const output = stations.length ? JSON.stringify(stations) : `No stations matched "${query}".`;
    return { output, files: [] };
  } catch (error) {
    log.error('Chat radio search failed', { error, input: preview(json) });
    return { output: 'Error: the station search failed.', files: [] };
  }
}

export interface ToolContext {
  env: Env;
  sql: SqlStorage;
  userId: string;
  // Missing in DMs.
  guildId?: string | undefined;
  image?: APIAttachment | undefined;
  signal: AbortSignal;
}

export async function runCommandTool(
  name: string,
  json: string,
  { env, sql, userId, guildId, image, signal }: ToolContext,
): Promise<{ output: string; files: File[] }> {
  const failure = (message: string) => ({ output: `Error: ${message}`, files: [] });
  const entry = offered.find((entry) => entry.name === name);
  if (!entry) return failure('unknown command.');
  const { command } = entry;

  let use: Usage | undefined;
  try {
    const args = JSON.parse(json) as Record<string, Value>;
    const options: APIApplicationCommandInteractionDataBasicOption[] = [];
    for (const option of entry.options) {
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
    if (command.limit === 'image') {
      use = takeUsage(sql, 'image', userId);
      if (!use.ok) return failure(use.reason);
    }
    const response = await runCommand(
      {
        id: '0',
        type: ApplicationCommandType.ChatInput,
        name: command.definition.name,
        options: entry.subcommand
          ? [{ type: ApplicationCommandOptionType.Subcommand, name: entry.subcommand, options }]
          : options,
        ...(image && { resolved: { attachments: { [image.id]: image } } }),
      },
      env,
      signal,
      { guildId, userId },
    );
    const files = command.textOnlyInChat ? [] : (response.files ?? []);
    const note = files.length ? '\nThe image is attached to your reply.' : '';
    return { output: (response.data.content ?? '') + note, files };
  } catch (error) {
    if (use?.ok) use.refund();
    if (error instanceof CommandError) return failure(error.message);
    log.error('Chat command failed', { command: name, error, input: preview(json) });
    return failure('the command failed.');
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
