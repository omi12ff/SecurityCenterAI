/** Docker exec overrides affect only this command, never the running container. */
export function postgresExecArgs(): string[] {
  const database = process.env.E2E_DATABASE_NAME;
  if (database === undefined) return ['compose', 'exec', '-T', 'postgres'];
  if (!/^scai_e2e_test_[a-f0-9]{32}$/.test(database))
    throw new Error('Refused a database outside the isolated E2E namespace.');
  return ['compose', 'exec', '-T', '-e', `POSTGRES_DB=${database}`, 'postgres'];
}
