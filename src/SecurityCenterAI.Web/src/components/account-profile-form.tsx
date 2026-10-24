"use client";

import { useActionState } from "react";
import { updateProfileNameAction } from "@/app/account-profile-actions";
import type { ProfileNameState } from "@/lib/account-profile";

export function AccountProfileForm({ name }: { name: string }) {
  const [state, action, pending] = useActionState<ProfileNameState, FormData>(updateProfileNameAction, {});
  return <section className="account-security-section" aria-labelledby="account-profile-title">
    <h2 id="account-profile-title">Tu perfil</h2>
    <p>Elegí el nombre que querés ver en tu espacio. Podés cambiarlo sin cerrar tus sesiones.</p>
    <form action={action} aria-label="Editar nombre del perfil" className="auth-form account-action-form" aria-busy={pending}>
      {state.message && <p className={`form-alert ${state.success ? "form-alert--success" : "form-alert--error"}`} role={state.success ? "status" : "alert"}>{state.message}</p>}
      <div className="field-group"><label className="field-label" htmlFor="profile-name">Nombre completo</label>
        <input id="profile-name" name="name" className="text-input" type="text" autoComplete="name" defaultValue={name}
          maxLength={200} required disabled={pending} aria-invalid={Boolean(state.fieldErrors?.name)}
          aria-describedby={state.fieldErrors?.name ? "profile-name-error" : "profile-name-help"} />
        <p className="input-help" id="profile-name-help">Entre 2 y 100 caracteres. Se quitan los espacios de los extremos.</p>
        {state.fieldErrors?.name && <p id="profile-name-error" className="field-error" role="alert">{state.fieldErrors.name}</p>}
      </div>
      <button className="button" type="submit" disabled={pending}>{pending ? "Guardando nombre…" : "Guardar nombre"}</button>
    </form>
  </section>;
}
