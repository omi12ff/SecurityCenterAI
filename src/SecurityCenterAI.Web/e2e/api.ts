import type { TestAccount } from './fixtures.ts';

export type Authentication = {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  refreshExpiresAtUtc: string;
  user: { id: string; name: string; email: string; emailVerified: boolean };
};

export async function accountApi(path: string, options: { body?: unknown; token?: string; method?: string; scheme?: 'Bearer' | 'Device' } = {}) {
  const base = process.env.E2E_API_BASE_URL ?? 'http://localhost:5169';
  try {
    return await fetch(new URL(path, base), {
      method: options.method ?? (options.body === undefined ? 'GET' : 'POST'),
      headers: {
        Accept: 'application/json',
        ...(options.body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(options.token ? { Authorization: `${options.scheme ?? 'Bearer'} ${options.token}` } : {}),
      },
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: AbortSignal.timeout(15_000),
    });
  } catch {
    throw new Error('The isolated account API request failed. Verify the QA backend.');
  }
}

export async function registerApi(account: TestAccount): Promise<Authentication> {
  const response = await accountApi('/api/auth/register', { body: account });
  if (response.status !== 201) throw new Error('The isolated account could not be registered.');
  return response.json() as Promise<Authentication>;
}
