import type { GatewayVoiceState } from 'discord-api-types/v10';

// Who is in which voice channel, kept from Gateway events so the radio knows when its
// channel has emptied. Discord has no endpoint that lists a channel's members.
type VoiceState = Pick<GatewayVoiceState, 'user_id' | 'channel_id'>;

// A new session sends every guild again, so start over.
export function clearVoiceStates(sql: SqlStorage) {
  sql.exec('DELETE FROM voice_states');
}

export function replaceVoiceStates(sql: SqlStorage, guildId: string, states: VoiceState[]) {
  sql.exec('DELETE FROM voice_states WHERE guild_id = ?', guildId);
  for (const state of states) saveVoiceState(sql, guildId, state);
}

export function saveVoiceState(sql: SqlStorage, guildId: string, state: VoiceState) {
  if (state.channel_id) {
    sql.exec(
      'INSERT OR REPLACE INTO voice_states (guild_id, user_id, channel_id) VALUES (?, ?, ?)',
      guildId,
      state.user_id,
      state.channel_id,
    );
  } else {
    sql.exec('DELETE FROM voice_states WHERE guild_id = ? AND user_id = ?', guildId, state.user_id);
  }
}

export function countListeners(sql: SqlStorage, guildId: string, channelId: string, botId: string) {
  return sql
    .exec<{ count: number }>(
      'SELECT COUNT(*) AS count FROM voice_states WHERE guild_id = ? AND channel_id = ? AND user_id != ?',
      guildId,
      channelId,
      botId,
    )
    .one().count;
}
