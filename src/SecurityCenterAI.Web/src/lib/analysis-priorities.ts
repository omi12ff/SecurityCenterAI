import type { DeviceAnalysis, DeviceFinding } from "./devices.ts";

export type PriorityFinding = Pick<DeviceFinding, "id" | "ruleId" | "title" | "severity">;
export type AnalysisPriorities = { attention: PriorityFinding[]; unknown: PriorityFinding[]; attentionTotal: number; unknownTotal: number; applicableCount: number };
export type DashboardPriorities = { kind: "ready"; deviceName: string; priorities: AnalysisPriorities } | { kind: "unavailable" } | null;

const severityOrder: Record<string, number> = { critical: 0, high: 1, medium: 2, low: 3, informational: 4 };
const identityOrder = (a: DeviceFinding, b: DeviceFinding) => a.ruleId.localeCompare(b.ruleId, "en") || a.id.localeCompare(b.id, "en");
const publicFinding = (finding: DeviceFinding): PriorityFinding => ({ id: finding.id, ruleId: finding.ruleId, title: finding.title, severity: finding.severity });

/** Presentation order only: preserve the original score, statuses and evidence. */
export function analysisPriorities(analysis: DeviceAnalysis): AnalysisPriorities | null {
  if (analysis.source !== "windows-agent") return null;
  const attention = analysis.findings.filter(finding => finding.status === "attention")
    .sort((a, b) => (severityOrder[a.severity] ?? 5) - (severityOrder[b.severity] ?? 5) || identityOrder(a, b));
  // Unknown is missing information, so its ordering never implies severity.
  const unknown = analysis.findings.filter(finding => finding.status === "unknown").sort(identityOrder);
  return { attention: attention.slice(0, 3).map(publicFinding), unknown: unknown.slice(0, 3).map(publicFinding),
    attentionTotal: attention.length, unknownTotal: unknown.length,
    applicableCount: analysis.findings.filter(finding => finding.status !== "notApplicable").length };
}
