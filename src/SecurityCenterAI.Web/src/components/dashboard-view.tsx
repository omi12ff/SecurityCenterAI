import Link from "next/link";
import { logoutAction } from "@/app/actions";
import type { DashboardData, UserProfile } from "@/lib/contracts";
import { Icon } from "./icons";
import { LogoutButton } from "./logout-button";
import { RefreshButton } from "./refresh-button";
import { AnalysisSummaryPanel } from "./analysis-summary";
import { AnalysisPrioritiesPanel } from "./analysis-priorities";
import type { DashboardPriorities } from "@/lib/analysis-priorities";
import type { DeviceRecord } from "@/lib/devices";
import { InformationLinks } from "./information-links";

const dateFormatter = new Intl.DateTimeFormat("es-PY", {
  dateStyle: "medium",
  timeZone: "America/Asuncion",
});
const timeFormatter = new Intl.DateTimeFormat("es-PY", {
  timeStyle: "short",
  timeZone: "America/Asuncion",
});

function analysisDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? { date: "Fecha no disponible", time: "" }
    : { date: dateFormatter.format(date), time: timeFormatter.format(date) };
}

export function DashboardView({ user, data, devices, priorities }: { user: UserProfile; data: DashboardData; devices: DeviceRecord[] | null; priorities: DashboardPriorities }) {
  const firstName = user.name.trim().split(/\s+/)[0];
  const deviceById = new Map((devices ?? []).map(device => [device.id, device]));
  const initials = user.name.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join("").toLocaleUpperCase("es");
  const today = new Intl.DateTimeFormat("es-PY", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone: "America/Asuncion",
  }).format(new Date());

  return (
    <div className="dashboard-shell">
      <a href="#contenido" className="skip-link">Ir al contenido</a>
      <aside className="sidebar" aria-label="Menú principal">
        <Link href="/dashboard" className="sidebar-brand" aria-label="Security Center AI, inicio">
          <span className="brand-mark"><Icon name="shield" width="25" height="25" /></span>
          <span>Security Center<span className="brand-ai">AI</span></span>
        </Link>

        <p className="nav-section-label">MI ESPACIO</p>
        <nav className="sidebar-nav" aria-label="Secciones de tu espacio">
          <a href="#resumen" className="nav-link nav-link--active"><Icon name="grid" className="nav-icon" /><span>Resumen</span></a>
          <a href="#historial" className="nav-link"><Icon name="history" className="nav-icon" /><span>Historial</span></a>
          <Link href="/getting-started" className="nav-link"><Icon name="info" className="nav-icon" /><span>Primeros pasos</span></Link>
          <Link href="/devices" className="nav-link"><Icon name="monitor" className="nav-icon" /><span>Dispositivos</span></Link>
          <Link href="/tools/scam-check" className="nav-link"><Icon name="mail" className="nav-icon" /><span>Enlaces y mensajes</span></Link>
          <Link href="/account" className="nav-link"><Icon name="user" className="nav-icon" /><span>Mi cuenta</span></Link>
        </nav>


        <div className="sidebar-bottom">
          <div className="sidebar-user">
            <span className="avatar" aria-hidden="true">{initials || user.email.slice(0, 2).toLocaleUpperCase("es")}</span>
            <div className="sidebar-user-meta"><strong>{user.name}</strong><span>{user.email}</span></div>
          </div>
          <form action={logoutAction}><LogoutButton /></form>
        </div>
      </aside>

      <main className="dashboard-main" id="contenido">
        <header className="dashboard-topbar">
          <p className="breadcrumb">Mi espacio <span aria-hidden="true">/</span> <strong>Resumen</strong></p>
          <span className="environment-tag">Versión inicial</span>
        </header>

        <div className="dashboard-content">
          <section id="resumen" aria-labelledby="dashboard-title">
            <div className="page-heading dashboard-page-heading">
              <div>
                <p className="page-greeting">Hola{firstName ? `, ${firstName}` : ""} <span aria-hidden="true">✦</span></p>
                <h1 id="dashboard-title">Tu seguridad, en un solo lugar</h1>
                <p className="page-description">Este es tu espacio para consultar los resultados guardados de tu cuenta.</p>
              </div>
              <div className="dashboard-heading-tools"><p className="today-date">{today}</p><RefreshButton label="Actualizar resumen" /></div>
            </div>

            <div className="stats-grid">
              <article className="metric-card metric-card--score">
                <div>
                  <p className="metric-label">{data.lastAnalysis?.source === "windows-agent" ? "Últimos puntos confirmados" : "Último puntaje guardado"}</p>
                  <p className="metric-value">{data.securityScore ?? "—"}</p>
                  <p className="metric-caption">{data.lastAnalysis?.source === "windows-agent" ? `Cobertura ${data.lastAnalysis.coveragePercent}% · ${data.lastAnalysis.unknownCount} sin determinar` : data.securityScore === null ? "Aún no hay un puntaje disponible." : "Corresponde a tu análisis más reciente."}</p>
                </div>
                <div className="score-visual" aria-hidden="true"><div className="score-orbit"><Icon name="shield" width="32" height="32" /></div></div>
              </article>
              <article className="metric-card">
                <p className="metric-label">Análisis registrados <span className="metric-icon"><Icon name="history" /></span></p>
                <p className="metric-value">{new Intl.NumberFormat("es-PY").format(data.totalAnalyses)}</p>
                <p className="metric-caption">{data.totalAnalyses === 0 ? "Tu historial está listo para recibir resultados." : "Consultá tus resultados más recientes abajo."}</p>
              </article>
            </div>

            {data.lastAnalysis?.source === "windows-agent" && <>
              <AnalysisPrioritiesPanel analysis={data.lastAnalysis} detail={priorities} revoked={deviceById.get(data.lastAnalysis.deviceId ?? "")?.status === "revoked"} />
              <details className="section-card dashboard-analysis"><summary>Ver puntos, cobertura y estados del último informe</summary><AnalysisSummaryPanel analysis={data.lastAnalysis} compact />{data.lastAnalysis.deviceId && <Link className="device-back-link" href={`/devices/${data.lastAnalysis.deviceId}#analysis-${data.lastAnalysis.id}`}>Ver comprobaciones del equipo →</Link>}</details>
            </>}
            {data.lastAnalysis && data.lastAnalysis.source !== "windows-agent" && <p className="form-alert">El último registro es anterior al agente y no incluye estados de comprobación o cobertura. Enviá un informe nuevo para conocer qué revisar.</p>}

            <div className="agent-callout">
              <span className="callout-icon"><Icon name="monitor" width="25" height="25" /></span>
              <div className="callout-content"><h2>{data.totalAnalyses === 0 ? "Prepará tu primer análisis" : "Consultá las comprobaciones de tu equipo"}</h2><p>{data.totalAnalyses === 0 ? "Seguí la guía para verificar tu correo, descargar el agente y vincular tu equipo. Vos decidís cuándo enviar el informe." : "Vinculá el agente Windows y enviá un informe para ver sus puntos confirmados, cobertura y recomendaciones. Cada envío requiere una acción desde el agente."}</p></div>
              <Link href="/getting-started" className="button button--secondary">{data.totalAnalyses === 0 ? "Empezar con mi equipo" : "Guía del agente"}</Link>
            </div>
          </section>

          <section className="history-section" id="historial" aria-labelledby="history-title">
            <div className="section-heading"><div><h2 id="history-title">Historial de análisis</h2><p>Los cinco resultados guardados más recientes de tu cuenta.</p></div><span className="status-pill">{data.recentAnalyses.length} {data.recentAnalyses.length === 1 ? "resultado" : "resultados"}</span></div>
            <div className="section-card">
              {data.recentAnalyses.length > 0 ? (
                <div className="table-wrap" tabIndex={0} role="region" aria-label="Resultados recientes, tabla desplazable">
                  <table className="history-table">
                    <caption className="sr-only">Últimos análisis registrados, del más reciente al más antiguo.</caption>
                    <thead><tr><th scope="col">Análisis y equipo</th><th scope="col">Fecha y hora</th><th scope="col">Puntos guardados</th><th scope="col">Cobertura y estados</th></tr></thead>
                    <tbody>{data.recentAnalyses.map((analysis) => {
                      const formatted = analysisDate(analysis.analyzedAtUtc);
                      const device = deviceById.get(analysis.deviceId ?? "");
                      return <tr key={analysis.id}><td><span className="history-id"><Icon name="shield" /><span>{analysis.deviceId ? <Link href={`/devices/${analysis.deviceId}/analyses/${analysis.id}`} aria-label={`Abrir análisis de ${device?.name ?? "equipo registrado"} del ${formatted.date}, ${formatted.time}`}>{device?.name ?? "Equipo registrado"}</Link> : "Registro anterior sin equipo"}</span></span><span className="table-time">{analysis.source === "windows-agent" ? "Agente Windows" : "Registro anterior"}{device?.status === "revoked" ? " · acceso revocado" : ""}</span></td><td><time dateTime={analysis.analyzedAtUtc}>{formatted.date}<span className="table-time">{formatted.time}</span></time></td><td><span className="score-chip">{analysis.score ?? "Sin puntaje"}</span></td><td>{analysis.source === "windows-agent" ? <><strong>{analysis.coveragePercent}%</strong><span className="table-time">{analysis.attentionCount} para revisar · {analysis.unknownCount} sin determinar</span>{analysis.unknownCount !== 0 && <span className="table-time">Resultado parcial</span>}</> : <span>Sin estados ni cobertura</span>}</td></tr>;
                    })}</tbody>
                  </table>
                </div>
              ) : (
                <div className="empty-state">
                  <div className="empty-state-icon"><Icon name="history" width="30" height="30" /></div>
                  <h3>Todavía no hay análisis registrados</h3>
                  <p className="empty-state-copy">Cuando existan resultados en tu cuenta, podrás consultar aquí su fecha y puntaje. Aún no tenemos información para evaluar el estado de tu equipo.</p>
                  <span className="status-pill">Sin resultados</span>
                </div>
              )}
            </div>
            {data.recentAnalyses.length > 0 && <p className="history-scope">Mostrando {data.recentAnalyses.length} de {data.totalAnalyses} análisis de tu cuenta. <Link href="/devices">Ver historial por equipo →</Link></p>}
            {devices === null && data.recentAnalyses.some(analysis => analysis.deviceId) && <p className="input-help" role="status">No pudimos cargar los nombres y accesos de tus equipos. Actualizá la página para consultarlos.</p>}
          </section>

          <section className="account-section" id="mi-cuenta" aria-labelledby="account-title">
            <div className="section-heading"><div><h2 id="account-title">Mi cuenta</h2><p>La información con la que accedés a tu espacio.</p></div><Icon name="user" /></div>
            <div className="section-card">
              <dl className="account-grid"><div className="account-field"><dt className="account-label">Nombre completo</dt><dd className="account-value">{user.name}</dd></div><div className="account-field"><dt className="account-label">Correo electrónico</dt><dd className="account-value">{user.email}</dd></div></dl>
              <p className="account-readonly"><Icon name="info" width="16" height="16" /><span><Link href="/account">Administrar la seguridad de mi cuenta</Link> para verificar tu correo y controlar tus sesiones.</span></p>
            </div>
          </section>

          <footer className="dashboard-footer"><span>Security Center AI</span><InformationLinks /><p>Información clara. Decisiones con evidencia.</p></footer>
        </div>
      </main>
    </div>
  );
}

export function DashboardUnavailable() {
  return (
    <main className="unavailable-layout">
      <section className="unavailable-card" aria-labelledby="unavailable-title">
        <div className="unavailable-icon"><Icon name="warning" width="30" height="30" /></div>
        <p className="eyebrow">SECURITY CENTER AI</p>
        <h1 id="unavailable-title">No pudimos cargar tu espacio</h1>
        <p>El servicio no está disponible en este momento. Volvé a intentar en unos instantes para consultar tu cuenta y sus resultados.</p>
        <div className="unavailable-actions"><RefreshButton /><form action={logoutAction}><LogoutButton secondary /></form></div>
      </section>
    </main>
  );
}
