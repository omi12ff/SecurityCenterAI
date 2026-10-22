import { accountApi, registerApi, type Authentication } from './api.ts';
import type { TestAccount } from './fixtures.ts';
import { waitForAccountLink } from './mailpit.ts';

export type Device = {
  id: string;
  name: string;
  status: 'pending' | 'linked' | 'revoked';
  pairedAtUtc: string | null;
  lastSeenAtUtc: string | null;
  revokedAtUtc: string | null;
  credentialExpiresAtUtc: string | null;
};
export type PairingCode = { device: Device; pairingCode: string; expiresAtUtc: string };
export type DeviceCredential = { deviceId: string; deviceToken: string; expiresAtUtc: string };

export async function registerVerifiedOwner(account: TestAccount): Promise<Authentication> {
  const auth = await registerApi(account);
  const link = await waitForAccountLink(account.email, 'verify-email');
  const token = new URL(link).searchParams.get('token');
  const verification = await accountApi('/api/auth/verify-email', { body: { token } });
  if (verification.status !== 204) throw new Error('Could not verify the isolated device owner.');
  return auth;
}

export async function createDevice(accessToken: string, name = 'Equipo E2E'): Promise<PairingCode> {
  const response = await accountApi('/api/devices', { token: accessToken, body: { name } });
  if (response.status !== 201) throw new Error('Could not create the isolated test device.');
  return response.json() as Promise<PairingCode>;
}

export async function pairDevice(pairingCode: string): Promise<DeviceCredential> {
  const response = await accountApi('/api/agent/pair', { body: { pairingCode, agentVersion: '0.0.1-e2e' } });
  if (response.status !== 201) throw new Error('Could not pair the isolated test device.');
  return response.json() as Promise<DeviceCredential>;
}

export function heartbeat(deviceToken: string) {
  return accountApi('/api/agent/heartbeat', { token: deviceToken, scheme: 'Device', body: { agentVersion: '0.0.2-e2e' } });
}
