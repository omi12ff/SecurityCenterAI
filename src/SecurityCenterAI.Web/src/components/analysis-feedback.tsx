"use client";

import { useActionState } from "react";
import { saveFeedbackAction } from "@/app/feedback-actions";
import type { Feedback } from "@/lib/guidance";

export function AnalysisFeedbackForm({ deviceId, analysisId, feedback }: { deviceId: string; analysisId: string; feedback: Feedback | null }) {
  const [state, action, pending] = useActionState(saveFeedbackAction, {});
  return <section className="section-card guidance-feedback" aria-labelledby="feedback-title"><h2 id="feedback-title">¿Te ayudó esta guía?</h2>
    <p>Tu valoración es privada y se guarda en este análisis. No incluyas contraseñas, claves de recuperación ni otros secretos.</p>
    <form action={action}><input type="hidden" name="deviceId" value={deviceId} /><input type="hidden" name="analysisId" value={analysisId} />
      <fieldset disabled={pending}><legend>Elegí una valoración</legend><div className="feedback-options">
        {([["helpful", "Me ayudó"], ["unclear", "No quedó clara"], ["incorrect", "Encontré un error"]] as const).map(([value, label]) =>
          <label key={value}><input type="radio" name="rating" value={value} required defaultChecked={feedback?.rating === value} /> {label}</label>)}
      </div><label htmlFor="feedback-comment">Comentario opcional</label><textarea id="feedback-comment" name="comment" rows={4} maxLength={1000} defaultValue={feedback?.comment ?? ""} aria-describedby="feedback-limit" />
      <p id="feedback-limit" className="input-help">Hasta 1000 caracteres. Un nuevo envío actualiza tu valoración anterior.</p></fieldset>
      <button className="button button-primary" type="submit" disabled={pending}>{pending ? "Guardando…" : "Guardar valoración"}</button>
      {state.message && <p role={state.success ? "status" : "alert"}>{state.message}</p>}
    </form></section>;
}
