import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchExplanation } from "@/lib/guidance-api";
import { fetchAiExplanationAvailability } from "@/lib/ai-explanation-api";
import { isDeviceId } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { DeviceShell } from "@/components/device-shell";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { SessionRenewal } from "@/components/session-renewal";
import { AnalysisSummaryPanel } from "@/components/analysis-summary";
import { AnalysisFeedbackForm } from "@/components/analysis-feedback";
import { AiAnalysisExplanation } from "@/components/ai-analysis-explanation";
import { AnalysisHistoryControls } from "@/components/analysis-history-controls";
import { deviceDate } from "@/components/device-card";
import { GuidanceDetails } from "@/components/guidance-details";
import { Icon } from "@/components/icons";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Entender el análisis", referrer: "no-referrer" };
const labels = { passed: "Correcto", attention: "Revisar", unknown: "Desconocido", notApplicable: "No aplica" };
const order = { attention: 0, unknown: 1, passed: 2, notApplicable: 3 };
export default async function AnalysisGuidePage({ params }: { params: Promise<{ id: string; analysisId: string }> }) {
  const session = await requireSession("/devices");
  const { id, analysisId } = await params;
  if (!isDeviceId(id) || !isDeviceId(analysisId)) notFound();
  let result;
  try { result = await Promise.all([fetchProfile(session.token), fetchExplanation(id, analysisId, session.token),
    fetchAiExplanationAvailability(id, analysisId, session.token).catch(error => {
      if (error instanceof ApiError && error.status === 401) throw error;
      return null;
    })]); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    if (error instanceof ApiError && error.status === 404) notFound();
    return <DashboardUnavailable />;
  }
  const [user, guide, aiAvailability] = result;
  return <DeviceShell user={user}><SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <Link className="device-back-link" href={`/devices/${id}#analysis-${analysisId}`}>← Volver al análisis del equipo</Link>
    <div className="page-heading"><div><h1>Entender este análisis</h1><p>{guide.deviceName} · Recibido el {deviceDate(guide.analysis.analyzedAtUtc)}</p></div></div>
    <section className="section-card guidance-intro"><div className="panel-heading"><h2>Resumen del informe</h2><span className="status-pill">Guía local · versión {guide.explanationVersion}</span></div><AnalysisSummaryPanel analysis={guide.analysis} /><p>{guide.summary}</p>
      <h2>Por dónde empezar</h2><ul>{guide.nextSteps.map(step => <li key={step}>{step}</li>)}</ul><p className="input-help">Esta guía se prepara con plantillas locales. No se envía el informe a un servicio de IA para elaborar la guía local. La ampliación opcional se solicita con su botón y envía solo los datos resumidos indicados abajo.</p></section>
    <AiAnalysisExplanation deviceId={id} analysisId={analysisId} availability={aiAvailability}
      findings={guide.analysis.findings.map(({ id, ruleId, title, status }) => ({ id, ruleId, title, status }))} />
    <section className="guidance-checks" aria-labelledby="guide-checks-title"><div className="guidance-list-heading"><h2 id="guide-checks-title">Qué significa cada comprobación</h2><p>Las que requieren revisión se muestran abiertas. Elegí cualquier otra para ver su evidencia y los pasos sugeridos.</p></div>
      {[...guide.analysis.findings].sort((a, b) => (a.status ? order[a.status] : -1) - (b.status ? order[b.status] : -1)).map(finding => {
        const explanation = guide.checks.find(c => c.findingId === finding.id)!;
        return <article id={`check-${finding.id}`} key={finding.id} className="section-card guidance-check"><GuidanceDetails anchorId={`check-${finding.id}`} initiallyOpen={finding.status === "attention"} summary={<><Icon name="arrow-right" /><h3>{finding.title}</h3><span className={`status-pill check-${finding.status ?? "unknown"}`}>{finding.status ? labels[finding.status] : "Sin estado compatible"}</span></>}><p>{explanation.meaning}</p>
          <dl><dt>Qué se comprobó</dt><dd>{explanation.whatWasChecked}</dd><dt>Por qué importa</dt><dd>{explanation.whyItMatters}</dd><dt>Evidencia del agente</dt><dd className="finding-evidence">{finding.evidence}</dd><dt>Qué podés revisar</dt><dd>{explanation.suggestedAction}</dd><dt>Alcance y límites</dt><dd>{explanation.verificationLimit}</dd></dl>
          <p className="input-help">{finding.ruleId} · versión {finding.ruleVersion}</p>{explanation.references.map(reference => <p key={reference.url}><a href={reference.url} target="_blank" rel="noopener noreferrer">{reference.title} ↗</a></p>)}</GuidanceDetails></article>;
      })}
    </section><p className="input-help">La siguiente valoración corresponde a la guía local de este análisis.</p><AnalysisFeedbackForm deviceId={id} analysisId={analysisId} feedback={guide.feedback} />
    <AnalysisHistoryControls deviceId={id} analysisId={analysisId} />
  </DeviceShell>;
}
