import { test, expect, registerAccount } from './fixtures.ts';
import { waitForAccountLink, openAccountLink } from './mailpit.ts';
import { pairDevice, heartbeat } from './device-api.ts';

test('dispositivos orienta a verificar el correo antes de agregar un equipo', async ({ page, account }) => {
  await registerAccount(page, account);
  await page.goto('/devices');
  await expect(page.getByRole('main')).toContainText(/verificá|verificar|verificación/i);
  await expect(page.getByRole('link', { name: /cuenta|verificar/i }).first()).toHaveAttribute('href', '/account');
  const create = page.getByRole('button', { name: 'Agregar equipo', exact: true });
  if (await create.count()) await expect(create).toBeDisabled();
});

test('alta, revinculación y revocación confirmada funcionan en móvil sin simular análisis', async ({ page, account }) => {
  test.setTimeout(90_000);
  await page.setViewportSize({ width: 360, height: 800 });
  await registerAccount(page, account);
  const verification = await waitForAccountLink(account.email, 'verify-email');
  await openAccountLink(page, verification);
  await page.getByRole('button', { name: 'Confirmar correo', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /correo.*verificado/i })).toBeVisible();
  await page.goto('/devices');
  await page.getByLabel('Nombre del equipo', { exact: true }).fill('Equipo de prueba móvil');
  await page.getByRole('button', { name: 'Agregar equipo', exact: true }).click();
  const codeField = page.getByLabel('Código de vinculación', { exact: true });
  await expect(codeField).toBeVisible();
  const firstCode = await codeField.inputValue();
  expect(/^[A-Za-z0-9_-]{43}$/.test(firstCode)).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const firstCredential = await pairDevice(firstCode);
  expect((await heartbeat(firstCredential.deviceToken)).status).toBe(204);
  await page.goto(`/devices/${firstCredential.deviceId}`);
  await expect(page.getByRole('heading', { name: 'Análisis del equipo', exact: true })).toBeVisible();
  await expect(page.getByText('Todavía no hay análisis de este equipo', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Generar nuevo código', exact: true }).click();
  await expect(codeField).toBeVisible();
  const secondCode = await codeField.inputValue();
  expect(secondCode !== firstCode).toBe(true);
  const current = await pairDevice(secondCode);
  expect((await heartbeat(firstCredential.deviceToken)).status).toBe(401);
  expect((await heartbeat(current.deviceToken)).status).toBe(204);

  await page.reload();
  await page.getByRole('button', { name: 'Revocar acceso', exact: true }).click();
  await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  expect((await heartbeat(current.deviceToken)).status).toBe(204);
  await page.getByRole('button', { name: 'Revocar acceso', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar revocación', exact: true }).click();
  await expect(page.getByRole('main')).toContainText(/revocad/i);
  expect((await heartbeat(current.deviceToken)).status).toBe(401);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
