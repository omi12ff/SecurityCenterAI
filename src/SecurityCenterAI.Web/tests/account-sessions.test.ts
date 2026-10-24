import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseAccountSessionList, parseSessionPage, validateSessionRevoke } from '../src/lib/account-sessions.ts';

const session = (isCurrent = false) => ({ id: randomUUID(), createdAtUtc: '2026-10-23T16:00:00Z', expiresAtUtc: '2026-11-22T16:00:00Z', isCurrent });
const list = (sessions: unknown[] = [session(true)]) => ({ sessions, page: 1, limit: 50, hasMore: false, maxActiveSessions: 10 });

test('session parser keeps only safe fields and accepts a current session older than others', () => {
  const current = { ...session(true), createdAtUtc: '2026-10-20T16:00:00Z', token: 'private', userId: randomUUID() };
  const value = parseAccountSessionList(list([current, session()]));
  assert.ok(value);
  assert.deepEqual(Object.keys(value.sessions[0]).sort(), ['createdAtUtc', 'expiresAtUtc', 'id', 'isCurrent']);
  assert.equal(JSON.stringify(value).includes('private'), false);
  assert.equal(parseAccountSessionList(list([]))?.sessions.length, 0);
});

test('session parser rejects invalid identifiers dates flags duplicates and ordering', () => {
  const first = session();
  for (const bad of [null, { ...first, id: 'bad' }, { ...first, id: '00000000-0000-0000-0000-000000000000' },
    { ...first, createdAtUtc: 'bad' }, { ...first, expiresAtUtc: first.createdAtUtc }, { ...first, isCurrent: 'true' }])
    assert.equal(parseAccountSessionList(list([bad])), null);
  assert.equal(parseAccountSessionList(list([first, first])), null);
  assert.equal(parseAccountSessionList(list([session(true), session(true)])), null);
  assert.equal(parseAccountSessionList(list([session(), session(true)])), null);
  assert.equal(parseAccountSessionList(list([{ ...session(), createdAtUtc: '2026-10-20T16:00:00Z' }, session()])), null);
});

test('session pagination validates metadata and disallows a current session on later pages', () => {
  assert.equal(parseAccountSessionList({ ...list(), page: 0 }), null);
  assert.equal(parseAccountSessionList({ ...list(), page: 10001 }), null);
  assert.equal(parseAccountSessionList({ ...list(), page: 1.5 }), null);
  assert.equal(parseAccountSessionList({ ...list(), limit: 5000 }), null);
  assert.equal(parseAccountSessionList({ ...list(), hasMore: true }), null);
  assert.equal(parseAccountSessionList(list(Array.from({ length: 51 }, () => session()))), null);
  assert.equal(parseAccountSessionList({ ...list(), page: 2 }), null);
  assert.ok(parseAccountSessionList({ ...list(Array.from({ length: 50 }, () => session())), page: 2, hasMore: true }));
});

test('closing a session requires an explicit confirmation and a valid identifier', () => {
  const form = new FormData(); const id = randomUUID();
  form.set('sessionId', id);
  assert.equal(validateSessionRevoke(form), null);
  form.set('confirmed', 'yes');
  assert.deepEqual(validateSessionRevoke(form), { id });
  form.set('sessionId', '../foreign');
  assert.equal(validateSessionRevoke(form), null);
});

test('query parsing selects only bounded numeric pages', () => {
  for (const value of [undefined, [], '0', '-1', '1.2', '1e2', '10001', ' 2', '2?token=secret']) assert.equal(parseSessionPage(value), 1);
  assert.equal(parseSessionPage('2'), 2);
  assert.equal(parseSessionPage('10000'), 10000);
});
