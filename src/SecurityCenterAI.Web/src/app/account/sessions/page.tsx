import type { Metadata } from "next";
import Link from "next/link";
import { AccountShell } from "@/components/account-shell";
import { SessionRenewal } from "@/components/session-renewal";
import { AccountSessionControl } from "@/components/account-session-control";
import { AccountActionForm } from "@/components/account-forms";
import { deviceDate } from "@/components/device-card";
import { ApiError } from "@/lib/api";
import { fetchAccountSessions } from "@/lib/account-sessions-api";
import { MAX_SESSION_PAGE, parseSessionPage } from "@/lib/account-sessions";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const metadata: Metadata = { title: "Sesiones activas", referrer: "no-referrer" };
export const dynamic = "force-dynamic";

export default async function AccountSessionsPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const session = await requireSession("/account/sessions");
  const page = parseSessionPage((await searchParams).page);
  let result;
  try { result = await fetchAccountSessions(session.token, page); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account/sessions");
    return <AccountShell title="Sesiones activas" description="Revisá los accesos web a tu cuenta.">
      <p role="alert">No pudimos cargar las sesiones. Volvé a intentar en unos momentos.</p>
      <Link href="/account/sessions" className="button button--secondary">Volver a intentar</Link>
      <p><Link href="/account">Volver a Mi cuenta</Link></p>
    </AccountShell>;
  }
  return <AccountShell title="Sesiones activas" description="Revisá y cerrá los accesos web de tu cuenta.">
    <SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <p>Una sesión se crea cada vez que iniciás sesión. Puede seguir habilitada aunque el navegador esté cerrado; esta lista no indica quién está conectado ahora.</p>
    <p>La sesión actual aparece primero en la página 1. Las demás se ordenan por creación, hasta 50 por página.</p>
    <p>Tu cuenta admite hasta {result.maxActiveSessions} sesiones web habilitadas. Al alcanzar el límite, cerrá una para abrir otra. Las sesiones vencidas no ocupan un lugar.</p>
    <p>No guardamos IP, ubicación o nombre de navegador para este listado. El acceso del agente se administra desde <Link href="/devices">Dispositivos</Link>.</p>
    <p><Link href="/account/sessions" className="button button--secondary">Actualizar sesiones</Link></p>
    {result.sessions.length === 0 ? <p>No hay sesiones activas en esta página.</p> :
      <ol className="account-session-list">{result.sessions.map((item, index) => <li key={item.id}>
        <article className="account-session-card" aria-labelledby={`session-title-${index}`}>
          <h2 id={`session-title-${index}`}>{item.isCurrent ? "Sesión actual" : `Otra sesión ${(page - 1) * 50 + index + 1}`}</h2>
          {item.isCurrent && <span className="status-pill email-verified">Estás usando esta sesión</span>}
          <dl className="device-facts"><div><dt>Creada</dt><dd><time dateTime={item.createdAtUtc}>{deviceDate(item.createdAtUtc)}</time></dd></div>
            <div><dt>Vence</dt><dd><time dateTime={item.expiresAtUtc}>{deviceDate(item.expiresAtUtc)}</time></dd></div></dl>
          <p>{item.isCurrent ? "Si la cerrás, vas a volver al inicio de sesión." : "Al cerrarla, ese acceso dejará de funcionar y será necesario iniciar sesión de nuevo."}</p>
          <AccountSessionControl sessionId={item.id} current={item.isCurrent} />
        </article>
      </li>)}</ol>}
    <nav className="account-session-pagination" aria-label="Páginas de sesiones">
      {page > 1 && <Link href={`/account/sessions?page=${page - 1}`} className="button button--secondary">Página anterior</Link>}
      <span>Página {page}</span>
      {result.hasMore && page < MAX_SESSION_PAGE && <Link href={`/account/sessions?page=${page + 1}`} className="button button--secondary">Página siguiente</Link>}
    </nav>
    {result.hasMore && page === MAX_SESSION_PAGE && <p>Se alcanzó el límite de consulta. Podés cerrar todas las sesiones y volver a iniciar sesión.</p>}
    <section className="account-security-section"><h2>Cerrar todos los accesos web</h2>
      <p>Incluye la sesión actual. Los equipos vinculados conservan su acceso; podés revocarlo desde Dispositivos.</p>
      <AccountActionForm mode="logout-all" />
    </section>
    <p><Link href="/account/activity">Ver actividad de seguridad</Link></p>
    <p><Link href="/account">← Volver a Mi cuenta</Link></p>
  </AccountShell>;
}
