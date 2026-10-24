import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchDevice } from "@/lib/device-api";
import { fetchAnalysisComparison } from "@/lib/comparison-api";
import { comparisonChanges, type AnalysisComparison, type ComparisonChange } from "@/lib/comparisons";
import { isDeviceId, type DeviceAnalysis, type DeviceFinding } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { DeviceShell } from "@/components/device-shell";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { SessionRenewal } from "@/components/session-renewal";
import { AnalysisSummaryPanel } from "@/components/analysis-summary";
import { deviceDate } from "@/components/device-card";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Comparar análisis", referrer: "no-referrer" };
const changeLabels: Record<ComparisonChange, string> = {
  improved: "Mejoras observadas", needsReview: "Para revisar", newInformation: "Nueva información",
  lostInformation: "Información perdida", unchanged: "Sin cambio de estado", notComparable: "No comparables",
};
const statusLabels = { passed: "Correcto", attention: "Revisar", unknown: "Desconocido", notApplicable: "No aplica" };
const signed = (value: number) => `${value > 0 ? "+" : ""}${value}`;
function Evidence({ findings, label }: { findings: DeviceFinding[]; label: string }) {
  const finding = findings.length === 1 ? findings[0] : undefined;
  return <section className="comparison-evidence"><h4>{label}</h4><p className={`status-pill check-${finding?.status ?? "unknown"}`}>{finding ? finding.status ? statusLabels[finding.status] : "Sin estado compatible" : findings.length > 1 ? "Sin correspondencia única" : "Sin comprobación"}</p>
    {findings.length ? findings.map(item => <div key={item.id}><p className="input-help">Regla versión {item.ruleVersion}</p><p className="finding-evidence">{item.evidence}</p></div>) : <p>No hay una comprobación de esta regla en este análisis.</p>}</section>;
}
function OriginalAnalysis({ analysis, label, deviceId }: { analysis: DeviceAnalysis; label: string; deviceId: string }) {
  return <article className="section-card comparison-original"><h2>{label}</h2><p>Recibido el {deviceDate(analysis.analyzedAtUtc)}</p>
    {analysis.completedAtUtc && <p className="input-help">Comprobado el {deviceDate(analysis.completedAtUtc)}</p>}<AnalysisSummaryPanel analysis={analysis} compact />
    <Link href={`/devices/${deviceId}/analyses/${analysis.id}`}>Entender este análisis →</Link></article>;
}
export default async function CompareAnalysesPage({ params, searchParams }: {
  params: Promise<{ id: string }>; searchParams: Promise<{ baselineId?: string | string[]; targetId?: string | string[] }>;
}) {
  const session = await requireSession("/devices");
  const { id } = await params;
  const query = await searchParams;
  if (!isDeviceId(id) || query.baselineId !== undefined && !isDeviceId(query.baselineId) || query.targetId !== undefined && !isDeviceId(query.targetId)) notFound();
  let result;
  try { result = await Promise.all([fetchProfile(session.token), fetchDevice(id, session.token)]); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    if (error instanceof ApiError && error.status === 404) notFound();
    return <DashboardUnavailable />;
  }
  const [user, detail] = result;
  const baselineId = query.baselineId ?? detail.analyses[1]?.id;
  const targetId = query.targetId ?? detail.analyses[0]?.id;
  let comparison: AnalysisComparison | undefined;
  let selectionError: string | undefined;
  if (baselineId && targetId) {
    try { comparison = await fetchAnalysisComparison(id, baselineId, targetId, session.token); }
    catch (error) {
      if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
      if (error instanceof ApiError && error.status === 404) notFound();
      if (error instanceof ApiError && error.status === 400) selectionError = error.message;
      else return <DashboardUnavailable />;
    }
  }
  const choices = [...detail.analyses];
  for (const analysis of comparison ? [comparison.baseline, comparison.target] : []) {
    if (!choices.some(choice => choice.id === analysis.id)) choices.push(analysis);
  }
  const changeOrder: ComparisonChange[] = ["needsReview", "lostInformation", "improved", "newInformation", "notComparable", "unchanged"];
  return <DeviceShell user={user}><SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <Link className="device-back-link" href={`/devices/${id}`}>← Volver al historial del equipo</Link>
    <div className="page-heading"><div><h1>Comparar análisis</h1><p className="page-description">{detail.device.name} · Cambios entre dos resultados guardados.</p></div></div>
    {choices.length < 2 ? <section className="section-card comparison-empty"><h2>Necesitás dos análisis del mismo equipo</h2><p>Enviá otro informe desde el agente Windows para comparar las comprobaciones. Cada análisis conserva su resultado original.</p></section> : <>
      <section className="section-card comparison-selection" aria-labelledby="comparison-selection-title"><h2 id="comparison-selection-title">Elegí los análisis</h2><p>Las opciones muestran la fecha de recepción del servidor. Ordená los informes por esa fecha y por la de sus comprobaciones: primero el anterior. El selector muestra los últimos 20 análisis del equipo.</p>
        <form action={`/devices/${id}/compare`} method="get" className="comparison-form"><div className="field-group"><label htmlFor="baselineId">Análisis anterior</label><select id="baselineId" name="baselineId" defaultValue={baselineId} className="text-input" required>{choices.map(analysis => <option key={analysis.id} value={analysis.id}>{deviceDate(analysis.analyzedAtUtc)} · {analysis.score ?? "Sin puntaje"}{analysis.score === null ? "" : "/100"} · {analysis.id.slice(0, 8)}</option>)}</select></div>
          <div className="field-group"><label htmlFor="targetId">Análisis reciente</label><select id="targetId" name="targetId" defaultValue={targetId} className="text-input" required>{choices.map(analysis => <option key={analysis.id} value={analysis.id}>{deviceDate(analysis.analyzedAtUtc)} · {analysis.score ?? "Sin puntaje"}{analysis.score === null ? "" : "/100"} · {analysis.id.slice(0, 8)}</option>)}</select></div><button className="button button--primary" type="submit">Comparar</button></form>
        {selectionError && <p role="alert" className="comparison-error">{selectionError}</p>}</section>
      {comparison && <><div className="comparison-originals"><OriginalAnalysis analysis={comparison.baseline} label="Análisis anterior" deviceId={id} /><OriginalAnalysis analysis={comparison.target} label="Análisis reciente" deviceId={id} /></div>
        <section className="section-card comparison-overview" aria-labelledby="comparison-overview-title"><h2 id="comparison-overview-title">Qué cambió</h2>
          <p className="comparison-delta">{comparison.scoreComparable && comparison.scoreDelta !== null ? <>Cambio de puntos confirmados: <strong>{signed(comparison.scoreDelta)} puntos</strong>.</> : "Los puntajes no son comparables. La diferencia requiere puntajes disponibles, el mismo modelo y versiones de reglas compatibles, con el mismo conjunto de criterios aplicables."}</p>
          {comparison.coverageDelta !== null && <p>Cambio de cobertura: <strong>{signed(comparison.coverageDelta)} puntos porcentuales</strong>.</p>}
          <ul className="comparison-counts" aria-label="Resumen de cambios">{comparisonChanges.map(change => <li key={change}><strong>{comparison.summary[change]}</strong>{changeLabels[change]}</li>)}</ul>
          <p className="input-help">La nueva información y la información perdida indican cambios en lo que se pudo observar. No equivalen por sí solas a una mejora o un empeoramiento de la protección. Un estado puede mantenerse aunque cambie la evidencia. Esta comparación no demuestra qué causó un cambio ni certifica la seguridad del equipo.</p></section>
        <section className="comparison-checks" aria-labelledby="comparison-checks-title"><h2 id="comparison-checks-title">Comparación de las comprobaciones</h2>
          {[...comparison.checks].sort((a, b) => changeOrder.indexOf(a.change) - changeOrder.indexOf(b.change)).map(check => <article key={check.ruleId} className="section-card comparison-check"><div className="comparison-check-heading"><h3>{check.title}</h3><span className={`status-pill comparison-change--${check.change}`}>{changeLabels[check.change]}</span></div><p>{check.explanation}</p><p className="input-help">{check.ruleId}{check.ruleVersion === null ? " · Versiones sin comparación directa" : ` · versión ${check.ruleVersion}`}</p>
            <div className="comparison-evidence-grid"><Evidence label="Evidencia anterior" findings={comparison.baseline.findings.filter(finding => check.baselineFindingId ? finding.id === check.baselineFindingId : finding.ruleId === check.ruleId)} /><Evidence label="Evidencia reciente" findings={comparison.target.findings.filter(finding => check.targetFindingId ? finding.id === check.targetFindingId : finding.ruleId === check.ruleId)} /></div></article>)}</section>
      </>}
    </>}
  </DeviceShell>;
}
