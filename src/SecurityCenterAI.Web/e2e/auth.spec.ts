import { randomBytes, randomUUID } from 'node:crypto';
import { test, expect, registerAccount, logout, seedTestAnalyses } from './fixtures.ts';

test('registro real, dashboard vacío, cierre de sesión y nuevo login', async ({ page, context, account }) => {
  await registerAccount(page, account);
  await expect(page.getByRole('heading', { name: 'Todavía no hay análisis registrados' })).toBeVisible();

  const session = (await context.cookies()).find(cookie => cookie.name === 'security_center_session');
  expect(Boolean(session)).toBe(true);
  expect(session?.httpOnly).toBe(true);
  expect(session?.sameSite).toBe('Lax');

  await logout(page);
  expect((await context.cookies()).some(cookie => cookie.name === 'security_center_session')).toBe(false);
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login(?:\?.*)?$/);

  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByLabel('Contraseña', { exact: true }).fill(account.password);
  await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/?$/);
  await expect(page.getByRole('main').getByRole('heading', { name: /tu seguridad/i })).toBeVisible();
});

test('el dashboard redirige al login cuando no existe cookie', async ({ page }) => {
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
  await expect(page.getByLabel('Correo electrónico', { exact: true })).toBeVisible();
});

test('una cookie estructuralmente válida con firma falsa no autoriza el dashboard', async ({ page, context }) => {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString('base64url');
  const value = [
    encode({ alg: 'HS256', typ: 'JWT' }),
    encode({ sub: randomUUID(), name: 'Usuario E2E', email: `e2e-${randomUUID()}@example.invalid`,
      exp: Math.floor(Date.now() / 1000) + 3600 }),
    randomBytes(32).toString('base64url'),
  ].join('.');
  await context.addCookies([{ name: 'security_center_session', value,
    url: 'http://localhost:3000', httpOnly: true, sameSite: 'Lax' }]);
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
});

test('un registro inválido permanece en el formulario sin crear sesión', async ({ page, context }) => {
  await page.goto('/register');
  await page.getByLabel('Nombre completo', { exact: true }).fill('A');
  await page.getByLabel('Correo electrónico', { exact: true }).fill('not-an-email');
  await page.getByLabel('Contraseña', { exact: true }).fill(randomBytes(3).toString('hex'));
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(randomBytes(3).toString('hex'));
  await page.getByRole('button', { name: 'Crear cuenta', exact: true }).click();
  await expect(page).toHaveURL(/\/register\/?$/);
  expect((await context.cookies()).some(cookie => cookie.name === 'security_center_session')).toBe(false);
  await expect(page.getByLabel('Nombre completo', { exact: true })).toBeVisible();
});

test('el backend rechaza un correo duplicado y una contraseña incorrecta', async ({ page, context, account }) => {
  await registerAccount(page, account);
  await logout(page);

  await page.goto('/register');
  await page.getByLabel('Nombre completo', { exact: true }).fill(account.name);
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByLabel('Contraseña', { exact: true }).fill(account.password);
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(account.password);
  await page.getByRole('button', { name: 'Crear cuenta', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: /correo.*registrad|ya.*existe/i })).toBeVisible();
  await expect(page).toHaveURL(/\/register\/?$/);
  expect((await context.cookies()).some(cookie => cookie.name === 'security_center_session')).toBe(false);

  await page.goto('/login');
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByLabel('Contraseña', { exact: true }).fill(randomBytes(24).toString('base64url'));
  await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: /credenciales|correo.*contraseña|datos.*incorrect/i })).toBeVisible();
  await expect(page).toHaveURL(/\/login\/?$/);
  expect((await context.cookies()).some(cookie => cookie.name === 'security_center_session')).toBe(false);
});

test('registro y dashboard se adaptan a un ancho móvil de 360 píxeles', async ({ page, account }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/register');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await registerAccount(page, account);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await expect(page.getByRole('button', { name: 'Cerrar sesión', exact: true })).toBeVisible();
  if (process.env.SC_AI_VISUAL_REVIEW === '1') {
    await page.screenshot({ path: 'test-results/dashboard-mobile.png', fullPage: true });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.screenshot({ path: 'test-results/dashboard-desktop.png', fullPage: true });
  }
});

test('el historial muestra los últimos cinco registros reales y el total de análisis', async ({ page, account }) => {
  await registerAccount(page, account);
  seedTestAnalyses(account.email);
  await page.reload();
  const table = page.getByRole('table');
  await expect(table.getByRole('row')).toHaveCount(6);
  await expect(table.getByRole('cell', { name: '91', exact: true })).toBeVisible();
  await expect(table.getByRole('cell', { name: '85', exact: true })).toHaveCount(0);
  await expect(page.locator('.metric-card').filter({ hasText: 'Análisis registrados' }).locator('.metric-value')).toHaveText('7');
  await expect(table.locator('time').first()).toHaveAttribute('datetime', /2026-10-20T12:00:00/);
});
