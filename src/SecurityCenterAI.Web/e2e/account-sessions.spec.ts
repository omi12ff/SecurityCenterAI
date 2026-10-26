import { postgresExecArgs } from './database.ts';
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test, expect, loginAccount, type TestAccount } from './fixtures.ts';
import { accountApi } from './api.ts';
import { registerVerifiedOwner } from './device-api.ts';

function seedSessions(account: TestAccount) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(account.email)) throw new Error('Refused an account outside the fictitious E2E namespace.');
  let root = process.cwd();
  while (!existsSync(join(root, 'SecurityCenterAI.sln'))) { const parent = dirname(root); if (parent === root) throw new Error('Repository missing.'); root = parent; }
  const result = spawnSync('docker', [...postgresExecArgs(), 'sh', '-c',
    'exec psql --no-psqlrc --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=seed_email="$1"', 'sessions-e2e', account.email], {
    cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30_000,
    input: `INSERT INTO user_sessions ("Id","UserId","CreatedAtUtc","ExpiresAtUtc","RevokedAtUtc","ConcurrencyStamp")
      SELECT gen_random_uuid(),u.id_usuario,CURRENT_TIMESTAMP - sample * INTERVAL '1 minute',
        CURRENT_TIMESTAMP + INTERVAL '2 days',NULL,gen_random_uuid()
      FROM users u CROSS JOIN generate_series(1,60) AS sample
      WHERE u.correo_electronico = :'seed_email' AND u.correo_electronico LIKE 'e2e-%@example.invalid';`,
  });
  if (result.error || result.status !== 0) throw new Error('Isolated session setup failed.');
}

test('Mi cuenta permite cerrar otra sesión y luego la actual con confirmación y renovación', async ({ page, account }) => {
  test.setTimeout(90_000);
  const owner = await registerVerifiedOwner(account);
  await loginAccount(page, account);
  await page.goto('/account');
  await page.getByRole('link', { name: 'Ver sesiones activas', exact: true }).click();
  await expect(page).toHaveURL(/\/account\/sessions$/);
  await expect(page.locator('.account-session-card')).toHaveCount(2);
  await expect(page.getByRole('heading', { name: 'Sesión actual', exact: true })).toBeVisible();
  const html = await page.content();
  for (const [label, value] of [['password', account.password], ['access token', owner.accessToken],
    ['refresh token', owner.refreshToken]]) expect(html.includes(value), `Unexpected ${label} in document`).toBe(false);
  // A client navigation can retain the initial account profile in hydration scripts.
  expect((await page.locator('.account-session-list').innerHTML()).includes(account.email)).toBe(false);
  expect((await page.locator('body').innerText()).includes(account.email)).toBe(false);

  // Keep the real refresh cookie, remove only access, and verify the new return path.
  await page.context().clearCookies({ name: 'security_center_session' });
  await page.goto('/account/sessions');
  await expect(page).toHaveURL(/\/account\/sessions$/);
  await expect(page.locator('.account-session-card')).toHaveCount(2);

  const other = page.locator('.account-session-card').filter({ has: page.getByRole('heading', { name: /^Otra sesión/ }) });
  await other.getByRole('button', { name: 'Cerrar esta sesión', exact: true }).click();
  await expect(page.locator('.account-session-card')).toHaveCount(2);
  expect((await accountApi('/api/account/sessions', { token: owner.accessToken })).status).toBe(200);
  await other.getByRole('checkbox').check();
  await other.getByRole('button', { name: 'Cerrar esta sesión', exact: true }).click();
  await expect(page.locator('.account-session-card')).toHaveCount(1);
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(401);
  expect((await accountApi('/api/auth/refresh', { method: 'POST', body: { refreshToken: owner.refreshToken } })).status).toBe(401);

  const current = page.locator('.account-session-card');
  await current.getByRole('checkbox', { name: 'Confirmo que quiero salir de esta sesión actual', exact: true }).check();
  await current.getByRole('button', { name: 'Cerrar mi sesión actual', exact: true }).click();
  await expect(page).toHaveURL(/\/login\?reason=session-closed$/);
  await expect(page.getByText('Se cerró esta sesión. Podés iniciar sesión de nuevo.', { exact: true })).toBeVisible();
  const cookies = await page.context().cookies();
  expect(cookies.filter(cookie => ['security_center_session', 'security_center_refresh'].includes(cookie.name))).toHaveLength(0);
});

test('sesiones móviles paginan sin desbordar y distinguen una página vacía', async ({ page, account }) => {
  test.setTimeout(90_000);
  await registerVerifiedOwner(account);
  await loginAccount(page, account);
  seedSessions(account);
  await page.setViewportSize({ width: 360, height: 800 });
  const response = await page.goto('/account/sessions');
  await expect(page.locator('.account-session-card')).toHaveCount(50);
  await expect(page.getByRole('heading', { name: 'Sesión actual', exact: true })).toBeVisible();
  expect(response?.headers()['cache-control']).toContain('no-store');
  expect(response?.headers()['referrer-policy']).toBe('no-referrer');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('link', { name: 'Página siguiente', exact: true }).click();
  await expect(page).toHaveURL(/\/account\/sessions\?page=2$/);
  await expect(page.locator('.account-session-card')).toHaveCount(12);
  await expect(page.getByRole('heading', { name: 'Sesión actual', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Página siguiente', exact: true })).toHaveCount(0);
  await page.goto('/account/sessions?page=3');
  await expect(page.getByText('No hay sesiones activas en esta página.', { exact: true })).toBeVisible();
});
