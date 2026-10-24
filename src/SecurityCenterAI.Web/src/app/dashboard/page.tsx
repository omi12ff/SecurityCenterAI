import type { Metadata } from "next";
import { DashboardUnavailable, DashboardView } from "@/components/dashboard-view";
import { loadDashboard, requireSession, redirectToRenewal } from "@/lib/session";
import { SessionRenewal } from "@/components/session-renewal";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchDevices } from "@/lib/device-api";
import { fetchExplanation } from "@/lib/guidance-api";
import { analysisPriorities, type DashboardPriorities } from "@/lib/analysis-priorities";
import type { DeviceRecord } from "@/lib/devices";

export const metadata: Metadata = { title: "Tu panel" };
export const dynamic = "force-dynamic";

export default async function DashboardPage() {
  const session = await requireSession("/dashboard");
  let loaded;
  try { loaded = await Promise.all([fetchProfile(session.token), loadDashboard(session)]); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/dashboard");
    return <DashboardUnavailable />;
  }
  const [user, result] = loaded;
  if (result.kind === "unauthenticated") return redirectToRenewal("/dashboard");
  if (result.kind === "unavailable") return <DashboardUnavailable />;
  let devices: DeviceRecord[] | null = null;
  let priorities: DashboardPriorities = null;
  const latest = result.data.lastAnalysis;
  const extra = await Promise.allSettled([
    fetchDevices(session.token),
    latest?.source === "windows-agent" && latest.deviceId ? fetchExplanation(latest.deviceId, latest.id, session.token) : Promise.resolve(null),
  ]);
  for (const item of extra) if (item.status === "rejected" && item.reason instanceof ApiError && item.reason.status === 401) return redirectToRenewal("/dashboard");
  if (extra[0].status === "fulfilled") devices = extra[0].value;
  if (latest?.source === "windows-agent") {
    const guide = extra[1].status === "fulfilled" ? extra[1].value : null;
    const model = guide && guide.analysis.id === latest.id && guide.analysis.reportId === latest.reportId ? analysisPriorities(guide.analysis) : null;
    priorities = guide && model ? { kind: "ready", deviceName: guide.deviceName, priorities: model } : { kind: "unavailable" };
  }
  return <><SessionRenewal expiresAt={session.expiresAt.toISOString()} /><DashboardView user={user} data={result.data} devices={devices} priorities={priorities} /></>;
}
