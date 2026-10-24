import { isAnalysisDate } from "./analyses.ts";

export type LinkMessageQuota = { dayUtc: string; used: number; limit: number; remaining: number; resetsAtUtc: string };
export type LinkMessageSignal = { code: string; title: string; explanation: string; advice: string };
export type LinkMessageResponse = { report: { rulesVersion: "local-signals-v1"; kind: "link" | "message"; outcome: "signals-found" | "no-obvious-signals";
  signals: LinkMessageSignal[]; hosts: string[]; linksChecked: number; linksOmitted: number }; quota: LinkMessageQuota; checkedAtUtc: string };
export type LinkMessageState = { message?: string; response?: LinkMessageResponse; quota?: LinkMessageQuota };
const record = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);
const integer = (value: unknown, max = 1000): value is number => Number.isInteger(value) && (value as number) >= 0 && (value as number) <= max;
const text = (value: unknown, max: number): value is string => typeof value === "string" && value.length > 0 && value.length <= max;
export function isUsageDay(value: unknown): value is string {
  return typeof value === "string" && /^(?!0000)\d{4}-\d{2}-\d{2}$/.test(value) &&
    Number.isFinite(Date.parse(`${value}T00:00:00Z`)) && new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;
}
export function parseLinkMessageQuota(value: unknown): LinkMessageQuota | null {
  if (!record(value) || !isUsageDay(value.dayUtc) || !integer(value.used) || !integer(value.limit) || value.limit < 1 ||
    !integer(value.remaining) || value.remaining !== Math.max(0, value.limit - value.used) || !isAnalysisDate(value.resetsAtUtc) ||
    Date.parse(value.resetsAtUtc) !== Date.parse(`${value.dayUtc}T00:00:00Z`) + 86_400_000) return null;
  return { dayUtc: value.dayUtc, used: value.used, limit: value.limit, remaining: value.remaining, resetsAtUtc: value.resetsAtUtc };
}
const signalCodes = new Set(["urgent-language", "credential-reference", "prize-reference", "payment-reference", "unencrypted-link",
  "embedded-userinfo", "numeric-host", "internationalized-host", "unusual-port", "invisible-characters", "shortened-link"]);
export function parseLinkMessageResponse(value: unknown): LinkMessageResponse | null {
  if (!record(value) || !record(value.report) || !isAnalysisDate(value.checkedAtUtc)) return null;
  const quota = parseLinkMessageQuota(value.quota); const report = value.report;
  if (!quota || report.rulesVersion !== "local-signals-v1" || !["link", "message"].includes(report.kind as string) ||
    !["signals-found", "no-obvious-signals"].includes(report.outcome as string) || !Array.isArray(report.signals) ||
    report.signals.length > signalCodes.size || !Array.isArray(report.hosts) || report.hosts.length > 5 ||
    !integer(report.linksChecked, 5) || !integer(report.linksOmitted, 1000)) return null;
  const signals: LinkMessageSignal[] = []; const codes = new Set<string>();
  for (const item of report.signals) {
    if (!record(item) || !text(item.code, 40) || !signalCodes.has(item.code) || codes.has(item.code) || !text(item.title, 120) ||
      !text(item.explanation, 500) || !text(item.advice, 500)) return null;
    codes.add(item.code); signals.push({ code: item.code, title: item.title, explanation: item.explanation, advice: item.advice });
  }
  if ((signals.length === 0) !== (report.outcome === "no-obvious-signals")) return null;
  const hosts: string[] = [];
  for (const host of report.hosts) {
    if (!text(host, 253) || !/^[a-z0-9._:%[\]-]+$/.test(host) || hosts.includes(host)) return null;
    hosts.push(host);
  }
  if (hosts.length > report.linksChecked || report.kind === "link" && (report.linksChecked !== 1 || report.linksOmitted !== 0)) return null;
  return { report: { rulesVersion: report.rulesVersion, kind: report.kind as "link" | "message", outcome: report.outcome as "signals-found" | "no-obvious-signals",
    signals, hosts, linksChecked: report.linksChecked, linksOmitted: report.linksOmitted }, quota, checkedAtUtc: value.checkedAtUtc };
}
export function validateLinkMessage(data: FormData): { kind: "link" | "message"; content: string; confirmed: true } | null {
  const kind = data.get("kind"); const content = data.get("content");
  if (kind !== "link" && kind !== "message" || typeof content !== "string" || !content.trim() ||
    content.length > (kind === "link" ? 2048 : 6000) || data.get("confirmed") !== "yes") return null;
  return { kind, content: content.trim(), confirmed: true };
}
