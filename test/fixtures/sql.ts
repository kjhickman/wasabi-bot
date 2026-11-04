import { DatabaseSync } from 'node:sqlite';
import { migrate } from '../../src/storage.ts';

export function emptySqlStorage() {
  const db = new DatabaseSync(':memory:');
  return {
    exec(query: string, ...bindings: (string | number | null)[]) {
      const rows = db.prepare(query).all(...bindings);
      return { toArray: () => rows, one: () => rows[0] };
    },
  } as unknown as SqlStorage;
}

export function sqlStorage() {
  const sql = emptySqlStorage();
  migrate(sql);
  return sql;
}
