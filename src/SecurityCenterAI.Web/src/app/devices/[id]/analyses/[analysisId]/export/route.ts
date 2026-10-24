import { ApiError } from "@/lib/api";
import { exportAnalysis } from "@/lib/analysis-history-api";
import { isDeviceId } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const dynamic = "force-dynamic";
const headers = {
  "Cache-Control": "private, no-store",
  "Referrer-Policy": "no-referrer",
  "X-Content-Type-Options": "nosniff",
};

export async function GET(_request: Request, { params }: { params: Promise<{ id: string; analysisId: string }> }) {
  const session = await requireSession("/devices");
  const { id, analysisId } = await params;
  if (!isDeviceId(id) || !isDeviceId(analysisId)) return Response.json({ message: "No encontramos ese análisis en tu cuenta." }, { status: 404, headers });
  try {
    const result = await exportAnalysis(id, analysisId, session.token);
    return new Response(JSON.stringify(result, null, 2) + "\n", { headers: { ...headers,
      "Content-Type": "application/json; charset=utf-8",
      "Content-Disposition": `attachment; filename="SecurityCenterAI-analysis-${analysisId.toLowerCase()}.json"`,
    } });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    const missing = error instanceof ApiError && error.status === 404;
    return Response.json({ message: missing ? "No encontramos ese análisis en tu cuenta." : "No pudimos exportar el análisis. Volvé al historial e intentá de nuevo." },
      { status: missing ? 404 : 503, headers });
  }
}
