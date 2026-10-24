import test from 'node:test';
import assert from 'node:assert/strict';
import { validatePasswordChange } from '../src/lib/account-password.ts';
import { parseSecurityActivityList } from '../src/lib/security-activity.ts';
import { randomUUID } from 'node:crypto';

function form(current = ' anterior ', next = ' contraseña nueva ') {
  const value = new FormData();
  value.set('currentPassword', current); value.set('newPassword', next);
  value.set('confirmNewPassword', next); value.set('confirmed', 'yes'); return value;
}

test('password change preserves secrets exactly and accepts legacy short current passwords', () => {
  assert.deepEqual(validatePasswordChange(form(' p ')), { success: true,
    data: { currentPassword: ' p ', newPassword: ' contraseña nueva ', confirmed: true } });
  assert.equal(validatePasswordChange(form('old', 'frase muy larga')).success, true);
  assert.equal(validatePasswordChange(form('old', 'a'.repeat(14))).success, false);
  assert.equal(validatePasswordChange(form('old', '🔒'.repeat(8))).success, false);
  assert.equal(validatePasswordChange(form('old', 'a'.repeat(128))).success, true);
});

test('password change requires a different bounded nonblank new password and matching repetition', () => {
  for (const next of ['', 'short', ' '.repeat(8), 'a'.repeat(129), ' anterior ']) assert.equal(validatePasswordChange(form(' anterior ', next)).success, false);
  const mismatch = form(); mismatch.set('confirmNewPassword', 'different');
  assert.equal(validatePasswordChange(mismatch).success, false);
  assert.equal(validatePasswordChange(form('', 'new secret password')).success, false);
  assert.equal(validatePasswordChange(form('a'.repeat(129))).success, false);
});

test('password errors never return either secret or values to the client', () => {
  const value = form('PRIVATE_OLD_MARKER', 'PRIVATE_NEW_MARKER');
  value.set('confirmNewPassword', 'different');
  const result = validatePasswordChange(value); assert.equal(result.success, false);
  const json = JSON.stringify(result);
  assert.equal(json.includes('PRIVATE_OLD_MARKER'), false);
  assert.equal(json.includes('PRIVATE_NEW_MARKER'), false);
  assert.equal(json.includes('values'), false);
});

test('password change requires explicit session-closure confirmation and string inputs', () => {
  const value = form(); value.delete('confirmed');
  assert.equal(validatePasswordChange(value).success, false);
  value.set('confirmed', 'true'); assert.equal(validatePasswordChange(value).success, false);
  value.set('confirmed', 'yes'); value.set('currentPassword', new Blob(['private']), 'private.txt');
  assert.equal(validatePasswordChange(value).success, false);
});

test('the new activity action is recognized without changing the export event shape', () => {
  const value = parseSecurityActivityList({ limit: 50, hasMore: false,
    events: [{ id: randomUUID(), action: 'password.changed', occurredAtUtc: '2026-10-23T18:00:00Z', password: 'private' }] });
  assert.ok(value); assert.equal(value.events[0].action, 'password.changed');
  assert.equal(JSON.stringify(value).includes('private'), false);
});
