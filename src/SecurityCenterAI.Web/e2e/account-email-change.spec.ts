import { randomBytes } from 'node:crypto';
import { test, expect, loginAccount, allowTestAccountVerificationResend } from './fixtures.ts';
import { accountApi } from './api.ts';
import { registerVerifiedOwner } from './device-api.ts';
import { waitForAccountLink, waitForPasswordChangeNotice, openAccountLink } from './mailpit.ts';

async function begin(page: import('@playwright/test').Page, currentPassword: string, target: string) {
  const form = page.getByRole('form', { name: 'Solicitar cambio de correo', exact: true });
  await form.getByLabel('Correo nuevo', { exact: true }).fill(target);
  await form.getByLabel('Repetí el correo nuevo', { exact: true }).fill(target);
  await form.getByLabel('Contraseña actual', { exact: true }).fill(currentPassword);
  await form.getByRole('checkbox').check();
  await form.getByRole('button', { name: 'Enviar confirmaciones', exact: true }).click();
}

test('dos direcciones se confirman antes de finalizar el cambio y cerrar las sesiones web', async ({ page, account, newAccount }) => {
  test.setTimeout(100_000);
  const owner = await registerVerifiedOwner(account); const target = newAccount();
  allowTestAccountVerificationResend(account.email); await loginAccount(page, account); await page.goto('/account');
  await begin(page, account.password, target.email);
  await expect(page.getByRole('heading', { name: 'Solicitud pendiente', exact: true })).toBeVisible();
  const finish = page.getByRole('form', { name: 'Finalizar cambio de correo', exact: true });
  await expect(finish.getByRole('button', { name: 'Finalizar cambio de correo', exact: true })).toBeDisabled();
  for (const [address, kind] of [[account.email, 'current'], [target.email, 'new']] as const) {
    const link = await waitForAccountLink(address, 'confirm-email-change', undefined, kind);
    const approval = await page.context().newPage();
    try {
      await openAccountLink(approval, link);
      await expect.poll(() => new URL(approval.url()).search).toBe('');
      const status = await (await accountApi('/api/account/email', { token: owner.accessToken })).json();
      expect(kind === 'current' ? status.pending.currentAddressConfirmed : status.pending.newAddressConfirmed).toBe(false);
      await approval.getByRole('checkbox').check();
      await approval.getByRole('button', { name: 'Confirmar dirección', exact: true }).click();
      await expect(approval.getByRole('status')).toContainText('Confirmación recibida.');
    } finally { await approval.close(); }
  }
  await page.getByRole('button', { name: 'Actualizar confirmaciones', exact: true }).click();
  await expect(finish.getByRole('button', { name: 'Finalizar cambio de correo', exact: true })).toBeEnabled();
  await finish.getByLabel('Contraseña actual para finalizar', { exact: true }).fill(account.password);
  await finish.getByRole('checkbox').check();
  await finish.getByRole('button', { name: 'Finalizar cambio de correo', exact: true }).click();
  await expect(page).toHaveURL(/\/login\?reason=email-changed$/);
  expect((await page.context().cookies()).filter(cookie => ['security_center_session', 'security_center_refresh'].includes(cookie.name))).toHaveLength(0);
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(401);
  expect((await accountApi('/api/auth/refresh', { body: { refreshToken: owner.refreshToken } })).status).toBe(401);
  expect((await accountApi('/api/auth/login', { body: { email: account.email, password: account.password } })).status).toBe(401);
  for (const address of [account.email, target.email]) {
    const notice = await waitForPasswordChangeNotice(address, 'email');
    for (const secret of [account.password, owner.accessToken, 'http://', 'https://']) expect(notice.includes(secret)).toBe(false);
  }
  await loginAccount(page, { ...account, email: target.email }); await page.goto('/account/activity');
  await expect(page.getByRole('list').getByText('Correo cambiado', { exact: true })).toBeVisible();
});

test('la vista móvil permite cancelar, invalida los enlaces y mantiene el correo actual', async ({ page, account, newAccount }) => {
  const owner = await registerVerifiedOwner(account); const target = newAccount();
  allowTestAccountVerificationResend(account.email); await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 }); await page.goto('/account');
  await begin(page, 'wrong fictitious password', target.email);
  await expect(page.getByRole('form', { name: 'Solicitar cambio de correo', exact: true }).getByRole('alert')).toContainText('No pudimos confirmar');
  await begin(page, account.password, target.email);
  await expect(page.getByRole('heading', { name: 'Solicitud pendiente', exact: true })).toBeVisible();
  const oldLink = await waitForAccountLink(target.email, 'confirm-email-change', undefined, 'new');
  const cancel = page.getByRole('form', { name: 'Cancelar cambio de correo', exact: true });
  await cancel.getByRole('checkbox').check(); await cancel.getByRole('button', { name: 'Cancelar solicitud', exact: true }).click();
  await expect(page.getByText('La solicitud fue cancelada. Conservás tu correo actual.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Solicitud pendiente', exact: true })).toHaveCount(0);
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  expect((await accountApi('/api/account/email/confirm-new', { body: { token: new URL(oldLink).searchParams.get('token'), confirmed: true } })).status).toBe(400);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test('recuperar la contraseña cancela el cambio de correo pendiente y envía un aviso sin enlaces', async ({ page, account, newAccount }) => {
  test.setTimeout(80_000);
  const owner = await registerVerifiedOwner(account); const target = newAccount(); allowTestAccountVerificationResend(account.email);
  expect((await accountApi('/api/account/email/request', { token: owner.accessToken, body: { newEmail: target.email, currentPassword: account.password, confirmed: true } })).status).toBe(204);
  const pendingLink = await waitForAccountLink(target.email, 'confirm-email-change', undefined, 'new');
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const resetLink = await waitForAccountLink(account.email, 'reset-password'); const replacement = randomBytes(24).toString('hex');
  await openAccountLink(page, resetLink);
  await page.getByLabel('Contraseña', { exact: true }).fill(replacement); await page.getByLabel('Confirmar contraseña', { exact: true }).fill(replacement);
  await page.getByRole('button', { name: 'Guardar contraseña', exact: true }).click(); await expect(page.getByRole('status')).toContainText('Tu contraseña se actualizó.');
  const notice = await waitForPasswordChangeNotice(account.email, 'reset');
  for (const secret of [replacement, account.password, owner.refreshToken, 'http://', 'https://']) expect(notice.includes(secret)).toBe(false);
  expect((await accountApi('/api/account/email/confirm-new', { body: { token: new URL(pendingLink).searchParams.get('token'), confirmed: true } })).status).toBe(400);
  await loginAccount(page, { ...account, password: replacement });
});
