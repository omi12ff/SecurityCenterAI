import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { aiExplanationFailureMessage, parseAiExplanation, parseAiExplanationAvailability, validateAiExplanationRequest } from '../src/lib/ai-explanation.ts';

const availability = { available: true, code: 'available', message: 'Disponible.', provider: 'Proveedor de prueba',
  model: 'modelo-prueba', dailyLimit: 5, dataDisclosure: 'Solo datos de las comprobaciones.' };
const generated = { method: 'ai-explanation', status: 'generated', explanationVersion: 1,
  summary: 'Revisá el estado desconocido sin suponer que la protección está desactivada.',
  checks: [{ ruleId: 'WIN-DEFENDER-ANTIVIRUS', explanation: 'Comprobá el estado original.' }],
  provider: 'Proveedor de prueba', model: 'modelo-prueba', generatedAtUtc: new Date().toISOString(), notice: 'La IA puede equivocarse.' };

test('availability copies only display metadata and validates limits', () => {
  const parsed = parseAiExplanationAvailability({ ...availability, apiKey: 'private', baseUrl: 'https://private.invalid' });
  assert.deepEqual(parsed, availability);
  for (const extra of [{ available: 'true' }, { dailyLimit: -1 }, { dailyLimit: 2.5 }, { dailyLimit: 10001 },
    { model: undefined }, { provider: { apiKey: 'private' } }, { dataDisclosure: '' }, { code: 'x'.repeat(81) }])
    assert.equal(parseAiExplanationAvailability({ ...availability, ...extra }), null);
  assert.ok(parseAiExplanationAvailability({ ...availability, available: false, code: 'disabled', provider: null, model: null, dailyLimit: 0 }));
});

test('generated explanations preserve plain text and drop extra fields at every level', () => {
  const text = '<script>alert(1)</script> [abrir](https://example.invalid)';
  const parsed = parseAiExplanation({ ...generated, summary: text, token: 'private',
    checks: [{ ...generated.checks[0], explanation: text, evidence: 'private', url: 'https://private.invalid' }] });
  assert.ok(parsed);
  assert.equal(parsed.summary, text);
  assert.deepEqual(parsed.checks, [{ ruleId: generated.checks[0].ruleId, explanation: text }]);
  assert.ok(!JSON.stringify(parsed).includes('private'));
});

test('generated responses reject contradictory methods, invalid versions, duplicate rules and malformed data', () => {
  for (const extra of [{ method: 'local-guide' }, { method: 'chat' }, { status: 'complete' }, { explanationVersion: 2 },
    { summary: '' }, { summary: 'x'.repeat(6001) }, { summary: 'control\u0000' }, { generatedAtUtc: 'invalid' },
    { checks: [] }, { checks: [...generated.checks, ...generated.checks] },
    { checks: [{ ruleId: '', explanation: 'texto' }] }, { checks: [{ ruleId: 'rule', explanation: 'x'.repeat(4001) }] }])
    assert.equal(parseAiExplanation({ ...generated, ...extra }), null);
});

test('every documented fallback keeps the local-guide method without inventing an AI result', () => {
  for (const status of ['disabled', 'unconfigured', 'unavailable', 'invalid-response', 'unsupported']) {
    const fallback = { ...generated, status, method: 'local-guide', checks: [], provider: null, model: null };
    assert.ok(parseAiExplanation(fallback));
    assert.equal(parseAiExplanation({ ...fallback, method: 'ai-explanation' }), null);
  }
});

test('a button request requires valid IDs and one explicit submit action, without a checkbox or free text', () => {
  const deviceId = randomUUID(), analysisId = randomUUID();
  const data = new FormData(); data.set('deviceId', deviceId); data.set('analysisId', analysisId);
  for (const value of [null, 'false', 'on', '1']) {
    if (value === null) data.delete('requestExplanation'); else data.set('requestExplanation', value);
    assert.equal(validateAiExplanationRequest(data), null);
  }
  data.set('requestExplanation', 'true'); data.set('prompt', 'private'); data.set('evidence', 'private');
  assert.deepEqual(validateAiExplanationRequest(data), { deviceId, analysisId, confirmed: true });
  data.append('requestExplanation', 'true'); assert.equal(validateAiExplanationRequest(data), null);
  data.delete('requestExplanation'); data.set('requestExplanation', 'true'); data.set('analysisId', '../other');
  assert.equal(validateAiExplanationRequest(data), null);
});

test('fallbacks expose only known failure categories and give an actionable reason', () => {
  const fallback = { ...generated, method: 'local-guide', status: 'unavailable', failureCode: 'gateway-request-rejected' };
  assert.equal(parseAiExplanation(fallback)?.failureCode, 'gateway-request-rejected');
  assert.match(aiExplanationFailureMessage('unavailable', 'gateway-request-rejected'), /OmniRoute rechazó.*ruta explícita/);
  assert.match(aiExplanationFailureMessage('unavailable', 'provider-model-denied'), /clave no permite/);
  assert.match(aiExplanationFailureMessage('unavailable', 'provider-rate-limited'), /cuota/);
  assert.match(aiExplanationFailureMessage('unavailable', 'timeout'), /tardó más/);
  assert.match(aiExplanationFailureMessage('invalid-response'), /contenido no cumple/);
  const privateCode = 'PRIVATE-UPSTREAM-ERROR';
  assert.equal(parseAiExplanation({ ...fallback, failureCode: privateCode })?.failureCode, undefined);
  assert.ok(!aiExplanationFailureMessage('unavailable', privateCode).includes(privateCode));
});
