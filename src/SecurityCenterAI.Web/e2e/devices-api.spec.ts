import { randomBytes } from 'node:crypto';
import { test, expect } from './fixtures.ts';
import { accountApi, registerApi, type Authentication } from './api.ts';
import { registerVerifiedOwner, createDevice, pairDevice, heartbeat, type Device, type PairingCode, type DeviceCredential } from './device-api.ts';
import { waitForAccountLink } from './mailpit.ts';

test('dos altas concurrentes respetan un solo dispositivo activo en PostgreSQL', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const responses = await Promise.all([
    accountApi('/api/devices', { token: owner.accessToken, body: { name: 'Equipo uno' } }),
    accountApi('/api/devices', { token: owner.accessToken, body: { name: 'Equipo dos' } }),
  ]);
  expect(responses.map(response => response.status).sort()).toEqual([201, 409]);
  const created = await responses.find(response => response.status === 201)!.json() as PairingCode;
  const list = await accountApi('/api/devices', { token: owner.accessToken });
  expect(list.status).toBe(200);
  const text = await list.text();
  expect(!text.includes(created.pairingCode)).toBe(true);
  const devices = JSON.parse(text) as Device[];
  expect(devices.filter(device => device.revokedAtUtc === null).length).toBe(1);
});

test('un código tiene un ganador concurrente y Device nunca autoriza las rutas de cuenta', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const responses = await Promise.all([
    accountApi('/api/agent/pair', { body: { pairingCode: created.pairingCode, agentVersion: '0.0.1-e2e' } }),
    accountApi('/api/agent/pair', { body: { pairingCode: created.pairingCode, agentVersion: '0.0.1-e2e' } }),
  ]);
  expect(responses.map(response => response.status).sort()).toEqual([201, 400]);
  const credential = await responses.find(response => response.status === 201)!.json() as DeviceCredential;
  expect((await heartbeat(credential.deviceToken)).status).toBe(204);
  expect((await accountApi('/api/auth/me', { token: credential.deviceToken, scheme: 'Device' })).status).toBe(401);
  expect((await accountApi('/api/auth/me', { token: credential.deviceToken })).status).toBe(401);
  expect((await accountApi('/api/agent/heartbeat', { token: owner.accessToken, body: { agentVersion: '0.0.1-e2e' } })).status).toBe(401);
});

test('otra cuenta no ve ni modifica el dispositivo y una cuenta sin verificar solo puede leer', async ({ account, newAccount }) => {
  const owner = await registerVerifiedOwner(account);
  const ownDevice = await createDevice(owner.accessToken);
  const other = await registerApi(newAccount());
  const list = await accountApi('/api/devices', { token: other.accessToken });
  expect(list.status).toBe(200);
  expect((await list.json() as Device[]).length).toBe(0);
  expect((await accountApi('/api/devices', { token: other.accessToken, body: { name: 'Otro equipo' } })).status).toBe(403);
  expect((await accountApi(`/api/devices/${ownDevice.device.id}`, { token: other.accessToken })).status).toBe(404);
  expect((await accountApi(`/api/devices/${ownDevice.device.id}/revoke`, { token: other.accessToken, method: 'POST' })).status).toBe(404);
  const credential = await pairDevice(ownDevice.pairingCode);
  expect((await heartbeat(credential.deviceToken)).status).toBe(204);
});

test('recuperar la cuenta invalida el agente y conserva su dispositivo pendiente', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  const credential = await pairDevice(created.pairingCode);
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const link = await waitForAccountLink(account.email, 'reset-password');
  const token = new URL(link).searchParams.get('token');
  const password = randomBytes(24).toString('base64url');
  expect((await accountApi('/api/auth/reset-password', { body: { token, password } })).status).toBe(204);
  expect((await heartbeat(credential.deviceToken)).status).toBe(401);
  const login = await accountApi('/api/auth/login', { body: { email: account.email, password } });
  expect(login.status).toBe(200);
  const current = await login.json() as Authentication;
  const detail = await accountApi(`/api/devices/${created.device.id}`, { token: current.accessToken });
  expect(detail.status).toBe(200);
  const result = await detail.json() as { device: Device; analyses: unknown[] };
  expect(result.device.status).toBe('pending');
  expect(result.device.revokedAtUtc).toBeNull();
  expect(result.analyses.length).toBe(0);
  expect((await accountApi('/api/devices', { token: current.accessToken, body: { name: 'Equipo adicional' } })).status).toBe(409);
});

test('reset y vinculación concurrentes no dejan credenciales válidas después de recuperar la cuenta', async ({ account }) => {
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken);
  expect((await accountApi('/api/auth/forgot-password', { body: { email: account.email } })).status).toBe(200);
  const link = await waitForAccountLink(account.email, 'reset-password');
  const token = new URL(link).searchParams.get('token');
  const password = randomBytes(24).toString('base64url');
  const [pairing, initialReset] = await Promise.all([
    accountApi('/api/agent/pair', { body: { pairingCode: created.pairingCode, agentVersion: '0.0.1-e2e' } }),
    accountApi('/api/auth/reset-password', { body: { token, password } }),
  ]);
  // A conflicting user-version change may make reset retryable. Retry once after pair settles.
  const reset = initialReset.status === 400
    ? await accountApi('/api/auth/reset-password', { body: { token, password } })
    : initialReset;
  expect(reset.status).toBe(204);
  expect([201, 400].includes(pairing.status)).toBe(true);
  if (pairing.status === 201) {
    const credential = await pairing.json() as DeviceCredential;
    expect((await heartbeat(credential.deviceToken)).status).toBe(401);
  }
  const replay = await accountApi('/api/agent/pair', {
    body: { pairingCode: created.pairingCode, agentVersion: '0.0.1-e2e' },
  });
  expect(replay.status).toBe(400);
});
