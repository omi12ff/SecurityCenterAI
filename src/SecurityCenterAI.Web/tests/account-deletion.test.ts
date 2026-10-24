import assert from 'node:assert/strict';
import test from 'node:test';
import { validateAccountDeletion } from '../src/lib/account-deletion.ts';

function form(password: string, confirmation = 'ELIMINAR') {
  const data = new FormData(); data.set('password', password); data.set('confirmation', confirmation); return data;
}

test('deletion requires the exact explicit confirmation without normalizing it', () => {
  for (const confirmation of ['', 'eliminar', ' ELIMINAR', 'ELIMINAR ', 'yes']) {
    const result = validateAccountDeletion(form('private-secret', confirmation));
    assert.equal(result.success, false);
    assert.ok(!JSON.stringify(result).includes('private-secret'));
  }
});
test('deletion accepts the actual legacy password without trimming or registration rules', () => {
  assert.deepEqual(validateAccountDeletion(form(' x ')), { success: true, data: { password: ' x ', confirmation: 'ELIMINAR' } });
  assert.equal(validateAccountDeletion(form('x'.repeat(128))).success, true);
});
test('deletion rejects absent, non-text and oversized secrets without returning their values', () => {
  for (const data of [new FormData(), form(''), form('secret'.repeat(30))]) {
    const result = validateAccountDeletion(data);
    assert.equal(result.success, false);
    assert.ok(!JSON.stringify(result).includes('secret'));
    assert.ok(!Object.hasOwn(result, 'values'));
  }
  const data = form('valid-secret'); data.set('password', new Blob(['fake-secret']), 'secret.txt');
  assert.equal(validateAccountDeletion(data).success, false);
});
