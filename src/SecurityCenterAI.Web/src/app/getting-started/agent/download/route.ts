import { ApiError, fetchProfile } from "@/lib/api";
import { openConfiguredAgentPackage } from "@/lib/agent-package";
import { agentPackageStream } from "@/lib/agent-package-file";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
const headers = { "Cache-Control": "private, no-store", "Referrer-Policy": "no-referrer", "X-Content-Type-Options": "nosniff" };

export async function GET(request: Request) {
  const session = await requireSession("/getting-started");
  try {
    const user = await fetchProfile(session.token); // Recheck revocation; a decoded cookie is insufficient.
    if (!user.emailVerified) return Response.json({ message: "Verificá tu correo desde Mi cuenta antes de descargar el agente." }, { status: 403, headers });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/getting-started");
    return Response.json({ message: "No pudimos comprobar tu acceso. Volvé a Primeros pasos e intentá de nuevo." }, { status: 503, headers });
  }
  if (new URL(request.url).search) return Response.json({ message: "Volvé a Primeros pasos para descargar el paquete disponible." }, { status: 400, headers });
  const prepared = await openConfiguredAgentPackage();
  if (!prepared) return Response.json({ message: "El paquete no está disponible. Volvé a Primeros pasos e intentá más tarde." }, { status: 503, headers });
  try {
    return new Response(agentPackageStream(prepared.file), { headers: { ...headers,
      "Content-Type": "application/zip", "Content-Length": String(prepared.info.bytes),
      "Content-Disposition": `attachment; filename="${prepared.info.fileName}"`,
    } });
  } catch {
    await prepared.file.close();
    return Response.json({ message: "No pudimos iniciar la descarga. Volvé a Primeros pasos e intentá de nuevo." }, { status: 503, headers });
  }
}
