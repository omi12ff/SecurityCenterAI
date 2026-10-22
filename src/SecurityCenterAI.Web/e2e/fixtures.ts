import { spawnSync } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test as base, expect, type Page } from '@playwright/test';
import { removeAccountMessages } from './mailpit.ts';

export type TestAccount = { name: string; email: string; password: string };

function findRepositoryRoot() {
  let directory = process.cwd();
  while (!existsSync(join(directory, 'SecurityCenterAI.sln'))) {
    const parent = dirname(directory);
    if (parent === directory) throw new Error('Run E2E tests from the SecurityCenterAI repository.');
    directory = parent;
  }
  return directory;
}

function runAccountSql(email: string, sql: string) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(email)) {
    throw new Error('Cleanup refused an address outside the isolated test namespace.');
  }

  const result = spawnSync('docker', [
    'compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql --no-psqlrc --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=cleanup_email="$1"',
    'cleanup-e2e', email,
  ], {
    cwd: findRepositoryRoot(),
    input: sql,
    encoding: 'utf8',
    timeout: 30_000,
    windowsHide: true,
  });

  if (result.error || result.status !== 0) {
    // Do not attach child-process output: it may contain environment details.
    throw new Error('The isolated E2E database operation failed. Verify local PostgreSQL in Docker.');
  }
}

function removeTestAccount(email: string) {
  runAccountSql(email, 'DELETE FROM users WHERE correo_electronico = :\'cleanup_email\' AND correo_electronico LIKE \'e2e-%@example.invalid\';\n');
}

export function seedTestAnalyses(email: string) {
  runAccountSql(email, `
    INSERT INTO security_analyses ("Id", "UserId", "Score", "AnalyzedAtUtc")
    SELECT gen_random_uuid(), u.id_usuario, sample.score, sample.at
    FROM users u
    CROSS JOIN (VALUES
      (91, '2026-10-20T12:00:00Z'::timestamptz),
      (90, '2026-10-20T11:50:00Z'::timestamptz),
      (89, '2026-10-20T11:40:00Z'::timestamptz),
      (88, '2026-10-20T11:30:00Z'::timestamptz),
      (87, '2026-10-20T11:20:00Z'::timestamptz),
      (86, '2026-10-20T11:10:00Z'::timestamptz),
      (85, '2026-10-20T11:00:00Z'::timestamptz)
    ) AS sample(score, at)
    WHERE u.correo_electronico = :'cleanup_email' AND u.correo_electronico LIKE 'e2e-%@example.invalid';
  `);
}

export function allowTestAccountVerificationResend(email: string) {
  runAccountSql(email, 'UPDATE users SET fecha_ultimo_envio_verificacion_utc = NULL WHERE correo_electronico = :\'cleanup_email\' AND correo_electronico LIKE \'e2e-%@example.invalid\';\n');
}

export const test = base.extend<{ account: TestAccount; newAccount: () => TestAccount }>({
  newAccount: async ({}, use) => {
    const owned: TestAccount[] = [];
    try {
      await use(() => {
        const account = { name: 'Usuario E2E', email: `e2e-${randomUUID()}@example.invalid`,
          password: randomBytes(24).toString('base64url') };
        owned.push(account);
        return account;
      });
    } finally {
      let cleanupFailed = false;
      for (const account of owned) {
        try { await removeAccountMessages(account.email); }
        catch { cleanupFailed = true; }
        try { removeTestAccount(account.email); }
        catch { cleanupFailed = true; }
      }
      if (cleanupFailed) throw new Error('Could not completely clean the isolated E2E accounts or their messages.');
    }
  },
  account: async ({ newAccount }, use) => use(newAccount()),
});

export { expect };

export async function registerAccount(page: Page, account: TestAccount) {
  await page.goto('/register');
  await page.getByLabel('Nombre completo', { exact: true }).fill(account.name);
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByLabel('Contraseña', { exact: true }).fill(account.password);
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(account.password);
  await page.getByRole('button', { name: 'Crear cuenta', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/?$/);
  await expect(page.getByRole('main').getByRole('heading', { name: /tu seguridad/i })).toBeVisible();
}

export async function logout(page: Page) {
  await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
  await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
}

export async function loginAccount(page: Page, account: TestAccount) {
  await page.goto('/login');
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByLabel('Contraseña', { exact: true }).fill(account.password);
  await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/?$/);
}
