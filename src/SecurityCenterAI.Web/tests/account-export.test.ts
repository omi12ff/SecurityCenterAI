import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseAccountExport } from '../src/lib/account-export.ts';

const date = '2026-10-22T18:00:00Z';
const finding = (index = 0) => ({ id: randomUUID(), ruleId: `TEST-${index}`, ruleVersion: 1,
  title: `Comprobación ${index}`, severity: 'informational', evidence: `<script>Texto conservado ${index}</script>`,
  recommendation: 'Consultar manualmente.', detectedAtUtc: date, status: index < 7 ? 'passed' : 'unknown' });
const device = (revoked = false) => ({ id: randomUUID(), name: revoked ? 'Equipo anterior' : 'Equipo actual',
  status: revoked ? 'revoked' : 'linked', createdAtUtc: date, pairedAtUtc: date, lastSeenAtUtc: date,
  revokedAtUtc: revoked ? date : null, credentialExpiresAtUtc: null, platform: 'Windows', agentVersion: '0.4.1-preview.1' });
const report = (reportId = randomUUID()) => ({ id: randomUUID(), score: 75, analyzedAtUtc: date,
  source: 'windows-agent', scoringVersion: 1, coveragePercent: 70, passedCount: 7, attentionCount: 0,
  unknownCount: 3, notApplicableCount: 0, agentVersion: '0.4.1-preview.1', startedAtUtc: date,
  completedAtUtc: date, reportId, findings: Array.from({ length: 10 }, (_, index) => finding(index)) });
const legacyReport = (count = 1) => ({ id: randomUUID(), score: 60, analyzedAtUtc: date,
  findings: Array.from({ length: count }, (_, index) => ({ ...finding(index), status: null })) });

function savedAccount() {
  const first = device(), revoked = device(true), userId = randomUUID();
  return { userId, value: {
    exportVersion: 1, exportedAtUtc: date,
    profile: { id: userId, name: 'Usuario de prueba', email: 'persona@example.invalid', createdAtUtc: date, emailVerified: true },
    devices: [first, revoked],
    analyses: [
      { deviceId: first.id, analysis: report(), feedback: { rating: 'unclear', comment: '<b>Mi comentario original</b>\nOtra línea', explanationVersion: 1, updatedAtUtc: date } },
      { deviceId: null, analysis: legacyReport(), feedback: null },
    ],
    deletedReports: [{ deviceId: revoked.id, reportId: randomUUID(), deletedAtUtc: date }],
  } };
}

test('account export preserves the profile, revoked devices, private feedback and unassigned legacy analyses', () => {
  const { userId, value } = savedAccount();
  const parsed = parseAccountExport(value, userId);
  assert.ok(parsed);
  assert.deepEqual(parsed.profile, value.profile);
  assert.deepEqual(parsed.devices, value.devices);
  assert.deepEqual(parsed.analyses[0].analysis.findings, value.analyses[0].analysis.findings);
  assert.deepEqual(parsed.analyses[0].feedback, value.analyses[0].feedback);
  assert.deepEqual(parsed.deletedReports, value.deletedReports);
  assert.equal(parsed.analyses[1].deviceId, null);
  assert.equal(parsed.analyses[1].analysis.score, 60);
  assert.equal(parsed.analyses[1].analysis.coveragePercent, null);
  assert.equal(parsed.analyses[1].analysis.findings[0].status, null);
  assert.equal(parsed.analyses[1].feedback, null);
});

test('account export copies only public fields at every nested level without adding a nested deviceId', () => {
  const { userId, value } = savedAccount();
  const candidate = { ...value, accessToken: 'NEVER_EXPORT_ACCESS',
    profile: { ...value.profile, passwordHash: 'NEVER_EXPORT_PASSWORD', concurrencyStamp: 'NEVER_EXPORT_STAMP' },
    devices: value.devices.map(item => ({ ...item, tokenHash: 'NEVER_EXPORT_DEVICE_HASH', deviceToken: 'NEVER_EXPORT_DEVICE_TOKEN' })),
    analyses: value.analyses.map(item => ({ ...item, userId: 'NEVER_EXPORT_OWNER',
      analysis: { ...item.analysis, reportHash: 'NEVER_EXPORT_REPORT_HASH', refreshToken: 'NEVER_EXPORT_REFRESH',
        deviceId: item.deviceId, findings: item.analysis.findings.map(check => ({ ...check, secret: 'NEVER_EXPORT_FINDING' })) },
      feedback: item.feedback && { ...item.feedback, userId: 'NEVER_EXPORT_FEEDBACK_OWNER' } })),
    deletedReports: value.deletedReports.map(item => ({ ...item, evidence: 'NEVER_EXPORT_EVIDENCE', reportHash: 'NEVER_EXPORT_DELETED_HASH' })),
  };
  const parsed = parseAccountExport(candidate, userId);
  assert.ok(parsed);
  assert.ok(!JSON.stringify(parsed).includes('NEVER_EXPORT'));
  assert.equal(parsed.profile.email, value.profile.email);
  assert.deepEqual(Object.keys(parsed).sort(), ['analyses', 'deletedReports', 'devices', 'exportVersion', 'exportedAtUtc', 'linkMessageUsage', 'pendingEmailChange', 'profile', 'securityActivity']);
  assert.deepEqual(Object.keys(parsed.profile).sort(), ['createdAtUtc', 'email', 'emailVerified', 'id', 'name']);
  assert.deepEqual(Object.keys(parsed.devices[0]).sort(), Object.keys(value.devices[0]).sort());
  assert.deepEqual(Object.keys(parsed.analyses[0]).sort(), ['analysis', 'deviceId', 'feedback']);
  assert.deepEqual(Object.keys(parsed.analyses[0].analysis).sort(), Object.keys(report()).sort());
  assert.deepEqual(Object.keys(parsed.deletedReports[0]).sort(), ['deletedAtUtc', 'deviceId', 'reportId']);
  assert.ok(parsed.analyses.every(item => !('deviceId' in item.analysis)));
});

test('account export retains all rows and legacy findings beyond the history display limits', () => {
  const { userId, value } = savedAccount();
  const devices = Array.from({ length: 105 }, () => device(true));
  const analyses = Array.from({ length: 125 }, (_, index) => ({ deviceId: devices[index % devices.length].id,
    analysis: legacyReport(index === 124 ? 137 : 1), feedback: null }));
  const deletedReports = devices.map(item => ({ deviceId: item.id, reportId: randomUUID(), deletedAtUtc: date }));
  const parsed = parseAccountExport({ ...value, devices, analyses, deletedReports }, userId);
  assert.ok(parsed);
  assert.equal(parsed.devices.length, 105);
  assert.equal(parsed.analyses.length, 125);
  assert.equal(parsed.analyses[124].analysis.findings.length, 137);
  assert.equal(parsed.analyses[124].analysis.findings[136].evidence, analyses[124].analysis.findings[136].evidence);
  assert.equal(parsed.deletedReports.length, 105);
});

test('account export accepts an empty account and nullable feedback without inventing rows', () => {
  const { userId, value } = savedAccount();
  const parsed = parseAccountExport({ ...value, profile: { ...value.profile, emailVerified: false },
    devices: [], analyses: [], deletedReports: [] }, userId.toUpperCase());
  assert.ok(parsed);
  assert.equal(parsed.profile.emailVerified, false);
  assert.deepEqual(parsed.devices, []);
  assert.deepEqual(parsed.analyses, []);
  assert.deepEqual(parsed.deletedReports, []);
});

test('account export rejects another owner, malformed profile data and unsupported root shapes', () => {
  const { userId, value } = savedAccount();
  assert.equal(parseAccountExport(value, randomUUID()), null);
  assert.equal(parseAccountExport(value, '../../account'), null);
  const invalid: unknown[] = [null, [], {}, { ...value, exportVersion: 2 }, { ...value, exportedAtUtc: 'invalid' },
    { ...value, profile: { ...value.profile, id: '../owner' } }, { ...value, profile: { ...value.profile, name: '' } },
    { ...value, profile: { ...value.profile, email: null } }, { ...value, profile: { ...value.profile, email: 'x'.repeat(321) } },
    { ...value, profile: { ...value.profile, createdAtUtc: 'invalid' } }, { ...value, profile: { ...value.profile, emailVerified: 'true' } },
    { ...value, devices: null }, { ...value, analyses: {} }, { ...value, deletedReports: undefined }];
  for (const candidate of invalid) assert.equal(parseAccountExport(candidate, userId), null);
});

test('account export rejects orphan relationships, duplicate identities and reports that are both live and deleted', () => {
  const { userId, value } = savedAccount();
  const live = value.analyses[0], liveReport = live.analysis as ReturnType<typeof report>;
  const invalid = [
    { ...value, analyses: [{ ...live, deviceId: randomUUID() }] },
    { ...value, analyses: [{ ...live, deviceId: '../device' }] },
    { ...value, analyses: [{ ...live, analysis: { ...liveReport, deviceId: value.devices[1].id } }] },
    { ...value, analyses: [{ deviceId: null, analysis: { ...legacyReport(), deviceId: value.devices[0].id }, feedback: null }] },
    { ...value, devices: [...value.devices, { ...value.devices[0], id: value.devices[0].id.toUpperCase() }] },
    { ...value, analyses: [live, { ...live, analysis: { ...report(), id: liveReport.id.toUpperCase() } }] },
    { ...value, analyses: [live, { ...live, analysis: report(liveReport.reportId) }] },
    { ...value, analyses: [{ ...live, analysis: { ...liveReport, findings: liveReport.findings.map((check, index) => index === 1 ? { ...check, id: liveReport.findings[0].id } : check) } }] },
    { ...value, deletedReports: [{ ...value.deletedReports[0], deviceId: randomUUID() }] },
    { ...value, deletedReports: [...value.deletedReports, { ...value.deletedReports[0], reportId: value.deletedReports[0].reportId.toUpperCase() }] },
    { ...value, deletedReports: [{ deviceId: live.deviceId, reportId: liveReport.reportId, deletedAtUtc: date }] },
    { ...value, deletedReports: [{ ...value.deletedReports[0], reportId: '../report' }] },
    { ...value, deletedReports: [{ ...value.deletedReports[0], deletedAtUtc: 'invalid' }] },
  ];
  for (const candidate of invalid) assert.equal(parseAccountExport(candidate, userId), null);
});

test('account export scopes report identities to their device and permits case-insensitive device references', () => {
  const { userId, value } = savedAccount();
  const liveReport = value.analyses[0].analysis as ReturnType<typeof report>;
  assert.ok(parseAccountExport({ ...value, analyses: [...value.analyses,
    { deviceId: value.devices[1].id.toUpperCase(), analysis: report(liveReport.reportId), feedback: null }] }, userId));
  assert.ok(parseAccountExport({ ...value, deletedReports: [{ deviceId: value.devices[1].id,
    reportId: liveReport.reportId, deletedAtUtc: date }] }, userId));
});

test('account export rejects malformed saved counts, evidence identities and feedback', () => {
  const { userId, value } = savedAccount();
  const live = value.analyses[0];
  const invalid = [
    { ...value, analyses: [{ ...live, analysis: { ...live.analysis, passedCount: 8 } }] },
    { ...value, analyses: [{ ...live, analysis: { ...live.analysis, coveragePercent: 100 } }] },
    { ...value, analyses: [{ ...live, analysis: { ...live.analysis, findings: live.analysis.findings.slice(1) } }] },
    { ...value, analyses: [{ ...live, analysis: { ...live.analysis, score: 101 } }] },
    { ...value, analyses: [{ ...live, feedback: undefined }] },
    { ...value, analyses: [{ ...live, feedback: { ...live.feedback, rating: 'other' } }] },
    { ...value, analyses: [{ ...live, feedback: { ...live.feedback, comment: 'x'.repeat(1001) } }] },
    { ...value, analyses: [{ ...live, feedback: { ...live.feedback, updatedAtUtc: 'invalid' } }] },
    { ...value, analyses: [{ ...live, analysis: { ...live.analysis, id: '00000000-0000-0000-0000-000000000000' } }] },
  ];
  for (const candidate of invalid) assert.equal(parseAccountExport(candidate, userId), null);
});

test('account export validates rows and findings after display limits rather than silently truncating them', () => {
  const { userId, value } = savedAccount();
  const analyses = Array.from({ length: 25 }, () => ({ deviceId: null, analysis: legacyReport(), feedback: null }));
  const invalidAnalyses = analyses.map((item, index) => index === 24 ? { ...item,
    analysis: { ...item.analysis, id: '../invalid-final-analysis' } } : item);
  assert.equal(parseAccountExport({ ...value, analyses: invalidAnalyses }, userId), null);
  const analysis = legacyReport(105);
  const findings = analysis.findings.map((item, index) => index === 104 ? { ...item, id: '../invalid-final-finding' } : item);
  assert.equal(parseAccountExport({ ...value, analyses: [{ deviceId: null, analysis: { ...analysis, findings }, feedback: null }] }, userId), null);
});
