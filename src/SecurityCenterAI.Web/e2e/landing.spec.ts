import { test, expect, loginAccount } from './fixtures.ts';
import { registerVerifiedOwner } from './device-api.ts';

test('la página pública explica el alcance y permite registrarse en escritorio y móvil', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tu seguridad empieza por entender qué revisar.');
  await expect(page.getByText('Proyecto en desarrollo.', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Conocer el recorrido', exact: true }).click();
  await expect(page).toHaveURL(/#como-funciona$/);
  await page.setViewportSize({ width: 360, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByText('¿Qué Windows admite el agente actual?', { exact: true }).click();
  await expect(page.getByText('La vista previa está orientada a Windows 11 x64.', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Crear mi cuenta', exact: true }).click();
  await expect(page).toHaveURL(/\/register$/);
});

test('una sesión existente conserva el acceso directo al dashboard desde la raíz', async ({ page, account }) => {
  await registerVerifiedOwner(account); await loginAccount(page, account);
  await page.goto('/'); await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole('link', { name: 'Enlaces y mensajes', exact: true })).toBeVisible();
});
