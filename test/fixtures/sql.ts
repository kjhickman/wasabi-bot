import { DatabaseSync } from 'node:sqlite';
import { migrate } from '../../src/storage.ts';

export function sqlStorage() {
  const db = new DatabaseSync(':memory:');
  const sql = {
    exec(query: string, ...bindings: (string | number | null)[]) {
      const rows = db.prepare(query).all(...bindings);
      return { toArray: () => rows, one: () => rows[0] };
    },
  } as unknown as SqlStorage;
  migrate(sql);
  return sql;
}
