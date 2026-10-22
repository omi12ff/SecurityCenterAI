import { randomBytes } from 'node:crypto';
import { test, expect, registerAccount, loginAccount, allowTestAccountVerificationResend } from './fixtures.ts';
import { accountMessages, waitForAccountLink, openAccountLink } from './mailpit.ts';

test('el correo de registro se confirma una vez y la cuenta refleja su estado', async ({ page, account }) => {
  await registerAccount(page, account);
  await page.goto('/account');
  await expect(page.getByText('Verificación pendiente', { exact: true })).toBeVisible();
  const link = await waitForAccountLink(account.email, 'verify-email');
  await openAccountLink(page, link);
  await expect(page.getByRole('button', { name: 'Confirmar correo', exact: true })).toBeEnabled();
  await expect.poll(() => !new URL(page.url()).searchParams.has('token')).toBe(true);
  await page.getByRole('button', { name: 'Confirmar correo', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /correo.*verificado/i })).toBeVisible();
  await page.goto('/account');
  await expect(page.getByText('Correo verificado', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enviar correo de verificación', exact: true })).toHaveCount(0);

  await openAccountLink(page, link);
  await page.getByRole('button', { name: 'Confirmar correo', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: /no es válido|venció/i })).toBeVisible();
});

test('la cuenta puede reenviar su verificación sin inspeccionar correos ajenos', async ({ page, account }) => {
  await registerAccount(page, account);
  const firstLink = await waitForAccountLink(account.email, 'verify-email');
  allowTestAccountVerificationResend(account.email);
  await page.goto('/account');
  await page.getByRole('button', { name: 'Enviar correo de verificación', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /recibirás.*enlace/i })).toBeVisible();
  const secondLink = await waitForAccountLink(account.email, 'verify-email', firstLink);
  expect(secondLink !== firstLink).toBe(true);
  expect((await accountMessages(account.email)).length).toBe(2);
  await openAccountLink(page, secondLink);
  await page.getByRole('button', { name: 'Confirmar correo', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /correo.*verificado/i })).toBeVisible();
});

test('la recuperación cambia la contraseña una vez y revoca otras sesiones', async ({ browser, page, account, baseURL }) => {
  test.setTimeout(90_000);
  await registerAccount(page, account);
  const secondContext = await browser.newContext({ baseURL });
  try {
    const secondPage = await secondContext.newPage();
    await loginAccount(secondPage, account);
    await page.goto('/forgot-password');
    await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
    await page.getByRole('button', { name: 'Enviar enlace', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: /si existe una cuenta/i })).toBeVisible();
    const link = await waitForAccountLink(account.email, 'reset-password');
    await openAccountLink(page, link);
    await expect.poll(() => !new URL(page.url()).searchParams.has('token')).toBe(true);
    const replacement = randomBytes(24).toString('base64url');
    await page.getByLabel('Contraseña', { exact: true }).fill(replacement);
    await page.getByLabel('Confirmar contraseña', { exact: true }).fill(replacement);
    await page.getByRole('button', { name: 'Guardar contraseña', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: /contraseña.*actualizó/i })).toBeVisible();
    await secondPage.goto('/dashboard');
    await expect(secondPage).toHaveURL(/\/login(?:\?.*)?$/);

    await page.goto('/login');
    await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
    await page.getByLabel('Contraseña', { exact: true }).fill(account.password);
    await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: /correo.*contraseña|credenciales/i })).toBeVisible();
    await loginAccount(page, { ...account, password: replacement });

    await openAccountLink(page, link);
    await page.getByLabel('Contraseña', { exact: true }).fill(replacement);
    await page.getByLabel('Confirmar contraseña', { exact: true }).fill(replacement);
    await page.getByRole('button', { name: 'Guardar contraseña', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: /no es válido|venció/i })).toBeVisible();
  } finally {
    await secondContext.close();
  }
});

test('recuperar una cuenta desconocida conserva la respuesta genérica y no envía correo', async ({ page, account }) => {
  await page.goto('/forgot-password');
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  await page.getByRole('button', { name: 'Enviar enlace', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: /si existe una cuenta/i })).toBeVisible();
  expect((await accountMessages(account.email)).length).toBe(0);
});

test('cerrar todas las sesiones expulsa otro navegador de la misma cuenta', async ({ browser, page, account, baseURL }) => {
  await registerAccount(page, account);
  const secondContext = await browser.newContext({ baseURL });
  try {
    const secondPage = await secondContext.newPage();
    await loginAccount(secondPage, account);
    await page.goto('/account');
    await page.getByRole('button', { name: 'Cerrar todas las sesiones', exact: true }).click();
    await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
    await secondPage.goto('/dashboard');
    await expect(secondPage).toHaveURL(/\/login(?:\?.*)?$/);
    const names = (await secondContext.cookies()).map(cookie => cookie.name);
    expect(names.includes('security_center_session')).toBe(false);
    expect(names.includes('security_center_refresh')).toBe(false);
  } finally {
    await secondContext.close();
  }
});

test('un navegador sin access cookie renueva con refresh y mantiene el dashboard', async ({ page, context, account }) => {
  await registerAccount(page, account);
  const before = (await context.cookies()).find(cookie => cookie.name === 'security_center_refresh');
  expect(Boolean(before)).toBe(true);
  await context.clearCookies({ name: 'security_center_session' });
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/dashboard\/?$/);
  await expect(page.getByRole('main').getByRole('heading', { name: /tu seguridad/i })).toBeVisible();
  const refreshed = (await context.cookies()).find(cookie => cookie.name === 'security_center_refresh');
  expect(Boolean(refreshed)).toBe(true);
  expect(refreshed?.value !== before?.value).toBe(true);
  expect(refreshed?.httpOnly).toBe(true);
  expect(refreshed?.sameSite).toBe('Lax');
});
