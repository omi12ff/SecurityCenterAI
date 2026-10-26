"use client";

import { useActionState } from "react";
import { generateAiExplanationAction } from "@/app/ai-explanation-actions";
import { aiExplanationFailureMessage, type AiExplanationAvailability, type AiExplanationState } from "@/lib/ai-explanation";
import type { DeviceFinding } from "@/lib/devices";

type OriginalCheck = Pick<DeviceFinding, "id" | "ruleId" | "title" | "status">;
const labels = { passed: "Correcto", attention: "Revisar", unknown: "Desconocido", notApplicable: "No aplica" };

export function AiAnalysisExplanation({ deviceId, analysisId, availability, findings }: {
  deviceId: string; analysisId: string; availability: AiExplanationAvailability | null; findings: OriginalCheck[];
}) {
  const [state, action, pending] = useActionState<AiExplanationState, FormData>(generateAiExplanationAction, {});
  const response = state.response;
  const validChecks = response?.status !== "generated" || response.checks.length === findings.length &&
    response.checks.every(check => findings.some(finding => finding.ruleId === check.ruleId));
  const generated = response?.status === "generated" && validChecks;
  return <section className="section-card ai-analysis-explanation" aria-labelledby="ai-explanation-title">
    <div className="panel-heading"><h2 id="ai-explanation-title">Una explicación más detallada con IA</h2><span className="status-pill">Opcional</span></div>
    <p>Podés ampliar la guía con una explicación sobre las mismas comprobaciones. La IA puede equivocarse y no realiza un nuevo análisis ni cambia el puntaje o los estados originales.</p>
    <p id="ai-data-disclosure">Al pulsar el botón para pedir la explicación, se envían al proveedor de IA solo las reglas, sus estados, el puntaje y la cobertura, junto con textos de la guía local. No se envían evidencia libre, nombre, correo ni identificadores de tu cuenta, equipo o análisis.</p>
    <p className="input-help">El resultado se muestra temporalmente en esta página; no se guarda en el historial. Al recargar se descarta.</p>
    {!availability?.available && <p>{availability?.message ?? "No pudimos consultar la disponibilidad de la IA. La guía local sigue disponible; podés actualizar la página más tarde."}</p>}
    {availability?.available && <><p className="input-help">Hasta {availability.dailyLimit} ampliaciones diarias por cuenta.</p><details className="ai-provider-details"><summary>Ver proveedor y modelo</summary><p>{availability.provider && <>Proveedor: {availability.provider}. </>}{availability.model && <>Modelo: {availability.model}.</>}</p></details></>}
    {!generated && <div className="ai-explanation-action"><form action={action} aria-busy={pending} hidden={pending}>
      <input type="hidden" name="deviceId" value={deviceId} />
      <input type="hidden" name="analysisId" value={analysisId} />
      <button className="button button-primary" type="submit" name="requestExplanation" value="true" aria-describedby="ai-data-disclosure" disabled={!availability?.available || pending}>
        {state.message || response ? "Volver a intentar con IA" : "Ampliar explicación con IA"}
      </button>
    </form><div role="status" aria-live="polite" aria-atomic="true">{pending && <span className="ai-thinking-word">Pensando</span>}</div></div>}
    {!pending && state.message && <p role="alert">{state.message}</p>}
    {!pending && response && !generated && <div role="alert"><p><strong>No se generó una explicación con IA.</strong></p><p>{validChecks ? aiExplanationFailureMessage(response.status, response.failureCode) : "La respuesta no coincide con las comprobaciones del análisis. Podés seguir usando la guía local o volver a intentar."}</p><p>La guía local sigue disponible abajo; no es una respuesta generada por IA.</p></div>}
    {generated && response && <div className="ai-explanation-result" role="region" aria-label="Explicación ampliada con IA" aria-live="polite">
      <h3>Explicación ampliada con IA</h3>
      <p className="ai-explanation-text">{response.summary}</p>
      <p className="input-help">{response.notice}</p>
      <ul className="ai-explanation-checks">{response.checks.map(check => {
        const original = findings.find(finding => finding.ruleId === check.ruleId)!;
        return <li key={check.ruleId}><h4><a href={`#check-${original.id}`}>{original.title}</a></h4>
          <p className={`status-pill check-${original.status ?? "unknown"}`}>Estado original: {original.status ? labels[original.status] : "Sin estado compatible"}</p>
          <p className="ai-explanation-text">{check.explanation}</p>
          <a href={`#check-${original.id}`}>Ver comprobación y guía local</a></li>;
      })}</ul>
      <p className="input-help">{response.provider && <>Proveedor: {response.provider}. </>}{response.model && <>Modelo: {response.model}. </>}La valoración al final de la página corresponde a la guía local.</p>
    </div>}
  </section>;
}
