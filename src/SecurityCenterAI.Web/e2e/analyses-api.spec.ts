import { randomBytes } from 'node:crypto';
import { test, expect } from './fixtures.ts';
import { accountApi, registerApi, type Authentication } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice, heartbeat, type DeviceCredential, type PairingCode } from './device-api.ts';
import { syntheticReport, submitReport, type AnalysisReceipt, type StoredAnalysis } from './analysis-api.ts';
import { waitForAccountLink } from './mailpit.ts';

test('ingesta concurrente conserva un análisis y calcula puntaje y cobertura desde el catálogo del servidor', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport({ 'WIN-DEFENDER-ANTIVIRUS': 'attention', 'WIN-DEFENDER-REALTIME': 'unknown' });
  const responses = await Promise.all([
    submitReport(credential.deviceToken, report), submitReport(credential.deviceToken, report),
  ]);
  expect(responses.map(response => response.status).sort()).toEqual([200, 201]);
  const receipts = await Promise.all(responses.map(response => response.json() as Promise<AnalysisReceipt>));
  expect(receipts[0].analysisId).toBe(receipts[1].analysisId);
  expect(receipts.map(receipt => receipt.alreadyReceived).sort()).toEqual([false, true]);
  expect(receipts[0].score).toBe(70);
  expect(receipts[0].coveragePercent).toBe(90);
  expect(receipts[0].passedCount).toBe(8);
  expect(receipts[0].attentionCount).toBe(1);
  expect(receipts[0].unknownCount).toBe(1);
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken });
  expect(detail.status).toBe(200);
  const body = await detail.text();
  expect(!body.includes(credential.deviceToken) && !body.includes(created.pairingCode)).toBe(true);
  expect(!body.includes('reportHash')).toBe(true);
  const stored = (JSON.parse(body) as { analyses: StoredAnalysis[] }).analyses;
  expect(stored.length).toBe(1);
  expect(stored[0].reportId).toBe(report.reportId);
  expect(stored[0].source).toBe('windows-agent');
  expect(stored[0].score).toBe(70);
  expect(stored[0].findings.length).toBe(10);
  expect(stored[0].findings.find(finding => finding.ruleId === 'WIN-DEFENDER-REALTIME')?.status).toBe('unknown');
});

test('ingesta rechaza metadatos inventados, cambios bajo el mismo ID y credenciales revocadas', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport();
  expect((await submitReport(credential.deviceToken, { ...report, score: 100 })).status).toBe(400);
  expect((await submitReport(credential.deviceToken, {
    ...report, checks: report.checks.map(check => ({ ...check, recommendation: 'Dato inventado por el cliente.' })),
  })).status).toBe(400);
  expect((await accountApi('/api/agent/analyses', { token: owner.accessToken, body: report })).status).toBe(401);
  expect((await submitReport(credential.deviceToken, report)).status).toBe(201);
  const changed = {
    ...report, checks: report.checks.map((check, index) => index === 0 ? { ...check, evidence: 'Otra evidencia sintética.' } : check),
  };
  expect((await submitReport(credential.deviceToken, changed)).status).toBe(409);
  const other = await registerApi(newAccount());
  expect((await accountApi(`/api/devices/${created.device.id}`, { token: other.accessToken })).status).toBe(404);
  expect((await accountApi(`/api/devices/${created.device.id}/revoke`, { token: owner.accessToken, method: 'POST' })).status).toBe(204);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(401);
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken });
  const stored = (await detail.json() as { analyses: StoredAnalysis[] }).analyses;
  expect(stored.length).toBe(1);
  expect(stored[0].score).toBe(100);
});

test('diez comprobaciones desconocidas conservan evidencia sin inventar un puntaje', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  const report = syntheticReport();
  report.checks = report.checks.map(check => ({ ...check, status: 'unknown' }));
  const response = await submitReport(credential.deviceToken, report);
  expect(response.status).toBe(201);
  const receipt = await response.json() as AnalysisReceipt;
  expect(receipt.score).toBeNull();
  expect(receipt.coveragePercent).toBe(0);
  expect(receipt.unknownCount).toBe(10);
  expect(receipt.passedCount).toBe(0);
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken });
  const stored = (await detail.json() as { analyses: StoredAnalysis[] }).analyses;
  expect(stored[0].score).toBeNull();
  expect(stored[0].findings.length).toBe(10);
});

test('envío y revocación concurrentes conservan resultados previos sin aceptar nuevos envíos revocados', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  const [submission, revocation] = await Promise.all([
    submitReport(credential.deviceToken, syntheticReport()),
    accountApi(`/api/devices/${created.device.id}/revoke`, { token: owner.accessToken, method: 'POST' }),
  ]);
  expect([201, 401, 409].includes(submission.status)).toBe(true);
  expect(revocation.status).toBe(204);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(401);
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken });
  const result = await detail.json() as { device: { status: string }; analyses: StoredAnalysis[] };
  expect(result.device.status).toBe('revoked');
  expect(result.analyses.length).toBe(submission.status === 201 ? 1 : 0);
});

test('envío y recuperación concurrentes no permiten nuevos análisis con la credencial anterior', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const link = await waitForAccountLink(account.email, 'reset-password');
  const token = new URL(link).searchParams.get('token');
  const password = randomBytes(24).toString('base64url');
  const [submission, initialReset] = await Promise.all([
    submitReport(credential.deviceToken, syntheticReport()),
    accountApi('/api/auth/reset-password', { body: { token, password } }),
  ]);
  const reset = initialReset.status === 400
    ? await accountApi('/api/auth/reset-password', { body: { token, password } })
    : initialReset;
  expect(reset.status).toBe(204);
  expect([201, 401, 409].includes(submission.status)).toBe(true);
  expect((await submitReport(credential.deviceToken, syntheticReport())).status).toBe(401);
  const login = await accountApi('/api/auth/login', { body: { email: account.email, password } });
  expect(login.status).toBe(200);
  const current = await login.json() as Authentication;
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: current.accessToken });
  const result = await detail.json() as { device: { status: string }; analyses: StoredAnalysis[] };
  expect(result.device.status).toBe('pending');
  expect(result.analyses.length).toBe(submission.status === 201 ? 1 : 0);
});

test('envío y revinculación concurrentes solo dejan utilizable la credencial nueva', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const old = await pairDevice(created.pairingCode);
  const reissue = await accountApi(`/api/devices/${created.device.id}/pairing-code`, { token: owner.accessToken, method: 'POST' });
  expect(reissue.status).toBe(200);
  const replacement = await reissue.json() as PairingCode;
  const pairBody = { pairingCode: replacement.pairingCode, agentVersion: '0.2.0-e2e' };
  const [submission, initialPair] = await Promise.all([
    submitReport(old.deviceToken, syntheticReport()),
    accountApi('/api/agent/pair', { body: pairBody }),
  ]);
  const pairing = initialPair.status === 400
    ? await accountApi('/api/agent/pair', { body: pairBody })
    : initialPair;
  expect(pairing.status).toBe(201);
  const current = await pairing.json() as DeviceCredential;
  expect([201, 401, 409].includes(submission.status)).toBe(true);
  expect((await submitReport(old.deviceToken, syntheticReport())).status).toBe(401);
  expect((await heartbeat(current.deviceToken)).status).toBe(204);
  expect((await submitReport(current.deviceToken, syntheticReport())).status).toBe(201);
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: owner.accessToken });
  const result = await detail.json() as { analyses: StoredAnalysis[] };
  expect(result.analyses.length).toBe(submission.status === 201 ? 2 : 1);
});
