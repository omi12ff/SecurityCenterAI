"use server";

import { redirect } from "next/navigation";
import { ApiError, authenticate, refreshAuth, revokeSession, revokeAllSessions, sendPasswordReset, resetPassword, requestVerification, confirmEmail } from "@/lib/api";
import type { AuthFormState } from "@/lib/contracts";
import { createSession, deleteSession, readRefreshToken, requireSession } from "@/lib/session";
import { validateCredentials } from "@/lib/validation";
import { validNewPasswordLength, NEW_PASSWORD_HELP } from "@/lib/new-password";

async function submitCredentials(mode: "login" | "register", formData: FormData): Promise<AuthFormState> {
  const validation = validateCredentials(mode, formData);
  if (!validation.success) return validation.state;
  const { password: _password, ...values } = validation.data;
  try {
    const auth = await authenticate(mode, validation.data);
    await createSession(auth);
  } catch (error) {
    return {
      message: error instanceof ApiError ? error.message : "No pudimos iniciar la sesión. Intentá nuevamente.",
      fieldErrors: error instanceof ApiError ? error.fieldErrors : undefined,
      values,
    };
  }
  redirect("/dashboard");
}

export async function loginAction(_previous: AuthFormState, formData: FormData): Promise<AuthFormState> {
  return submitCredentials("login", formData);
}

export async function registerAction(_previous: AuthFormState, formData: FormData): Promise<AuthFormState> {
  return submitCredentials("register", formData);
}

export async function logoutAction(): Promise<void> {
  let remoteUnavailable = false;
  const refreshToken = await readRefreshToken();
  if (refreshToken) {
    try { await revokeSession(refreshToken); }
    catch (error) { remoteUnavailable = !(error instanceof ApiError && [400, 401].includes(error.status)); }
  }
  await deleteSession();
  redirect(remoteUnavailable ? "/login?reason=logout-local" : "/login");
}

export async function renewSessionAction(): Promise<{ ok: boolean; expired?: boolean; message?: string }> {
  const refreshToken = await readRefreshToken();
  if (!refreshToken) { await deleteSession(); return { ok: false, expired: true }; }
  try {
    await createSession(await refreshAuth(refreshToken));
    return { ok: true };
  } catch (error) {
    if (error instanceof ApiError && [400, 401].includes(error.status)) {
      await deleteSession();
      return { ok: false, expired: true };
    }
    return { ok: false, message: "No pudimos renovar tu sesión. Intentá nuevamente." };
  }
}

function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function actionError(error: unknown, fallback: string): AuthFormState {
  return { message: error instanceof ApiError && error.status !== 400 ? error.message : fallback };
}

export async function forgotPasswordAction(_previous: AuthFormState, formData: FormData): Promise<AuthFormState> {
  const email = text(formData, "email").trim().toLowerCase();
  if (!email || email.length > 320 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    return { fieldErrors: { email: "Ingresá un correo electrónico válido." }, values: { email } };
  }
  try { await sendPasswordReset(email); }
  catch (error) { return actionError(error, "No pudimos procesar la solicitud."); }
  return { success: true, message: "Si existe una cuenta con ese correo, recibirás un enlace para cambiar tu contraseña." };
}

export async function resetPasswordAction(_previous: AuthFormState, formData: FormData): Promise<AuthFormState> {
  const token = text(formData, "token");
  const password = text(formData, "password");
  if (!validNewPasswordLength(password)) return { fieldErrors: { password: NEW_PASSWORD_HELP } };
  if (password !== text(formData, "confirmPassword")) return { fieldErrors: { confirmPassword: "Las contraseñas deben coincidir." } };
  if (!/^[A-Za-z0-9_-]{43,512}$/.test(token)) return { message: "El enlace no es válido o ya venció. Solicitá uno nuevo." };
  try { await resetPassword(token, password); }
  catch (error) {
    if (error instanceof ApiError && error.fieldErrors?.password) return { fieldErrors: error.fieldErrors };
    return actionError(error, "El enlace no es válido o ya venció. Solicitá uno nuevo.");
  }
  // El restablecimiento revoca todas las sesiones; no inicia sesión automáticamente.
  await deleteSession();
  return { success: true, message: "Tu contraseña se actualizó. Iniciá sesión con la nueva contraseña." };
}

export async function verifyEmailAction(_previous: AuthFormState, formData: FormData): Promise<AuthFormState> {
  const token = text(formData, "token");
  if (!/^[A-Za-z0-9_-]{43,512}$/.test(token)) return { message: "El enlace no es válido o ya venció. Pedí uno nuevo desde tu cuenta." };
  try { await confirmEmail(token); }
  catch (error) { return actionError(error, "El enlace no es válido o ya venció. Pedí uno nuevo desde tu cuenta."); }
  return { success: true, message: "Tu correo quedó verificado. Ya podés volver a tu cuenta." };
}

export async function resendVerificationAction(_previous: AuthFormState, _formData: FormData): Promise<AuthFormState> {
  const session = await requireSession("/account");
  try { await requestVerification(session.token); }
  catch (error) { return actionError(error, "No pudimos enviar el enlace. Intentá de nuevo."); }
  return { success: true, message: "Si tu correo está pendiente de verificación, recibirás un nuevo enlace." };
}

export async function logoutAllAction(_previous: AuthFormState, _formData: FormData): Promise<AuthFormState> {
  const session = await requireSession("/account");
  try { await revokeAllSessions(session.token); }
  catch (error) { return actionError(error, "No pudimos cerrar las sesiones. Intentá nuevamente."); }
  await deleteSession();
  redirect("/login?reason=all-sessions-closed");
}
