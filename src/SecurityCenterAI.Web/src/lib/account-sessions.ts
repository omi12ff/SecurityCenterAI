import { isAnalysisDate } from "./analyses.ts";
import { isDeviceId } from "./devices.ts";

export type AccountSession = { id: string; createdAtUtc: string; expiresAtUtc: string; isCurrent: boolean };
export type AccountSessionList = { sessions: AccountSession[]; page: number; limit: 50; hasMore: boolean; maxActiveSessions: number };
export type SessionRevokeState = { message?: string };
export const MAX_SESSION_PAGE = 10_000;
const record = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);
export const isSessionId = (value: unknown): value is string => isDeviceId(value) && value !== "00000000-0000-0000-0000-000000000000";

export function parseAccountSessionList(value: unknown): AccountSessionList | null {
  if (!record(value) || value.limit !== 50 || !Number.isInteger(value.page) || (value.page as number) < 1 ||
    (value.page as number) > MAX_SESSION_PAGE || typeof value.hasMore !== "boolean" || !Array.isArray(value.sessions) ||
    value.sessions.length > 50 || value.hasMore && value.sessions.length !== 50 ||
    !Number.isInteger(value.maxActiveSessions) || (value.maxActiveSessions as number) < 1 || (value.maxActiveSessions as number) > 100) return null;
  const ids = new Set<string>();
  const sessions: AccountSession[] = [];
  for (const item of value.sessions) {
    if (!record(item) || !isSessionId(item.id) || ids.has(item.id.toLowerCase()) ||
      !isAnalysisDate(item.createdAtUtc) || !isAnalysisDate(item.expiresAtUtc) ||
      Date.parse(item.expiresAtUtc) <= Date.parse(item.createdAtUtc) || typeof item.isCurrent !== "boolean" ||
      item.isCurrent && (value.page !== 1 || sessions.length !== 0)) return null;
    ids.add(item.id.toLowerCase());
    sessions.push({ id: item.id, createdAtUtc: item.createdAtUtc, expiresAtUtc: item.expiresAtUtc, isCurrent: item.isCurrent });
  }
  if (sessions.some((item, index) => index > 0 && !sessions[index - 1].isCurrent &&
    Date.parse(item.createdAtUtc) > Date.parse(sessions[index - 1].createdAtUtc))) return null;
  return { sessions, page: value.page as number, limit: 50, hasMore: value.hasMore, maxActiveSessions: value.maxActiveSessions as number };
}

export function validateSessionRevoke(data: FormData): { id: string } | null {
  const id = data.get("sessionId");
  return isSessionId(id) && data.get("confirmed") === "yes" ? { id } : null;
}

export function parseSessionPage(value: unknown): number {
  if (typeof value !== "string" || !/^[1-9]\d{0,4}$/.test(value)) return 1;
  const page = Number(value);
  return page <= MAX_SESSION_PAGE ? page : 1;
}
