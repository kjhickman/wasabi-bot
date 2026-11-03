import { describe, expect, it } from 'vite-plus/test';
import { claim, migrate, prune } from '../src/storage.ts';
import { takeUsage } from '../src/usage.ts';
import { emptySqlStorage, sqlStorage } from './fixtures/sql.ts';

const MINUTE = 60 * 1_000;
const DAY = 24 * 60 * MINUTE;
const now = Date.UTC(2026, 10, 2, 12);

describe('claim', () => {
  it('claims an ID only once', () => {
    const sql = sqlStorage();

    expect(claim(sql, '500', now)).toBe(true);
    expect(claim(sql, '500', now)).toBe(false);
    expect(claim(sql, '501', now)).toBe(true);
  });
});

describe('prune', () => {
  it('deletes uses older than a day', () => {
    const sql = sqlStorage();
    takeUsage(sql, 'text', 'kyle', now - DAY);
    takeUsage(sql, 'text', 'kyle', now - DAY + 1);

    prune(sql, now);

    expect(sql.exec('SELECT at FROM usage').toArray()).toEqual([{ at: now - DAY + 1 }]);
  });

  it('forgets claims after 15 minutes', () => {
    const sql = sqlStorage();
    claim(sql, '500', now - 15 * MINUTE);
    claim(sql, '501', now - 15 * MINUTE + 1);

    prune(sql, now);

    expect(claim(sql, '500', now)).toBe(true);
    expect(claim(sql, '501', now)).toBe(false);
  });
});

describe('migrate', () => {
  it('keeps claims made before the table was renamed', () => {
    const sql = emptySqlStorage();
    sql.exec('CREATE TABLE interactions (id TEXT PRIMARY KEY, expires_at INTEGER NOT NULL)');
    sql.exec('INSERT INTO interactions (id, expires_at) VALUES (?, ?)', '500', now + MINUTE);

    migrate(sql);
    migrate(sql);

    expect(claim(sql, '500', now)).toBe(false);
  });
});
