import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseAnalysisSummary, parseDashboard } from '../src/lib/analyses.ts';

const report = () => ({ id: randomUUID(), score: 75, analyzedAtUtc: new Date().toISOString(), source: 'windows-agent',
  scoringVersion: 1, coveragePercent: 70, passedCount: 7, attentionCount: 0, unknownCount: 3, notApplicableCount: 0,
  agentVersion: '0.3.0-poc.1', startedAtUtc: new Date().toISOString(), completedAtUtc: new Date().toISOString(), reportId: randomUUID(), deviceId: randomUUID() });

test('partial analyses retain coverage and unknowns while dropping extra fields', () => {
  const value = report();
  const parsed = parseAnalysisSummary({ ...value, deviceToken: 'private', tokenHash: 'private' });
  assert.ok(parsed);
  assert.equal(parsed.coveragePercent, 70);
  assert.equal(parsed.unknownCount, 3);
  assert.ok(!JSON.stringify(parsed).includes('private'));
});
test('unknown-only analyses have no score and cannot masquerade as fully verified', () => {
  const value = { ...report(), score: null, passedCount: 0, unknownCount: 10, coveragePercent: 0 };
  assert.ok(parseAnalysisSummary(value));
  assert.equal(parseAnalysisSummary({ ...value, score: 100 }), null);
  assert.equal(parseAnalysisSummary({ ...value, coveragePercent: 100 }), null);
});
test('invalid counts, states, report metadata and scoring versions are rejected', () => {
  for (const extra of [{ passedCount: 8 }, { coveragePercent: 75 }, { reportId: '../other' }, { scoringVersion: 2 },
    { score: -1 }, { score: null }, { source: 'unverified-import' }, { completedAtUtc: 'invalid' }]) {
    assert.equal(parseAnalysisSummary({ ...report(), ...extra }), null);
  }
});
test('legacy results retain their score without inventing coverage or check states', () => {
  const value = report();
  const parsed = parseAnalysisSummary({ id: value.id, score: 82, analyzedAtUtc: value.analyzedAtUtc });
  assert.ok(parsed);
  assert.equal(parsed.source, null);
  assert.equal(parsed.coveragePercent, null);
});
test('dashboard keeps latest analysis aligned with the score and rejects inconsistent data', () => {
  const latest = report();
  const data = { securityScore: 75, totalAnalyses: 1, recentAnalyses: [latest], lastAnalysis: latest };
  assert.ok(parseDashboard(data));
  assert.equal(parseDashboard({ ...data, securityScore: 100 }), null);
  assert.equal(parseDashboard({ ...data, lastAnalysis: { ...latest, id: randomUUID() } }), null);
});
