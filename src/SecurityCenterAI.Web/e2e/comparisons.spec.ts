import { test, expect, loginAccount } from './fixtures.ts';
import { accountApi, registerApi } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt } from './analysis-api.ts';

function comparisonPath(deviceId: string, baselineId: string, targetId: string) {
  const query = new URLSearchParams({ baselineId, targetId });
  return `/api/devices/${deviceId}/analyses/compare?${query}`;
}

test('comparación distingue cambios e información faltante sin modificar informes y respeta al propietario', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de comparación API');
  const credential = await pairDevice(created.pairingCode);
  const baselineReport = syntheticReport({ 'WIN-DEFENDER-ANTIVIRUS': 'attention', 'WIN-TPM-READY': 'unknown' });
  const first = await submitReport(credential.deviceToken, baselineReport);
  expect(first.status).toBe(201);
  const baseline = await first.json() as AnalysisReceipt;
  const targetReport = syntheticReport({ 'WIN-UAC-ENABLED': 'unknown' });
  const second = await submitReport(credential.deviceToken, targetReport);
  expect(second.status).toBe(201);
  const target = await second.json() as AnalysisReceipt;
  const detailPath = `/api/devices/${created.device.id}`;
  const before = await (await accountApi(detailPath, { token: owner.accessToken })).json();

  const path = comparisonPath(created.device.id, baseline.analysisId, target.analysisId);
  const response = await accountApi(path, { token: owner.accessToken });
  expect(response.status).toBe(200);
  const comparison = await response.json();
  expect(comparison.deviceId).toBe(created.device.id);
  expect(comparison.baseline.id).toBe(baseline.analysisId);
  expect(comparison.target.id).toBe(target.analysisId);
  expect(comparison.baseline.score).toBe(80);
  expect(comparison.target.score).toBe(90);
  expect(comparison.scoreComparable).toBe(true);
  expect(comparison.scoreDelta).toBe(10);
  expect(comparison.coverageDelta).toBe(0);
  expect(comparison.summary).toEqual({ improved: 1, needsReview: 0, newInformation: 1, lostInformation: 1, unchanged: 7, notComparable: 0 });
  expect(comparison.checks).toHaveLength(10);
  expect(comparison.checks.find((check: { ruleId: string }) => check.ruleId === 'WIN-DEFENDER-ANTIVIRUS')).toMatchObject({ baselineStatus: 'attention', targetStatus: 'passed', change: 'improved' });
  expect(comparison.checks.find((check: { ruleId: string }) => check.ruleId === 'WIN-TPM-READY')).toMatchObject({ baselineStatus: 'unknown', targetStatus: 'passed', change: 'newInformation' });
  expect(comparison.checks.find((check: { ruleId: string }) => check.ruleId === 'WIN-UAC-ENABLED')).toMatchObject({ baselineStatus: 'passed', targetStatus: 'unknown', change: 'lostInformation' });

  expect((await accountApi(path)).status).toBe(401);
  expect((await accountApi(path, { token: credential.deviceToken, scheme: 'Device' })).status).toBe(401);
  const foreign = await registerApi(newAccount());
  expect((await accountApi(path, { token: foreign.accessToken })).status).toBe(404);
  expect((await accountApi(comparisonPath(created.device.id, target.analysisId, baseline.analysisId), { token: owner.accessToken })).status).toBe(400);
  expect((await accountApi(comparisonPath(created.device.id, baseline.analysisId, baseline.analysisId), { token: owner.accessToken })).status).toBe(400);
  const after = await (await accountApi(detailPath, { token: owner.accessToken })).json();
  expect(after).toEqual(before);
  expect(comparison.baseline.findings.find((finding: { ruleId: string }) => finding.ruleId === 'WIN-DEFENDER-ANTIVIRUS').evidence).toBe(baselineReport.checks[0].evidence);
  expect(comparison.target.findings.find((finding: { ruleId: string }) => finding.ruleId === 'WIN-DEFENDER-ANTIVIRUS').evidence).toBe(targetReport.checks[0].evidence);
});

test('comparación móvil permite elegir informes y muestra evidencia como texto e información perdida', async ({ page, account }, testInfo) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de comparación móvil');
  const credential = await pairDevice(created.pairingCode);
  const first = await submitReport(credential.deviceToken, syntheticReport({ 'WIN-DEFENDER-ANTIVIRUS': 'attention', 'WIN-TPM-READY': 'unknown' }));
  expect(first.status).toBe(201);
  const baseline = await first.json() as AnalysisReceipt;
  const targetReport = syntheticReport({ 'WIN-UAC-ENABLED': 'unknown' });
  const second = await submitReport(credential.deviceToken, targetReport);
  expect(second.status).toBe(201);
  const target = await second.json() as AnalysisReceipt;
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/devices/${created.device.id}`);
  await page.getByRole('link', { name: 'Comparar análisis', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Comparar análisis', exact: true })).toBeVisible();
  await page.getByLabel('Análisis anterior', { exact: true }).selectOption(baseline.analysisId);
  await page.getByLabel('Análisis reciente', { exact: true }).selectOption(target.analysisId);
  await page.getByRole('button', { name: 'Comparar', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/devices/${created.device.id}/compare\\?`));
  const main = page.getByRole('main');
  await expect(main).toContainText('Mejoras observadas');
  await expect(main).toContainText('Nueva información');
  await expect(main).toContainText('Información perdida');
  await expect(main).toContainText(/desconocid/i);
  await expect(main.getByText(targetReport.checks[0].evidence, { exact: true }).first()).toBeVisible();
  expect(await page.evaluate(() => Object.hasOwn(window, '__analysisScriptExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect((await main.innerText()).includes(credential.deviceToken)).toBe(false);
  await page.reload();
  await expect(page.getByLabel('Análisis anterior', { exact: true })).toHaveValue(baseline.analysisId);
  await expect(page.getByLabel('Análisis reciente', { exact: true })).toHaveValue(target.analysisId);
  await page.getByLabel('Análisis reciente', { exact: true }).selectOption(baseline.analysisId);
  await page.getByRole('button', { name: 'Comparar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Elegí dos análisis distintos');
  await page.getByLabel('Análisis reciente', { exact: true }).selectOption(target.analysisId);
  await page.getByRole('button', { name: 'Comparar', exact: true }).click();
  await expect(main).toContainText('Información perdida');
  if (process.env.E2E_CAPTURE_COMPARISON === '1') {
    await page.getByRole('heading', { name: 'Qué cambió', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: testInfo.outputPath('comparison-mobile.png') });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByRole('heading', { name: 'Qué cambió', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: testInfo.outputPath('comparison-desktop.png') });
  }
  await page.getByRole('link', { name: '← Volver al historial del equipo', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/devices/${created.device.id}/?$`));
});

test('un solo informe conserva el historial y pide dos análisis antes de comparar', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo con un informe');
  const credential = await pairDevice(created.pairingCode);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(201);
  await loginAccount(page, account);
  await page.goto(`/devices/${created.device.id}`);
  await expect(page.getByRole('link', { name: 'Comparar análisis', exact: true })).toHaveCount(0);
  await page.goto(`/devices/${created.device.id}/compare`);
  await expect(page.getByRole('heading', { name: 'Comparar análisis', exact: true })).toBeVisible();
  await expect(page.getByRole('main')).toContainText(/dos análisis/i);
  await expect(page.getByRole('button', { name: 'Comparar', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: '← Volver al historial del equipo', exact: true })).toBeVisible();
});
