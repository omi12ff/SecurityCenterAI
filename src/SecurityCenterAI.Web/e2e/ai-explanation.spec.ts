import { test, expect, unsafeAiText } from './ai-explanation-fixture.ts';
import { mkdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
import type { Locator } from '@playwright/test';

async function captureDesignState(panel: Locator, name: string) {
  if (process.env.SC_AI_VISUAL_REVIEW !== '1') return;
  const output = resolve(process.cwd(), '../../artifacts/design-review/after');
  mkdirSync(output, { recursive: true });
  await panel.screenshot({ path: join(output, `${name}.png`) });
}

test.beforeEach(async ({ aiApp, context }) => {
  aiApp.reset();
  await context.addCookies([{ name: 'security_center_session', value: aiApp.token, url: aiApp.baseUrl, httpOnly: true, sameSite: 'Lax' }]);
});

test('IA se solicita con un clic, muestra carga y conserva estados; texto escapado y resultado temporal', async ({ aiApp, page }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(aiApp.baseUrl + aiApp.analysisPath);
  const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
  const button = panel.getByRole('button', { name: 'Ampliar explicación con IA', exact: true });
  await expect(button).toBeEnabled();
  expect(aiApp.postBodies).toEqual([]);
  await expect(panel).toContainText('No se envían evidencia libre, nombre, correo ni identificadores');
  await expect(panel.locator('textarea, input[type="text"]')).toHaveCount(0);
  aiApp.holdResponse = true;
  await button.click();
  await expect(button).toBeHidden();
  await expect(panel.getByRole('checkbox')).toHaveCount(0);
  await expect(panel.getByRole('status')).toHaveText('Pensando');
  await captureDesignState(panel, 'ai-thinking-360');
  const thinking = panel.getByText('Pensando', { exact: true });
  expect(await thinking.evaluate(element => getComputedStyle(element).animationName)).toBe('ai-thinking-pulse');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  expect(await thinking.evaluate(element => getComputedStyle(element).animationName)).toBe('none');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await page.emulateMedia({ forcedColors: 'active' });
  expect(await thinking.evaluate(element => getComputedStyle(element).animationName)).toBe('none');
  await page.emulateMedia({ forcedColors: 'none' });
  await expect(page.getByText('Guía local disponible aunque la IA falle.', { exact: true })).toBeVisible();
  await expect.poll(() => aiApp.postBodies.length).toBe(1);
  expect(aiApp.postBodies).toEqual([{ confirmed: true }]);
  aiApp.release();
  const result = page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true });
  await expect(result).toBeVisible();
  await captureDesignState(panel, 'ai-generated-360');
  await expect(result).toContainText(unsafeAiText);
  await expect(result).toContainText('Estado original: Desconocido');
  await expect(result).toContainText('no implica protección desactivada');
  await expect(result.locator('a[href*="model-output.invalid"], script')).toHaveCount(0);
  expect(await page.evaluate(() => Object.hasOwn(window, '__aiScriptExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const link = result.getByRole('link', { name: 'Estado del antivirus', exact: true });
  const originalId = await link.getAttribute('href');
  expect(originalId).toMatch(/^#check-/);
  await link.click();
  await expect(page.locator(originalId!)).toContainText('EVIDENCE_LOCAL_ONLY');
  await expect(page.locator(originalId!).locator('.finding-evidence')).toBeVisible();
  await expect(page.getByRole('progressbar', { name: 'Cobertura de comprobaciones' })).toHaveAttribute('value', '90');
  await page.reload();
  await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toHaveCount(0);
  await expect(panel.getByRole('checkbox')).toHaveCount(0);
  await expect(panel.getByRole('button', { name: 'Ampliar explicación con IA' })).toBeEnabled();
  expect(aiApp.postBodies).toHaveLength(1);
});

for (const scenario of ['unavailable', 'invalid-response', 'quota', 'malformed-response', 'foreign-rule', 'gateway-rejected', 'provider-denied', 'provider-timeout'] as const) {
  test(`IA ${scenario}: mantiene guía, feedback y permite reintentar`, async ({ aiApp, page }) => {
    aiApp.scenario = scenario;
    await page.goto(aiApp.baseUrl + aiApp.analysisPath);
    const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
    await panel.getByRole('button', { name: 'Ampliar explicación con IA' }).click();
    await expect(panel.getByRole('button', { name: 'Volver a intentar con IA' })).toBeEnabled();
    await expect(panel.getByRole('checkbox')).toHaveCount(0);
    await expect(panel.getByRole('alert')).toBeVisible();
    if (scenario === 'invalid-response') await captureDesignState(panel, 'ai-error-1280');
    if (scenario === 'gateway-rejected') await expect(panel.getByRole('alert')).toContainText('OmniRoute rechazó la solicitud');
    if (scenario === 'provider-denied') await expect(panel.getByRole('alert')).toContainText('La clave no permite usar el modelo');
    if (scenario === 'provider-timeout') await expect(panel.getByRole('alert')).toContainText('tardó más de lo permitido');
    await expect(page.getByText('Guía local disponible aunque la IA falle.', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Guardar valoración', exact: true })).toBeEnabled();
    await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toHaveCount(0);
    aiApp.scenario = 'generated';
    await panel.getByRole('button', { name: 'Volver a intentar con IA' }).click();
    await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toBeVisible();
    expect(aiApp.postBodies).toEqual([{ confirmed: true }, { confirmed: true }]);
  });
}

test('reintentar oculta el error anterior y solo muestra Pensando hasta recibir el resultado', async ({ aiApp, page }) => {
  aiApp.scenario = 'invalid-response';
  await page.goto(aiApp.baseUrl + aiApp.analysisPath);
  const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
  await panel.getByRole('button', { name: 'Ampliar explicación con IA' }).click();
  await expect(panel.getByRole('alert')).toBeVisible();
  aiApp.scenario = 'generated'; aiApp.holdResponse = true;
  await panel.getByRole('button', { name: 'Volver a intentar con IA' }).click();
  await expect(panel.getByRole('status')).toHaveText('Pensando');
  await expect(panel.getByRole('alert')).toHaveCount(0);
  await expect(panel.getByRole('button')).toHaveCount(0);
  await expect.poll(() => aiApp.postBodies.length).toBe(2);
  await page.emulateMedia({ forcedColors: 'active' });
  expect(await panel.getByText('Pensando', { exact: true }).evaluate(element => getComputedStyle(element).animationName)).toBe('none');
  aiApp.release();
  await expect(page.getByRole('region', { name: 'Explicación ampliada con IA', exact: true })).toBeVisible();
  await expect(panel.getByText('Pensando', { exact: true })).toHaveCount(0);
});

for (const scenario of ['disabled', 'unconfigured', 'availability-error', 'availability-not-found'] as const) {
  test(`IA ${scenario}: sección clara sin envío automático y guía local utilizable`, async ({ aiApp, page }) => {
    aiApp.scenario = scenario;
    await page.goto(aiApp.baseUrl + aiApp.analysisPath);
    const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
    await expect(panel.getByRole('button', { name: 'Ampliar explicación con IA' })).toBeDisabled();
    await expect(panel.getByRole('checkbox')).toHaveCount(0);
    await expect(panel).toContainText(scenario.startsWith('availability-') ? 'No pudimos consultar la disponibilidad' : 'desactivada o sin configurar');
    await expect(page.getByRole('heading', { name: 'Qué significa cada comprobación' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Guardar valoración' })).toBeEnabled();
    expect(aiApp.postBodies).toEqual([]);
  });
}

test('401 durante generación renueva la sesión y 404 conserva un error recuperable', async ({ aiApp, page }) => {
  aiApp.scenario = 'not-found';
  await page.goto(aiApp.baseUrl + aiApp.analysisPath);
  const panel = page.getByRole('region', { name: 'Una explicación más detallada con IA', exact: true });
  await panel.getByRole('button', { name: 'Ampliar explicación con IA' }).click();
  await expect(panel.getByRole('alert')).toContainText('No encontramos ese análisis en tu cuenta');
  aiApp.scenario = 'session-expired';
  await panel.getByRole('button', { name: 'Volver a intentar con IA' }).click();
  await expect(page).toHaveURL(/\/login\?reason=session-expired$/);
});
