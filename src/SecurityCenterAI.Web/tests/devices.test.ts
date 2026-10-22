import assert from 'node:assert/strict';
import test from 'node:test';
import { randomBytes, randomUUID } from 'node:crypto';
import { parseDevice, parseDeviceDetail, parsePairing } from '../src/lib/devices.ts';

const device = () => ({ id: randomUUID(), name: 'Equipo de prueba', status: 'pending', createdAtUtc: new Date().toISOString(), pairedAtUtc: null, lastSeenAtUtc: null, revokedAtUtc: null, credentialExpiresAtUtc: null, platform: null, agentVersion: null });

test('device data includes only the permitted public fields', () => {
  const secret = randomBytes(32).toString('base64url');
  const parsed = parseDevice({ ...device(), deviceToken: secret, tokenHash: secret });
  assert.ok(parsed);
  assert.ok(!JSON.stringify(parsed).includes(secret));
  assert.ok(!('deviceToken' in parsed));
});
test('malformed device identifiers, dates, and states are rejected', () => {
  for (const override of [{ id: '../account' }, { status: 'online' }, { createdAtUtc: 'invalid' }, { lastSeenAtUtc: 123 }, { name: '' }]) {
    assert.equal(parseDevice({ ...device(), ...override }), null);
  }
});
test('pairing data expires and never carries an agent credential to the browser', () => {
  const now = Date.now();
  const secret = randomBytes(32).toString('base64url');
  const data = { device: device(), pairingCode: randomBytes(32).toString('base64url'), expiresAtUtc: new Date(now + 600_000).toISOString(), deviceToken: secret };
  assert.ok(parsePairing(data, now));
  assert.ok(!JSON.stringify(parsePairing(data, now)).includes(secret));
  assert.equal(parsePairing(data, now + 600_000), null);
  assert.equal(parsePairing({ ...data, pairingCode: 'short' }, now), null);
});
test('analysis detail retains evidence and rule version and rejects malformed findings', () => {
  const finding = { id: randomUUID(), ruleId: 'WIN-TEST-001', ruleVersion: 1, title: 'Ejemplo sintético', severity: 'high', evidence: '<script>Texto de evidencia</script>', recommendation: 'Revisar la evidencia de prueba.', detectedAtUtc: new Date().toISOString() };
  const analysis = { id: randomUUID(), score: 80, analyzedAtUtc: new Date().toISOString(), findings: [finding] };
  const data = { device: device(), analyses: [analysis] };
  assert.equal(parseDeviceDetail(data)?.analyses[0].findings[0].evidence, finding.evidence);
  for (const override of [{ ruleVersion: 0 }, { severity: 'invented' }, { detectedAtUtc: null }]) {
    assert.equal(parseDeviceDetail({ ...data, analyses: [{ ...analysis, findings: [{ ...finding, ...override }] }] }), null);
  }
});
