import { isDeviceId, parseDeviceAnalysis, type DeviceAnalysis, type DeviceFinding } from "./devices.ts";
import type { CheckStatus } from "./analyses.ts";

export const comparisonChanges = ["improved", "needsReview", "newInformation", "lostInformation", "unchanged", "notComparable"] as const;
export type ComparisonChange = typeof comparisonChanges[number];
export type ComparisonCheck = {
  ruleId: string; ruleVersion: number | null; title: string; baselineFindingId: string | null;
  targetFindingId: string | null; baselineStatus: CheckStatus | null; targetStatus: CheckStatus | null;
  change: ComparisonChange; explanation: string;
};
export type AnalysisComparison = {
  deviceId: string; deviceName: string; baseline: DeviceAnalysis; target: DeviceAnalysis;
  scoreComparable: boolean; scoreDelta: number | null; coverageDelta: number | null;
  summary: Record<ComparisonChange, number>; checks: ComparisonCheck[];
};
const statuses = ["passed", "attention", "unknown", "notApplicable"];
const validStatus = (value: unknown): value is CheckStatus | null => value === null || statuses.includes(value as string);
const delta = (value: unknown): value is number => typeof value === "number" && Number.isInteger(value) && Math.abs(value) <= 100;
const matchesFinding = (findings: DeviceFinding[], id: unknown, ruleId: string, status: CheckStatus | null) =>
  id === null ? status === null && findings.filter(finding => finding.ruleId === ruleId).length !== 1 :
    isDeviceId(id) && findings.some(finding => finding.id === id && finding.ruleId === ruleId && finding.status === status);

export function parseAnalysisComparison(value: unknown): AnalysisComparison | null {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  const baseline = parseDeviceAnalysis(item.baseline), target = parseDeviceAnalysis(item.target);
  if (!isDeviceId(item.deviceId) || typeof item.deviceName !== "string" || !item.deviceName.trim() || item.deviceName.length > 100 ||
    !baseline || !target || baseline.id === target.id || typeof item.scoreComparable !== "boolean" ||
    !item.summary || typeof item.summary !== "object" || !Array.isArray(item.checks) || item.checks.length > 200) return null;
  if (item.scoreComparable ? baseline.score === null || target.score === null || !delta(item.scoreDelta) || item.scoreDelta !== target.score - baseline.score : item.scoreDelta !== null) return null;
  const expectedCoverage = !item.scoreComparable || baseline.coveragePercent === null || target.coveragePercent === null ? null : target.coveragePercent - baseline.coveragePercent;
  if (item.coverageDelta !== expectedCoverage) return null;
  if ([baseline, target].some(analysis => item.scoreComparable && new Set(analysis.findings.map(finding => finding.ruleId)).size !== analysis.findings.length ||
    new Set(analysis.findings.map(finding => finding.id)).size !== analysis.findings.length || analysis.deviceId !== null && analysis.deviceId !== item.deviceId)) return null;
  if (item.scoreComparable && (baseline.source !== "windows-agent" || target.source !== "windows-agent" ||
    baseline.findings.some(finding => finding.ruleVersion !== 1 || !target.findings.some(other => other.ruleId === finding.ruleId && other.ruleVersion === finding.ruleVersion && (other.status === "notApplicable") === (finding.status === "notApplicable"))))) return null;
  const summary = {} as Record<ComparisonChange, number>;
  const counts = item.summary as Record<string, unknown>;
  for (const change of comparisonChanges) {
    const count = counts[change];
    if (typeof count !== "number" || !Number.isInteger(count) || count < 0 || count > item.checks.length) return null;
    summary[change] = count;
  }
  const checks: ComparisonCheck[] = [];
  const ruleIds = new Set([...baseline.findings, ...target.findings].map(finding => finding.ruleId));
  if (ruleIds.size !== item.checks.length) return null;
  for (const value of item.checks) {
    if (!value || typeof value !== "object") return null;
    const check = value as Record<string, unknown>;
    if (typeof check.ruleId !== "string" || !ruleIds.has(check.ruleId) || checks.some(existing => existing.ruleId === check.ruleId) ||
      !(check.ruleVersion === null || typeof check.ruleVersion === "number" && Number.isInteger(check.ruleVersion) && check.ruleVersion > 0) ||
      typeof check.title !== "string" || typeof check.explanation !== "string" ||
      !validStatus(check.baselineStatus) || !validStatus(check.targetStatus) || !comparisonChanges.includes(check.change as ComparisonChange) ||
      (check.baselineFindingId === null || check.targetFindingId === null) && check.change !== "notComparable" ||
      !matchesFinding(baseline.findings, check.baselineFindingId, check.ruleId, check.baselineStatus) ||
      !matchesFinding(target.findings, check.targetFindingId, check.ruleId, check.targetStatus)) return null;
    checks.push({ ruleId: check.ruleId, ruleVersion: check.ruleVersion as number | null, title: check.title,
      baselineFindingId: check.baselineFindingId as string | null, targetFindingId: check.targetFindingId as string | null,
      baselineStatus: check.baselineStatus, targetStatus: check.targetStatus, change: check.change as ComparisonChange, explanation: check.explanation });
  }
  if (comparisonChanges.some(change => checks.filter(check => check.change === change).length !== summary[change])) return null;
  return { deviceId: item.deviceId, deviceName: item.deviceName, baseline, target, scoreComparable: item.scoreComparable,
    scoreDelta: item.scoreDelta as number | null, coverageDelta: item.coverageDelta as number | null, summary, checks };
}
