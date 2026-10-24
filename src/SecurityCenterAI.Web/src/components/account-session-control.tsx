"use client";

import { useActionState, useId } from "react";
import { revokeAccountSessionAction } from "@/app/account-session-actions";

export function AccountSessionControl({ sessionId, current }: { sessionId: string; current: boolean }) {
  const [state, action, pending] = useActionState(revokeAccountSessionAction, {});
  const errorId = useId();
  return <form action={action} className="account-session-control">
    <input type="hidden" name="sessionId" value={sessionId} />
    <label className="history-confirmation"><input type="checkbox" name="confirmed" value="yes" required disabled={pending}
      aria-describedby={state.message ? errorId : undefined} /><span>{current
        ? "Confirmo que quiero salir de esta sesión actual" : "Confirmo que quiero cerrar esta sesión"}</span></label>
    <button className="button button--danger" type="submit" disabled={pending}>{pending ? "Cerrando…"
      : current ? "Cerrar mi sesión actual" : "Cerrar esta sesión"}</button>
    {state.message && <p id={errorId} className="history-error" role="alert">{state.message}</p>}
  </form>;
}
