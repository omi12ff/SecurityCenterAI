"use client";

import { useActionState } from "react";
import { deleteAccountAction } from "@/app/account-deletion-actions";
import type { AccountDeletionState } from "@/lib/account-deletion";

export function AccountDeletionForm() {
  const [state, action, pending] = useActionState<AccountDeletionState, FormData>(deleteAccountAction, {});
  return <section className="account-security-section account-deletion" aria-labelledby="account-deletion-title">
    <h2 id="account-deletion-title">Eliminar mi cuenta</h2>
    <p>La eliminación es permanente: borra tu perfil, equipos, informes, evidencias, valoraciones y marcas de informes eliminados de la aplicación. Se cierran todas tus sesiones y los agentes pierden su acceso.</p>
    <p>Las descargas, informes y credenciales locales del agente, correos ya enviados y backups permanecen fuera de esta operación. Podés <a href="#account-export-title">exportar tus datos antes de continuar</a>.</p>
    <form action={action} className="auth-form account-action-form" aria-busy={pending}>
      {state.message && <p className="form-alert form-alert--error" role="alert">{state.message}</p>}
      <div className="field-group"><label className="field-label" htmlFor="deletion-password">Contraseña actual</label>
        <input id="deletion-password" className="text-input" type="password" name="password" autoComplete="current-password" maxLength={128} required disabled={pending} aria-invalid={Boolean(state.fieldErrors?.password)} />
        {state.fieldErrors?.password && <p className="field-error" role="alert">{state.fieldErrors.password}</p>}
      </div>
      <div className="field-group"><label className="field-label" htmlFor="deletion-confirmation">Escribí ELIMINAR para confirmar</label>
        <input id="deletion-confirmation" className="text-input" type="text" name="confirmation" autoComplete="off" autoCapitalize="characters" pattern="ELIMINAR" maxLength={8} required disabled={pending} aria-invalid={Boolean(state.fieldErrors?.confirmation)} />
        {state.fieldErrors?.confirmation && <p className="field-error" role="alert">{state.fieldErrors.confirmation}</p>}
      </div>
      <button className="button button--danger" type="submit" disabled={pending}>{pending ? "Eliminando cuenta…" : "Eliminar mi cuenta"}</button>
    </form>
  </section>;
}
