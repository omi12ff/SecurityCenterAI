import { ApiError } from "@/lib/api";
import { exportAccount } from "@/lib/account-export-api";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const dynamic = "force-dynamic";
const headers = {
  "Cache-Control": "private, no-store",
  "Referrer-Policy": "no-referrer",
  "X-Content-Type-Options": "nosniff",
};

export async function GET() {
  const session = await requireSession("/account");
  try {
    const result = await exportAccount(session.token, session.user.id);
    return new Response(JSON.stringify(result, null, 2) + "\n", { headers: { ...headers,
      "Content-Type": "application/json; charset=utf-8",
      "Content-Disposition": `attachment; filename="SecurityCenterAI-account-${result.profile.id.toLowerCase()}.json"`,
    } });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    if (error instanceof ApiError && error.status === 429) {
      return Response.json({ message: "Ya pediste varias copias de tu cuenta. Esperá un minuto y volvé a intentar desde Mi cuenta." },
        { status: 429, headers });
    }
    return Response.json({ message: "No pudimos exportar los datos de tu cuenta. Volvé a Mi cuenta e intentá de nuevo." },
      { status: 503, headers });
  }
}
