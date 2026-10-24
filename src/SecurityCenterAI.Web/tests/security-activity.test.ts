import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { activityHref, activityLabels, activityQueryString, parseActivityQuery, parseSecurityActivityEvents, parseSecurityActivityList } from '../src/lib/security-activity.ts';
import { parseAccountExport } from '../src/lib/account-export.ts';

const event = () => ({ id: randomUUID(), action: 'session.started', occurredAtUtc: '2026-10-23T16:00:00Z' });

test('activity only reconstructs the three public fields and uses fixed action labels', () => {
  const original = event();
  const parsed = parseSecurityActivityList({ events: [{ ...original, email: 'PRIVATE', token: 'PRIVATE', deviceId: 'PRIVATE' }], hasMore: false, limit: 50 });
  assert.ok(parsed);
  assert.deepEqual(parsed.events, [original]);
  assert.ok(!JSON.stringify(parsed).includes('PRIVATE'));
  assert.equal(activityLabels[parsed.events[0].action], 'Sesión iniciada');
});

test('activity rejects injected action labels, invalid dates, IDs and duplicates', () => {
  for (const bad of [{ ...event(), action: '<script>alert(1)</script>' }, { ...event(), action: '__proto__' },
    { ...event(), occurredAtUtc: 'yesterday' }, { ...event(), id: 'not-an-id' }]) assert.equal(parseSecurityActivityEvents([bad]), null);
  const first = event();
  assert.equal(parseSecurityActivityEvents([first, { ...first, id: first.id.toUpperCase() }]), null);
});

test('empty activity remains empty and bounded responses have honest hasMore semantics', () => {
  assert.deepEqual(parseSecurityActivityList({ events: [], limit: 50, hasMore: false }), { events: [], limit: 50, hasMore: false, nextBefore: null });
  assert.equal(parseSecurityActivityList({ events: [], limit: 50, hasMore: true }), null);
  assert.equal(parseSecurityActivityList({ events: Array.from({ length: 51 }, event), limit: 50, hasMore: true }), null);
  assert.equal(parseSecurityActivityList({ events: [event()], limit: 5000, hasMore: false }), null);
});

test('continuation must identify the last returned event and is absent on the final page', () => {
  const events = Array.from({ length: 50 }, event);
  const response = { events, limit: 50, hasMore: true, nextBefore: events.at(-1)!.id };
  assert.equal(parseSecurityActivityList(response)?.nextBefore, response.nextBefore);
  for (const nextBefore of [null, undefined, randomUUID(), '<script>', 123]) {
    assert.equal(parseSecurityActivityList({ ...response, nextBefore }), null);
  }
  assert.equal(parseSecurityActivityList({ ...response, hasMore: false }), null);
});

test('activity filters reject ambiguous, impossible and reversed dates without guessing', () => {
  assert.deepEqual(parseActivityQuery({}), { action: '', from: '', to: '', before: '' });
  assert.ok(parseActivityQuery({ from: '2024-02-29', to: '2024-03-01', action: 'email.changed' }));
  for (const query of [{ from: '2026-02-30' }, { from: '2026-02-29' }, { from: '0000-01-01' }, { to: '9999-12-31' },
    { from: '2026-09-11', to: '2026-09-10' }, { from: '2026-09-10T00:00:00Z' }, { action: '0' }, { action: '__proto__' },
    { before: randomUUID().replaceAll('-', '') }, { before: '00000000-0000-0000-0000-000000000000' }, { action: ['session.started', 'password.reset'] }]) {
    assert.equal(parseActivityQuery(query), null);
  }
});

test('continuation links preserve only validated filters and discard injected parameters', () => {
  const before = randomUUID();
  const query = parseActivityQuery({ action: 'session.started', from: '2026-09-10', to: '2026-09-11', before: before.toUpperCase(), userId: 'PRIVATE' });
  assert.ok(query);
  assert.equal(activityQueryString(query), `action=session.started&from=2026-09-10&to=2026-09-11&before=${before}`);
  assert.equal(activityHref({ ...query, before: '' }), '/account/activity?action=session.started&from=2026-09-10&to=2026-09-11');
  assert.ok(!JSON.stringify(query).includes('PRIVATE'));
});

test('the account export includes all activity and accepts the older additive version 1 response', () => {
  const userId = randomUUID();
  const value = { exportVersion: 1, exportedAtUtc: '2026-10-23T16:00:00Z', profile: { id: userId, name: 'Ficticio', email: 'fixture@example.invalid', createdAtUtc: '2026-10-23T15:00:00Z', emailVerified: true }, devices: [], analyses: [], deletedReports: [] };
  assert.deepEqual(parseAccountExport(value, userId)?.securityActivity, []);
  const events = Array.from({ length: 65 }, event);
  assert.equal(parseAccountExport({ ...value, securityActivity: events }, userId)?.securityActivity.length, 65);
  assert.equal(parseAccountExport({ ...value, securityActivity: null }, userId), null);
  assert.equal(parseAccountExport({ ...value, securityActivity: [{ ...event(), action: 'unknown' }] }, userId), null);
});
