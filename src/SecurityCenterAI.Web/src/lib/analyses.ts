export type CheckStatus = "passed" | "attention" | "unknown" | "notApplicable";
export type AnalysisMetadata = {
  source: "windows-agent" | null; scoringVersion: number | null; coveragePercent: number | null;
  passedCount: number | null; attentionCount: number | null; unknownCount: number | null;
  notApplicableCount: number | null; agentVersion: string | null; startedAtUtc: string | null;
  completedAtUtc: string | null; reportId: string | null;
};
export type AnalysisSummary = AnalysisMetadata & {
  id: string; score: number | null; analyzedAtUtc: string; deviceId: string | null;
};
export const isUuid = (value: unknown): value is string => typeof value === "string" && /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(value);
export const isAnalysisDate = (value: unknown): value is string => typeof value === "string" && Number.isFinite(Date.parse(value));
const integer = (value: unknown, max: number): value is number => typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= max;

export function parseAnalysisSummary(value: unknown): AnalysisSummary | null {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  if (!isUuid(item.id) || !(item.score === null || integer(item.score, 100)) || !isAnalysisDate(item.analyzedAtUtc) ||
    !(item.deviceId === undefined || item.deviceId === null || isUuid(item.deviceId))) return null;
  const basic = { id: item.id, score: item.score as number | null, analyzedAtUtc: item.analyzedAtUtc, deviceId: (item.deviceId ?? null) as string | null };
  if (item.source === null || item.source === undefined) return { ...basic, source: null, scoringVersion: null,
    coveragePercent: null, passedCount: null, attentionCount: null, unknownCount: null, notApplicableCount: null,
    agentVersion: null, startedAtUtc: null, completedAtUtc: null, reportId: null };
  if (item.source !== "windows-agent" || item.scoringVersion !== 1 || !integer(item.coveragePercent, 100) ||
    !integer(item.passedCount, 10) || !integer(item.attentionCount, 10) || !integer(item.unknownCount, 10) ||
    !integer(item.notApplicableCount, 10) || !isUuid(item.reportId) || typeof item.agentVersion !== "string" ||
    !item.agentVersion || item.agentVersion.length > 64 || !isAnalysisDate(item.startedAtUtc) || !isAnalysisDate(item.completedAtUtc) ||
    Date.parse(item.startedAtUtc) > Date.parse(item.completedAtUtc)) return null;
  const known = item.passedCount + item.attentionCount;
  if (known + item.unknownCount + item.notApplicableCount !== 10 ||
    item.coveragePercent !== (item.notApplicableCount === 10 ? 0 : Math.floor(100 * known / (10 - item.notApplicableCount))) ||
    ((known === 0) !== (item.score === null))) return null;
  return { ...basic, source: "windows-agent", scoringVersion: item.scoringVersion, coveragePercent: item.coveragePercent,
    passedCount: item.passedCount, attentionCount: item.attentionCount, unknownCount: item.unknownCount,
    notApplicableCount: item.notApplicableCount, agentVersion: item.agentVersion, startedAtUtc: item.startedAtUtc,
    completedAtUtc: item.completedAtUtc, reportId: item.reportId };
}

export function parseDashboard(value: unknown): { securityScore: number | null; totalAnalyses: number; recentAnalyses: AnalysisSummary[]; lastAnalysis: AnalysisSummary | null } | null {
  if (!value || typeof value !== "object") return null;
  const data = value as Record<string, unknown>;
  if (!(data.securityScore === null || integer(data.securityScore, 100)) || !integer(data.totalAnalyses, Number.MAX_SAFE_INTEGER) ||
    !Array.isArray(data.recentAnalyses)) return null;
  const recent = data.recentAnalyses.map(parseAnalysisSummary);
  if (recent.some(item => !item)) return null;
  const last = data.lastAnalysis === undefined ? recent[0] ?? null : data.lastAnalysis === null ? null : parseAnalysisSummary(data.lastAnalysis);
  if (data.lastAnalysis && !last) return null;
  if ((last?.score ?? null) !== data.securityScore || (recent.length > 0 && last?.id !== recent[0]?.id)) return null;
  return { securityScore: data.securityScore as number | null, totalAnalyses: data.totalAnalyses as number,
    recentAnalyses: recent as AnalysisSummary[], lastAnalysis: last };
}
