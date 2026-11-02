const DAY_MS = 24 * 60 * 60 * 1_000;
const LIMITS = {
  image: { user: 50, bot: 100, label: 'images' },
  text: { user: 100, bot: 200, label: 'requests' },
};

export type Kind = keyof typeof LIMITS;
export type Usage = { refund(): void } | { refused: string };

export function createUsageTable(sql: SqlStorage) {
  sql.exec(
    'CREATE TABLE IF NOT EXISTS usage (id INTEGER PRIMARY KEY, kind TEXT NOT NULL, user_id TEXT NOT NULL, at INTEGER NOT NULL)',
  );
  sql.exec('CREATE INDEX IF NOT EXISTS usage_kind_at ON usage (kind, at)');
}

export function pruneUsage(sql: SqlStorage, now = Date.now()) {
  sql.exec('DELETE FROM usage WHERE at <= ?', now - DAY_MS);
}

// Counts uses over the last 24 hours, for the user and for everyone. The Durable Object
// runs one event at a time, so the check and the insert can't interleave with another.
export function takeUsage(sql: SqlStorage, kind: Kind, userId: string, now = Date.now()): Usage {
  const { user, bot, label } = LIMITS[kind];
  const since = now - DAY_MS;
  const mine = sql
    .exec<{ count: number; oldest: number | null }>(
      'SELECT COUNT(*) AS count, MIN(at) AS oldest FROM usage WHERE kind = ? AND at > ? AND user_id = ?',
      kind,
      since,
      userId,
    )
    .one();
  if (mine.count >= user) {
    return {
      refused: `You've used all ${user} of your ${label} for today. Try again in ${wait(mine.oldest! + DAY_MS - now)}.`,
    };
  }
  const everyone = sql
    .exec<{ count: number; oldest: number | null }>(
      'SELECT COUNT(*) AS count, MIN(at) AS oldest FROM usage WHERE kind = ? AND at > ?',
      kind,
      since,
    )
    .one();
  if (everyone.count >= bot) {
    return {
      refused: `I've used all ${bot} of my ${label} for today across everyone. Try again in ${wait(everyone.oldest! + DAY_MS - now)}.`,
    };
  }
  const { id } = sql
    .exec<{ id: number }>(
      'INSERT INTO usage (kind, user_id, at) VALUES (?, ?, ?) RETURNING id',
      kind,
      userId,
      now,
    )
    .one();
  return { refund: () => void sql.exec('DELETE FROM usage WHERE id = ?', id) };
}

function wait(ms: number) {
  const minutes = Math.max(1, Math.ceil(ms / 60_000));
  const hours = Math.floor(minutes / 60);
  return hours ? `${hours}h ${minutes % 60}m` : `${minutes}m`;
}
