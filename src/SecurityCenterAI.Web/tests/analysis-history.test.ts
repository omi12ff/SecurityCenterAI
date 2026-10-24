import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseAnalysisExport } from '../src/lib/analysis-history.ts';

function savedExport() {
  const deviceId = randomUUID(), analysisId = randomUUID(), date = new Date().toISOString();
  return {
    deviceId, analysisId,
    value: {
      exportVersion: 1, exportedAtUtc: date, device: { id: deviceId, name: 'PC de prueba' },
      analysis: {
        id: analysisId, score: 75, analyzedAtUtc: date, source: 'windows-agent', scoringVersion: 1,
        coveragePercent: 70, passedCount: 7, attentionCount: 0, unknownCount: 3, notApplicableCount: 0,
        agentVersion: '0.4.0-preview.1', startedAtUtc: date, completedAtUtc: date, reportId: randomUUID(),
        findings: Array.from({ length: 10 }, (_, index) => ({ id: randomUUID(), ruleId: `TEST-${index}`, ruleVersion: 1,
          title: `Comprobación ${index}`, severity: 'medium', evidence: `Evidencia original ${index}\n<script>texto</script>`,
          recommendation: 'Consultar manualmente', detectedAtUtc: date, status: index < 7 ? 'passed' : 'unknown' })),
      },
      feedback: { rating: 'unclear', comment: '<b>Mi comentario original</b>', explanationVersion: 1, updatedAtUtc: date },
    },
  };
}

test('export preserves saved evidence, points, coverage and private feedback without reinterpreting text', () => {
  const { value, deviceId, analysisId } = savedExport();
  const parsed = parseAnalysisExport(value, deviceId, analysisId);
  assert.ok(parsed);
  assert.equal(parsed.exportVersion, 1);
  assert.equal(parsed.analysis.score, 75);
  assert.equal(parsed.analysis.coveragePercent, 70);
  assert.equal(parsed.analysis.unknownCount, 3);
  assert.deepEqual(parsed.analysis.findings, value.analysis.findings);
  assert.deepEqual(parsed.feedback, value.feedback);
  assert.equal(parsed.analysis.reportId, value.analysis.reportId);
});

test('export rebuilds the public contract at every level and drops credentials, hashes and user details', () => {
  const { value, deviceId, analysisId } = savedExport();
  const candidate = { ...value, accessToken: 'NEVER_EXPORT_ACCESS', tokenHash: 'NEVER_EXPORT_HASH', email: 'NEVER_EXPORT_EMAIL',
    device: { ...value.device, credential: 'NEVER_EXPORT_CREDENTIAL' },
    analysis: { ...value.analysis, reportHash: 'NEVER_EXPORT_REPORT_HASH', userId: 'NEVER_EXPORT_USER',
      findings: value.analysis.findings.map(finding => ({ ...finding, userToken: 'NEVER_EXPORT_FINDING_SECRET' })) },
    feedback: { ...value.feedback, userEmail: 'NEVER_EXPORT_FEEDBACK_SECRET' } };
  const parsed = parseAnalysisExport(candidate, deviceId, analysisId);
  assert.ok(parsed);
  assert.ok(!JSON.stringify(parsed).includes('NEVER_EXPORT'));
  assert.deepEqual(Object.keys(parsed).sort(), ['analysis', 'device', 'exportVersion', 'exportedAtUtc', 'feedback']);
  assert.deepEqual(Object.keys(parsed.device).sort(), ['id', 'name']);
  assert.deepEqual(Object.keys(parsed.analysis).sort(), Object.keys(value.analysis).sort());
});

test('export rejects responses for another device or analysis, including a conflicting nested device identity', () => {
  const { value, deviceId, analysisId } = savedExport();
  assert.equal(parseAnalysisExport(value, randomUUID(), analysisId), null);
  assert.equal(parseAnalysisExport(value, deviceId, randomUUID()), null);
  assert.equal(parseAnalysisExport({ ...value, analysis: { ...value.analysis, deviceId: randomUUID() } }, deviceId, analysisId), null);
  assert.equal(parseAnalysisExport(value, '../another-device', analysisId), null);
  assert.equal(parseAnalysisExport(value, deviceId, '../../export'), null);
  assert.ok(parseAnalysisExport(value, deviceId.toUpperCase(), analysisId.toUpperCase()));
});

test('legacy exports retain their saved score and evidence with no invented coverage or feedback', () => {
  const { value, deviceId, analysisId } = savedExport();
  const finding = { ...value.analysis.findings[0], status: null };
  const parsed = parseAnalysisExport({ ...value, analysis: { id: analysisId, score: null,
    analyzedAtUtc: value.analysis.analyzedAtUtc, findings: [finding] }, feedback: null }, deviceId, analysisId);
  assert.ok(parsed);
  assert.equal(parsed.analysis.score, null);
  assert.equal(parsed.analysis.coveragePercent, null);
  assert.equal(parsed.analysis.source, null);
  assert.equal(parsed.analysis.findings[0].status, null);
  assert.equal(parsed.analysis.findings[0].evidence, finding.evidence);
  assert.equal(parsed.feedback, null);
});

test('export rejects unsupported versions, invalid dates, malformed feedback and inconsistent saved counts', () => {
  const { value, deviceId, analysisId } = savedExport();
  const invalid = [null, {}, { ...value, exportVersion: 2 }, { ...value, exportedAtUtc: 'invalid' },
    { ...value, device: { ...value.device, name: '' } }, { ...value, feedback: undefined },
    { ...value, feedback: { ...value.feedback, rating: 'other' } },
    { ...value, feedback: { ...value.feedback, comment: 'x'.repeat(1001) } },
    { ...value, analysis: { ...value.analysis, coveragePercent: 100 } },
    { ...value, analysis: { ...value.analysis, findings: value.analysis.findings.slice(1) } },
    { ...value, analysis: { ...value.analysis, score: 101 } }];
  for (const candidate of invalid) assert.equal(parseAnalysisExport(candidate, deviceId, analysisId), null);
});
