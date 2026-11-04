import { spawn, type ChildProcess } from 'node:child_process';
import { createServer, type IncomingMessage } from 'node:http';
import {
  AudioPlayerStatus,
  createAudioPlayer,
  createAudioResource,
  entersState,
  joinVoiceChannel,
  NoSubscriberBehavior,
  StreamType,
  VoiceConnectionStatus,
  type DiscordGatewayAdapterLibraryMethods,
  type VoiceConnection,
} from '@discordjs/voice';

const PORT = 8080;
// The Durable Object that started this container answers requests to this host.
const CALLBACK_URL = 'http://player.internal';
const READY_TIMEOUT_MS = 20_000;
const AUDIO_TIMEOUT_MS = 15_000;
const RECONNECT_GRACE_MS = 5_000;
// Only network protocols, since the stream URL comes from users. Radio streams drop now and
// then, so ffmpeg reconnects, even when the server ends the stream, before giving up.
const INPUT_FLAGS =
  '-hide_banner -loglevel error -protocol_whitelist http,https,tcp,tls,crypto -reconnect 1 -reconnect_streamed 1 -reconnect_at_eof 1 -reconnect_delay_max 5'.split(
    ' ',
  );
// Discord voice takes 48 kHz stereo Opus, which @discordjs/voice reads from Ogg without re-encoding.
const OUTPUT_FLAGS = '-vn -c:a libopus -b:a 128k -ar 48000 -ac 2 -f ogg pipe:1'.split(' ');

let connection: VoiceConnection | undefined;
let adapter: DiscordGatewayAdapterLibraryMethods | undefined;
let ffmpeg: ChildProcess | undefined;
const player = createAudioPlayer({ behaviors: { noSubscriber: NoSubscriberBehavior.Play } });

player.on('error', (error) => console.error('Audio error:', error.message));
player.on(AudioPlayerStatus.Idle, () => {
  if (ffmpeg) leave();
});

function startStream(url: string) {
  const child = spawn('ffmpeg', [...INPUT_FLAGS, '-i', url, ...OUTPUT_FLAGS], {
    stdio: ['ignore', 'pipe', 'inherit'],
  });
  child.on('error', (error) => console.error('ffmpeg failed to start:', error.message));
  ffmpeg = child;
  player.play(createAudioResource(child.stdout, { inputType: StreamType.OggOpus }));
}

function stopStream() {
  const stopped = ffmpeg;
  ffmpeg = undefined;
  stopped?.kill('SIGKILL');
  player.stop(true);
}

function join(guildId: string, channelId: string) {
  if (connection) {
    if (connection.joinConfig.channelId !== channelId) {
      connection.rejoin({ channelId, selfDeaf: true, selfMute: false });
    }
    return connection;
  }
  const joined = joinVoiceChannel({
    guildId,
    channelId,
    selfDeaf: true,
    adapterCreator: (methods) => {
      adapter = methods;
      return {
        sendPayload: (payload) => {
          void fetch(`${CALLBACK_URL}/gateway`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
          })
            .then((response) => {
              if (!response.ok) console.error(`Gateway callback failed: HTTP ${response.status}`);
            })
            .catch((error: unknown) => console.error('Gateway callback failed:', error));
          return true;
        },
        destroy: () => {
          adapter = undefined;
        },
      };
    },
  });
  joined.subscribe(player);
  joined.on('stateChange', (from, to) => console.log(`Voice: ${from.status} -> ${to.status}`));
  joined.on('error', (error) => console.error('Voice error:', error.message));
  // Being moved reconnects within a few seconds. Anything else that disconnects ends the session.
  joined.on(VoiceConnectionStatus.Disconnected, async () => {
    try {
      await Promise.race([
        entersState(joined, VoiceConnectionStatus.Signalling, RECONNECT_GRACE_MS),
        entersState(joined, VoiceConnectionStatus.Connecting, RECONNECT_GRACE_MS),
      ]);
    } catch {
      if (joined.state.status !== VoiceConnectionStatus.Destroyed) joined.destroy();
    }
  });
  joined.on(VoiceConnectionStatus.Destroyed, () => {
    if (connection !== joined) return;
    connection = undefined;
    stopStream();
  });
  connection = joined;
  return joined;
}

function leave() {
  stopStream();
  connection?.destroy();
}

async function play({
  guildId,
  channelId,
  url,
}: {
  guildId: string;
  channelId: string;
  url: string;
}) {
  const voice = join(guildId, channelId);
  try {
    await entersState(voice, VoiceConnectionStatus.Ready, READY_TIMEOUT_MS);
  } catch {
    leave();
    return "I couldn't join your voice channel. Check that I can connect and speak there.";
  }
  stopStream();
  startStream(url);
  try {
    await entersState(player, AudioPlayerStatus.Playing, AUDIO_TIMEOUT_MS);
  } catch {
    leave();
    return "That station isn't streaming right now. Try another one.";
  }
  return undefined;
}

type Route = (body: any) => unknown;
const routes: Record<string, Route> = {
  'GET /status': () => ({ active: connection !== undefined }),
  'POST /voice': ({ t, d }) => {
    if (t === 'VOICE_STATE_UPDATE') adapter?.onVoiceStateUpdate(d);
    if (t === 'VOICE_SERVER_UPDATE') adapter?.onVoiceServerUpdate(d);
    return { ok: true };
  },
  'POST /play': async (body) => ({ error: await play(body) }),
};

async function readJson(request: IncomingMessage) {
  let text = '';
  for await (const chunk of request) text += chunk;
  return text ? JSON.parse(text) : undefined;
}

createServer(async (request, response) => {
  const route =
    routes[`${request.method} ${new URL(request.url ?? '/', 'http://container').pathname}`];
  if (!route) {
    response.writeHead(404).end();
    return;
  }
  try {
    const body = await route(await readJson(request));
    response.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(body));
  } catch (error) {
    console.error('Request failed:', error);
    response.writeHead(500).end();
  }
}).listen(PORT, () => console.log(`Radio container listening on ${PORT}`));
