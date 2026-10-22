import { test, expect, loginAccount } from './fixtures.ts';
import { registerVerifiedOwner, createDevice, pairDevice } from './device-api.ts';
import { syntheticReport, submitReport } from './analysis-api.ts';

test('el dueño ve en móvil resultados enviados, cobertura y evidencia como texto sin ejecutar HTML', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de ingesta E2E');
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport({ 'WIN-DEFENDER-ANTIVIRUS': 'attention', 'WIN-DEFENDER-REALTIME': 'unknown' });
  expect((await submitReport(credential.deviceToken, report)).status).toBe(201);
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/devices/${created.device.id}`);
  const main = page.getByRole('main');
  await expect(main.getByRole('heading', { name: 'Análisis del equipo', exact: true })).toBeVisible();
  await expect(main).toContainText('70');
  await expect(main).toContainText(/90\s*%/);
  await expect(main).toContainText(/revisar|atención/i);
  await expect(main).toContainText(/sin determinar|desconocid/i);
  await expect(main.getByText(report.checks[0].evidence, { exact: true }).first()).toBeVisible();
  expect(await page.evaluate(() => Object.hasOwn(window, '__analysisScriptExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(!(await main.innerText()).includes(credential.deviceToken)).toBe(true);
  await page.goto('/dashboard');
  await expect(page.getByRole('main')).toContainText('70');
  await expect(page.getByRole('table')).toBeVisible();
});
