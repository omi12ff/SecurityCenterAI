import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { test, expect, loginAccount, type TestAccount } from './fixtures.ts';
import { accountApi, registerApi } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice, heartbeat } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt } from './analysis-api.ts';

type ExportedAnalysis = {
  id: string;
  reportId: string | null;
  source: string | null;
  score: number | null;
  findings: { id: string; ruleId: string; evidence: string }[];
};
type AccountExport = {
  exportVersion: number;
  exportedAtUtc: string;
  profile: { id: string; name: string; email: string; createdAtUtc: string; emailVerified: boolean };
  devices: { id: string; status: string; name: string }[];
  analyses: { deviceId: string | null; analysis: ExportedAnalysis;
    feedback: { rating: string; comment: string | null; explanationVersion: number; updatedAtUtc: string } | null }[];
  deletedReports: { deviceId: string; reportId: string; deletedAtUtc: string }[];
  securityActivity: { id: string; action: string; occurredAtUtc: string }[];
};

function expectAccountExportShape(value: AccountExport) {
  expect(Object.keys(value).sort()).toEqual(['exportVersion', 'exportedAtUtc', 'profile', 'devices', 'analyses', 'deletedReports', 'securityActivity'].sort());
  expect(value.exportVersion).toBe(1);
  expect(Number.isFinite(Date.parse(value.exportedAtUtc))).toBe(true);
  expect(Object.keys(value.profile).sort()).toEqual(['id', 'name', 'email', 'createdAtUtc', 'emailVerified'].sort());
  expect(Number.isFinite(Date.parse(value.profile.createdAtUtc))).toBe(true);
  expect(typeof value.profile.emailVerified).toBe('boolean');
  for (const device of value.devices) {
    expect(Object.keys(device).sort()).toEqual([
      'id', 'name', 'createdAtUtc', 'pairedAtUtc', 'lastSeenAtUtc', 'revokedAtUtc',
      'platform', 'agentVersion', 'status', 'credentialExpiresAtUtc',
    ].sort());
  }
  for (const record of value.analyses) {
    expect(Object.keys(record).sort()).toEqual(['deviceId', 'analysis', 'feedback'].sort());
    expect(Object.keys(record.analysis).sort()).toEqual([
      'id', 'score', 'analyzedAtUtc', 'findings', 'source', 'scoringVersion', 'coveragePercent',
      'passedCount', 'attentionCount', 'unknownCount', 'notApplicableCount', 'agentVersion',
      'startedAtUtc', 'completedAtUtc', 'reportId',
    ].sort());
    for (const finding of record.analysis.findings) {
      expect(Object.keys(finding).sort()).toEqual([
        'id', 'ruleId', 'ruleVersion', 'title', 'severity', 'evidence', 'recommendation', 'detectedAtUtc', 'status',
      ].sort());
    }
    if (record.feedback) {
      expect(Object.keys(record.feedback).sort()).toEqual(['rating', 'comment', 'explanationVersion', 'updatedAtUtc'].sort());
    }
  }
  for (const marker of value.deletedReports) {
    expect(Object.keys(marker).sort()).toEqual(['deviceId', 'reportId', 'deletedAtUtc'].sort());
    expect(Number.isFinite(Date.parse(marker.deletedAtUtc))).toBe(true);
  }
}

function repositoryRoot() {
  let directory = process.cwd();
  while (!existsSync(join(directory, 'SecurityCenterAI.sln'))) {
    const parent = dirname(directory);
    if (parent === directory) throw new Error('Run account export E2E from the SecurityCenterAI repository.');
    directory = parent;
  }
  return directory;
}

/** Seed only the UUID test account; the fixture's existing cascade cleanup owns every row. */
function seedLegacyExportHistory(account: TestAccount): string {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(account.email)) {
    throw new Error('Legacy export setup refused an account outside the isolated E2E namespace.');
  }
  const analysisId = randomUUID();
  const result = spawnSync('docker', [
    'compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql --no-psqlrc --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --set=seed_email="$1" --set=seed_analysis="$2"',
    'account-export-e2e', account.email, analysisId,
  ], {
    cwd: repositoryRoot(), encoding: 'utf8', windowsHide: true, timeout: 30_000,
    input: `
      BEGIN;
      INSERT INTO security_analyses ("Id", "UserId", "Score", "AnalyzedAtUtc")
      SELECT CASE WHEN sample = 1 THEN :'seed_analysis'::uuid ELSE gen_random_uuid() END,
        u.id_usuario, 42, CURRENT_TIMESTAMP - sample * INTERVAL '1 minute'
      FROM users u CROSS JOIN generate_series(1, 21) AS sample
      WHERE u.correo_electronico = :'seed_email' AND u.correo_electronico LIKE 'e2e-%@example.invalid';
      INSERT INTO security_findings (id_hallazgo, id_analisis, id_regla, version_regla,
        titulo, severidad, evidencia, recomendacion, fecha_deteccion_utc)
      SELECT gen_random_uuid(), a."Id", 'WIN-DEFENDER-ANTIVIRUS', 1,
        'Comprobación histórica E2E ' || sample, 'informational',
        'Evidencia histórica sintética ' || sample, 'Recomendación histórica sintética', CURRENT_TIMESTAMP
      FROM security_analyses a JOIN users u ON a."UserId" = u.id_usuario
      CROSS JOIN generate_series(1, 101) AS sample
      WHERE a."Id" = :'seed_analysis'::uuid AND u.correo_electronico = :'seed_email'
        AND u.correo_electronico LIKE 'e2e-%@example.invalid';
      COMMIT;
    `,
  });
  if (result.error || result.status !== 0) {
    // Never include process output, connection information or credentials in failures.
    throw new Error('Could not seed the isolated legacy export history in local PostgreSQL.');
  }
  return analysisId;
}

test('la exportación de cuenta incluye historial completo y marcas mínimas sin datos de autenticación ni de otros usuarios', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const previousDevice = await createDevice(owner.accessToken, 'Equipo anterior de exportación');
  const previousCredential = await pairDevice(previousDevice.pairingCode);
  const previousResult = await submitReport(previousCredential.deviceToken, syntheticReport());
  expect(previousResult.status).toBe(201);
  const previous = await previousResult.json() as AnalysisReceipt;
  const deletedReport = syntheticReport();
  const deletedResult = await submitReport(previousCredential.deviceToken, deletedReport);
  expect(deletedResult.status).toBe(201);
  const deleted = await deletedResult.json() as AnalysisReceipt;
  const deletedPath = `/api/devices/${previousDevice.device.id}/analyses/${deleted.analysisId}`;
  expect((await accountApi(`${deletedPath}/feedback`, {
    token: owner.accessToken, body: { rating: 'incorrect', comment: 'Comentario de un resultado eliminado' },
  })).status).toBe(200);
  expect((await accountApi(deletedPath, { token: owner.accessToken, method: 'DELETE', body: { confirmed: true } })).status).toBe(204);
  expect((await accountApi(`/api/devices/${previousDevice.device.id}/revoke`, { token: owner.accessToken, method: 'POST' })).status).toBe(204);

  const currentDevice = await createDevice(owner.accessToken, 'Equipo actual de exportación');
  const currentCredential = await pairDevice(currentDevice.pairingCode);
  const currentReport = syntheticReport({ 'WIN-BITLOCKER-SYSTEM': 'unknown' });
  const currentResult = await submitReport(currentCredential.deviceToken, currentReport);
  expect(currentResult.status).toBe(201);
  const current = await currentResult.json() as AnalysisReceipt;
  const comment = 'Valoración privada sintética: <b>Conservar como texto</b>';
  expect((await accountApi(`/api/devices/${currentDevice.device.id}/analyses/${current.analysisId}/feedback`, {
    token: owner.accessToken, body: { rating: 'helpful', comment },
  })).status).toBe(200);
  const legacyId = seedLegacyExportHistory(account);
  const beforeDevice = await (await accountApi(`/api/devices/${currentDevice.device.id}`, { token: owner.accessToken })).json();
  const beforeDashboard = await (await accountApi('/api/dashboard', { token: owner.accessToken })).json();
  const foreignAccount = newAccount();
  const foreign = await registerApi(foreignAccount);

  expect((await accountApi('/api/account/export')).status).toBe(401);
  expect((await accountApi('/api/account/export', { token: currentCredential.deviceToken, scheme: 'Device' })).status).toBe(401);
  const response = await accountApi('/api/account/export', { token: owner.accessToken });
  expect(response.status).toBe(200);
  expect(response.headers.get('content-type')).toMatch(/^application\/json/);
  expect(response.headers.get('content-disposition')).toMatch(/^attachment;/);
  expect(response.headers.get('cache-control')).toContain('no-store');
  expect(response.headers.get('x-content-type-options')).toBe('nosniff');
  const content = await response.text();
  expect([owner.accessToken, owner.refreshToken, previousCredential.deviceToken, currentCredential.deviceToken,
    previousDevice.pairingCode, currentDevice.pairingCode, account.password, foreignAccount.email,
    'Comentario de un resultado eliminado'].every(secret => !content.includes(secret))).toBe(true);
  const exported = JSON.parse(content) as AccountExport;
  expectAccountExportShape(exported);
  expect(exported.profile).toMatchObject({ id: owner.user.id, name: account.name, email: account.email, emailVerified: true });
  expect(exported.devices).toHaveLength(2);
  expect(exported.devices.find(device => device.id === previousDevice.device.id)?.status).toBe('revoked');
  expect(exported.devices.find(device => device.id === currentDevice.device.id)?.status).toBe('linked');
  expect(exported.analyses).toHaveLength(23);
  expect(exported.analyses.filter(record => record.deviceId === null)).toHaveLength(21);
  const legacy = exported.analyses.find(record => record.analysis.id === legacyId)!;
  expect(legacy.deviceId).toBeNull();
  expect(legacy.analysis.source).toBeNull();
  expect(legacy.analysis.reportId).toBeNull();
  expect(legacy.analysis.score).toBe(42);
  expect(legacy.analysis.findings).toHaveLength(101);
  expect(legacy.feedback).toBeNull();
  expect(exported.analyses.find(record => record.analysis.id === previous.analysisId)?.deviceId).toBe(previousDevice.device.id);
  const retained = exported.analyses.find(record => record.analysis.id === current.analysisId)!;
  expect(retained.deviceId).toBe(currentDevice.device.id);
  expect(retained.analysis.reportId).toBe(currentReport.reportId);
  expect(retained.analysis.findings).toHaveLength(10);
  expect(retained.feedback).toMatchObject({ rating: 'helpful', comment, explanationVersion: 1 });
  expect(exported.analyses.some(record => record.analysis.id === deleted.analysisId)).toBe(false);
  expect(exported.deletedReports).toHaveLength(1);
  expect(exported.deletedReports[0]).toMatchObject({ deviceId: previousDevice.device.id, reportId: deletedReport.reportId });

  // An arbitrary userId query cannot change the authenticated owner of this export.
  const otherResponse = await accountApi(`/api/account/export?userId=${owner.user.id}`, { token: foreign.accessToken });
  expect(otherResponse.status).toBe(200);
  const other = await otherResponse.json() as AccountExport;
  expectAccountExportShape(other);
  expect(other.profile.id).toBe(foreign.user.id);
  expect(other.profile.email).toBe(foreignAccount.email);
  expect(other.profile.emailVerified).toBe(false);
  expect(other.devices).toEqual([]);
  expect(other.analyses).toEqual([]);
  expect(other.deletedReports).toEqual([]);
  const afterDevice = await (await accountApi(`/api/devices/${currentDevice.device.id}`, { token: owner.accessToken })).json();
  const afterDashboard = await (await accountApi('/api/dashboard', { token: owner.accessToken })).json();
  expect(afterDevice).toEqual(beforeDevice);
  expect(afterDashboard).toEqual(beforeDashboard);
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  expect((await heartbeat(currentCredential.deviceToken)).status).toBe(204);
});

test('Mi cuenta descarga JSON completo en móvil y mantiene la página, la sesión y los resultados', async ({ page, context, account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de la descarga móvil');
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport({ 'WIN-TPM-READY': 'unknown' });
  const submitted = await submitReport(credential.deviceToken, report);
  expect(submitted.status).toBe(201);
  const receipt = await submitted.json() as AnalysisReceipt;
  const comment = 'Comentario exportado: <script>window.__accountExportExecuted=true</script>';
  expect((await accountApi(`/api/devices/${created.device.id}/analyses/${receipt.analysisId}/feedback`, {
    token: owner.accessToken, body: { rating: 'unclear', comment },
  })).status).toBe(200);
  const before = await (await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken })).json();
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/account');
  await expect(page.getByRole('heading', { name: 'Descargar datos de mi cuenta', exact: true })).toBeVisible();
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('link', { name: 'Exportar mi cuenta (JSON)', exact: true }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.json$/);
  expect(await download.failure()).toBeNull();
  const downloadedPath = await download.path();
  expect(downloadedPath !== null).toBe(true);
  const content = await readFile(downloadedPath!, 'utf8');
  const authCookies = (await context.cookies()).filter(cookie => ['security_center_session', 'security_center_refresh'].includes(cookie.name));
  expect(authCookies).toHaveLength(2);
  expect(authCookies.every(cookie => cookie.httpOnly)).toBe(true);
  expect([owner.accessToken, owner.refreshToken, credential.deviceToken, created.pairingCode,
    account.password, ...authCookies.map(cookie => cookie.value)].every(secret => !content.includes(secret))).toBe(true);
  const exported = JSON.parse(content) as AccountExport;
  expectAccountExportShape(exported);
  expect(exported.profile.id).toBe(owner.user.id);
  expect(exported.profile.email).toBe(account.email);
  expect(exported.devices.map(device => device.id)).toEqual([created.device.id]);
  expect(exported.analyses).toHaveLength(1);
  expect(exported.analyses[0].deviceId).toBe(created.device.id);
  expect(exported.analyses[0].analysis.id).toBe(receipt.analysisId);
  expect(exported.analyses[0].analysis.findings.find(finding => finding.ruleId === report.checks[0].ruleId)?.evidence).toBe(report.checks[0].evidence);
  expect(exported.analyses[0].feedback).toMatchObject({ rating: 'unclear', comment });
  expect(exported.deletedReports).toEqual([]);
  await expect(page).toHaveURL(/\/account\/?$/);
  expect(await page.evaluate(() => Object.hasOwn(window, '__analysisScriptExecuted') || Object.hasOwn(window, '__accountExportExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.goto(`/devices/${created.device.id}`);
  await expect(page).toHaveURL(new RegExp(`/devices/${created.device.id}/?$`));
  await expect(page.locator(`#analysis-${receipt.analysisId}`)).toHaveCount(1);
  const after = await (await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken })).json();
  expect(after).toEqual(before);
  expect((await accountApi('/api/auth/me', { token: owner.accessToken })).status).toBe(200);
  await download.delete();
});
