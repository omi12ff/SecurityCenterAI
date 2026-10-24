"use client";

import { useActionState } from "react";
import { deleteAnalysisAction } from "@/app/analysis-history-actions";

export function AnalysisHistoryControls({ deviceId, analysisId }: { deviceId: string; analysisId: string }) {
  const [state, action, pending] = useActionState(deleteAnalysisAction, {});
  return <section className="section-card history-controls" aria-labelledby="history-controls-title">
    <h2 id="history-controls-title">Control de este análisis</h2>
    <p>Podés descargar una copia con los puntos, la cobertura, las evidencias y tu valoración privada. El archivo puede contener información de tu equipo y tu comentario; compartilo con cuidado.</p>
    <a className="button button--secondary history-export" href={`/devices/${deviceId}/analyses/${analysisId}/export`} download>Exportar análisis (JSON)</a>
    <div className="history-deletion"><h3>Eliminar este resultado</h3>
      <p>La eliminación es permanente: borra este resultado, sus evidencias y su valoración de la aplicación. El equipo conserva su acceso y sus otros análisis.</p>
      <p>Las copias descargadas y los archivos del agente en tu PC siguen disponibles. Se conserva una marca mínima del identificador del informe y su fecha de eliminación para impedir que el mismo informe vuelva a aparecer. Para enviar otro resultado, ejecutá un nuevo <code>scan</code>.</p>
      <form action={action}><input type="hidden" name="deviceId" value={deviceId} /><input type="hidden" name="analysisId" value={analysisId} />
        <label className="history-confirmation"><input type="checkbox" name="confirmed" value="yes" required disabled={pending} /> <span>Confirmo que quiero eliminar este análisis</span></label>
        <button className="button button--danger" type="submit" disabled={pending}>{pending ? "Eliminando…" : "Eliminar análisis"}</button>
        {state.message && <p className="history-error" role="alert">{state.message}</p>}
      </form>
    </div>
  </section>;
}
