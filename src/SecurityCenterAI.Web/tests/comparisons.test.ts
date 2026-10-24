import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseAnalysisComparison } from '../src/lib/comparisons.ts';

function comparison() {
  const deviceId = randomUUID();
  const baselineFinding = { id: randomUUID(), ruleId: 'legacy', ruleVersion: 1, title: 'Prueba', severity: 'medium', evidence: '<script>literal</script>', recommendation: 'Revisar', detectedAtUtc: '2026-10-21T12:00:00Z', status: null };
  const targetFinding = { ...baselineFinding, id: randomUUID(), detectedAtUtc: '2026-10-22T12:00:00Z' };
  return { deviceId, deviceName: 'Mi PC', baseline: { id: randomUUID(), score: 50, analyzedAtUtc: baselineFinding.detectedAtUtc, deviceId, findings: [baselineFinding] },
    target: { id: randomUUID(), score: 75, analyzedAtUtc: targetFinding.detectedAtUtc, deviceId, findings: [targetFinding] },
    scoreComparable: false, scoreDelta: null, coverageDelta: null,
    summary: { improved: 0, needsReview: 0, newInformation: 0, lostInformation: 0, unchanged: 0, notComparable: 1 },
    checks: [{ ruleId: 'legacy', ruleVersion: 1, title: 'Prueba', baselineFindingId: baselineFinding.id, targetFindingId: targetFinding.id,
      baselineStatus: null, targetStatus: null, change: 'notComparable', explanation: 'Este registro no informa estados compatibles.' }] };
}
function nativeComparison() {
  const base = comparison();
  const ruleIds = ['WIN-DEFENDER-ANTIVIRUS', 'WIN-DEFENDER-REALTIME', 'WIN-DEFENDER-SIGNATURES', 'WIN-FIREWALL-PROFILES', 'WIN-SECURE-BOOT', 'WIN-TPM-READY', 'WIN-BITLOCKER-SYSTEM', 'WIN-UPDATE-SERVICE', 'WIN-UAC-ENABLED', 'WIN-SMB1-SERVER'];
  const baselineFindings = ruleIds.map(ruleId => ({ ...base.baseline.findings[0], id: randomUUID(), ruleId, status: 'passed' }));
  const targetFindings = baselineFindings.map(finding => ({ ...finding, id: randomUUID() }));
  const metadata = { source: 'windows-agent', scoringVersion: 1, coveragePercent: 100, passedCount: 10, attentionCount: 0, unknownCount: 0, notApplicableCount: 0,
    agentVersion: '0.4.0-preview.1', startedAtUtc: '2026-10-21T11:59:00Z', completedAtUtc: '2026-10-21T12:00:00Z', reportId: randomUUID() };
  return { ...base, baseline: { ...base.baseline, ...metadata, score: 100, findings: baselineFindings }, target: { ...base.target, ...metadata, score: 100, reportId: randomUUID(), findings: targetFindings },
    scoreComparable: true, scoreDelta: 0, coverageDelta: 0, summary: { ...base.summary, unchanged: 10, notComparable: 0 },
    checks: baselineFindings.map((finding, index) => ({ ...base.checks[0], ruleId: finding.ruleId, baselineFindingId: finding.id, targetFindingId: targetFindings[index].id, baselineStatus: 'passed', targetStatus: 'passed', change: 'unchanged' })) };
}

test('comparison copies only explicit fields and preserves escaped evidence as plain text', () => {
  const value = comparison();
  const parsed = parseAnalysisComparison({ ...value, credential: 'private', summary: { ...value.summary, token: 'private' }, checks: value.checks.map(check => ({ ...check, token: 'private' })) });
  assert.ok(parsed);
  assert.equal(parsed.baseline.findings[0].evidence, '<script>literal</script>');
  assert.equal(parsed.scoreDelta, null);
  assert.ok(!JSON.stringify(parsed).includes('private'));
});
test('comparable reports require consistent original scores, coverage and matching applicable rules', () => {
  const value = nativeComparison();
  assert.ok(parseAnalysisComparison(value));
  assert.equal(parseAnalysisComparison({ ...value, scoreDelta: 100 }), null);
  assert.equal(parseAnalysisComparison({ ...value, coverageDelta: -10 }), null);
  assert.equal(parseAnalysisComparison({ ...value, baseline: { ...value.baseline, findings: value.baseline.findings.map((finding, index) => ({ ...finding, ruleVersion: index ? 1 : 2 })) } }), null);
  assert.equal(parseAnalysisComparison({ ...comparison(), scoreComparable: true, scoreDelta: 25 }), null);
});
test('unknown-only results retain original coverage and never manufacture a score delta', () => {
  const value = nativeComparison();
  const uncertain = { ...value, baseline: { ...value.baseline, score: null, passedCount: 0, unknownCount: 10, coveragePercent: 0,
    findings: value.baseline.findings.map(finding => ({ ...finding, status: 'unknown' })) }, scoreComparable: false, scoreDelta: null, coverageDelta: null,
    summary: { ...value.summary, unchanged: 0, newInformation: 10 }, checks: value.checks.map(check => ({ ...check, baselineStatus: 'unknown', change: 'newInformation' })) };
  assert.ok(parseAnalysisComparison(uncertain));
  assert.equal(parseAnalysisComparison({ ...uncertain, scoreDelta: 100 }), null);
  assert.equal(parseAnalysisComparison({ ...uncertain, coverageDelta: 100 }), null);
});
test('comparison rejects unrelated finding identities, states, duplicate rules and misleading totals', () => {
  const value = comparison();
  for (const check of [{ ...value.checks[0], baselineFindingId: randomUUID() }, { ...value.checks[0], ruleId: 'other' },
    { ...value.checks[0], targetStatus: 'passed' }, { ...value.checks[0], change: 'fixed' }]) {
    assert.equal(parseAnalysisComparison({ ...value, checks: [check] }), null);
  }
  assert.equal(parseAnalysisComparison({ ...value, summary: { ...value.summary, unchanged: 1 } }), null);
  assert.equal(parseAnalysisComparison({ ...value, checks: [value.checks[0], value.checks[0]] }), null);
  assert.equal(parseAnalysisComparison({ ...value, target: value.baseline }), null);
  assert.equal(parseAnalysisComparison({ ...value, target: { ...value.target, deviceId: randomUUID() } }), null);
});
test('a rule absent from one original report requires a null finding and status on that side', () => {
  const value = comparison();
  const absent = { ...value, baseline: { ...value.baseline, findings: [] }, checks: [{ ...value.checks[0], baselineFindingId: null }] };
  assert.ok(parseAnalysisComparison(absent));
  assert.equal(parseAnalysisComparison({ ...absent, checks: [{ ...absent.checks[0], baselineStatus: 'unknown' }] }), null);
});
test('ambiguous legacy rules retain all original evidence without choosing a duplicate as comparable', () => {
  const value = comparison();
  const duplicate = { ...value.baseline.findings[0], id: randomUUID(), evidence: 'Otra observación original.' };
  const ambiguous = { ...value, baseline: { ...value.baseline, findings: [...value.baseline.findings, duplicate] },
    checks: [{ ...value.checks[0], baselineFindingId: null, ruleVersion: 1 }] };
  const parsed = parseAnalysisComparison(ambiguous);
  assert.ok(parsed);
  assert.equal(parsed.baseline.findings.length, 2);
  assert.equal(parsed.baseline.findings[1].evidence, duplicate.evidence);
  assert.equal(parseAnalysisComparison({ ...ambiguous, checks: [{ ...ambiguous.checks[0], change: 'improved' }] }), null);
});
