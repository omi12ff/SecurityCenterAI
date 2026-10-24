import { isDeviceId, parseDeviceAnalysis, type DeviceAnalysis } from "./devices.ts";

export type Feedback = { rating: "helpful" | "unclear" | "incorrect"; comment: string | null; explanationVersion: number; updatedAtUtc: string };
export type FeedbackState = { message?: string; success?: boolean };
export type CheckExplanation = { findingId: string; meaning: string; whatWasChecked: string; whyItMatters: string; suggestedAction: string; verificationLimit: string; references: { title: string; url: string }[] };
export type AnalysisExplanation = { analysis: DeviceAnalysis; deviceName: string; method: "local-guide"; explanationVersion: number; summary: string; nextSteps: string[]; checks: CheckExplanation[]; feedback: Feedback | null };
export const isRating = (value: unknown): value is Feedback["rating"] => ["helpful", "unclear", "incorrect"].includes(value as string);
export function isOfficialReference(value: unknown): value is string {
  if (typeof value !== "string") return false;
  try { const url = new URL(value); return url.protocol === "https:" && ["support.microsoft.com", "learn.microsoft.com"].includes(url.hostname) && !url.username && !url.password && !url.port; }
  catch { return false; }
}
export function parseFeedback(value: unknown): Feedback | null {
  if (!value || typeof value !== "object") return null;
  const v = value as Record<string, unknown>;
  if (!isRating(v.rating) || !(v.comment === null || typeof v.comment === "string" && v.comment.length <= 1000) ||
    !Number.isInteger(v.explanationVersion) || (v.explanationVersion as number) < 1 || typeof v.updatedAtUtc !== "string" || !Number.isFinite(Date.parse(v.updatedAtUtc))) return null;
  return { rating: v.rating, comment: v.comment as string | null, explanationVersion: v.explanationVersion as number, updatedAtUtc: v.updatedAtUtc };
}
export function parseExplanation(value: unknown): AnalysisExplanation | null {
  if (!value || typeof value !== "object") return null;
  const v = value as Record<string, unknown>, analysis = parseDeviceAnalysis(v.analysis);
  if (!analysis || v.method !== "local-guide" || !Number.isInteger(v.explanationVersion) || (v.explanationVersion as number) < 1 ||
    typeof v.deviceName !== "string" || typeof v.summary !== "string" || !Array.isArray(v.nextSteps) || !v.nextSteps.every(s => typeof s === "string") ||
    !Array.isArray(v.checks) || v.checks.length !== analysis.findings.length) return null;
  const checks: CheckExplanation[] = [];
  for (const check of v.checks) {
    if (!check || typeof check !== "object") return null;
    const c = check as Record<string, unknown>;
    if (!isDeviceId(c.findingId) || !analysis.findings.some(f => f.id === c.findingId) || checks.some(f => f.findingId === c.findingId) ||
      ![c.meaning, c.whatWasChecked, c.whyItMatters, c.suggestedAction, c.verificationLimit].every(s => typeof s === "string") ||
      !Array.isArray(c.references) || !c.references.every(r => r && typeof r.title === "string" && isOfficialReference(r.url))) return null;
    checks.push({ findingId: c.findingId, meaning: c.meaning as string, whatWasChecked: c.whatWasChecked as string,
      whyItMatters: c.whyItMatters as string, suggestedAction: c.suggestedAction as string, verificationLimit: c.verificationLimit as string,
      references: c.references.map(r => ({ title: r.title, url: r.url })) });
  }
  const feedback = parseFeedback(v.feedback);
  if (v.feedback !== null && !feedback) return null;
  return { analysis, deviceName: v.deviceName, method: v.method, explanationVersion: v.explanationVersion as number,
    summary: v.summary, nextSteps: v.nextSteps, checks, feedback };
}
