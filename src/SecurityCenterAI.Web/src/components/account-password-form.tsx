"use client";

import Link from "next/link";
import { useActionState } from "react";
import { changeAccountPasswordAction } from "@/app/account-password-actions";
import type { PasswordChangeState } from "@/lib/account-password";

export function AccountPasswordForm() {
  const [state, action, pending] = useActionState<PasswordChangeState, FormData>(changeAccountPasswordAction, {});
  const fields = [
    { name: "currentPassword", label: "Contraseña actual", complete: "current-password" },
    { name: "newPassword", label: "Contraseña nueva", complete: "new-password" },
    { name: "confirmNewPassword", label: "Repetí la contraseña nueva", complete: "new-password" },
  ] as const;
  return <section className="account-security-section" aria-labelledby="account-password-title">
    <h2 id="account-password-title">Cambiar contraseña</h2>
    <p>Usá entre 15 y 128 caracteres para la nueva contraseña; evitá valores comunes o previsibles. Se cerrarán todas tus sesiones web, incluida esta, y se invalidarán los enlaces de recuperación pendientes.</p>
    <p>Los agentes vinculados conservan su acceso. Podés revocarlo desde Dispositivos.</p>
    <form aria-label="Cambiar contraseña" action={action} className="auth-form account-action-form" aria-busy={pending}>
      {state.message && <p className="form-alert form-alert--error" role="alert">{state.message}</p>}
      {fields.map(field => <div className="field-group" key={field.name}>
        <label className="field-label" htmlFor={`change-${field.name}`}>{field.label}</label>
        <input id={`change-${field.name}`} className="text-input" type="password" name={field.name} autoComplete={field.complete}
          maxLength={128} minLength={field.name === "currentPassword" ? undefined : 15} required disabled={pending}
          aria-invalid={Boolean(state.fieldErrors?.[field.name])}
          aria-describedby={state.fieldErrors?.[field.name] ? `change-${field.name}-error` : undefined} />
        {state.fieldErrors?.[field.name] && <p id={`change-${field.name}-error`} className="field-error" role="alert">{state.fieldErrors[field.name]}</p>}
      </div>)}
      <label className="history-confirmation"><input name="confirmed" type="checkbox" value="yes" required disabled={pending}
        aria-describedby={state.fieldErrors?.confirmed ? "change-confirmed-error" : undefined} />
        <span>Confirmo el cambio y el cierre de todas las sesiones web</span></label>
      {state.fieldErrors?.confirmed && <p id="change-confirmed-error" className="field-error" role="alert">{state.fieldErrors.confirmed}</p>}
      <button className="button button--primary" type="submit" disabled={pending}>{pending ? "Cambiando contraseña…" : "Guardar contraseña nueva"}</button>
    </form>
    <p><Link href="/forgot-password">Olvidé mi contraseña actual</Link></p>
  </section>;
}
