import { isAnalysisDate } from "./analyses.ts";
import { isDeviceId } from "./devices.ts";

export type EmailChangePending = { id: string; newEmail: string; createdAtUtc: string; expiresAtUtc: string;
  currentAddressConfirmed: boolean; newAddressConfirmed: boolean; expired: boolean };
export type EmailChangeStatus = { pending: EmailChangePending | null };
export type EmailChangeState = { message?: string; success?: boolean };
export const isEmailChangeId = (value: unknown): value is string => isDeviceId(value) && value !== "00000000-0000-0000-0000-000000000000";
export const validEmail = (value: unknown): value is string => typeof value === "string" && value.length <= 320 && /^[^\s@]+@[^\s@]+$/.test(value);

export function parseEmailChangeStatus(value: unknown): EmailChangeStatus | null {
  if (!value || typeof value !== "object" || !("pending" in value)) return null;
  if (value.pending === null) return { pending: null };
  const item = value.pending;
  if (!item || typeof item !== "object" || !("id" in item) || !("newEmail" in item) || !("createdAtUtc" in item) ||
    !("expiresAtUtc" in item) || !("currentAddressConfirmed" in item) || !("newAddressConfirmed" in item) || !("expired" in item) ||
    !isEmailChangeId(item.id) || !validEmail(item.newEmail) || !isAnalysisDate(item.createdAtUtc) || !isAnalysisDate(item.expiresAtUtc) ||
    Date.parse(item.expiresAtUtc) <= Date.parse(item.createdAtUtc) || typeof item.currentAddressConfirmed !== "boolean" ||
    typeof item.newAddressConfirmed !== "boolean" || typeof item.expired !== "boolean") return null;
  return { pending: { id: item.id, newEmail: item.newEmail, createdAtUtc: item.createdAtUtc, expiresAtUtc: item.expiresAtUtc,
    currentAddressConfirmed: item.currentAddressConfirmed, newAddressConfirmed: item.newAddressConfirmed, expired: item.expired } };
}
