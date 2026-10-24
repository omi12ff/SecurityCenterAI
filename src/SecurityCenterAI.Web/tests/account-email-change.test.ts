import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { parseEmailChangeStatus } from '../src/lib/account-email-change.ts';
import { parseAccountExport } from '../src/lib/account-export.ts';
import { parseSecurityActivityEvents } from '../src/lib/security-activity.ts';

const pending = () => ({ id: randomUUID(), newEmail: 'pending@example.invalid', createdAtUtc: '2026-10-24T03:00:00Z',
  expiresAtUtc: '2026-10-24T03:30:00Z', currentAddressConfirmed: false, newAddressConfirmed: true, expired: false });

test('pending email response reconstructs safe metadata and rejects credential-shaped unknown fields', () => {
  const value = pending();
  const parsed = parseEmailChangeStatus({ pending: { ...value, currentTokenHash: 'PRIVATE', newToken: 'PRIVATE', password: 'PRIVATE' } });
  assert.deepEqual(parsed, { pending: value }); assert.equal(JSON.stringify(parsed).includes('PRIVATE'), false);
  for (const bad of [{ ...value, id: '../foreign' }, { ...value, currentAddressConfirmed: 'true' },
    { ...value, newEmail: '<script>' }, { ...value, expiresAtUtc: value.createdAtUtc }]) assert.equal(parseEmailChangeStatus({ pending: bad }), null);
  assert.deepEqual(parseEmailChangeStatus({ pending: null }), { pending: null });
});

test('account download preserves pending address metadata while dropping all delivery material', () => {
  const id = randomUUID(); const value = pending();
  const source = { exportVersion: 1, exportedAtUtc: value.createdAtUtc, profile: { id, name: 'Fixture', email: 'old@example.invalid', createdAtUtc: value.createdAtUtc, emailVerified: true },
    devices: [], analyses: [], deletedReports: [], pendingEmailChange: { ...value, token: 'PRIVATE', newTokenHash: 'PRIVATE' } };
  const parsed = parseAccountExport(source, id); assert.ok(parsed?.pendingEmailChange);
  assert.equal(parsed.pendingEmailChange.newEmail, value.newEmail); assert.equal(JSON.stringify(parsed).includes('PRIVATE'), false);
  assert.equal('id' in parsed.pendingEmailChange, false); assert.equal('expired' in parsed.pendingEmailChange, false);
  assert.equal(parseAccountExport({ ...source, pendingEmailChange: { ...value, currentAddressConfirmed: 'yes' } }, id), null);
  assert.equal(parseAccountExport({ ...source, pendingEmailChange: undefined }, id)?.pendingEmailChange, null);
});

test('email changed activity retains only the fixed action and date', () => {
  const result = parseSecurityActivityEvents([{ id: randomUUID(), action: 'email.changed', occurredAtUtc: pending().createdAtUtc, newEmail: 'PRIVATE' }]);
  assert.ok(result); assert.equal(JSON.stringify(result).includes('PRIVATE'), false);
});
