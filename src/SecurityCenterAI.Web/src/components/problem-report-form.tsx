"use client";

import { useEffect, useState, type FormEvent } from "react";
import { emptyProblemFields, prepareProblemReport, problemScreens, type ProblemFields, type ProblemErrors, type ProblemReport } from "@/lib/problem-report";

export function ProblemReportForm() {
  const [fields, setFields] = useState(emptyProblemFields);
  const [errors, setErrors] = useState<ProblemErrors>({});
  const [report, setReport] = useState<ProblemReport | null>(null);
  const [message, setMessage] = useState(""); const [ready, setReady] = useState(false); const [busy, setBusy] = useState(false);
  useEffect(() => setReady(true), []);
  function edit(key: Exclude<keyof ProblemFields, "reviewed">, value: string) {
    setFields(previous => ({ ...previous, [key]: value, reviewed: false })); setReport(null); setErrors({}); setMessage("");
  }
  function prepare(event: FormEvent) {
    event.preventDefault(); const result = prepareProblemReport(fields);
    if (result.errors) { setErrors(result.errors); setReport(null); return; }
    setErrors({}); setReport(result.report); setMessage("Borrador preparado en este navegador. No se envió ningún reporte.");
  }
  function clear() { setFields(emptyProblemFields()); setReport(null); setErrors({}); setMessage("Borrador y vista previa limpiados."); }
  async function copy() {
    if (!report) return; setBusy(true);
    try { await navigator.clipboard.writeText(JSON.stringify(report, null, 2) + "\n"); setMessage("Reporte copiado. Compartilo solo por un canal que hayas acordado."); }
    catch { setMessage("No pudimos copiarlo. Seleccioná el texto de la vista previa o descargá el JSON."); }
    finally { setBusy(false); }
  }
  function download() {
    if (!report) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2) + "\n"], { type: "application/json;charset=utf-8" }));
    const link = document.createElement("a"); link.href = url; link.download = "SecurityCenterAI-reporte-local.json";
    document.body.appendChild(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    setMessage("Se preparó la descarga local. El reporte no fue enviado.");
  }
  return <div className="problem-report">
    {!ready && <p className="form-alert">Para preparar un reporte local se necesita JavaScript. Podés usar las indicaciones de esta página para escribir tu resumen por separado.</p>}
    <form aria-label="Preparar reporte local" method="post" onSubmit={prepare}>
      <fieldset disabled={busy}><legend>Describí el problema sin datos sensibles</legend>
        <p className="input-help">No pegues contraseñas, códigos, enlaces de verificación o recuperación, credenciales, correos personales ni informes completos del agente.</p>
        <div className="field-group"><label className="field-label" htmlFor="problem-screen">Dónde ocurre</label><select className="text-input" id="problem-screen" value={fields.screen} required aria-invalid={!!errors.screen} aria-describedby={errors.screen ? "problem-screen-error" : undefined} onChange={event => edit("screen", event.target.value)}><option value="">Elegí una sección</option>{problemScreens.map(screen => <option key={screen.value} value={screen.value}>{screen.label}</option>)}</select>{errors.screen && <p id="problem-screen-error" className="field-error">{errors.screen}</p>}</div>
        {([{ key: "summary", label: "Resumen del problema", max: 160 }, { key: "steps", label: "Pasos para reproducirlo", max: 2000 }, { key: "expected", label: "Qué esperabas que ocurriera", max: 1000 }, { key: "actual", label: "Qué ocurrió", max: 1000 }] as const).map(field => <div className="field-group" key={field.key}><label className="field-label" htmlFor={`problem-${field.key}`}>{field.label}</label>
          {field.key === "summary" ? <input id={`problem-${field.key}`} className="text-input" value={fields[field.key]} maxLength={field.max} required autoComplete="off" aria-invalid={!!errors[field.key]} aria-describedby={errors[field.key] ? `problem-${field.key}-error` : undefined} onChange={event => edit(field.key, event.target.value)} /> : <textarea id={`problem-${field.key}`} className="text-input" rows={3} value={fields[field.key]} maxLength={field.max} required autoComplete="off" aria-invalid={!!errors[field.key]} aria-describedby={errors[field.key] ? `problem-${field.key}-error` : undefined} onChange={event => edit(field.key, event.target.value)} />}
          {errors[field.key] && <p id={`problem-${field.key}-error`} className="field-error">{errors[field.key]}</p>}</div>)}
        <label className="history-confirmation"><input type="checkbox" checked={fields.reviewed} onChange={event => { setFields(previous => ({ ...previous, reviewed: event.target.checked })); setErrors(previous => ({ ...previous, reviewed: undefined })); setReport(null); setMessage(""); }} aria-describedby={errors.reviewed ? "problem-reviewed-error" : undefined} /><span>Revisé que este borrador no incluya datos sensibles.</span></label>
        {errors.reviewed && <p id="problem-reviewed-error" className="field-error" role="alert">{errors.reviewed}</p>}
        <div className="tool-actions"><button className="button button--primary" type="submit" disabled={!ready}>Preparar borrador</button><button className="button button--secondary" type="button" onClick={clear}>Limpiar borrador</button></div>
      </fieldset>
    </form>
    {message && <p className="form-alert" role="status">{message}</p>}
    {report && <section className="problem-preview" aria-labelledby="problem-preview-title"><h2 id="problem-preview-title">Revisá el reporte preparado</h2><p>Solo incluye lo que escribiste y la fecha de preparación. No se adjuntan datos de tu cuenta, direcciones de la página, registros o archivos automáticamente.</p><label className="field-label" htmlFor="problem-preview">Contenido del reporte local</label><textarea className="text-input" id="problem-preview" rows={14} value={JSON.stringify(report, null, 2)} readOnly spellCheck={false} /><div className="tool-actions"><button type="button" className="button button--secondary" onClick={copy} disabled={busy}>Copiar reporte</button><button type="button" className="button button--primary" onClick={download} disabled={busy}>Descargar reporte (JSON)</button></div></section>}
  </div>;
}
