"use server";

import { redirect } from "next/navigation";
import { ApiError, request } from "@/lib/api";
import { validatePasswordChange, type PasswordChangeState } from "@/lib/account-password";
import { deleteSession, requireSession, redirectToRenewal } from "@/lib/session";

export async function changeAccountPasswordAction(_previous: PasswordChangeState, data: FormData): Promise<PasswordChangeState> {
  const validation = validatePasswordChange(data);
  if (!validation.success) return validation.state;
  const session = await requireSession("/account");
  try { await request("/api/account/password", { token: session.token, body: validation.data }); }
  catch (error) {
    if (error instanceof ApiError && error.fieldErrors?.password) return { fieldErrors: { newPassword: error.fieldErrors.password } };
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    if (error instanceof ApiError && error.status === 403)
      return { message: "No pudimos confirmar tu contraseña actual. Revisala o esperá antes de intentar de nuevo." };
    if (error instanceof ApiError && error.status === 429)
      return { message: "Hubo demasiados intentos. Esperá un momento antes de volver a intentar." };
    return { message: "No pudimos confirmar el cambio. Intentá iniciar sesión con la contraseña nueva antes de repetirlo; también podés recuperar el acceso por correo." };
  }
  await deleteSession();
  redirect("/login?reason=password-changed");
}
