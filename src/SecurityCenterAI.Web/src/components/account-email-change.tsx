"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState, useEffect, useState } from "react";
import { requestEmailChangeAction, cancelEmailChangeAction, completeEmailChangeAction, confirmEmailChangeAction } from "@/app/account-email-actions";
import { deviceDate } from "@/components/device-card";
import type { EmailChangeState, EmailChangeStatus, EmailChangePending } from "@/lib/account-email-change";

function Result({ state }: { state: EmailChangeState }) {
  return state.message ? <p className={`form-alert ${state.success ? "form-alert--success" : "form-alert--error"}`} role={state.success ? "status" : "alert"}>{state.message}</p> : null;
}

function RequestForm() {
  const [state, action, pending] = useActionState<EmailChangeState, FormData>(requestEmailChangeAction, {});
  return <form aria-label="Solicitar cambio de correo" action={action} className="auth-form account-action-form" aria-busy={pending}>
    <Result state={state} />
    <div className="field-group"><label className="field-label" htmlFor="change-email-new">Correo nuevo</label><input id="change-email-new" name="newEmail" type="email" className="text-input" autoComplete="email" maxLength={320} required disabled={pending} /></div>
    <div className="field-group"><label className="field-label" htmlFor="change-email-repeat">Repetí el correo nuevo</label><input id="change-email-repeat" name="confirmNewEmail" type="email" className="text-input" autoComplete="off" maxLength={320} required disabled={pending} /></div>
    <div className="field-group"><label className="field-label" htmlFor="change-email-password">Contraseña actual</label><input id="change-email-password" name="currentPassword" type="password" className="text-input" autoComplete="current-password" maxLength={128} required disabled={pending} /></div>
    <label className="history-confirmation"><input name="confirmed" type="checkbox" value="yes" required disabled={pending} /><span>Tengo acceso al correo actual y al nuevo</span></label>
    <button className="button button--secondary" type="submit" disabled={pending}>{pending ? "Solicitando…" : "Enviar confirmaciones"}</button>
  </form>;
}

function PendingControls({ value, cancelAction, cancelling }: { value: EmailChangePending; cancelAction: (data: FormData) => void; cancelling: boolean }) {
  const [complete, completeAction, completing] = useActionState<EmailChangeState, FormData>(completeEmailChangeAction, {});
  return <>
    <form aria-label="Cancelar cambio de correo" action={cancelAction} className="auth-form account-action-form" aria-busy={cancelling}>
      <input type="hidden" name="requestId" value={value.id} />
      <label className="history-confirmation"><input name="confirmed" type="checkbox" value="yes" required disabled={cancelling} /><span>Confirmo cancelar esta solicitud</span></label>
      <button className="button button--secondary" disabled={cancelling}>{cancelling ? "Cancelando…" : "Cancelar solicitud"}</button>
    </form>
    {!value.expired && <form aria-label="Finalizar cambio de correo" action={completeAction} className="auth-form account-action-form" aria-busy={completing}>
      <Result state={complete} /><input type="hidden" name="requestId" value={value.id} />
      <p>Al finalizar se cerrarán todas tus sesiones web. Entrarás con el correo nuevo y la misma contraseña. Los equipos vinculados conservan su acceso.</p>
      <div className="field-group"><label className="field-label" htmlFor="finish-email-password">Contraseña actual para finalizar</label><input id="finish-email-password" name="currentPassword" type="password" className="text-input" autoComplete="current-password" maxLength={128} required disabled={completing} /></div>
      <label className="history-confirmation"><input name="confirmed" type="checkbox" value="yes" required disabled={completing} /><span>Confirmo el correo nuevo y el cierre de mis sesiones web</span></label>
      <button className="button button--primary" disabled={completing || !value.currentAddressConfirmed || !value.newAddressConfirmed}>{completing ? "Finalizando…" : "Finalizar cambio de correo"}</button>
    </form>}
  </>;
}

export function AccountEmailChange({ status, verified }: { status: EmailChangeStatus | null; verified: boolean }) {
  const router = useRouter(); const pending = status?.pending;
  const [cancel, cancelAction, cancelling] = useActionState<EmailChangeState, FormData>(cancelEmailChangeAction, {});
  return <section className="account-security-section" aria-labelledby="email-change-title">
    <h2 id="email-change-title">Cambiar correo electrónico</h2>
    <p>Necesitás acceso a ambas direcciones. Confirmá cada una desde su correo y después volvé aquí para finalizar con tu contraseña. Hasta entonces conservás el correo actual.</p>
    {(!pending || !cancel.success) && <Result state={cancel} />}
    {!verified ? <p>Primero verificá tu correo actual desde la sección anterior.</p> : status === null ? <p role="alert">No pudimos cargar las solicitudes de cambio. Actualizá Mi cuenta antes de continuar.</p> : <>
      {pending && <div>
        <h3>Solicitud pendiente</h3><p className="account-email">{pending.newEmail}</p>
        <p>Vence: {deviceDate(pending.expiresAtUtc)}.</p>
        {pending.expired ? <p role="status">La solicitud venció. Podés cancelarla o generar otra.</p> : <ul>
          <li>Correo actual: {pending.currentAddressConfirmed ? "Confirmado" : "Pendiente de confirmación"}</li>
          <li>Correo nuevo: {pending.newAddressConfirmed ? "Confirmado" : "Pendiente de confirmación"}</li>
        </ul>}
        <p>Revisá ambos correos. Los enlaces solo confirman cada dirección; finalizá el cambio desde esta pantalla.</p>
        <PendingControls key={pending.id} value={pending} cancelAction={cancelAction} cancelling={cancelling} />
      </div>}
      {(!pending || pending.expired) && <RequestForm />}
    </>}
    <button className="button button--secondary" type="button" onClick={() => router.refresh()}>Actualizar confirmaciones</button>
  </section>;
}

export function EmailChangeConfirmation({ token, kind }: { token: string; kind: string }) {
  const [linkToken] = useState(token);
  const [state, action, pending] = useActionState<EmailChangeState, FormData>(confirmEmailChangeAction, {});
  useEffect(() => { if (window.location.search) window.history.replaceState(window.history.state, "", window.location.pathname); }, []);
  if (state.success) return <><Result state={state} /><Link href="/account" className="button button--primary">Volver a Mi cuenta</Link></>;
  return <form aria-label="Confirmar dirección de correo" action={action} className="auth-form" aria-busy={pending}>
    <Result state={state} /><input type="hidden" name="token" value={linkToken} /><input type="hidden" name="kind" value={kind} />
    <p>Esta confirmación corresponde al correo {kind === "current" ? "actual" : "nuevo"}. Revisá la dirección nueva desde Mi cuenta antes de finalizar.</p>
    <label className="history-confirmation"><input name="confirmed" type="checkbox" value="yes" required disabled={pending} /><span>Solicité este cambio y quiero confirmar esta dirección</span></label>
    <button className="button button--primary" disabled={pending || !linkToken || !["current", "new"].includes(kind)}>{pending ? "Confirmando…" : "Confirmar dirección"}</button>
    {!linkToken && <p role="alert">El enlace no está completo. Revisá la solicitud desde Mi cuenta.</p>}
    <Link href="/account">Volver a Mi cuenta</Link>
  </form>;
}
