import { test, expect, loginAccount } from './fixtures.ts';
import { registerVerifiedOwner, createDevice, pairDevice } from './device-api.ts';
import { syntheticReport, submitReport } from './analysis-api.ts';
import { accountApi, registerApi } from './api.ts';

test('guía local conserva evidencia, funciona en móvil y permite actualizar feedback privado', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de guías');
  const credential = await pairDevice(created.pairingCode);
  const result = await submitReport(credential.deviceToken, syntheticReport({ 'WIN-BITLOCKER-SYSTEM': 'unknown', 'WIN-DEFENDER-ANTIVIRUS': 'attention' }));
  expect(result.status).toBe(201);
  const receipt = await result.json();
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/devices/${created.device.id}`);
  await page.getByRole('link', { name: 'Entender este análisis' }).click();
  await expect(page.getByRole('heading', { name: 'Entender este análisis', exact: true })).toBeVisible();
  await expect(page.getByRole('main')).toContainText('No equivale a una protección desactivada');
  await expect(page.getByRole('main')).toContainText('No se envía el informe a un servicio de IA');
  expect(await page.evaluate(() => Object.hasOwn(window, '__analysisScriptExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const refs = page.getByRole('link', { name: 'Documentación de Microsoft' });
  await expect(refs).toHaveCount(10);
  await page.getByLabel('No quedó clara', { exact: true }).check();
  await page.getByLabel('Comentario opcional').fill('<b>Necesito más detalle</b>');
  await page.getByRole('button', { name: 'Guardar valoración' }).click();
  await expect(page.getByRole('status')).toContainText('quedó guardada');
  await page.reload();
  await expect(page.getByLabel('No quedó clara', { exact: true })).toBeChecked();
  await expect(page.getByLabel('Comentario opcional')).toHaveValue('<b>Necesito más detalle</b>');
  await page.getByLabel('Me ayudó', { exact: true }).check();
  await page.getByRole('button', { name: 'Guardar valoración' }).click();
  await expect(page.getByRole('status')).toContainText('quedó guardada');
  const stored = await accountApi(`/api/devices/${created.device.id}/analyses/${receipt.analysisId}/explanation`, { token: owner.accessToken });
  expect(stored.status).toBe(200);
  const body = await stored.json();
  expect(body.feedback.rating).toBe('helpful');
  expect(body.analysis.score).toBe(receipt.score);
  expect(body.analysis.coveragePercent).toBe(receipt.coveragePercent);
});

test('feedback rechaza acceso ajeno y Device; envíos simultáneos conservan una valoración', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  const submitted = await submitReport(credential.deviceToken, syntheticReport());
  const receipt = await submitted.json();
  const path = `/api/devices/${created.device.id}/analyses/${receipt.analysisId}`;
  const foreign = await registerApi(newAccount());
  for (const suffix of ['explanation', 'feedback']) {
    expect((await accountApi(`${path}/${suffix}`, { token: foreign.accessToken, ...(suffix === 'feedback' ? { body: { rating: 'helpful' } } : {}) })).status).toBe(404);
    expect((await accountApi(`${path}/${suffix}`, { token: credential.deviceToken, scheme: 'Device', ...(suffix === 'feedback' ? { body: { rating: 'helpful' } } : {}) })).status).toBe(401);
  }
  const results = await Promise.all(['helpful', 'unclear', 'incorrect'].map(rating => accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating, comment: 'Valoración concurrente' } })));
  expect(results.map(r => r.status)).toEqual([200, 200, 200]);
  expect((await accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating: 'helpful', userId: foreign.user.id } })).status).toBe(400);
  expect((await accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating: 'helpful', comment: 'x'.repeat(1001) } })).status).toBe(400);
  expect((await accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating: 'helpful', comment: 'x'.repeat(9000) } })).status).toBe(413);
  const guide = await (await accountApi(`${path}/explanation`, { token: owner.accessToken })).json();
  expect(['helpful', 'unclear', 'incorrect']).toContain(guide.feedback.rating);
  expect(guide.analysis.score).toBe(100);
});
