import type { Metadata } from "next";
import Link from "next/link";
import { AccountShell } from "@/components/account-shell";
import { SessionRenewal } from "@/components/session-renewal";
import { deviceDate } from "@/components/device-card";
import { ApiError } from "@/lib/api";
import { activityHref, activityLabels, parseActivityQuery, type ActivityQuery } from "@/lib/security-activity";
import { fetchSecurityActivity } from "@/lib/security-activity-api";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const metadata: Metadata = { title: "Actividad de seguridad", referrer: "no-referrer" };
export const dynamic = "force-dynamic";

function ActivityFilters({ query }: { query: ActivityQuery }) {
  return <form key={activityHref(query)} action="/account/activity" method="get" aria-label="Filtrar actividad de seguridad" aria-describedby="activity-date-help" className="activity-filter-form">
    <div className="activity-filter-field"><label htmlFor="activity-action">Acción</label><select id="activity-action" name="action" defaultValue={query.action} className="text-input">
      <option value="">Todas las acciones</option>
      {Object.entries(activityLabels).map(([code, label]) => <option key={code} value={code}>{label}</option>)}
    </select></div>
    <div className="activity-filter-field"><label htmlFor="activity-from">Desde (UTC)</label><input id="activity-from" name="from" type="date" defaultValue={query.from} className="text-input" /></div>
    <div className="activity-filter-field"><label htmlFor="activity-to">Hasta (UTC)</label><input id="activity-to" name="to" type="date" defaultValue={query.to} max="9999-12-30" className="text-input" /></div>
    <div className="activity-filter-actions"><button className="button button--primary">Aplicar filtros</button>
      <Link href="/account/activity" className="button button--secondary">Limpiar filtros</Link></div>
  </form>;
}

export default async function SecurityActivityPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const session = await requireSession("/account/activity");
  const query = parseActivityQuery(await searchParams);
  if (!query) return <AccountShell title="Actividad de seguridad" description="Consultá los cambios de acceso de tu cuenta.">
    <p role="alert">Los filtros no son válidos. Revisá la acción, las fechas UTC y el enlace de continuación.</p>
    <Link href="/account/activity" className="button button--secondary">Volver al historial</Link>
  </AccountShell>;
  let activity;
  try { activity = await fetchSecurityActivity(session.token, query); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account/activity");
    return <AccountShell title="Actividad de seguridad" description="Consultá los cambios de acceso de tu cuenta.">
      <p role="alert">{error instanceof ApiError && error.status === 400 ? "El enlace de continuación no está disponible para estos filtros. Volvé a los eventos más recientes." : "No pudimos cargar tu actividad. El estado de tu cuenta no cambió."}</p>
      <Link href={activityHref({ ...query, before: "" })} className="button button--secondary">Volver a intentar</Link>
      <p><Link href="/account">Volver a Mi cuenta</Link></p>
    </AccountShell>;
  }
  return <AccountShell title="Actividad de seguridad" description="Cambios de acceso y acciones registradas en tu cuenta.">
    <SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <section className="account-security-section" aria-labelledby="security-activity-title">
      <h2 id="security-activity-title">Eventos registrados</h2>
      <p>Se muestran hasta 50 eventos por consulta, del más reciente al más antiguo. Podés seguir hacia los eventos anteriores.</p>
      <p id="activity-date-help">Los filtros incluyen días completos en UTC; las horas de cada evento se muestran en America/Asuncion. El registro no reconstruye acciones pasadas.</p>
      <ActivityFilters query={query} />
      <p><Link href={activityHref({ ...query, before: "" })}>Actualizar resultados</Link></p>
      {activity.events.length === 0 ? <p>{query.action || query.from || query.to || query.before ? "No hay eventos que coincidan con esta consulta." : "Todavía no hay actividad registrada."}</p> :
        <ol className="security-activity-list">{activity.events.map(event => <li key={event.id}>
          <p><strong>{activityLabels[event.action]}</strong></p>
          <time dateTime={event.occurredAtUtc}>{deviceDate(event.occurredAtUtc)}</time>
        </li>)}</ol>}
      {activity.hasMore && <p>Hay eventos anteriores.</p>}
      <nav className="account-session-pagination" aria-label="Navegación de actividad">
        {query.before && <Link href={activityHref({ ...query, before: "" })} className="button button--secondary">Ver más recientes</Link>}
        {activity.nextBefore && <Link href={activityHref({ ...query, before: activity.nextBefore })} className="button button--secondary">Ver eventos anteriores</Link>}
      </nav>
    </section>
    <section className="account-security-section" aria-labelledby="activity-scope-title">
      <h2 id="activity-scope-title">Alcance del historial</h2>
      <p>Incluye creación de cuenta, accesos completados, cierre de sesiones, reutilización de renovación bloqueada, verificación o cambio de correo, recuperación o cambio de contraseña, cambios de dispositivos y borrado de análisis.</p>
      <p>No registra intentos de acceso fallidos ni todas las solicitudes. La reutilización de renovación también puede ocurrir por solicitudes simultáneas; este historial por sí solo no confirma una intrusión.</p>
      <p>Las entradas no incluyen contraseñas, tokens, direcciones IP ni contenido de los análisis. Se eliminan al borrar tu cuenta.</p>
      <p>La exportación incluye toda tu cuenta y actividad; no aplica estos filtros.</p>
      <a href="/account/export" download className="button button--secondary">Exportar mi cuenta y actividad (JSON)</a>
    </section>
    <p><Link href="/account">← Volver a Mi cuenta</Link></p>
  </AccountShell>;
}
