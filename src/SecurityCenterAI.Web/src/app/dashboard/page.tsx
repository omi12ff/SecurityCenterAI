import type { Metadata } from "next";
import { DashboardUnavailable, DashboardView } from "@/components/dashboard-view";
import { loadDashboard, requireSession, redirectToRenewal } from "@/lib/session";
import { SessionRenewal } from "@/components/session-renewal";

export const metadata: Metadata = { title: "Tu panel" };
export const dynamic = "force-dynamic";

export default async function DashboardPage() {
  const session = await requireSession("/dashboard");
  const result = await loadDashboard(session);
  if (result.kind === "unauthenticated") return redirectToRenewal("/dashboard");
  if (result.kind === "unavailable") return <DashboardUnavailable />;
  return <><SessionRenewal expiresAt={session.expiresAt.toISOString()} /><DashboardView user={session.user} data={result.data} /></>;
}
