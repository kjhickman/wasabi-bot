import { RouteBases, Routes } from 'discord-api-types/v10';
import { commands } from '../src/commands/index.ts';

const { DISCORD_APPLICATION_ID, DISCORD_BOT_TOKEN } = process.env;

if (!DISCORD_APPLICATION_ID || !DISCORD_BOT_TOKEN) {
  throw new Error('Set DISCORD_APPLICATION_ID and DISCORD_BOT_TOKEN in .env.');
}

const response = await fetch(
  `${RouteBases.api}${Routes.applicationCommands(DISCORD_APPLICATION_ID)}`,
  {
    method: 'PUT',
    headers: {
      Authorization: `Bot ${DISCORD_BOT_TOKEN}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(commands.map((command) => command.definition)),
  },
);

if (!response.ok) {
  throw new Error(`Discord returned ${response.status}: ${await response.text()}`);
}

console.log('Synced global commands.');
