import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { isOfficialReference, parseExplanation, parseFeedback } from '../src/lib/guidance.ts';

test('references accept only HTTPS Microsoft documentation without credentials', () => {
  assert.ok(isOfficialReference('https://learn.microsoft.com/en-us/windows/'));
  for (const value of ['javascript:alert(1)', 'http://support.microsoft.com/', 'https://support.microsoft.com.evil.test/', 'https://user:pass@learn.microsoft.com/', 'https://evil.test/']) assert.equal(isOfficialReference(value), false);
});
test('feedback keeps plain text and drops unknown fields', () => {
  const result = parseFeedback({ rating: 'unclear', comment: '<script>text</script>', explanationVersion: 1, updatedAtUtc: new Date().toISOString(), token: 'secret' });
  assert.ok(result);
  assert.equal(result.comment, '<script>text</script>');
  assert.ok(!JSON.stringify(result).includes('secret'));
  assert.equal(parseFeedback({ ...result, rating: 'other' }), null);
  assert.equal(parseFeedback({ ...result, comment: 'x'.repeat(1001) }), null);
});
test('explanations match finding identities and reject unsafe references', () => {
  const finding = { id: randomUUID(), ruleId: 'legacy', ruleVersion: 1, title: 'Finding', severity: 'medium', evidence: 'Evidence', recommendation: 'Review', detectedAtUtc: new Date().toISOString(), status: null };
  const check = { findingId: finding.id, meaning: 'Meaning', whatWasChecked: 'Scope', whyItMatters: 'Reason', suggestedAction: 'Review', verificationLimit: 'Limit', references: [{ title: 'Microsoft', url: 'https://learn.microsoft.com/' }] };
  const value = { analysis: { id: randomUUID(), score: 75, analyzedAtUtc: new Date().toISOString(), findings: [finding] }, deviceName: 'PC', method: 'local-guide', explanationVersion: 1, summary: 'Summary', nextSteps: ['Review'], checks: [check], feedback: null, token: 'secret' };
  const parsed = parseExplanation(value);
  assert.ok(parsed);
  assert.ok(!JSON.stringify(parsed).includes('secret'));
  assert.equal(parseExplanation({ ...value, checks: [{ ...check, findingId: randomUUID() }] }), null);
  assert.equal(parseExplanation({ ...value, checks: [{ ...check, references: [{ title: 'fake', url: 'javascript:alert(1)' }] }] }), null);
  assert.equal(parseExplanation({ ...value, checks: [check, check] }), null);
});
