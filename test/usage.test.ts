import { describe, expect, it } from 'vite-plus/test';
import { pruneUsage, takeUsage, type Kind } from '../src/usage.ts';
import { sqlStorage } from './fixtures/sql.ts';

const MINUTE = 60 * 1_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const now = Date.UTC(2026, 10, 2, 12);

function fill(sql: SqlStorage, kind: Kind, userId: string, count: number, at = now) {
  for (let i = 0; i < count; i++) expect(takeUsage(sql, kind, userId, at)).toHaveProperty('refund');
}

describe('takeUsage', () => {
  it('allows 50 images per person, then says when the oldest one expires', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 1, now - 20 * HOUR - 30 * MINUTE);
    fill(sql, 'image', 'kyle', 49);

    expect(takeUsage(sql, 'image', 'kyle', now)).toEqual({
      refused: "You've used all 50 of your images for today. Try again in 3h 30m.",
    });
    expect(takeUsage(sql, 'image', 'sam', now)).toHaveProperty('refund');
  });

  it('allows 100 images across everyone', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 1, now - DAY + 30_000);
    fill(sql, 'image', 'kyle', 49);
    fill(sql, 'image', 'sam', 50);

    expect(takeUsage(sql, 'image', 'alex', now)).toEqual({
      refused: "I've used all 100 of my images for today across everyone. Try again in 1m.",
    });
  });

  it('counts requests separately from images, with double the limits', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 50);
    fill(sql, 'text', 'kyle', 100);

    expect(takeUsage(sql, 'text', 'kyle', now)).toEqual({
      refused: "You've used all 100 of your requests for today. Try again in 24h 0m.",
    });
    fill(sql, 'text', 'sam', 100);
    expect(takeUsage(sql, 'text', 'alex', now)).toEqual({
      refused: "I've used all 200 of my requests for today across everyone. Try again in 24h 0m.",
    });
  });

  it('frees a use that is refunded', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 49);
    const last = takeUsage(sql, 'image', 'kyle', now);
    assertRefundable(last);

    last.refund();

    expect(takeUsage(sql, 'image', 'kyle', now)).toHaveProperty('refund');
  });

  it('forgets uses older than a day', () => {
    const sql = sqlStorage();
    fill(sql, 'image', 'kyle', 50, now - DAY);

    expect(takeUsage(sql, 'image', 'kyle', now)).toHaveProperty('refund');
  });
});

describe('pruneUsage', () => {
  it('deletes uses older than a day', () => {
    const sql = sqlStorage();
    fill(sql, 'text', 'kyle', 1, now - DAY);
    fill(sql, 'text', 'kyle', 1, now - DAY + 1);

    pruneUsage(sql, now);

    expect(sql.exec('SELECT at FROM usage').toArray()).toEqual([{ at: now - DAY + 1 }]);
  });
});

function assertRefundable(use: ReturnType<typeof takeUsage>): asserts use is { refund(): void } {
  expect(use).toHaveProperty('refund');
}
