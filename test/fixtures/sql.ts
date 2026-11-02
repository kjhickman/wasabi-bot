import { DatabaseSync } from 'node:sqlite';
import { createUsageTable } from '../../src/usage.ts';

// Enough of a Durable Object's SqlStorage for code that runs one statement at a time.
export function sqlStorage() {
  const db = new DatabaseSync(':memory:');
  const sql = {
    exec(query: string, ...bindings: (string | number | null)[]) {
      const rows = db.prepare(query).all(...bindings);
      return { toArray: () => rows, one: () => rows[0] };
    },
  } as unknown as SqlStorage;
  createUsageTable(sql);
  return sql;
}
