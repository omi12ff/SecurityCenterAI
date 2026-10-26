import assert from 'node:assert/strict';
import test from 'node:test';
import { postgresExecArgs } from '../e2e/database.ts';

test('E2E SQL selects only a generated owned database and refuses arbitrary targets', () => {
  const previous = process.env.E2E_DATABASE_NAME;
  try {
    const database = `scai_e2e_test_${'a'.repeat(32)}`;
    process.env.E2E_DATABASE_NAME = database;
    assert.deepEqual(postgresExecArgs(), ['compose', 'exec', '-T', '-e', `POSTGRES_DB=${database}`, 'postgres']);
    for (const invalid of ['', 'postgres', 'securitycenterai', `scai_e2e_test_${'a'.repeat(31)}`,
      `scai_e2e_test_${'A'.repeat(32)}`, `${database};drop database postgres`, `${database}\n`]) {
      process.env.E2E_DATABASE_NAME = invalid;
      assert.throws(postgresExecArgs, /outside the isolated E2E namespace/);
    }
    delete process.env.E2E_DATABASE_NAME;
    assert.deepEqual(postgresExecArgs(), ['compose', 'exec', '-T', 'postgres']);
  } finally {
    if (previous === undefined) delete process.env.E2E_DATABASE_NAME;
    else process.env.E2E_DATABASE_NAME = previous;
  }
});
