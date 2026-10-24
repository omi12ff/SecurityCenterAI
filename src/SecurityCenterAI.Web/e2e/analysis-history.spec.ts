import { readFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { test, expect, loginAccount } from './fixtures.ts';
import { accountApi, registerApi } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice, heartbeat } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt, type StoredAnalysis } from './analysis-api.ts';

type AnalysisExport = {
  exportVersion: number;
  exportedAtUtc: string;
  device: { id: string; name: string };
  analysis: StoredAnalysis;
  feedback: { rating: string; comment: string | null; explanationVersion: number; updatedAtUtc: string } | null;
};

function analysisPath(deviceId: string, analysisId: string) {
  return `/api/devices/${deviceId}/analyses/${analysisId}`;
}

function expectExportShape(value: AnalysisExport) {
  expect(Object.keys(value).sort()).toEqual(['analysis', 'device', 'exportVersion', 'exportedAtUtc', 'feedback'].sort());
  expect(value.exportVersion).toBe(1);
  expect(Number.isFinite(Date.parse(value.exportedAtUtc))).toBe(true);
  expect(Object.keys(value.device).sort()).toEqual(['id', 'name']);
  expect(Object.keys(value.analysis).sort()).toEqual([
    'id', 'score', 'analyzedAtUtc', 'findings', 'source', 'scoringVersion', 'coveragePercent',
    'passedCount', 'attentionCount', 'unknownCount', 'notApplicableCount', 'agentVersion',
    'startedAtUtc', 'completedAtUtc', 'reportId',
  ].sort());
  expect(value.analysis.findings).toHaveLength(10);
  for (const finding of value.analysis.findings) {
    expect(Object.keys(finding).sort()).toEqual([
      'id', 'ruleId', 'ruleVersion', 'title', 'severity', 'evidence', 'recommendation', 'detectedAtUtc', 'status',
    ].sort());
  }
  if (value.feedback) {
    expect(Object.keys(value.feedback).sort()).toEqual(['rating', 'comment', 'explanationVersion', 'updatedAtUtc'].sort());
  }
}

test('exportación privada y eliminación confirmada conservan otros informes y bloquean reenvíos eliminados', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo del historial privado');
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport({ 'WIN-BITLOCKER-SYSTEM': 'unknown' });
  const submission = await submitReport(credential.deviceToken, report);
  expect(submission.status).toBe(201);
  const receipt = await submission.json() as AnalysisReceipt;
  const keptSubmission = await submitReport(credential.deviceToken, syntheticReport());
  expect(keptSubmission.status).toBe(201);
  const kept = await keptSubmission.json() as AnalysisReceipt;
  const path = analysisPath(created.device.id, receipt.analysisId);
  const comment = 'Comentario sintético: <b>Conservar como texto</b>';
  expect((await accountApi(`${path}/feedback`, {
    token: owner.accessToken, body: { rating: 'helpful', comment },
  })).status).toBe(200);
  const before = await (await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken })).json();
  const keptBefore = before.analyses.find((analysis: StoredAnalysis) => analysis.id === kept.analysisId);

  const foreignAccount = newAccount();
  const foreign = await registerVerifiedOwner(foreignAccount);
  const foreignDevice = await createDevice(foreign.accessToken, 'Equipo ajeno al borrado');
  const foreignCredential = await pairDevice(foreignDevice.pairingCode);
  const foreignSubmission = await submitReport(foreignCredential.deviceToken, syntheticReport());
  expect(foreignSubmission.status).toBe(201);
  const foreignBefore = await (await accountApi(`/api/devices/${foreignDevice.device.id}`, { token: foreign.accessToken })).json();
  expect((await accountApi(`${path}/export`)).status).toBe(401);
  expect((await accountApi(path, { method: 'DELETE', body: { confirmed: true } })).status).toBe(401);
  for (const suffix of ['/export', '']) {
    const deletion = suffix === '' ? { method: 'DELETE', body: { confirmed: true } } : {};
    expect((await accountApi(`${path}${suffix}`, { token: foreign.accessToken, ...deletion })).status).toBe(404);
    expect((await accountApi(`${path}${suffix}`, {
      token: credential.deviceToken, scheme: 'Device', ...deletion,
    })).status).toBe(401);
    expect((await accountApi(`${analysisPath(foreignDevice.device.id, receipt.analysisId)}${suffix}`, {
      token: owner.accessToken, ...deletion,
    })).status).toBe(404);
  }

  const exportedResponse = await accountApi(`${path}/export`, { token: owner.accessToken });
  expect(exportedResponse.status).toBe(200);
  expect(exportedResponse.headers.get('content-type')).toMatch(/^application\/json/);
  expect(exportedResponse.headers.get('content-disposition')).toMatch(/^attachment;/);
  expect(exportedResponse.headers.get('cache-control')).toContain('no-store');
  expect(exportedResponse.headers.get('x-content-type-options')).toBe('nosniff');
  const exportedText = await exportedResponse.text();
  expect([owner.accessToken, owner.refreshToken, credential.deviceToken, created.pairingCode,
    account.email, account.password].every(secret => !exportedText.includes(secret))).toBe(true);
  const exported = JSON.parse(exportedText) as AnalysisExport;
  expectExportShape(exported);
  expect(exported.device).toEqual({ id: created.device.id, name: created.device.name });
  expect(exported.analysis.id).toBe(receipt.analysisId);
  expect(exported.analysis.reportId).toBe(report.reportId);
  expect(exported.analysis.score).toBe(receipt.score);
  expect(exported.analysis.findings.find(finding => finding.ruleId === report.checks[0].ruleId)?.evidence).toBe(report.checks[0].evidence);
  expect(exported.feedback).toMatchObject({ rating: 'helpful', comment, explanationVersion: 1 });

  // An absent Content-Type is rejected before JSON confirmation validation.
  expect((await accountApi(path, { token: owner.accessToken, method: 'DELETE' })).status).toBe(415);
  for (const body of [{}, { confirmed: false }, { confirmed: true, extra: 'rechazar' }]) {
    expect((await accountApi(path, { token: owner.accessToken, method: 'DELETE', body })).status).toBe(400);
  }
  expect((await accountApi(path, { token: owner.accessToken, method: 'DELETE',
    body: { confirmed: true, padding: 'x'.repeat(1100) },
  })).status).toBe(413);
  expect((await accountApi(`${path}/export`, { token: owner.accessToken })).status).toBe(200);

  expect((await accountApi(path, { token: owner.accessToken, method: 'DELETE', body: { confirmed: true } })).status).toBe(204);
  expect((await accountApi(path, { token: owner.accessToken, method: 'DELETE', body: { confirmed: true } })).status).toBe(404);
  for (const suffix of ['export', 'explanation']) {
    expect((await accountApi(`${path}/${suffix}`, { token: owner.accessToken })).status).toBe(404);
  }
  expect((await accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating: 'helpful' } })).status).toBe(404);
  const comparison = new URLSearchParams({ baselineId: receipt.analysisId, targetId: kept.analysisId });
  expect((await accountApi(`/api/devices/${created.device.id}/analyses/compare?${comparison}`, { token: owner.accessToken })).status).toBe(404);
  const after = await (await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken })).json();
  expect(after.device.status).toBe('linked');
  expect(after.analyses).toEqual([keptBefore]);
  const dashboard = await (await accountApi('/api/dashboard', { token: owner.accessToken })).json();
  expect(dashboard.totalAnalyses).toBe(1);
  expect(dashboard.lastAnalysis.id).toBe(kept.analysisId);
  expect(dashboard.recentAnalyses.map((analysis: { id: string }) => analysis.id)).toEqual([kept.analysisId]);
  const foreignAfter = await (await accountApi(`/api/devices/${foreignDevice.device.id}`, { token: foreign.accessToken })).json();
  expect(foreignAfter).toEqual(foreignBefore);
  expect((await submitReport(credential.deviceToken, report)).status).toBe(410);
  const changed = { ...report, checks: report.checks.map(check => ({ ...check, evidence: 'Otro contenido sintético' })) };
  expect((await submitReport(credential.deviceToken, changed)).status).toBe(410);
  expect((await heartbeat(credential.deviceToken)).status).toBe(204);
  expect((await submitReport(credential.deviceToken, { ...report, reportId: randomUUID() })).status).toBe(201);
});

test('el dueño descarga JSON y confirma el borrado desde la guía sin eliminar otros análisis', async ({ page, account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo para descargar');
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport({ 'WIN-TPM-READY': 'unknown' });
  const submitted = await submitReport(credential.deviceToken, report);
  expect(submitted.status).toBe(201);
  const receipt = await submitted.json() as AnalysisReceipt;
  const keptSubmission = await submitReport(credential.deviceToken, syntheticReport());
  expect(keptSubmission.status).toBe(201);
  const kept = await keptSubmission.json() as AnalysisReceipt;
  await loginAccount(page, account);
  await page.setViewportSize({ width: 360, height: 800 });
  const guide = `/devices/${created.device.id}/analyses/${receipt.analysisId}`;
  await page.goto(guide);
  await expect(page.getByRole('heading', { name: 'Control de este análisis', exact: true })).toBeVisible();
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('link', { name: 'Exportar análisis (JSON)', exact: true }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.json$/);
  expect(await download.failure()).toBeNull();
  const downloadPath = await download.path();
  expect(downloadPath !== null).toBe(true);
  const content = await readFile(downloadPath!, 'utf8');
  expect([owner.accessToken, owner.refreshToken, credential.deviceToken, account.email, account.password]
    .every(secret => !content.includes(secret))).toBe(true);
  const exported = JSON.parse(content) as AnalysisExport;
  expectExportShape(exported);
  expect(exported.analysis.id).toBe(receipt.analysisId);
  expect(exported.feedback).toBeNull();
  expect(await page.evaluate(() => Object.hasOwn(window, '__analysisScriptExecuted'))).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  const confirm = page.getByLabel('Confirmo que quiero eliminar este análisis', { exact: true });
  const remove = page.getByRole('button', { name: 'Eliminar análisis', exact: true });
  await expect(confirm).not.toBeChecked();
  // A disabled button or native required validation must both prevent an unconfirmed action.
  if (await remove.isEnabled()) {
    await remove.click();
    expect(await confirm.evaluate(element => (element as HTMLInputElement).validity.valueMissing)).toBe(true);
  }
  expect((await accountApi(`${analysisPath(created.device.id, receipt.analysisId)}/export`, { token: owner.accessToken })).status).toBe(200);
  await confirm.check();
  await remove.click();
  await expect(page).toHaveURL(new RegExp(`/devices/${created.device.id}\\?deleted=1$`));
  await expect(page.getByRole('status')).toContainText('Se eliminó el análisis');
  await expect(page.locator(`#analysis-${receipt.analysisId}`)).toHaveCount(0);
  await expect(page.locator(`#analysis-${kept.analysisId}`)).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'Comparar análisis', exact: true })).toHaveCount(0);
  expect((await accountApi(`${analysisPath(created.device.id, receipt.analysisId)}/export`, { token: owner.accessToken })).status).toBe(404);
  expect((await submitReport(credential.deviceToken, report)).status).toBe(410);
  await download.delete();
});

test('eliminación y reenvíos concurrentes en PostgreSQL no resucitan informes', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, 'Equipo de concurrencia del historial');
  const credential = await pairDevice(created.pairingCode);
  const keptResponse = await submitReport(credential.deviceToken, syntheticReport());
  expect(keptResponse.status).toBe(201);
  const kept = await keptResponse.json() as AnalysisReceipt;
  const foreign = await registerApi(newAccount());
  for (let index = 0; index < 5; index++) {
    const report = syntheticReport();
    const submitted = await submitReport(credential.deviceToken, report);
    expect(submitted.status).toBe(201);
    const receipt = await submitted.json() as AnalysisReceipt;
    const path = analysisPath(created.device.id, receipt.analysisId);
    if (index % 2 === 0) {
      expect((await accountApi(`${path}/feedback`, {
        token: owner.accessToken, body: { rating: 'unclear', comment: 'Valoración previa sintética' },
      })).status).toBe(200);
    }
    const [firstDelete, secondDelete, feedback, ...retries] = await Promise.all([
      accountApi(path, { token: owner.accessToken, method: 'DELETE', body: { confirmed: true } }),
      accountApi(path, { token: owner.accessToken, method: 'DELETE', body: { confirmed: true } }),
      accountApi(`${path}/feedback`, { token: owner.accessToken, body: { rating: 'helpful' } }),
      submitReport(credential.deviceToken, report),
      submitReport(credential.deviceToken, report),
      submitReport(credential.deviceToken, report),
    ]);
    expect([firstDelete.status, secondDelete.status]).toContain(204);
    expect([firstDelete.status, secondDelete.status].every(status => [204, 404, 409].includes(status))).toBe(true);
    expect([200, 404, 409].includes(feedback.status)).toBe(true);
    expect(retries.every(response => [200, 409, 410].includes(response.status))).toBe(true);
    expect((await submitReport(credential.deviceToken, report)).status).toBe(410);
    expect((await accountApi(`${path}/export`, { token: owner.accessToken })).status).toBe(404);
    expect((await accountApi(`${path}/explanation`, { token: owner.accessToken })).status).toBe(404);
    expect((await accountApi(path, { token: foreign.accessToken, method: 'DELETE', body: { confirmed: true } })).status).toBe(404);
    const detail = await (await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken })).json();
    expect(detail.analyses.map((analysis: StoredAnalysis) => analysis.id)).toEqual([kept.analysisId]);
  }
  expect((await heartbeat(credential.deviceToken)).status).toBe(204);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(201);
});
