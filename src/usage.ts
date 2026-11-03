export const USAGE_WINDOW_MS = 24 * 60 * 60 * 1_000;
const LIMITS = {
  image: { user: 50, bot: 100, label: 'images' },
  text: { user: 100, bot: 200, label: 'requests' },
};

export type UsageKind = keyof typeof LIMITS;
export type Usage = { ok: true; refund(): void } | { ok: false; reason: string };

// Counts uses over the last 24 hours, for the user and for everyone. The Durable Object
// runs one event at a time, so the check and the insert can't interleave with another.
export function takeUsage(
  sql: SqlStorage,
  kind: UsageKind,
  userId: string,
  now = Date.now(),
): Usage {
  const { user, bot, label } = LIMITS[kind];
  const since = now - USAGE_WINDOW_MS;
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
      ok: false,
      reason: `You've used all ${user} of your ${label} for today. Try again in ${formatDuration(mine.oldest! + USAGE_WINDOW_MS - now)}.`,
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
      ok: false,
      reason: `I've used all ${bot} of my ${label} for today across everyone. Try again in ${formatDuration(everyone.oldest! + USAGE_WINDOW_MS - now)}.`,
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
  return { ok: true, refund: () => void sql.exec('DELETE FROM usage WHERE id = ?', id) };
}

function formatDuration(ms: number) {
  const minutes = Math.max(1, Math.ceil(ms / 60_000));
  const hours = Math.floor(minutes / 60);
  return hours ? `${hours}h ${minutes % 60}m` : `${minutes}m`;
}
