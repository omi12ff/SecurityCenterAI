import type { AnalysisSummary } from "@/lib/analyses";

export function AnalysisSummaryPanel({ analysis, compact = false }: { analysis: AnalysisSummary; compact?: boolean }) {
  if (analysis.source !== "windows-agent") return <p className="input-help">Registro anterior: puntaje guardado {analysis.score ?? "sin dato"}, sin cobertura ni estados de comprobación informados.</p>;
  const label = analysis.score === null ? "Sin puntaje verificable" : analysis.unknownCount ? "Resultado parcial" : "Criterios aplicables verificados";
  return <div className={`analysis-summary${compact ? " analysis-summary--compact" : ""}`}>
    <div className="analysis-summary-heading"><div><p className="metric-label">Puntos confirmados</p><p className="analysis-points">{analysis.score === null ? "—" : <>{analysis.score}<span>/100</span></>}</p></div><span className={`status-pill${analysis.unknownCount || analysis.score === null ? " analysis-partial" : ""}`}>{label}</span></div>
    <div className="analysis-coverage"><p>Cobertura de comprobaciones: <strong>{analysis.coveragePercent}%</strong></p><progress max={100} value={analysis.coveragePercent ?? 0} aria-label="Cobertura de comprobaciones" /></div>
    <ul className="analysis-counts" aria-label="Estados de las comprobaciones"><li><strong>{analysis.passedCount}</strong> correctas</li><li><strong>{analysis.attentionCount}</strong> para revisar</li><li><strong>{analysis.unknownCount}</strong> desconocidas</li><li><strong>{analysis.notApplicableCount}</strong> no aplicables</li></ul>
    <p className="input-help">Informado por agente Windows · versión {analysis.agentVersion}. Son diez criterios limitados y autodeclarados por el cliente.</p>
    {!compact && <details className="score-explanation"><summary>Cómo se calculan estos puntos</summary><p>Solo los criterios correctos suman sus puntos asignados. Las comprobaciones desconocidas no suman puntos y requieren verificación; las no aplicables se excluyen del total. La cobertura indica cuántos criterios aplicables pudieron comprobarse. Un resultado parcial no evalúa todo el equipo.</p><p>Modelo de puntos versión {analysis.scoringVersion}. El servidor fija las reglas, ponderaciones y recomendaciones.</p></details>}
  </div>;
}
