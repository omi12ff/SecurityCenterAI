import { postgresExecArgs } from './database.ts';
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test, expect, loginAccount, seedTestAnalyses, type TestAccount } from './fixtures.ts';
import { accountApi, registerApi, type Authentication } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice, heartbeat } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt } from './analysis-api.ts';
import { waitForAccountLink, accountMessages } from './mailpit.ts';

function removeAccount(token: string, password: string, confirmation = 'ELIMINAR') {
  return accountApi('/api/account', { token, method: 'DELETE', body: { password, confirmation } });
}

function storedCounts(account: TestAccount, userId: string, deviceIds: string[]) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(account.email) ||
    ![userId, ...deviceIds].every(id => /^[a-f0-9-]{36}$/i.test(id))) throw new Error('Refused non-isolated test data.');
  let root = process.cwd();
  while (!existsSync(join(root, 'SecurityCenterAI.sln'))) {
    const parent = dirname(root); if (parent === root) throw new Error('Repository root not found.'); root = parent;
  }
  const result = spawnSync('docker', [...postgresExecArgs(), 'sh', '-c',
    'psql -X -A -t --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=owner_id="$1" --set=devices="$2"',
    'account-deletion-e2e', userId, deviceIds.join(',')], { cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30_000,
    input: `
      SELECT count(*) FROM users WHERE id_usuario=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM user_sessions WHERE "UserId"=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM account_action_tokens WHERE "UserId"=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM security_analyses WHERE "UserId"=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM analysis_feedback WHERE id_usuario=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM devices WHERE id_usuario=:'owner_id'::uuid
      UNION ALL SELECT count(*) FROM device_credentials WHERE id_dispositivo=ANY(string_to_array(:'devices',',')::uuid[])
      UNION ALL SELECT count(*) FROM device_pairing_codes WHERE id_dispositivo=ANY(string_to_array(:'devices',',')::uuid[])
      UNION ALL SELECT count(*) FROM deleted_analysis_reports WHERE id_dispositivo=ANY(string_to_array(:'devices',',')::uuid[]);
    ` });
  if (result.error || result.status !== 0) throw new Error('Could not check isolated deletion records.');
  return result.stdout.trim().split(/\r?\n/).map(Number);
}

test('el borrado autenticado elimina la cuenta completa e invalida sus accesos sin afectar otra cuenta', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const device = await createDevice(owner.accessToken);
  const credential = await pairDevice(device.pairingCode);
  const kept = await submitReport(credential.deviceToken, syntheticReport());
  expect(kept.status).toBe(201);
  const keptId = (await kept.json() as AnalysisReceipt).analysisId;
  expect((await accountApi(`/api/devices/${device.device.id}/analyses/${keptId}/feedback`, {
    token: owner.accessToken, body: { rating: 'helpful', comment: 'Comentario ficticio para eliminar' },
  })).status).toBe(200);
  const deleted = await submitReport(credential.deviceToken, syntheticReport());
  const deletedId = (await deleted.json() as AnalysisReceipt).analysisId;
  expect((await accountApi(`/api/devices/${device.device.id}/analyses/${deletedId}`, {
    token: owner.accessToken, method: 'DELETE', body: { confirmed: true },
  })).status).toBe(204);
  const pending = await accountApi(`/api/devices/${device.device.id}/pairing-code`, { token: owner.accessToken, method: 'POST' });
  const pendingCode = (await pending.json()).pairingCode as string;
  seedTestAnalyses(account.email);
  const secondLogin = await accountApi('/api/auth/login', { body: { email: account.email, password: account.password } });
  const second = await secondLogin.json() as Authentication;
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const resetToken = new URL(await waitForAccountLink(account.email, 'reset-password')).searchParams.get('token');
  const foreignAccount = newAccount(), foreign = await registerApi(foreignAccount);
  expect((await accountApi('/api/account', { method: 'DELETE', body: { password: account.password, confirmation: 'ELIMINAR' } })).status).toBe(401);
  expect((await accountApi('/api/account', { token: credential.deviceToken, scheme: 'Device', method: 'DELETE',
    body: { password: account.password, confirmation: 'ELIMINAR' } })).status).toBe(401);
  expect((await removeAccount(owner.accessToken, foreignAccount.password)).status).toBe(403);
  expect((await removeAccount(owner.accessToken, account.password, 'eliminar')).status).toBe(400);
  const before = storedCounts(account, owner.user.id, [device.device.id]);
  expect(before[0]).toBe(1); expect(before[3]).toBe(8); expect(before[4]).toBe(1); expect(before[8]).toBe(1);
  const result = await removeAccount(owner.accessToken, account.password);
  expect(result.status).toBe(204);
  expect(storedCounts(account, owner.user.id, [device.device.id])).toEqual(Array(9).fill(0));
  for (const token of [owner.accessToken, second.accessToken]) {
    expect((await accountApi('/api/auth/me', { token })).status).toBe(401);
    expect((await accountApi('/api/account/export', { token })).status).toBe(401);
  }
  for (const refreshToken of [owner.refreshToken, second.refreshToken])
    expect((await accountApi('/api/auth/refresh', { body: { refreshToken } })).status).toBe(401);
  expect((await heartbeat(credential.deviceToken)).status).toBe(401);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(401);
  expect((await accountApi('/api/agent/pair', { body: { pairingCode: pendingCode, agentVersion: '0.4.1-e2e' } })).status).toBe(400);
  expect((await accountApi('/api/auth/reset-password', { body: { token: resetToken, password: account.password } })).status).toBe(400);
  expect((await accountApi('/api/auth/me', { token: foreign.accessToken })).status).toBe(200);
  expect((await accountMessages(account.email)).length).toBeGreaterThan(0);
  // Registering the address again creates a new identity with no old history.
  const replacement = await registerApi(account);
  expect(replacement.user.id).not.toBe(owner.user.id);
  const exported = await (await accountApi('/api/account/export', { token: replacement.accessToken })).json();
  expect(exported.devices).toEqual([]); expect(exported.analyses).toEqual([]); expect(exported.deletedReports).toEqual([]);
});

test('Mi cuenta exige contraseña y confirmación, elimina en móvil y limpia las cookies del navegador', async ({ page, context, account }) => {
  const owner = await registerVerifiedOwner(account);
  const device = await createDevice(owner.accessToken);
  const credential = await pairDevice(device.pairingCode);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(201);
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/account');
  const deletion = page.getByRole('region', { name: 'Eliminar mi cuenta', exact: true });
  const password = deletion.getByLabel('Contraseña actual', { exact: true });
  const confirmation = deletion.getByLabel('Escribí ELIMINAR para confirmar', { exact: true });
  const remove = deletion.getByRole('button', { name: 'Eliminar mi cuenta', exact: true });
  await remove.click();
  expect(await password.evaluate(element => (element as HTMLInputElement).validity.valueMissing)).toBe(true);
  await password.fill('Wrong-Synthetic-Password'); await confirmation.fill('ELIMINAR'); await remove.click();
  await expect(page.locator('.account-deletion').getByRole('alert')).toContainText('No pudimos confirmar tu contraseña');
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await password.fill(account.password); await confirmation.fill('ELIMINAR'); await remove.click();
  await expect(page).toHaveURL(/\/login\?reason=account-deleted$/);
  await expect(page.getByText('Tu cuenta fue eliminada.', { exact: false })).toBeVisible();
  expect((await context.cookies()).filter(cookie => ['security_center_session', 'security_center_refresh'].includes(cookie.name))).toHaveLength(0);
  expect((await heartbeat(credential.deviceToken)).status).toBe(401);
  expect(storedCounts(account, owner.user.id, [device.device.id])).toEqual(Array(9).fill(0));
});

test('las carreras con ingesta, login y renovación no restauran una cuenta eliminada', async ({ newAccount }) => {
  for (let iteration = 0; iteration < 3; iteration++) {
    const account = newAccount(), owner = await registerVerifiedOwner(account);
    const device = await createDevice(owner.accessToken), credential = await pairDevice(device.pairingCode);
    const [deletion, upload, refresh, login] = await Promise.all([
      removeAccount(owner.accessToken, account.password), submitReport(credential.deviceToken, syntheticReport()),
      accountApi('/api/auth/refresh', { body: { refreshToken: owner.refreshToken } }),
      accountApi('/api/auth/login', { body: { email: account.email, password: account.password } }),
    ]);
    if (deletion.status === 409) expect((await removeAccount(owner.accessToken, account.password)).status).toBe(204);
    else expect(deletion.status).toBe(204);
    expect([201, 401, 409]).toContain(upload.status);
    for (const response of [refresh, login]) {
      expect([200, 401]).toContain(response.status);
      if (response.status === 200) {
        const stale = await response.json() as Authentication;
        expect((await accountApi('/api/auth/me', { token: stale.accessToken })).status).toBe(401);
        expect((await accountApi('/api/auth/refresh', { body: { refreshToken: stale.refreshToken } })).status).toBe(401);
      }
    }
    expect((await heartbeat(credential.deviceToken)).status).toBe(401);
    expect(storedCounts(account, owner.user.id, [device.device.id])).toEqual(Array(9).fill(0));
  }
});
