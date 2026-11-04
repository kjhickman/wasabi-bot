import { USAGE_WINDOW_MS } from './usage.ts';

const SEEN_TTL_MS = 15 * 60 * 1_000;

export function migrate(sql: SqlStorage) {
  // `seen` replaced `interactions`. Its claims aren't worth keeping: the first deploy with
  // `seen` starts a fresh Gateway session, and Discord replays events only on a resume.
  sql.exec('DROP TABLE IF EXISTS interactions');
  sql.exec('CREATE TABLE IF NOT EXISTS seen (id TEXT PRIMARY KEY, expires_at INTEGER NOT NULL)');
  sql.exec(
    'CREATE TABLE IF NOT EXISTS usage (id INTEGER PRIMARY KEY, kind TEXT NOT NULL, user_id TEXT NOT NULL, at INTEGER NOT NULL)',
  );
  sql.exec('CREATE INDEX IF NOT EXISTS usage_kind_at ON usage (kind, at)');
  sql.exec(
    'CREATE TABLE IF NOT EXISTS voice_states (guild_id TEXT NOT NULL, user_id TEXT NOT NULL, channel_id TEXT NOT NULL, PRIMARY KEY (guild_id, user_id))',
  );
}

export function prune(sql: SqlStorage, now = Date.now()) {
  sql.exec('DELETE FROM seen WHERE expires_at <= ?', now);
  sql.exec('DELETE FROM usage WHERE at <= ?', now - USAGE_WINDOW_MS);
}

// Claims an interaction or message ID once, so a replayed Gateway event never runs twice.
export function claim(sql: SqlStorage, id: string, now = Date.now()) {
  const inserted = sql
    .exec(
      'INSERT OR IGNORE INTO seen (id, expires_at) VALUES (?, ?) RETURNING id',
      id,
      now + SEEN_TTL_MS,
    )
    .toArray();
  return inserted.length > 0;
}
