"use client";

import Link from "next/link";
import { useActionState, useEffect, useState } from "react";
import { forgotPasswordAction, resetPasswordAction, verifyEmailAction, resendVerificationAction, logoutAllAction } from "@/app/actions";
import type { AuthFormState } from "@/lib/contracts";

function Result({ state }: { state: AuthFormState }) {
  return state.message ? <p className={`form-alert ${state.success ? "form-alert--success" : "form-alert--error"}`} role={state.success ? "status" : "alert"}>{state.message}</p> : null;
}

export function RecoveryForm({ mode, token = "" }: { mode: "forgot" | "reset" | "verify"; token?: string }) {
  const [linkToken] = useState(token);
  const action = mode === "forgot" ? forgotPasswordAction : mode === "reset" ? resetPasswordAction : verifyEmailAction;
  const [state, formAction, pending] = useActionState<AuthFormState, FormData>(action, {});
  useEffect(() => {
    // Mantiene el enlace sensible fuera del historial y del Referer de las acciones.
    if (mode !== "forgot" && new URLSearchParams(window.location.search).has("token")) {
      window.history.replaceState(window.history.state, "", window.location.pathname);
    }
  }, [mode]);
  if (state.success) return <div className="auth-form"><Result state={state} /><Link className="button button--primary" href={mode === "verify" ? "/account" : "/login"}>{mode === "verify" ? "Volver a mi cuenta" : "Iniciar sesión"}</Link></div>;
  return <form className="auth-form recovery-form" action={formAction} aria-busy={pending}>
    <Result state={state} />
    {mode !== "forgot" && <input type="hidden" name="token" value={linkToken} />}
    {mode === "forgot" && <div className="field-group">
      <label className="field-label" htmlFor="recovery-email">Correo electrónico</label>
      <input id="recovery-email" className="text-input" name="email" type="email" autoComplete="email" maxLength={320} required disabled={pending} defaultValue={state.values?.email} aria-invalid={Boolean(state.fieldErrors?.email)} aria-describedby={state.fieldErrors?.email ? "recovery-email-error" : undefined} />
      {state.fieldErrors?.email && <p className="field-error" id="recovery-email-error" role="alert">{state.fieldErrors.email}</p>}
    </div>}
    {mode === "reset" && <>
      <div className="field-group"><label className="field-label" htmlFor="new-password">Contraseña</label><input id="new-password" className="text-input" name="password" type="password" autoComplete="new-password" required minLength={15} maxLength={128} disabled={pending} aria-invalid={Boolean(state.fieldErrors?.password)} aria-describedby="new-password-help" /><p className="input-help" id="new-password-help">Usá entre 15 y 128 caracteres. Evitá contraseñas comunes o previsibles. Podés pegar una contraseña de tu gestor.</p>{state.fieldErrors?.password && <p className="field-error" role="alert">{state.fieldErrors.password}</p>}</div>
      <div className="field-group"><label className="field-label" htmlFor="new-password-confirm">Confirmar contraseña</label><input id="new-password-confirm" className="text-input" name="confirmPassword" type="password" autoComplete="new-password" required minLength={15} maxLength={128} disabled={pending} aria-invalid={Boolean(state.fieldErrors?.confirmPassword)} />{state.fieldErrors?.confirmPassword && <p className="field-error" role="alert">{state.fieldErrors.confirmPassword}</p>}</div>
      <p className="input-help">Al guardar la nueva contraseña se cerrarán todas las sesiones de tu cuenta.</p>
    </>}
    <button className="button button--primary button--full" type="submit" disabled={pending || (mode !== "forgot" && !linkToken)}>{pending ? "Procesando…" : mode === "forgot" ? "Enviar enlace" : mode === "reset" ? "Guardar contraseña" : "Confirmar correo"}</button>
    {mode !== "forgot" && !linkToken && <p className="form-alert form-alert--error" role="alert">El enlace no está completo. Abrí el enlace del correo o solicitá uno nuevo.</p>}
    <Link className="auth-switch" href={mode === "verify" ? "/account" : mode === "reset" ? "/forgot-password" : "/login"}>{mode === "verify" ? "Volver a mi cuenta" : mode === "reset" ? "Solicitar otro enlace" : "Volver a iniciar sesión"}</Link>
  </form>;
}

export function AccountActionForm({ mode }: { mode: "resend" | "logout-all" }) {
  const [state, action, pending] = useActionState<AuthFormState, FormData>(mode === "resend" ? resendVerificationAction : logoutAllAction, {});
  return <form action={action} className="auth-form account-action-form" aria-busy={pending}>
    <Result state={state} />
    <button className="button button--secondary" type="submit" disabled={pending}>{pending ? "Procesando…" : mode === "resend" ? "Enviar correo de verificación" : "Cerrar todas las sesiones"}</button>
  </form>;
}
