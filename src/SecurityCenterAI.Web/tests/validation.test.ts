import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import test from 'node:test';
import { validateCredentials } from '../src/lib/validation.ts';

function password() {
  return randomBytes(24).toString('hex');
}

function form(values: Record<string, string>) {
  const data = new FormData();
  for (const [name, value] of Object.entries(values)) data.set(name, value);
  return data;
}

function registration(overrides: Record<string, string> = {}) {
  const secret = password();
  return form({
    name: 'Usuario de prueba',
    email: 'unit-test@example.invalid',
    password: secret,
    confirmPassword: secret,
    ...overrides,
  });
}

test('registration trims account fields and preserves the password exactly', () => {
  const secret = ` ${password()} `;
  const result = validateCredentials('register', registration({
    name: '  Usuario de prueba  ',
    email: '  unit-test@example.invalid  ',
    password: secret,
    confirmPassword: secret,
  }));

  assert.ok(result.success);
  assert.equal(result.data.name, 'Usuario de prueba');
  assert.equal(result.data.email, 'unit-test@example.invalid');
  assert.ok(result.data.password === secret, 'The original password must be preserved.');
  assert.ok(!('confirmPassword' in result.data), 'Confirmation is not sent to the API.');
});

test('login accepts an existing short password without requiring registration fields', () => {
  const secret = password().slice(0, 3);
  const result = validateCredentials('login', form({
    email: '  unit-test@example.invalid  ',
    password: secret,
    name: 'A',
    confirmPassword: password(),
  }));

  assert.ok(result.success);
  assert.equal(result.data.email, 'unit-test@example.invalid');
  assert.ok(result.data.password === secret);
});

test('registration accepts names at both documented boundaries', () => {
  for (const name of ['AB', 'A'.repeat(100)]) {
    const result = validateCredentials('register', registration({ name }));
    assert.ok(result.success);
  }
});

test('registration rejects empty, short, and oversized names', () => {
  for (const name of ['', ' ', 'A', 'A'.repeat(101)]) {
    const result = validateCredentials('register', registration({ name }));
    assert.ok(!result.success);
    assert.ok(result.state.fieldErrors.name);
  }
});

test('both forms reject missing, malformed, and oversized email addresses', () => {
  const invalidEmails = ['', ' ', 'missing-at', 'user@', '@example.invalid',
    'user name@example.invalid', `${'a'.repeat(321)}@example.invalid`];

  for (const mode of ['register', 'login'] as const) {
    for (const email of invalidEmails) {
      const result = validateCredentials(mode, registration({ email }));
      assert.ok(!result.success);
      assert.ok(result.state.fieldErrors.email);
    }
  }
});

test('registration rejects a short password and mismatched confirmation', () => {
  const shortSecret = password().slice(0, 7);
  const short = validateCredentials('register', registration({
    password: shortSecret,
    confirmPassword: shortSecret,
  }));
  assert.ok(!short.success);
  assert.ok(short.state.fieldErrors.password);

  const mismatch = validateCredentials('register', registration({ confirmPassword: password() }));
  assert.ok(!mismatch.success);
  assert.ok(mismatch.state.fieldErrors.confirmPassword);
});

test('login rejects a missing password', () => {
  const result = validateCredentials('login', form({ email: 'unit-test@example.invalid' }));
  assert.ok(!result.success);
  assert.ok(result.state.fieldErrors.password);
});

test('both forms allow 128-character passwords and reject longer input', () => {
  for (const mode of ['register', 'login'] as const) {
    const allowed = 'a'.repeat(128);
    const rejected = 'a'.repeat(129);
    assert.ok(validateCredentials(mode, registration({ password: allowed, confirmPassword: allowed })).success);
    const result = validateCredentials(mode, registration({ password: rejected, confirmPassword: rejected }));
    assert.ok(!result.success);
    assert.ok(result.state.fieldErrors.password);
    assert.ok(!JSON.stringify(result.state).includes(rejected));
  }
});

test('invalid results retain account fields without returning either password', () => {
  const secret = password();
  const confirmation = password();
  const result = validateCredentials('register', registration({
    name: '  Usuario de prueba  ',
    email: '  invalid-address  ',
    password: secret,
    confirmPassword: confirmation,
  }));

  assert.ok(!result.success);
  assert.equal(result.state.values.name, 'Usuario de prueba');
  assert.equal(result.state.values.email, 'invalid-address');
  assert.ok(!('password' in result.state.values));
  assert.ok(!('confirmPassword' in result.state.values));
  const serialized = JSON.stringify(result);
  assert.ok(!serialized.includes(secret), 'Do not return the submitted password.');
  assert.ok(!serialized.includes(confirmation), 'Do not return the confirmation.');
});

test('missing or non-text form fields fail validation without throwing', () => {
  const empty = validateCredentials('register', new FormData());
  assert.ok(!empty.success);
  assert.ok(empty.state.fieldErrors.name);
  assert.ok(empty.state.fieldErrors.email);
  assert.ok(empty.state.fieldErrors.password);

  const data = registration();
  data.set('email', new Blob(['invalid field type']), 'field.txt');
  const fileField = validateCredentials('register', data);
  assert.ok(!fileField.success);
  assert.ok(fileField.state.fieldErrors.email);
});
