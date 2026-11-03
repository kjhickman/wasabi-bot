import { describe, expect, it } from 'vite-plus/test';
import { takeUsage, type Kind, type Usage } from '../src/usage.ts';
import { sqlStorage } from './fixtures/sql.ts';

const MINUTE = 60 * 1_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const now = Date.UTC(2026, 10, 2, 12);

function fill(sql: SqlStorage, kind: Kind, userId: string, count: number, at = now) {
  for (let i = 0; i < count; i++)
    expect(takeUsage(sql, kind, userId, at)).toMatchObject({ ok: true });
}

describe('takeUsage', () => {
  it('allows 50 images per person, then says when the oldest one expires', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 1, now - 20 * HOUR - 30 * MINUTE);
    fill(sql, 'image', 'kyle', 49);

    expect(takeUsage(sql, 'image', 'kyle', now)).toEqual({
      ok: false,
      reason: "You've used all 50 of your images for today. Try again in 3h 30m.",
    });
    expect(takeUsage(sql, 'image', 'sam', now)).toMatchObject({ ok: true });
  });

  it('allows 100 images across everyone', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 1, now - DAY + 30_000);
    fill(sql, 'image', 'kyle', 49);
    fill(sql, 'image', 'sam', 50);

    expect(takeUsage(sql, 'image', 'alex', now)).toEqual({
      ok: false,
      reason: "I've used all 100 of my images for today across everyone. Try again in 1m.",
    });
  });

  it('counts requests separately from images, with double the limits', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 50);
    fill(sql, 'text', 'kyle', 100);

    expect(takeUsage(sql, 'text', 'kyle', now)).toEqual({
      ok: false,
      reason: "You've used all 100 of your requests for today. Try again in 24h 0m.",
    });
    fill(sql, 'text', 'sam', 100);
    expect(takeUsage(sql, 'text', 'alex', now)).toEqual({
      ok: false,
      reason: "I've used all 200 of my requests for today across everyone. Try again in 24h 0m.",
    });
  });

  it('frees a use that is refunded', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 49);
    const last = takeUsage(sql, 'image', 'kyle', now);
    assertRefundable(last);

    last.refund();

    expect(takeUsage(sql, 'image', 'kyle', now)).toMatchObject({ ok: true });
  });

  it('forgets uses older than a day', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 50, now - DAY);

    expect(takeUsage(sql, 'image', 'kyle', now)).toMatchObject({ ok: true });
  });
});

function assertRefundable(use: Usage): asserts use is Extract<Usage, { ok: true }> {
  expect(use).toMatchObject({ ok: true });
}
