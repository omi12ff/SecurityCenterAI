import assert from 'node:assert/strict';
import { randomBytes, randomUUID } from 'node:crypto';
import test from 'node:test';
import { parseSessionToken } from '../src/lib/token.ts';

const nowMs = Date.UTC(2026, 9, 20, 12);
const nowSeconds = nowMs / 1000;
const subject = randomUUID();

function encode(value: unknown) {
  return Buffer.from(JSON.stringify(value)).toString('base64url');
}

function token(overrides: Record<string, unknown> = {}) {
  return [
    encode({ alg: 'HS256', typ: 'JWT' }),
    encode({
      sub: subject,
      name: 'Usuario de prueba',
      email: 'unit-test@example.invalid',
      exp: nowSeconds + 3600,
      ...overrides,
    }),
    randomBytes(32).toString('base64url'),
  ].join('.');
}

test('a structurally valid token yields its user and expiration metadata', () => {
  const value = token();
  const parsed = parseSessionToken(value, nowMs);
  assert.ok(parsed);
  assert.ok(parsed.token === value, 'Keep the original token for API authorization.');
  assert.equal(parsed.user.id, subject);
  assert.equal(parsed.user.name, 'Usuario de prueba');
  assert.equal(parsed.user.email, 'unit-test@example.invalid');
  assert.ok(parsed.expiresAt instanceof Date);
  assert.equal(parsed.expiresAt.getTime(), nowMs + 3_600_000);
});

test('parsing metadata alone does not verify the signature', () => {
  // Each generated signature is random. Only the backend may authorize it.
  assert.ok(parseSessionToken(token(), nowMs));
  assert.ok(parseSessionToken(token(), nowMs));
});

test('missing segments and malformed payloads are rejected without throwing', () => {
  const header = encode({ alg: 'HS256', typ: 'JWT' });
  const signature = randomBytes(32).toString('base64url');
  const invalid = ['', 'one', 'two.segments', 'a.b.c.d', 'a..b',
    `${header}.%.${signature}`,
    `${header}.${Buffer.from('not json').toString('base64url')}.${signature}`];
  for (const value of invalid) {
    assert.ok(parseSessionToken(value, nowMs) === null, 'Malformed tokens must be rejected.');
  }
});

test('non-object JSON payloads are rejected', () => {
  for (const payload of [null, [], 'value', 42]) {
    const value = `${encode({ alg: 'HS256' })}.${encode(payload)}.${randomBytes(32).toString('base64url')}`;
    assert.ok(parseSessionToken(value, nowMs) === null);
  }
});

test('expired tokens and expiration at the current instant are rejected', () => {
  for (const exp of [nowSeconds - 1, nowSeconds, 0, -1]) {
    assert.ok(parseSessionToken(token({ exp }), nowMs) === null);
  }
});

test('expiration must be a finite future number yielding a valid Date', () => {
  for (const exp of [undefined, null, `${nowSeconds + 3600}`, false, Infinity, Number.MAX_VALUE]) {
    assert.ok(parseSessionToken(token({ exp }), nowMs) === null,
      'Missing, nonnumeric, or unrepresentable expiration must be rejected.');
  }
});

test('the subject must be a UUID and required user claims must be strings', () => {
  const invalidClaims = [
    { sub: undefined }, { sub: '' }, { sub: 'not-a-uuid' }, { sub: 123 },
    { name: undefined }, { name: null }, { name: 42 },
    { email: undefined }, { email: null }, { email: [] },
  ];
  for (const claims of invalidClaims) {
    assert.ok(parseSessionToken(token(claims), nowMs) === null,
      'Invalid or missing user claims must be rejected.');
  }
});
