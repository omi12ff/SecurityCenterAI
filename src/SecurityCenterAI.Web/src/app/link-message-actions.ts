"use server";

import { ApiError, request } from "@/lib/api";
import { parseLinkMessageQuota, parseLinkMessageResponse, validateLinkMessage, type LinkMessageState } from "@/lib/link-message";
import { readSession, redirectToRenewal } from "@/lib/session";

export async function checkLinkMessageAction(_previous: LinkMessageState, data: FormData): Promise<LinkMessageState> {
  const body = validateLinkMessage(data);
  if (!body) return { message: "Revisá el tipo, el contenido, su longitud y la confirmación de envío." };
  const session = await readSession();
  if (!session) return redirectToRenewal("/tools/scam-check");
  try {
    const response = parseLinkMessageResponse(await request("/api/tools/link-message", { token: session.token, body }));
    return response ? { response } : { message: "No pudimos comprobar el resultado recibido. Actualizá el límite antes de intentar otra consulta." };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/tools/scam-check");
    const message = error instanceof ApiError && error.status === 403 ? "Verificá tu correo desde Mi cuenta antes de usar la herramienta." :
      error instanceof ApiError && error.status === 400 ? "Usá un enlace HTTP/HTTPS completo o un mensaje dentro del límite indicado." :
      error instanceof ApiError && error.status === 429 ? "Se alcanzó el límite diario o hubo demasiadas solicitudes. Revisá el contador y esperá antes de intentar de nuevo." :
      "No pudimos completar la revisión. Actualizá el límite antes de volver a intentar.";
    try { return { message, quota: parseLinkMessageQuota(await request("/api/tools/link-message", { token: session.token })) ?? undefined }; }
    catch { return { message }; }
  }
}
