import { test, expect, loginAccount } from './fixtures.ts';
import { registerVerifiedOwner } from './device-api.ts';
import { accountApi } from './api.ts';

test('editar el nombre actualiza perfil y navegación sin renovar tokens ni cerrar otras sesiones', async ({ page, browser, account }) => {
  test.setTimeout(90_000);
  const owner = await registerVerifiedOwner(account);
  await loginAccount(page, account);
  const otherContext = await browser.newContext(); const otherPage = await otherContext.newPage();
  try {
    await loginAccount(otherPage, account);
    const before = (await page.context().cookies()).filter(cookie => cookie.name.startsWith('security_center_'));
    await page.goto('/account');
    const form = page.getByRole('form', { name: 'Editar nombre del perfil', exact: true });
    await form.getByLabel('Nombre completo', { exact: true }).fill('  María del Mar  ');
    await form.getByRole('button', { name: 'Guardar nombre', exact: true }).click();
    await expect(form.getByRole('status')).toHaveText('Tu nombre se actualizó.');
    await expect(form.getByLabel('Nombre completo', { exact: true })).toHaveValue('María del Mar');
    const after = (await page.context().cookies()).filter(cookie => cookie.name.startsWith('security_center_'));
    expect(after.map(cookie => [cookie.name, cookie.value, cookie.expires]).sort()).toEqual(before.map(cookie => [cookie.name, cookie.value, cookie.expires]).sort());
    await page.goto('/dashboard');
    await expect(page.locator('.sidebar-user-meta strong')).toHaveText('María del Mar');
    await page.goto('/devices');
    await expect(page.locator('.sidebar-user-meta strong')).toHaveText('María del Mar');
    await otherPage.goto('/dashboard');
    await expect(otherPage.locator('.sidebar-user-meta strong')).toHaveText('María del Mar');
    const result = await accountApi('/api/auth/me', { token: owner.accessToken });
    expect(result.status).toBe(200); const user = await result.json();
    expect(user.name).toBe('María del Mar'); expect(user.email).toBe(account.email); expect(user.emailVerified).toBe(true);
    const sessions = await (await accountApi('/api/account/sessions', { token: owner.accessToken })).json();
    expect(sessions.sessions).toHaveLength(3);
  } finally { await otherContext.close(); }
});

test('perfil móvil valida el nombre y muestra texto introducido sin interpretarlo como HTML', async ({ page, account }) => {
  await registerVerifiedOwner(account); await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/account');
  const form = page.getByRole('form', { name: 'Editar nombre del perfil', exact: true });
  await form.getByLabel('Nombre completo', { exact: true }).fill('A');
  await form.getByRole('button', { name: 'Guardar nombre', exact: true }).click();
  await expect(form.getByText('Usá entre 2 y 100 caracteres, sin saltos de línea o caracteres de control.', { exact: true })).toBeVisible();
  const literal = '<img src=x onerror="window.__profileInjected=1">';
  await form.getByLabel('Nombre completo', { exact: true }).fill(literal);
  await form.getByRole('button', { name: 'Guardar nombre', exact: true }).click();
  await expect(form.getByRole('status')).toHaveText('Tu nombre se actualizó.');
  await expect(form.getByLabel('Nombre completo', { exact: true })).toHaveValue(literal);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.goto('/dashboard');
  await expect(page.locator('.sidebar-user-meta strong')).toHaveText(literal);
  expect(await page.evaluate(() => '__profileInjected' in window)).toBe(false);
  expect(await page.locator('img[onerror]').count()).toBe(0);
});
