"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, request } from "@/lib/api";
import { deleteSession, requireSession, redirectToRenewal } from "@/lib/session";
import { isEmailChangeId, validEmail, type EmailChangeState } from "@/lib/account-email-change";

const text = (data: FormData, field: string) => typeof data.get(field) === "string" ? data.get(field) as string : "";
const currentPassword = (data: FormData) => { const value = text(data, "currentPassword"); return value.length > 0 && value.length <= 128 ? value : null; };

function errorState(error: unknown): EmailChangeState {
  if (error instanceof ApiError && error.status === 403) return { message: "No pudimos confirmar tu contraseña o tu correo actual. Revisalos o esperá antes de intentar de nuevo." };
  if (error instanceof ApiError && error.status === 429) return { message: "Esperá al menos un minuto antes de solicitar otro enlace o volver a intentar." };
  if (error instanceof ApiError && error.status === 409) return { message: "La solicitud cambió o la dirección no está disponible. Actualizá Mi cuenta y revisá los datos." };
  if (error instanceof ApiError && error.status === 400) return { message: "Revisá la confirmación y los datos. La solicitud puede haber vencido o faltar una dirección por confirmar." };
  return { message: "No pudimos confirmar el resultado. Actualizá Mi cuenta antes de repetir la operación." };
}

async function accountOperation(path: string, body: unknown): Promise<EmailChangeState | null> {
  const session = await requireSession("/account");
  try { await request(path, { token: session.token, body }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    return errorState(error);
  }
  revalidatePath("/account");
  return null;
}

export async function requestEmailChangeAction(_previous: EmailChangeState, data: FormData): Promise<EmailChangeState> {
  const newEmail = text(data, "newEmail").trim().toLowerCase();
  const repeated = text(data, "confirmNewEmail").trim().toLowerCase();
  const password = currentPassword(data);
  if (!validEmail(newEmail) || repeated !== newEmail || password === null || data.get("confirmed") !== "yes")
    return { message: "Revisá ambos correos nuevos, tu contraseña actual y la confirmación de acceso a las dos direcciones." };
  return await accountOperation("/api/account/email/request", { newEmail, currentPassword: password, confirmed: true }) ??
    { success: true, message: "Solicitud guardada. Revisá ambos correos y confirmá cada dirección desde su enlace. Después volvé aquí para finalizar." };
}

export async function cancelEmailChangeAction(_previous: EmailChangeState, data: FormData): Promise<EmailChangeState> {
  const requestId = data.get("requestId");
  if (!isEmailChangeId(requestId) || data.get("confirmed") !== "yes") return { message: "Confirmá la cancelación de la solicitud mostrada." };
  return await accountOperation("/api/account/email/cancel", { requestId, confirmed: true }) ?? { success: true, message: "La solicitud fue cancelada. Conservás tu correo actual." };
}

export async function completeEmailChangeAction(_previous: EmailChangeState, data: FormData): Promise<EmailChangeState> {
  const requestId = data.get("requestId"), password = currentPassword(data);
  if (!isEmailChangeId(requestId) || password === null || data.get("confirmed") !== "yes")
    return { message: "Ingresá tu contraseña actual y confirmá el cambio con cierre de sesiones web." };
  const error = await accountOperation("/api/account/email/complete", { requestId, currentPassword: password, confirmed: true });
  if (error) return error;
  await deleteSession(); redirect("/login?reason=email-changed");
}

export async function confirmEmailChangeAction(_previous: EmailChangeState, data: FormData): Promise<EmailChangeState> {
  const token = text(data, "token"), kind = text(data, "kind");
  if (!/^[A-Za-z0-9_-]{43}$/.test(token) || !["current", "new"].includes(kind) || data.get("confirmed") !== "yes")
    return { message: "El enlace no está completo o falta confirmar la dirección." };
  try { await request(`/api/account/email/confirm-${kind}`, { body: { token, confirmed: true } }); }
  catch { return { message: "No se pudo confirmar. El enlace puede estar usado, reemplazado o vencido. Revisá la solicitud desde Mi cuenta." }; }
  revalidatePath("/account");
  return { success: true, message: "Confirmación recibida. Volvé a Mi cuenta para revisar ambas direcciones y finalizar con tu contraseña." };
}
