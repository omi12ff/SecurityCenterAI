import type { Page } from '@playwright/test';

type Address = { Address?: string };
type Summary = { ID: string; To?: Address[] };
type Message = Summary & { Text?: string; HTML?: string };

function assertOwnedAddress(email: string) {
  if (!/^e2e-[0-9a-f-]+@example\.invalid$/.test(email)) {
    throw new Error('Mailbox access refused an address outside the isolated E2E namespace.');
  }
}

function mailpitUrl(path: string) {
  const base = process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025';
  return new URL(path, base);
}

async function mailboxRequest(path: string, options?: RequestInit): Promise<Response> {
  try {
    const response = await fetch(mailpitUrl(path), {
      ...options,
      signal: AbortSignal.timeout(10_000),
    });
    if (!response.ok) throw new Error();
    return response;
  } catch {
    // Never include message bodies, account links, or underlying request details.
    throw new Error('The local E2E mailbox request failed. Verify Mailpit availability.');
  }
}

function belongsOnlyToAccount(message: Summary, email: string) {
  return Boolean(message.To?.length) && message.To!.every(address => address.Address === email);
}

export async function accountMessages(email: string): Promise<Summary[]> {
  assertOwnedAddress(email);
  // Search the exact isolated recipient; never list the user's entire inbox.
  const query = new URLSearchParams({ query: `to:"${email}"`, limit: '100' });
  const response = await mailboxRequest(`/api/v1/search?${query}`);
  const result = await response.json() as { messages?: Summary[] };
  return (result.messages ?? []).filter(message => belongsOnlyToAccount(message, email));
}

export async function removeAccountMessages(email: string) {
  const messages = await accountMessages(email);
  for (const message of messages) {
    if (!message.ID) throw new Error('Refusing mailbox deletion without an individual message ID.');
    // An empty IDs array means DELETE ALL in Mailpit, so always delete one verified ID.
    await mailboxRequest('/api/v1/messages', {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ IDs: [message.ID] }),
    });
  }
}

export async function waitForAccountLink(email: string, purpose: 'verify-email' | 'reset-password', excludedLink?: string) {
  assertOwnedAddress(email);
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    for (const summary of await accountMessages(email)) {
      const response = await mailboxRequest(`/api/v1/message/${encodeURIComponent(summary.ID)}`);
      const message = await response.json() as Message;
      if (!belongsOnlyToAccount(message, email)) continue;
      const contents = `${message.Text ?? ''}\n${message.HTML ?? ''}`;
      const links = contents.match(/https?:\/\/[^\s<>"']+/g) ?? [];
      for (const link of links) {
        const candidate = new URL(link.replace(/&amp;/g, '&'));
        if (candidate.pathname === `/${purpose}` && candidate.searchParams.has('token') && candidate.toString() !== excludedLink) {
          return candidate.toString();
        }
      }
    }
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error('The isolated account did not receive its requested email link.');
}

export async function openAccountLink(page: Page, link: string) {
  const target = new URL(link);
  const expectedOrigin = new URL(process.env.E2E_BASE_URL ?? 'http://localhost:3000').origin;
  if (target.origin !== expectedOrigin) {
    throw new Error('The test email points to a different application origin. Verify QA FrontendUrl.');
  }
  try {
    await page.goto(link);
  } catch {
    throw new Error('Could not open the isolated account link.');
  }
}
