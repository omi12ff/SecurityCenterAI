import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchDevice } from "@/lib/device-api";
import { isDeviceId } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { DeviceShell } from "@/components/device-shell";
import { DeviceCard, deviceDate } from "@/components/device-card";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { SessionRenewal } from "@/components/session-renewal";
import { RefreshButton } from "@/components/refresh-button";
import { AnalysisSummaryPanel } from "@/components/analysis-summary";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Detalle del equipo", referrer: "no-referrer" };
const severityLabels: Record<string, string> = { informational: "Información", low: "Baja", medium: "Media", high: "Alta", critical: "Crítica" };
const statusLabels = { passed: "Correcto", attention: "Revisar", unknown: "Desconocido", notApplicable: "No aplica" };
const statusOrder = { attention: 0, unknown: 1, passed: 2, notApplicable: 3 };
export default async function DevicePage({ params }: { params: Promise<{ id: string }> }) {
  const session = await requireSession("/devices");
  const { id } = await params;
  if (!isDeviceId(id)) notFound();
  let result;
  try { result = await Promise.all([fetchProfile(session.token), fetchDevice(id, session.token)]); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    if (error instanceof ApiError && error.status === 404) notFound();
    return <DashboardUnavailable />;
  }
  const [user, detail] = result;
  return <DeviceShell user={user}><SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <Link className="device-back-link" href="/devices">← Volver a dispositivos</Link><div className="page-heading"><div><h1>Detalle del equipo</h1><p className="page-description">Acceso y resultados guardados de este dispositivo.</p></div><RefreshButton label="Actualizar" /></div>
    <DeviceCard device={detail.device} verified={user.emailVerified} detail />
    <section aria-labelledby="device-history-title"><div className="section-heading"><div><h2 id="device-history-title">Análisis del equipo</h2><p>Los últimos 20 resultados guardados, con hasta 100 hallazgos por análisis.</p></div></div>
      {detail.analyses.length ? <div className="device-analysis-list">{detail.analyses.map(analysis => <article id={`analysis-${analysis.id}`} key={analysis.id} className="section-card device-analysis"><h3>Análisis recibido el {deviceDate(analysis.analyzedAtUtc)}</h3><AnalysisSummaryPanel analysis={analysis} />
        {analysis.completedAtUtc && <p className="input-help">Comprobaciones realizadas el {deviceDate(analysis.completedAtUtc)}. La fecha del análisis indica cuándo lo recibió el servidor.</p>}
        {analysis.findings.length ? <div className="analysis-checks"><h4>Resultados de las comprobaciones</h4>{[...analysis.findings].sort((a, b) => (a.status ? statusOrder[a.status] : -1) - (b.status ? statusOrder[b.status] : -1)).map(finding => <details className="finding-card" key={finding.id} open={finding.status === "attention" || finding.status === "unknown" || finding.status === null}><summary className="finding-heading"><span>{finding.title}</span><span className={`status-pill ${finding.status ? `check-${finding.status}` : `severity-${finding.severity}`}`}>{finding.status ? statusLabels[finding.status] : severityLabels[finding.severity]}</span></summary><dl><dt>Regla</dt><dd>{finding.ruleId} · versión {finding.ruleVersion}</dd><dt>Evidencia informada</dt><dd className="finding-evidence">{finding.evidence}</dd>{(finding.status === "attention" || finding.status === null) && <><dt>Severidad</dt><dd>{severityLabels[finding.severity]}</dd></>}<dt>Recomendación</dt><dd>{finding.recommendation}</dd></dl></details>)}</div> : <p className="input-help">Este registro no contiene hallazgos detallados.</p>}
      </article>)}</div> : <div className="section-card empty-state"><h3>Todavía no hay análisis de este equipo</h3><p className="empty-state-copy">Vinculá el agente Windows y enviá un informe para consultar aquí sus puntos confirmados, cobertura y comprobaciones. El envío se inicia desde el agente.</p></div>}
    </section>
  </DeviceShell>;
}
