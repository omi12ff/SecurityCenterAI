"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { ApiError } from "@/lib/api";
import { revokeAccountSession } from "@/lib/account-sessions-api";
import { validateSessionRevoke, type SessionRevokeState } from "@/lib/account-sessions";
import { deleteSession, requireSession, redirectToRenewal } from "@/lib/session";

export async function revokeAccountSessionAction(_previous: SessionRevokeState, data: FormData): Promise<SessionRevokeState> {
  const validated = validateSessionRevoke(data);
  if (!validated) return { message: "Seleccioná una sesión válida y confirmá su cierre." };
  const session = await requireSession("/account/sessions");
  let result;
  try { result = await revokeAccountSession(session.token, validated.id); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account/sessions");
    if (error instanceof ApiError && error.status === 404)
      return { message: "Esa sesión ya no está disponible. Actualizá la lista." };
    if (error instanceof ApiError && error.status === 429)
      return { message: "Hubo demasiados intentos. Esperá un momento antes de volver a intentar." };
    return { message: "No pudimos confirmar el cierre. Actualizá la lista para revisar su estado." };
  }
  if (result.currentSession) {
    await deleteSession();
    redirect("/login?reason=session-closed");
  }
  revalidatePath("/account/sessions");
  revalidatePath("/account/activity");
  return {};
}
