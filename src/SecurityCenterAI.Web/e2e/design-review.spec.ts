import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { test, expect, loginAccount, logout } from './fixtures.ts';
import { registerVerifiedOwner, createDevice, pairDevice } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt } from './analysis-api.ts';

test('revisión visual de superficies principales con cuenta e informe ficticios', async ({ page, browser, account }) => {
  test.skip(process.env.SC_AI_VISUAL_REVIEW !== '1', 'Opt-in screenshots of synthetic local data.');
  test.setTimeout(120_000);
  const phase = process.env.DESIGN_REVIEW_PHASE ?? 'after';
  if (!['before', 'after'].includes(phase)) throw new Error('Unknown visual review phase.');
  const root = resolve(process.cwd(), '../..');
  if (!existsSync(join(root, 'SecurityCenterAI.sln'))) throw new Error('Visual review requires the repository root.');
  const output = join(root, 'artifacts', 'design-review', phase);
  const pageErrors: string[] = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  mkdirSync(output, { recursive: true });
  account.name = 'Cuenta de muestra';
  const owner = await registerVerifiedOwner(account);
  if (phase === 'after') {
    await page.setViewportSize({ width: 1440, height: 900 });
    await loginAccount(page, account);
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { name: 'Todavía no hay análisis registrados' })).toBeVisible();
    await page.screenshot({ path: join(output, 'empty-dashboard-1440x900.png'), fullPage: true });
    await logout(page);
  }
  const device = await createDevice(owner.accessToken, 'Equipo de muestra');
  const credential = await pairDevice(device.pairingCode);
  const report = syntheticReport({ 'WIN-DEFENDER-ANTIVIRUS': 'attention', 'WIN-BITLOCKER-SYSTEM': 'unknown', 'WIN-TPM-READY': 'unknown' });
  report.checks.forEach(check => { check.evidence = check.status === 'unknown'
    ? 'Observación ficticia: no se pudo determinar el estado con los permisos disponibles.'
    : 'Observación ficticia para revisar la presentación del informe.'; });
  const submitted = await submitReport(credential.deviceToken, report);
  expect(submitted.status).toBe(201);
  const receipt = await submitted.json() as AnalysisReceipt;
  const records: object[] = [];
  const routes = [
    ['dashboard', '/dashboard', 'Tu seguridad, en un solo lugar'],
    ['analysis', `/devices/${device.device.id}/analyses/${receipt.analysisId}`, 'Entender este análisis'],
    ['devices', '/devices', 'Mis dispositivos'],
    ['getting-started', '/getting-started', 'Primeros pasos'],
    ['account', '/account', 'Seguridad de tu cuenta'],
  ];
  for (const viewport of [{ width: 360, height: 800 }, { width: 768, height: 1024 }, { width: 1440, height: 900 }]) {
    await page.setViewportSize(viewport);
    await loginAccount(page, account);
    for (const [name, path, heading] of routes) {
      await page.goto(path);
      await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();
      await page.evaluate(() => document.fonts.ready);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const artifact = `${name}-${viewport.width}x${viewport.height}.png`;
      await page.screenshot({ path: join(output, artifact) });
      const presentation = await page.evaluate(() => {
        const body = getComputedStyle(document.body);
        const current = document.querySelector<HTMLElement>('.sidebar-nav [aria-current]');
        const nav = current?.closest('nav');
        const item = current?.getBoundingClientRect();
        const container = nav?.getBoundingClientRect();
        return { font: body.fontFamily, bodyFontPx: parseFloat(body.fontSize),
          currentNavigationVisible: item && container ? item.left >= container.left - 1 && item.right <= container.right + 1 : null };
      });
      if (phase === 'after' && presentation.currentNavigationVisible !== null) expect(presentation.currentNavigationVisible).toBe(true);
      records.push({ surface: name, viewport: `${viewport.width}x${viewport.height}`, artifact, presentation });
      if (phase === 'after' && name === 'analysis') {
        const checks = page.getByRole('region', { name: 'Qué significa cada comprobación', exact: true });
        await checks.evaluate(element => element.scrollIntoView({ block: 'start', behavior: 'instant' }));
        await page.screenshot({ path: join(output, `checks-${viewport.width}x${viewport.height}.png`) });
        const summary = checks.locator('summary').first();
        await summary.press('Enter');
        await summary.press('Enter');
        expect(await summary.evaluate(element => getComputedStyle(element).outlineWidth)).toBe('3px');
        await page.screenshot({ path: join(output, `keyboard-focus-${viewport.width}x${viewport.height}.png`) });
      }
    }
    if (phase === 'after' && viewport.width === 1440) {
      const context = await browser.newContext({ baseURL: new URL(page.url()).origin,
        viewport: { width: 720, height: 450 }, deviceScaleFactor: 2 });
      try {
        const zoomed = await context.newPage();
        zoomed.on('pageerror', error => pageErrors.push(error.message));
        await loginAccount(zoomed, account);
        for (const [name, path, heading] of routes.slice(0, 2)) {
          await zoomed.goto(path);
          await expect(zoomed.getByRole('heading', { name: heading, exact: true })).toBeVisible();
          await zoomed.evaluate(() => document.fonts.ready);
          expect(await zoomed.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
          await zoomed.screenshot({ path: join(output, `zoom-200-${name}.png`) });
        }
      } finally { await context.close(); }
      await page.goto(routes[1][1]);
      await page.emulateMedia({ forcedColors: 'active' });
      await expect(page.getByRole('heading', { name: 'Entender este análisis', exact: true })).toBeVisible();
      await page.screenshot({ path: join(output, 'forced-colors-analysis.png') });
      await page.emulateMedia({ forcedColors: 'none' });
    }
    await logout(page);
    const artifact = `login-${viewport.width}x${viewport.height}.png`;
    await page.screenshot({ path: join(output, artifact) });
    records.push({ surface: 'login', viewport: `${viewport.width}x${viewport.height}`, artifact });
  }
  expect(pageErrors).toEqual([]);
  writeFileSync(join(output, 'captures.json'), JSON.stringify({ phase, synthetic: true, records, pageErrors,
    zoomReview: '720x450 CSS pixels at scale factor 2, equivalent layout viewport to 200% at 1440x900' }, null, 2));
});
