import { postgresExecArgs } from './database.ts';
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test, expect, loginAccount, type TestAccount } from './fixtures.ts';
import { accountApi, registerApi } from './api.ts';
import { registerVerifiedOwner } from './device-api.ts';

function seedQuota(account: TestAccount, count: number, previousDay = false) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(account.email) || !Number.isInteger(count) || count < 0 || count > 1000) throw new Error('Unsafe quota fixture.');
  let root = process.cwd();
  while (!existsSync(join(root, 'SecurityCenterAI.sln'))) { const parent = dirname(root); if (parent === root) throw new Error('Repository missing.'); root = parent; }
  const fixtureProcess = spawnSync('docker', [...postgresExecArgs(), 'sh', '-c',
    'exec psql --no-psqlrc --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=fixture_email="$1"', 'quota-e2e', account.email], {
    cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30_000,
    input: `INSERT INTO link_message_usage(id_usuario,dia_utc,consultas_realizadas,version_concurrencia)
      SELECT id_usuario,(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date ${previousDay ? '- 1' : ''},${count},gen_random_uuid()
      FROM users WHERE correo_electronico = :'fixture_email' AND correo_electronico LIKE 'e2e-%@example.invalid'
      ON CONFLICT(id_usuario) DO UPDATE SET dia_utc=EXCLUDED.dia_utc,consultas_realizadas=EXCLUDED.consultas_realizadas,version_concurrencia=EXCLUDED.version_concurrencia;`,
  });
  if (fixtureProcess.error || fixtureProcess.status !== 0) throw new Error('Isolated quota setup failed.');
}

test('la herramienta requiere correo verificado antes de enviar contenido', async ({ page, account }) => {
  await registerApi(account); await loginAccount(page, account);
  await page.getByRole('link', { name: 'Enlaces y mensajes', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Revisar enlaces y mensajes', exact: true })).toBeVisible();
  await expect(page.locator('section[aria-labelledby="tool-input-title"]').getByRole('alert')).toContainText('verificá tu correo');
  await expect(page.getByRole('button', { name: 'Revisar contenido', exact: true })).toBeDisabled();
});

test('enlaces y mensajes muestran señales limitadas sin abrir destinos ni guardar el contenido', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account); await loginAccount(page, account);
  const destinations: string[] = [];
  page.on('request', request => { if (new URL(request.url()).hostname.endsWith('.invalid')) destinations.push(request.url()); });
  const navigation = await page.goto('/tools/scam-check');
  expect(navigation?.headers()['cache-control']).toContain('no-store'); expect(navigation?.headers()['referrer-policy']).toBe('no-referrer');
  const form = page.getByRole('form', { name: 'Revisar enlace o mensaje' });
  await form.getByLabel('Enlace completo (HTTP o HTTPS)', { exact: true }).fill('http://trusted.invalid:PRIVATE_FIXTURE@destination.invalid/path?code=PRIVATE_FIXTURE');
  await form.getByRole('checkbox').check(); await form.getByRole('button', { name: 'Revisar contenido', exact: true }).click();
  const result = page.getByRole('region', { name: 'Señales para revisar', exact: true });
  await expect(result).toBeVisible(); await expect(result).toContainText('destination.invalid'); await expect(result).not.toContainText('PRIVATE_FIXTURE');
  await expect(result.getByRole('link')).toHaveCount(0); expect(destinations).toEqual([]);
  await page.setViewportSize({ width: 360, height: 800 });
  await form.getByLabel('Tipo de contenido', { exact: true }).selectOption('message');
  await form.getByLabel('Texto del mensaje', { exact: true }).fill('¡URGENTE! Ganaste un premio. Ingresá tu contraseña. PRIVATE_FIXTURE');
  await form.getByRole('checkbox').check(); await form.getByRole('button', { name: 'Revisar contenido', exact: true }).click();
  await expect(result).toContainText('Lenguaje de urgencia'); await expect(result).toContainText('Referencia a claves o códigos');
  await expect(result).not.toContainText('PRIVATE_FIXTURE');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const exported = await (await accountApi('/api/account/export', { token: owner.accessToken })).json();
  expect(exported.linkMessageUsage.checks).toBe(2); expect(JSON.stringify(exported)).not.toContain('PRIVATE_FIXTURE');
  await form.getByRole('button', { name: 'Limpiar contenido', exact: true }).click();
  await expect(form.getByLabel('Texto del mensaje', { exact: true })).toHaveValue(''); await expect(result).toHaveCount(0);
  await expect(page.getByRole('status')).toContainText('23 de 25');
});

test('el límite diario es común a ambos modos y el día UTC siguiente permite nuevas consultas', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account); await loginAccount(page, account);
  const quota = await (await accountApi('/api/tools/link-message', { token: owner.accessToken })).json();
  seedQuota(account, quota.limit);
  await page.goto('/tools/scam-check');
  await expect(page.getByRole('status')).toContainText(`0 de ${quota.limit}`);
  await expect(page.getByRole('button', { name: 'Revisar contenido', exact: true })).toBeDisabled();
  expect((await accountApi('/api/tools/link-message', { token: owner.accessToken, body: { kind: 'message', content: 'Mensaje ficticio', confirmed: true } })).status).toBe(429);
  seedQuota(account, quota.limit, true); await page.reload();
  await expect(page.getByRole('status')).toContainText(`${quota.limit} de ${quota.limit}`);
  await expect(page.getByRole('button', { name: 'Revisar contenido', exact: true })).toBeEnabled();
});
