import { USAGE_WINDOW_MS } from './usage.ts';

const SEEN_TTL_MS = 15 * 60 * 1_000;

export function migrate(sql: SqlStorage) {
  sql.exec('CREATE TABLE IF NOT EXISTS seen (id TEXT PRIMARY KEY, expires_at INTEGER NOT NULL)');
  // `seen` was called `interactions` before it also held message IDs. Both can exist if a
  // rollback recreated the old table, so merge rather than rename.
  const legacy = sql
    .exec("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'interactions'")
    .toArray();
  if (legacy.length) {
    sql.exec('INSERT OR IGNORE INTO seen (id, expires_at) SELECT id, expires_at FROM interactions');
    sql.exec('DROP TABLE interactions');
  }
  sql.exec(
    'CREATE TABLE IF NOT EXISTS usage (id INTEGER PRIMARY KEY, kind TEXT NOT NULL, user_id TEXT NOT NULL, at INTEGER NOT NULL)',
  );
  sql.exec('CREATE INDEX IF NOT EXISTS usage_kind_at ON usage (kind, at)');
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
