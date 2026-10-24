import Link from "next/link";
import type { AnalysisSummary } from "@/lib/analyses";
import type { DashboardPriorities } from "@/lib/analysis-priorities";
import { deviceDate } from "./device-card";
import { RefreshButton } from "./refresh-button";

const severities: Record<string, string> = { critical: "Crítica", high: "Alta", medium: "Media", low: "Baja", informational: "Informativa" };

export function AnalysisPrioritiesPanel({ analysis, detail, revoked }: { analysis: AnalysisSummary; detail: DashboardPriorities; revoked: boolean }) {
  const guidePath = analysis.deviceId ? `/devices/${analysis.deviceId}/analyses/${analysis.id}` : null;
  return <section className="section-card priorities-panel" aria-labelledby="priorities-title">
    <div className="section-heading"><div><h2 id="priorities-title">Por dónde empezar</h2><p>Prioridades del último informe guardado{detail?.kind === "ready" ? ` · ${detail.deviceName}` : ""}.</p></div></div>
    <p className="input-help">Recibido el <time dateTime={analysis.analyzedAtUtc}>{deviceDate(analysis.analyzedAtUtc)}</time>. Describe las observaciones de ese momento.</p>
    {revoked && <p className="form-alert">Este informe pertenece a un equipo con acceso revocado. Sus resultados se conservan como historial.</p>}
    {detail?.kind !== "ready" ? <div><p>No pudimos cargar las comprobaciones de este informe. El resumen sigue disponible; actualizá antes de decidir qué revisar.</p><RefreshButton label="Actualizar comprobaciones" /></div> : <>
      <div className="priority-groups">
        <section className="priority-group priority-group--attention" aria-labelledby="priority-attention-title"><h3 id="priority-attention-title">Para revisar <span className="status-pill">{detail.priorities.attentionTotal}</span></h3>
          <p>Contrastá estas observaciones con el estado actual de Windows. Se ordenan por la severidad de la regla.</p>
          {detail.priorities.attention.length ? <ul>{detail.priorities.attention.map(finding => <li key={finding.id}><Link href={`${guidePath}#check-${finding.id}`}>{finding.title}</Link><span className="priority-severity">Severidad de la regla: {severities[finding.severity] ?? "Sin clasificar"}</span></li>)}</ul> : <p className="priority-empty">No se informaron comprobaciones para revisar.</p>}
          {detail.priorities.attentionTotal > 3 && <p className="input-help">Se muestran 3 de {detail.priorities.attentionTotal}. Abrí el análisis para verlas todas.</p>}
        </section>
        <section className="priority-group priority-group--unknown" aria-labelledby="priority-unknown-title"><h3 id="priority-unknown-title">Sin determinar <span className="status-pill">{detail.priorities.unknownTotal}</span></h3>
          <p>No se pudo conocer su estado. Consultalo manualmente antes de decidir si hace falta un cambio.</p>
          {detail.priorities.unknown.length ? <ul>{detail.priorities.unknown.map(finding => <li key={finding.id}><Link href={`${guidePath}#check-${finding.id}`}>{finding.title}</Link></li>)}</ul> : <p className="priority-empty">No se informaron estados desconocidos.</p>}
          {detail.priorities.unknownTotal > 3 && <p className="input-help">Se muestran 3 de {detail.priorities.unknownTotal}. Abrí el análisis para verlas todas.</p>}
        </section>
      </div>
      {detail.priorities.applicableCount === 0 ? <p>Este informe no contiene criterios aplicables. No permite evaluar el estado del equipo.</p>
        : detail.priorities.attentionTotal === 0 && detail.priorities.unknownTotal === 0 && <p>Las comprobaciones aplicables se informaron correctas. El alcance sigue limitado a los diez criterios del agente.</p>}
      {guidePath && <Link className="button button--secondary" href={guidePath}>Abrir todas las comprobaciones y sus guías</Link>}
    </>}
  </section>;
}
