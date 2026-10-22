import { randomBytes } from 'node:crypto';
import { test, expect } from './fixtures.ts';
import { accountApi, registerApi, type Authentication } from './api.ts';
import { waitForAccountLink } from './mailpit.ts';

test('dos refresh concurrentes nunca dejan dos credenciales utilizables', async ({ account }) => {
  const original = await registerApi(account);
  const results = await Promise.all([
    accountApi('/api/auth/refresh', { body: { refreshToken: original.refreshToken } }),
    accountApi('/api/auth/refresh', { body: { refreshToken: original.refreshToken } }),
  ]);
  expect(results.map(response => response.status).sort()).toEqual([200, 401]);
  const successful = results.find(response => response.status === 200)!;
  const rotated = await successful.json() as Authentication;
  const denied = await accountApi('/api/auth/refresh', { body: { refreshToken: rotated.refreshToken } });
  expect(denied.status).toBe(401);
  const revoked = await accountApi('/api/auth/me', { token: rotated.accessToken });
  expect(revoked.status).toBe(401);
});

test('la confirmación concurrente de un enlace tiene un solo ganador en PostgreSQL', async ({ account }) => {
  const auth = await registerApi(account);
  const link = await waitForAccountLink(account.email, 'verify-email');
  const token = new URL(link).searchParams.get('token');
  const results = await Promise.all([
    accountApi('/api/auth/verify-email', { body: { token } }),
    accountApi('/api/auth/verify-email', { body: { token } }),
  ]);
  expect(results.map(response => response.status).sort()).toEqual([204, 400]);
  const current = await accountApi('/api/auth/me', { token: auth.accessToken });
  expect(current.status).toBe(200);
  const user = await current.json() as { emailVerified: boolean };
  expect(user.emailVerified).toBe(true);
});

test('dos cambios de contraseña con el mismo enlace tienen un solo ganador', async ({ account }) => {
  const auth = await registerApi(account);
  const forgot = await accountApi('/api/auth/forgot-password', { body: { email: account.email } });
  expect(forgot.status).toBe(200);
  const link = await waitForAccountLink(account.email, 'reset-password');
  const token = new URL(link).searchParams.get('token');
  const replacements = [randomBytes(24).toString('base64url'), randomBytes(24).toString('base64url')];
  const results = await Promise.all(replacements.map(password =>
    accountApi('/api/auth/reset-password', { body: { token, password } })));
  expect(results.map(response => response.status).sort()).toEqual([204, 400]);
  const winner = results.findIndex(response => response.status === 204);
  const accepted = await accountApi('/api/auth/login', {
    body: { email: account.email, password: replacements[winner] },
  });
  expect(accepted.status).toBe(200);
  const rejected = await accountApi('/api/auth/login', {
    body: { email: account.email, password: replacements[1 - winner] },
  });
  expect(rejected.status).toBe(401);
  const revoked = await accountApi('/api/auth/me', { token: auth.accessToken });
  expect(revoked.status).toBe(401);
});

test('la revocación de una cuenta conserva el acceso y los datos de otra', async ({ account, newAccount }) => {
  const own = await registerApi(account);
  const otherAccount = newAccount();
  const other = await registerApi(otherAccount);
  const logout = await accountApi('/api/auth/logout-all', { method: 'POST', token: own.accessToken });
  expect(logout.status).toBe(204);
  const denied = await accountApi('/api/auth/me', { token: own.accessToken });
  expect(denied.status).toBe(401);
  const retained = await accountApi('/api/auth/me', { token: other.accessToken });
  expect(retained.status).toBe(200);
  const user = await retained.json() as { id: string; email: string };
  expect(user.id === other.user.id && user.email === otherAccount.email).toBe(true);
  const login = await accountApi('/api/auth/login', { body: { email: account.email, password: randomBytes(24).toString('base64url') } });
  expect(login.status).toBe(401);
});
