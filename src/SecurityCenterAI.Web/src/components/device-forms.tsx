"use client";

import Link from "next/link";
import { useActionState, useEffect, useId, useState } from "react";
import { addDeviceAction, regenerateDeviceCodeAction, revokeDeviceAction } from "@/app/device-actions";
import type { DeviceFormState, DevicePairing, DeviceRecord } from "@/lib/devices";

function Notice({ state }: { state: DeviceFormState }) {
  return state.message ? <p className={`form-alert ${state.success ? "form-alert--success" : "form-alert--error"}`} role={state.success ? "status" : "alert"}>{state.message}</p> : null;
}

function PairingCode({ pairing }: { pairing: DevicePairing }) {
  const id = useId();
  const [expired, setExpired] = useState(false);
  const [copyMessage, setCopyMessage] = useState("");
  useEffect(() => {
    const timer = setTimeout(() => setExpired(true), Math.max(0, Date.parse(pairing.expiresAtUtc) - Date.now()));
    return () => clearTimeout(timer);
  }, [pairing.expiresAtUtc]);
  if (expired) return <p className="form-alert" role="status">El código venció. Generá uno nuevo desde el equipo.</p>;
  const expiration = new Intl.DateTimeFormat("es-PY", { hour: "2-digit", minute: "2-digit", timeZone: "America/Asuncion" }).format(new Date(pairing.expiresAtUtc));
  async function copy() {
    try { await navigator.clipboard.writeText(pairing.pairingCode); setCopyMessage("Código copiado."); }
    catch { setCopyMessage("Seleccioná y copiá el código del campo."); }
  }
  return <div className="pairing-panel">
    <label className="field-label" htmlFor={id}>Código de vinculación</label>
    <input className="text-input pairing-code" id={id} value={pairing.pairingCode} readOnly autoComplete="off" spellCheck={false} onFocus={event => event.currentTarget.select()} />
    <div className="pairing-footer"><p>Un solo uso · vence a las {expiration}</p><button className="button button--secondary" type="button" onClick={copy}>Copiar código</button></div>
    {copyMessage && <p className="input-help" role="status">{copyMessage}</p>}
    <p className="input-help">El código se muestra solo al generarlo. Ingresalo en el agente Windows para vincular este equipo. Luego podrás enviar sus comprobaciones desde el agente.</p>
  </div>;
}

export function AddDeviceForm({ verified, activeDeviceId }: { verified: boolean; activeDeviceId?: string }) {
  const [state, action, pending] = useActionState<DeviceFormState, FormData>(addDeviceAction, {});
  return <div>
    {state.pairing && state.pairing.device.id === activeDeviceId && <PairingCode key={state.pairing.pairingCode} pairing={state.pairing} />}
    {!verified ? <p className="form-alert">Verificá tu correo para agregar un equipo. <Link href="/account">Ir a Mi cuenta</Link></p>
      : activeDeviceId ? <p className="device-limit-note">Tu cuenta admite un equipo activo. Podés generar un nuevo código o revocar su acceso abajo.</p>
      : <form className="device-create-form" action={action} aria-busy={pending}>
        <Notice state={state} />
        <div className="device-create-fields"><div className="field-group"><label className="field-label" htmlFor="device-name">Nombre del equipo</label><input id="device-name" className="text-input" name="name" required minLength={2} maxLength={100} autoComplete="off" placeholder="Por ejemplo, Mi notebook" defaultValue={state.name} disabled={pending} /></div>
          <button className="button button--primary" type="submit" disabled={pending}>{pending ? "Agregando…" : "Agregar equipo"}</button></div>
      </form>}
  </div>;
}

export function DeviceControls({ device, verified }: { device: DeviceRecord; verified: boolean }) {
  const [codeState, codeAction, codePending] = useActionState<DeviceFormState, FormData>(regenerateDeviceCodeAction, {});
  const [revokeState, revokeAction, revokePending] = useActionState<DeviceFormState, FormData>(revokeDeviceAction, {});
  const [confirming, setConfirming] = useState(false);
  return <div className="device-controls">
    <Notice state={codeState} /><Notice state={revokeState} />
    {device.status !== "revoked" && <>
      {codeState.pairing && <PairingCode key={codeState.pairing.pairingCode} pairing={codeState.pairing} />}
      <div className="device-control-buttons"><form action={codeAction}><input type="hidden" name="deviceId" value={device.id} /><button className="button button--secondary" type="submit" disabled={!verified || codePending || revokePending}>{codePending ? "Generando…" : "Generar nuevo código"}</button></form>
        {!confirming && <button className="button button--danger" type="button" onClick={() => setConfirming(true)} disabled={revokePending}>Revocar acceso</button>}</div>
      {confirming && <form action={revokeAction} className="revoke-confirmation" aria-label="Confirmar revocación">
        <input type="hidden" name="deviceId" value={device.id} /><input type="hidden" name="confirmed" value="yes" />
        <p>El equipo dejará de tener acceso. Sus códigos y credenciales quedarán invalidados y el historial se conservará.</p>
        <div className="device-control-buttons"><button className="button button--danger" type="submit" disabled={revokePending}>{revokePending ? "Revocando…" : "Confirmar revocación"}</button><button className="button button--secondary" type="button" onClick={() => setConfirming(false)} disabled={revokePending}>Cancelar</button></div>
      </form>}
    </>}
  </div>;
}
