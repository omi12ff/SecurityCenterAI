import { parseAnalysisSummary, type AnalysisSummary, type CheckStatus } from "./analyses.ts";

export type DeviceRecord = {
  id: string; name: string; status: "pending" | "linked" | "revoked";
  createdAtUtc: string; pairedAtUtc: string | null; lastSeenAtUtc: string | null;
  revokedAtUtc: string | null; credentialExpiresAtUtc: string | null;
  platform: string | null; agentVersion: string | null;
};
export type DevicePairing = { device: DeviceRecord; pairingCode: string; expiresAtUtc: string };
export type DeviceFinding = { id: string; ruleId: string; ruleVersion: number; title: string; severity: string; evidence: string; recommendation: string; detectedAtUtc: string; status: CheckStatus | null };
export type DeviceAnalysis = AnalysisSummary & { findings: DeviceFinding[] };
export type DeviceDetail = { device: DeviceRecord; analyses: DeviceAnalysis[] };
export type DeviceFormState = { message?: string; success?: boolean; pairing?: DevicePairing; name?: string };

export const isDeviceId = (value: unknown): value is string => typeof value === "string" && /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(value);
const isDate = (value: unknown): value is string => typeof value === "string" && Number.isFinite(Date.parse(value));
const nullableDate = (value: unknown) => value === null || isDate(value);
const nullableText = (value: unknown) => value === null || typeof value === "string";

export function parseDevice(value: unknown): DeviceRecord | null {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  if (!isDeviceId(item.id) || typeof item.name !== "string" || !item.name.trim() || item.name.length > 100 ||
    !["pending", "linked", "revoked"].includes(item.status as string) || !isDate(item.createdAtUtc) ||
    !nullableDate(item.pairedAtUtc) || !nullableDate(item.lastSeenAtUtc) || !nullableDate(item.revokedAtUtc) ||
    !nullableDate(item.credentialExpiresAtUtc) || !nullableText(item.platform) || !nullableText(item.agentVersion)) return null;
  // Copia explícita: nunca propagar credenciales o campos desconocidos a la interfaz.
  return { id: item.id, name: item.name, status: item.status as DeviceRecord["status"],
    createdAtUtc: item.createdAtUtc, pairedAtUtc: item.pairedAtUtc as string | null,
    lastSeenAtUtc: item.lastSeenAtUtc as string | null, revokedAtUtc: item.revokedAtUtc as string | null,
    credentialExpiresAtUtc: item.credentialExpiresAtUtc as string | null,
    platform: item.platform as string | null, agentVersion: item.agentVersion as string | null };
}

export function parsePairing(value: unknown, nowMs = Date.now()): DevicePairing | null {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  const device = parseDevice(item.device);
  if (!device || typeof item.pairingCode !== "string" || !/^[A-Za-z0-9_-]{43}$/.test(item.pairingCode) ||
    !isDate(item.expiresAtUtc) || Date.parse(item.expiresAtUtc) <= nowMs) return null;
  return { device, pairingCode: item.pairingCode, expiresAtUtc: item.expiresAtUtc };
}

export function parseDeviceDetail(value: unknown): DeviceDetail | null {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  const device = parseDevice(item.device);
  if (!device || !Array.isArray(item.analyses)) return null;
  const analyses: DeviceAnalysis[] = [];
  for (const record of item.analyses) {
    if (!record || typeof record !== "object") return null;
    const analysis = record as Record<string, unknown>;
    const summary = parseAnalysisSummary(analysis);
    if (!summary || !Array.isArray(analysis.findings)) return null;
    const findings: DeviceFinding[] = [];
    for (const record of analysis.findings) {
      if (!record || typeof record !== "object") return null;
      const finding = record as Record<string, unknown>;
      if (!isDeviceId(finding.id) || typeof finding.ruleId !== "string" || !Number.isInteger(finding.ruleVersion) || (finding.ruleVersion as number) < 1 ||
        typeof finding.title !== "string" || typeof finding.evidence !== "string" || typeof finding.recommendation !== "string" ||
        !["informational", "low", "medium", "high", "critical"].includes(finding.severity as string) || !isDate(finding.detectedAtUtc) ||
        !(finding.status === undefined || finding.status === null || ["passed", "attention", "unknown", "notApplicable"].includes(finding.status as string))) return null;
      findings.push({ id: finding.id, ruleId: finding.ruleId, ruleVersion: finding.ruleVersion as number, title: finding.title,
        severity: finding.severity as string, evidence: finding.evidence, recommendation: finding.recommendation, detectedAtUtc: finding.detectedAtUtc,
        status: (finding.status ?? null) as CheckStatus | null });
    }
    if (summary.source === "windows-agent" && (findings.length !== 10 || findings.some(item => item.status === null) ||
      findings.filter(item => item.status === "passed").length !== summary.passedCount || findings.filter(item => item.status === "attention").length !== summary.attentionCount ||
      findings.filter(item => item.status === "unknown").length !== summary.unknownCount || findings.filter(item => item.status === "notApplicable").length !== summary.notApplicableCount)) return null;
    analyses.push({ ...summary, findings });
  }
  return { device, analyses };
}
