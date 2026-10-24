import assert from 'node:assert/strict';
import test from 'node:test';
import { randomUUID } from 'node:crypto';
import { parseLinkMessageQuota, parseLinkMessageResponse, validateLinkMessage } from '../src/lib/link-message.ts';
import { parseAccountExport } from '../src/lib/account-export.ts';

const quota = { dayUtc: '2026-10-24', used: 1, limit: 25, remaining: 24, resetsAtUtc: '2026-10-25T00:00:00Z' };
const result = () => ({ checkedAtUtc: '2026-10-24T12:00:00Z', quota, report: { rulesVersion: 'local-signals-v1', kind: 'link',
  outcome: 'no-obvious-signals', signals: [], hosts: ['example.invalid'], linksChecked: 1, linksOmitted: 0 } });

test('tool responses reconstruct only permitted observations and counters', () => {
  const input = result();
  const parsed = parseLinkMessageResponse({ ...input, content: 'PRIVATE', token: 'PRIVATE', report: { ...input.report, url: 'PRIVATE' } });
  assert.deepEqual(parsed, input); assert.ok(!JSON.stringify(parsed).includes('PRIVATE'));
  for (const host of ['dev_box.example.invalid', 'fe80::1%25eth0']) {
    assert.ok(parseLinkMessageResponse({ ...input, report: { ...input.report, hosts: [host] } }));
  }
});

test('quota validates UTC days, exact remaining counts and midnight rollover', () => {
  assert.deepEqual(parseLinkMessageQuota(quota), quota);
  for (const bad of [{ ...quota, dayUtc: '2026-02-30' }, { ...quota, used: -1 }, { ...quota, remaining: 23 },
    { ...quota, limit: 0 }, { ...quota, resetsAtUtc: '2026-10-25T01:00:00Z' }]) assert.equal(parseLinkMessageQuota(bad), null);
});

test('tool response rejects invented verdicts, unsafe host links and inconsistent signal outcomes', () => {
  const original = result();
  for (const patch of [{ outcome: 'safe' }, { hosts: ['https://example.invalid/?PRIVATE'] }, { linksChecked: 0 },
    { outcome: 'signals-found' }, { signals: [{ code: '__proto__', title: 'x', explanation: 'x', advice: 'x' }] },
    { kind: 'file' }, { rulesVersion: 'unknown' }]) assert.equal(parseLinkMessageResponse({ ...original, report: { ...original.report, ...patch } }), null);
});

test('submission requires explicit consent and bounded textual content without normalizing the message', () => {
  const data = new FormData(); data.set('kind', 'message'); data.set('content', '  Línea uno\nLínea dos ñ  '); data.set('confirmed', 'yes');
  assert.deepEqual(validateLinkMessage(data), { kind: 'message', content: 'Línea uno\nLínea dos ñ', confirmed: true });
  data.delete('confirmed'); assert.equal(validateLinkMessage(data), null);
  data.set('confirmed', 'yes'); data.set('content', 'x'.repeat(6001)); assert.equal(validateLinkMessage(data), null);
});

test('account export includes only usage metadata and accepts the older additive format', () => {
  const id = randomUUID();
  const root = { exportVersion: 1, exportedAtUtc: '2026-10-24T12:00:00Z', profile: { id, name: 'Ficticio', email: 'fixture@example.invalid', createdAtUtc: '2026-10-24T11:00:00Z', emailVerified: true }, devices: [], analyses: [], deletedReports: [] };
  assert.equal(parseAccountExport(root, id)?.linkMessageUsage, null);
  assert.deepEqual(parseAccountExport({ ...root, linkMessageUsage: { dayUtc: quota.dayUtc, checks: 1, content: 'PRIVATE', version: 'PRIVATE' } }, id)?.linkMessageUsage, { dayUtc: quota.dayUtc, checks: 1 });
  assert.equal(parseAccountExport({ ...root, linkMessageUsage: { dayUtc: '2026-02-30', checks: 1 } }, id), null);
});
