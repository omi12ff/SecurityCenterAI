import type { DashboardSummary, RecentAnalysis } from "@/types/api";

type DashboardApiResponse = Partial<{
  securityScore: number;
  score: number;
  totalAnalyses: number;
  analysesCount: number;
  recentAnalyses: unknown[];
  history: unknown[];
}>;

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : {};
}

function statusFromScore(score: number | null): RecentAnalysis["status"] {
  if (score === null) {
    return "unknown";
  }

  if (score >= 80) {
    return "safe";
  }

  if (score >= 50) {
    return "warning";
  }

  return "danger";
}

function formatDate(value: unknown): string {
  if (typeof value !== "string" || !value) {
    return "";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("es", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function toAnalysis(value: unknown, index: number): RecentAnalysis {
  const item = asRecord(value);
  const score = typeof item.score === "number" ? item.score : null;
  const rawStatus = String(item.status ?? item.riskLevel ?? "").toLowerCase();
  const status: RecentAnalysis["status"] =
    rawStatus === "safe" || rawStatus === "warning" || rawStatus === "danger"
      ? rawStatus
      : statusFromScore(score);

  return {
    id: String(item.id ?? index),
    title: String(item.title ?? item.name ?? `Analisis de seguridad #${index + 1}`),
    status,
    createdAt: formatDate(item.analyzedAtUtc ?? item.createdAt ?? item.date),
    score,
    summary: String(
      item.summary ??
        item.description ??
        (score === null
          ? "Resultado pendiente de clasificacion."
          : `Puntaje obtenido: ${score}/100.`),
    ),
  };
}

export function normalizeDashboard(data: DashboardApiResponse): DashboardSummary {
  const recent = Array.isArray(data.recentAnalyses)
    ? data.recentAnalyses
    : Array.isArray(data.history)
      ? data.history
      : [];

  return {
    securityScore:
      typeof data.securityScore === "number"
        ? data.securityScore
        : typeof data.score === "number"
          ? data.score
          : null,
    totalAnalyses:
      typeof data.totalAnalyses === "number"
        ? data.totalAnalyses
        : typeof data.analysesCount === "number"
          ? data.analysesCount
          : recent.length,
    recentAnalyses: recent.map(toAnalysis),
  };
}
