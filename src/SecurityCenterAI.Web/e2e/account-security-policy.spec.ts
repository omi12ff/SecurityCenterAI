import { randomBytes } from 'node:crypto';
import { test, expect, registerAccount, loginAccount } from './fixtures.ts';
import { accountApi, registerApi } from './api.ts';
import { waitForAccountLink } from './mailpit.ts';

const help = 'Usá entre 15 y 128 caracteres y evitá contraseñas comunes o previsibles.';

test('registro explica el rechazo local y permite una frase larga sin exigir símbolos', async ({ page, account }) => {
  await page.goto('/register');
  await page.getByLabel('Nombre completo', { exact: true }).fill(account.name);
  await page.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
  for (const value of ['passwordpassword', '🔒'.repeat(8)]) {
    await page.getByLabel('Contraseña', { exact: true }).fill(value);
    await page.getByLabel('Confirmar contraseña', { exact: true }).fill(value);
    await page.getByRole('button', { name: 'Crear cuenta', exact: true }).click();
    await expect(page.getByText(help, { exact: true })).toBeVisible();
    expect(new URL(page.url()).pathname).toBe('/register');
  }
  account.password = 'frase de prueba larga ' + randomBytes(10).toString('hex');
  await registerAccount(page, account);
  await expect(page).toHaveURL(/\/dashboard$/);
});

test('recuperación mantiene el enlace después de rechazar una contraseña previsible', async ({ page, account }) => {
  const owner = await registerApi(account);
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const link = await waitForAccountLink(account.email, 'reset-password');
  await page.goto(link);
  await page.getByLabel('Contraseña', { exact: true }).fill('passwordpassword');
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill('passwordpassword');
  await page.getByRole('button', { name: 'Guardar contraseña', exact: true }).click();
  await expect(page.getByText(help, { exact: true })).toBeVisible();
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  const replacement = '  frase de recuperación ' + randomBytes(10).toString('hex') + '  ';
  await page.getByLabel('Contraseña', { exact: true }).fill(replacement);
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(replacement);
  await page.getByRole('button', { name: 'Guardar contraseña', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Tu contraseña se actualizó.');
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(401);
  await loginAccount(page, { ...account, password: replacement });
});

test('el límite de sesiones se informa en la web y cerrar una permite volver a entrar', async ({ page, browser, account }) => {
  const owner = await registerApi(account); await loginAccount(page, account);
  for (let index = 0; index < 8; index++)
    expect((await accountApi('/api/auth/login', { body: { email: account.email, password: account.password } })).status).toBe(200);
  await page.goto('/account/sessions');
  await expect(page.getByText(/Tu cuenta admite hasta 10 sesiones web habilitadas/)).toBeVisible();
  const other = await browser.newContext();
  try {
    const login = await other.newPage(); await login.goto('/login');
    await login.getByLabel('Correo electrónico', { exact: true }).fill(account.email);
    await login.getByLabel('Contraseña', { exact: true }).fill(account.password);
    await login.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
    await expect(login.locator('form.auth-form').getByRole('alert')).toContainText('Se alcanzó el límite de sesiones web.');
    expect((await other.cookies()).filter(cookie => ['security_center_session', 'security_center_refresh'].includes(cookie.name))).toHaveLength(0);
    const list = await (await accountApi('/api/account/sessions', { token: owner.accessToken })).json();
    expect(list.sessions).toHaveLength(10);
    const target = list.sessions.find((session: { isCurrent: boolean }) => !session.isCurrent)!;
    expect((await accountApi(`/api/account/sessions/${target.id}/revoke`, { token: owner.accessToken, body: { confirmed: true } })).status).toBe(200);
    await loginAccount(login, account);
    expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  } finally { await other.close(); }
});
