import { mkdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { test, expect } from './ai-explanation-fixture.ts';

test.beforeEach(async ({ aiApp, context }) => {
  aiApp.reset();
  await context.addCookies([{ name: 'security_center_session', value: aiApp.token, url: aiApp.baseUrl, httpOnly: true, sameSite: 'Lax' }]);
});

for (const width of [360, 1440]) {
  test(`botón ${width}px comunica presión sin mover el layout ni duplicar la solicitud`, async ({ page, aiApp }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(aiApp.baseUrl + aiApp.analysisPath);
    const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
    const button = panel.getByRole('button', { name: 'Ampliar explicación con IA', exact: true });
    await expect(button).toBeEnabled();
    await button.scrollIntoViewIfNeeded();
    const initial = await button.evaluate(element => ({ color: getComputedStyle(element).backgroundColor, width: (element as HTMLElement).offsetWidth }));
    await button.hover();
    await expect.poll(() => button.evaluate(element => getComputedStyle(element).backgroundColor)).not.toBe(initial.color);
    const output = resolve(process.cwd(), '../../artifacts/buttons-review');
    mkdirSync(output, { recursive: true });
    await button.screenshot({ path: join(output, `hover-${width}.png`) });
    const box = (await button.boundingBox())!;
    aiApp.holdResponse = true;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    try {
      await expect.poll(() => button.evaluate(element => {
        const transform = getComputedStyle(element).transform;
        return transform === 'none' ? 1 : new DOMMatrix(transform).a;
      })).toBeLessThan(1);
      expect(await button.evaluate(element => (element as HTMLElement).offsetWidth)).toBe(initial.width);
      await expect.poll(() => button.evaluate(element => getComputedStyle(element).boxShadow)).toBe('none');
      expect(aiApp.postBodies).toEqual([]);
      await button.screenshot({ path: join(output, `pressed-${width}.png`) });
    } finally { await page.mouse.up(); }
    await expect(panel.getByRole('status')).toHaveText('Pensando');
    await expect(button).toBeHidden();
    await expect.poll(() => aiApp.postBodies.length).toBe(1);
    aiApp.release();
    await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toBeVisible();
    expect(aiApp.postBodies).toEqual([{ confirmed: true }]);
  });
}

test('movimiento reducido conserva teclado, foco y solicitud sin transformación', async ({ page, aiApp }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(aiApp.baseUrl + aiApp.analysisPath);
  const button = page.getByRole('button', { name: 'Ampliar explicación con IA', exact: true });
  await page.keyboard.press('Tab');
  await button.focus();
  expect(await button.evaluate(element => getComputedStyle(element).outlineWidth)).toBe('3px');
  aiApp.holdResponse = true;
  await page.keyboard.down('Space');
  expect(await button.evaluate(element => getComputedStyle(element).transform)).toBe('none');
  await page.keyboard.up('Space');
  await expect(page.getByText('Pensando', { exact: true })).toBeVisible();
  expect(await page.getByText('Pensando', { exact: true }).evaluate(element => getComputedStyle(element).animationName)).toBe('none');
  aiApp.release();
  await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toBeVisible();
  await page.context().clearCookies();
  await page.goto(aiApp.baseUrl + '/login');
  const login = page.getByRole('button', { name: 'Iniciar sesión', exact: true });
  await login.hover();
  expect(await login.locator('.button-icon').evaluate(element => getComputedStyle(element).transform)).toBe('none');
});

test('botón deshabilitado no cambia de superficie ni envía solicitudes', async ({ page, aiApp }) => {
  aiApp.scenario = 'disabled';
  await page.goto(aiApp.baseUrl + aiApp.analysisPath);
  const button = page.getByRole('button', { name: 'Ampliar explicación con IA', exact: true });
  await expect(button).toBeDisabled();
  const surface = await button.evaluate(element => getComputedStyle(element).backgroundColor);
  await button.hover();
  expect(await button.evaluate(element => getComputedStyle(element).backgroundColor)).toBe(surface);
  expect(await button.evaluate(element => getComputedStyle(element).transform)).toBe('none');
  expect(aiApp.postBodies).toEqual([]);
});
