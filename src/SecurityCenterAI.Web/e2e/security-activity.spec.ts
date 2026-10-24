import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test, expect, loginAccount, type TestAccount } from './fixtures.ts';
import { accountApi } from './api.ts';
import { registerVerifiedOwner, createDevice } from './device-api.ts';

function seedActivity(account: TestAccount, count: 0 | 65, mixed = false) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(account.email)) throw new Error('Refused an account outside the fictitious E2E namespace.');
  let root = process.cwd();
  while (!existsSync(join(root, 'SecurityCenterAI.sln'))) { const parent = dirname(root); if (parent === root) throw new Error('Repository missing.'); root = parent; }
  const result = spawnSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql --no-psqlrc --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=seed_email="$1"', 'activity-e2e', account.email], {
    cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30_000,
    input: count === 0 ? "DELETE FROM security_activity WHERE id_usuario IN (SELECT id_usuario FROM users WHERE correo_electronico = :'seed_email' AND correo_electronico LIKE 'e2e-%@example.invalid');" : `
      INSERT INTO security_activity (id_evento,id_usuario,tipo_evento,fecha_evento_utc)
      SELECT gen_random_uuid(),u.id_usuario,'SessionStarted','2026-09-10T12:00:00Z'::timestamptz - sample * INTERVAL '1 minute'
      FROM users u CROSS JOIN generate_series(1,65) AS sample
      WHERE u.correo_electronico = :'seed_email' AND u.correo_electronico LIKE 'e2e-%@example.invalid';
      ${mixed ? `INSERT INTO security_activity (id_evento,id_usuario,tipo_evento,fecha_evento_utc)
        SELECT gen_random_uuid(),u.id_usuario,entry.kind,entry.at FROM users u CROSS JOIN (VALUES
          ('DeviceRevoked','2026-09-11T00:00:00Z'::timestamptz),
          ('PasswordReset','2026-09-10T00:00:00Z'::timestamptz)
        ) AS entry(kind,at) WHERE u.correo_electronico = :'seed_email' AND u.correo_electronico LIKE 'e2e-%@example.invalid';` : ''}`,
  });
  if (result.error || result.status !== 0) throw new Error('Isolated activity setup failed.');
}

test('Mi cuenta muestra actividad privada sin datos de otra cuenta o material de acceso', async ({ page, account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const device = await createDevice(owner.accessToken, 'Equipo ficticio privado');
  const other = await registerVerifiedOwner(newAccount());
  const otherDevice = await createDevice(other.accessToken);
  expect((await accountApi(`/api/devices/${otherDevice.device.id}/revoke`, { method: 'POST', token: other.accessToken })).status).toBe(204);
  await loginAccount(page, account);
  await page.goto('/account');
  await page.getByRole('link', { name: 'Ver actividad de seguridad', exact: true }).click();
  await expect(page).toHaveURL(/\/account\/activity$/);
  await expect(page.getByRole('heading', { name: 'Actividad de seguridad', exact: true })).toBeVisible();
  const list = page.locator('.security-activity-list');
  await expect(list.getByText('Cuenta creada', { exact: true })).toBeVisible();
  await expect(list.getByText('Dispositivo registrado', { exact: true })).toBeVisible();
  await expect(list.getByText('Acceso de un dispositivo revocado', { exact: true })).toHaveCount(0);
  const text = await page.locator('body').innerText();
  for (const value of [account.password, owner.accessToken, owner.refreshToken, device.pairingCode, device.device.id, other.user.id]) expect(text.includes(value)).toBe(false);
  const own = await (await accountApi('/api/account/security-activity', { token: owner.accessToken })).json();
  const forged = await (await accountApi(`/api/account/security-activity?userId=${other.user.id}`, { token: owner.accessToken })).json();
  expect(forged).toEqual(own);
  expect(own.events.every((event: Record<string, unknown>) => Object.keys(event).sort().join(',') === 'action,id,occurredAtUtc')).toBe(true);
  const foreignActivity = await (await accountApi('/api/account/security-activity', { token: other.accessToken })).json();
  expect((await accountApi(`/api/account/security-activity?before=${foreignActivity.events[0].id}`, { token: owner.accessToken })).status).toBe(400);
});

test('actividad móvil distingue vacío y límite de 50 y exporta el historial completo', async ({ page, account }) => {
  await registerVerifiedOwner(account);
  await loginAccount(page, account);
  seedActivity(account, 0);
  await page.setViewportSize({ width: 360, height: 800 });
  const response = await page.goto('/account/activity');
  await expect(page.getByText('Todavía no hay actividad registrada.', { exact: true })).toBeVisible();
  expect(response?.headers()['cache-control']).toContain('no-store');
  expect(response?.headers()['referrer-policy']).toBe('no-referrer');
  seedActivity(account, 65);
  await page.reload();
  await expect(page.locator('.security-activity-list li')).toHaveCount(50);
  await expect(page.getByText('Hay eventos anteriores.', { exact: false })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('link', { name: 'Ver eventos anteriores', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(15);
  await expect(page.getByRole('link', { name: 'Ver eventos anteriores', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'Ver más recientes', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(50);
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('link', { name: 'Exportar mi cuenta y actividad (JSON)', exact: true }).click();
  const download = await downloadPromise; const path = await download.path();
  if (!path) throw new Error('Download unavailable.');
  const exported = JSON.parse(await readFile(path, 'utf8')) as { securityActivity: { action: string; occurredAtUtc: string }[] };
  expect(exported.securityActivity).toHaveLength(65);
  expect(exported.securityActivity.every(event => event.action === 'session.started' && Number.isFinite(Date.parse(event.occurredAtUtc)))).toBe(true);
});

test('filtros conservan acción y días UTC al continuar y los errores no parecen un historial vacío', async ({ page, account }) => {
  await registerVerifiedOwner(account);
  await loginAccount(page, account);
  seedActivity(account, 0); seedActivity(account, 65, true);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/account/activity');
  const form = page.getByRole('form', { name: 'Filtrar actividad de seguridad' });
  await form.getByLabel('Acción', { exact: true }).selectOption('session.started');
  await form.getByLabel('Desde (UTC)', { exact: true }).fill('2026-09-10');
  await form.getByLabel('Hasta (UTC)', { exact: true }).fill('2026-09-10');
  await form.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(50);
  await page.getByRole('link', { name: 'Ver eventos anteriores', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(15);
  const params = new URL(page.url()).searchParams;
  expect(params.get('action')).toBe('session.started');
  expect(params.get('from')).toBe('2026-09-10'); expect(params.get('to')).toBe('2026-09-10');
  await expect(form.getByLabel('Acción', { exact: true })).toHaveValue('session.started');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await form.getByLabel('Acción', { exact: true }).selectOption('password.reset');
  await form.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(1);
  await expect(page.locator('.security-activity-list')).toContainText('Contraseña restablecida');
  expect(new URL(page.url()).searchParams.has('before')).toBe(false);
  await form.getByLabel('Acción', { exact: true }).selectOption('device.revoked');
  await form.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
  await expect(page.getByText('No hay eventos que coincidan con esta consulta.', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Limpiar filtros', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(50);
  await expect(form.getByLabel('Acción', { exact: true })).toHaveValue('');
  await expect(form.getByLabel('Desde (UTC)', { exact: true })).toHaveValue('');
  await expect(form.getByLabel('Hasta (UTC)', { exact: true })).toHaveValue('');
  await page.goto('/account/activity?from=2026-02-30&action=PRIVATE_INVALID');
  await expect(page.getByRole('alert')).toContainText('Los filtros no son válidos');
  await expect(page.locator('body')).not.toContainText('PRIVATE_INVALID');
  await page.getByRole('link', { name: 'Volver al historial', exact: true }).click();
  await expect(page.locator('.security-activity-list li')).toHaveCount(50);
});
