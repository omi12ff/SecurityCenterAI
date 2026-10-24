import { isAnalysisDate } from "./analyses.ts";
import { isDeviceId, parseDevice, parseDeviceAnalysis, type DeviceAnalysis, type DeviceRecord } from "./devices.ts";
import { parseFeedback, type Feedback } from "./guidance.ts";
import { parseSecurityActivityEvents, type SecurityActivity } from "./security-activity.ts";
import { validEmail } from "./account-email-change.ts";
import { isUsageDay } from "./link-message.ts";

export type AccountExportProfile = {
  id: string; name: string; email: string; createdAtUtc: string; emailVerified: boolean;
};
export type AccountExportAnalysis = {
  deviceId: string | null; analysis: Omit<DeviceAnalysis, "deviceId">; feedback: Feedback | null;
};
export type AccountExportDeletedReport = { deviceId: string; reportId: string; deletedAtUtc: string };
export type AccountExportEmailChange = { newEmail: string; createdAtUtc: string; expiresAtUtc: string; currentAddressConfirmed: boolean; newAddressConfirmed: boolean };
export type AccountExport = {
  exportVersion: 1; exportedAtUtc: string; profile: AccountExportProfile;
  devices: DeviceRecord[]; analyses: AccountExportAnalysis[]; deletedReports: AccountExportDeletedReport[];
  securityActivity: SecurityActivity[];
  pendingEmailChange?: AccountExportEmailChange | null;
  linkMessageUsage?: { dayUtc: string; checks: number } | null;
};

const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);
const isId = (value: unknown): value is string => isDeviceId(value) && value !== "00000000-0000-0000-0000-000000000000";
const key = (value: string): string => value.toLowerCase();
const reportKey = (deviceId: string, reportId: string): string => `${key(deviceId)}/${key(reportId)}`;

// Reconstruct every public level. The export includes the full account data,
// independently of the display limits used by dashboard and device pages.
export function parseAccountExport(value: unknown, expectedUserId: string): AccountExport | null {
  if (!isId(expectedUserId) || !isRecord(value) || value.exportVersion !== 1 || !isAnalysisDate(value.exportedAtUtc) ||
    !isRecord(value.profile) || !Array.isArray(value.devices) || !Array.isArray(value.analyses) || !Array.isArray(value.deletedReports)) return null;
  const profile = value.profile;
  if (!isId(profile.id) || key(profile.id) !== key(expectedUserId) ||
    typeof profile.name !== "string" || !profile.name.trim() || profile.name.length > 100 ||
    typeof profile.email !== "string" || !profile.email.trim() || profile.email.length > 320 ||
    !isAnalysisDate(profile.createdAtUtc) || typeof profile.emailVerified !== "boolean") return null;

  const devices: DeviceRecord[] = [];
  const deviceIds = new Set<string>();
  for (const candidate of value.devices) {
    const device = parseDevice(candidate);
    if (!device || !isId(device.id) || deviceIds.has(key(device.id))) return null;
    deviceIds.add(key(device.id));
    devices.push(device);
  }

  const analyses: AccountExportAnalysis[] = [];
  const analysisIds = new Set<string>();
  const findingIds = new Set<string>();
  const liveReports = new Set<string>();
  for (const candidate of value.analyses) {
    if (!isRecord(candidate) || !(candidate.deviceId === null || isId(candidate.deviceId))) return null;
    const deviceId = candidate.deviceId;
    if (deviceId !== null && !deviceIds.has(key(deviceId))) return null;
    const analysis = parseDeviceAnalysis(candidate.analysis);
    const feedback = candidate.feedback === null ? null : parseFeedback(candidate.feedback);
    if (!analysis || !isId(analysis.id) || analysisIds.has(key(analysis.id)) ||
      (candidate.feedback !== null && !feedback) ||
      (analysis.deviceId !== null && (deviceId === null || key(analysis.deviceId) !== key(deviceId))) ||
      (analysis.reportId !== null && !isId(analysis.reportId))) return null;
    analysisIds.add(key(analysis.id));
    for (const finding of analysis.findings) {
      if (!isId(finding.id) || findingIds.has(key(finding.id))) return null;
      findingIds.add(key(finding.id));
    }
    if (deviceId !== null && analysis.reportId !== null) {
      const identity = reportKey(deviceId, analysis.reportId);
      if (liveReports.has(identity)) return null;
      liveReports.add(identity);
    }
    // DeviceAnalysisResponse does not contain a nested deviceId. Preserve the
    // nullable relationship only in its account-export wrapper.
    const { deviceId: _nestedDeviceId, ...exportedAnalysis } = analysis;
    analyses.push({ deviceId, analysis: exportedAnalysis, feedback });
  }

  const deletedReports: AccountExportDeletedReport[] = [];
  const deletedIdentities = new Set<string>();
  for (const candidate of value.deletedReports) {
    if (!isRecord(candidate) || !isId(candidate.deviceId) || !isId(candidate.reportId) ||
      !isAnalysisDate(candidate.deletedAtUtc) || !deviceIds.has(key(candidate.deviceId))) return null;
    const identity = reportKey(candidate.deviceId, candidate.reportId);
    if (deletedIdentities.has(identity) || liveReports.has(identity)) return null;
    deletedIdentities.add(identity);
    deletedReports.push({ deviceId: candidate.deviceId, reportId: candidate.reportId, deletedAtUtc: candidate.deletedAtUtc });
  }

  // Version 1 gains an additive field. Older API responses remain readable.
  const securityActivity = value.securityActivity === undefined ? [] : parseSecurityActivityEvents(value.securityActivity);
  if (!securityActivity) return null;
  let pendingEmailChange: AccountExportEmailChange | null = null;
  if (value.pendingEmailChange !== undefined && value.pendingEmailChange !== null) {
    const pending = value.pendingEmailChange;
    if (!isRecord(pending) || !validEmail(pending.newEmail) || !isAnalysisDate(pending.createdAtUtc) || !isAnalysisDate(pending.expiresAtUtc) ||
      Date.parse(pending.expiresAtUtc) <= Date.parse(pending.createdAtUtc) || typeof pending.currentAddressConfirmed !== "boolean" ||
      typeof pending.newAddressConfirmed !== "boolean") return null;
    pendingEmailChange = { newEmail: pending.newEmail, createdAtUtc: pending.createdAtUtc, expiresAtUtc: pending.expiresAtUtc,
      currentAddressConfirmed: pending.currentAddressConfirmed, newAddressConfirmed: pending.newAddressConfirmed };
  }
  let linkMessageUsage: { dayUtc: string; checks: number } | null = null;
  if (value.linkMessageUsage !== undefined && value.linkMessageUsage !== null) {
    const usage = value.linkMessageUsage;
    if (!isRecord(usage) || !isUsageDay(usage.dayUtc) || !Number.isInteger(usage.checks) || (usage.checks as number) < 0 || (usage.checks as number) > 1000) return null;
    linkMessageUsage = { dayUtc: usage.dayUtc, checks: usage.checks as number };
  }
  return {
    exportVersion: 1, exportedAtUtc: value.exportedAtUtc,
    profile: { id: profile.id, name: profile.name, email: profile.email, createdAtUtc: profile.createdAtUtc, emailVerified: profile.emailVerified },
    devices, analyses, deletedReports, securityActivity, pendingEmailChange, linkMessageUsage,
  };
}
