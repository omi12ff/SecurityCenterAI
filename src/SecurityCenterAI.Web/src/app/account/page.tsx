import type { Metadata } from "next";
import Link from "next/link";
import { ApiError, fetchProfile, request } from "@/lib/api";
import { parseEmailChangeStatus, type EmailChangeStatus } from "@/lib/account-email-change";
import { AccountEmailChange } from "@/components/account-email-change";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { AccountShell } from "@/components/account-shell";
import { AccountActionForm } from "@/components/account-forms";
import { SessionRenewal } from "@/components/session-renewal";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { LogoutButton } from "@/components/logout-button";
import { logoutAction } from "@/app/actions";
import { AccountDeletionForm } from "@/components/account-deletion-form";
import { AccountPasswordForm } from "@/components/account-password-form";
import { AccountProfileForm } from "@/components/account-profile-form";

export const metadata: Metadata = { title: "Seguridad de tu cuenta" };
export const dynamic = "force-dynamic";
export default async function AccountPage() {
  const session = await requireSession("/account");
  let user;
  let emailChange: EmailChangeStatus | null = null;
  try {
    [user, emailChange] = await Promise.all([fetchProfile(session.token), request("/api/account/email", { token: session.token })
      .then(parseEmailChangeStatus).catch(error => { if (error instanceof ApiError && error.status === 401) throw error; return null; })]);
  }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    return <DashboardUnavailable />;
  }
  return <AccountShell title="Seguridad de tu cuenta" description="Verificá tu correo y controlá el acceso a tu espacio.">
    <SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <AccountProfileForm name={user.name} />
    <div className="account-security-section"><h2>Correo electrónico</h2><p className="account-email">{user.email}</p><p className={`status-pill ${user.emailVerified ? "email-verified" : ""}`}>{user.emailVerified ? "Correo verificado" : "Verificación pendiente"}</p>{!user.emailVerified && <><p>Podés usar tu dashboard mientras verificás tu dirección.</p><AccountActionForm mode="resend" /></>}</div>
    <AccountEmailChange status={emailChange} verified={user.emailVerified} />
    <AccountPasswordForm />
    <div className="account-security-section"><h2>Sesiones</h2><p>Tu sesión se renueva mientras usás la aplicación, hasta un máximo de 30 días. Podés revisar los accesos web y cerrarlos individualmente o cerrar todas las sesiones. El acceso de tus equipos se administra desde <Link href="/devices">Dispositivos</Link>.</p><p><Link href="/account/sessions" className="button button--secondary">Ver sesiones activas</Link></p><AccountActionForm mode="logout-all" /></div>
    <section className="account-security-section"><h2>Actividad de seguridad</h2><p>Consultá accesos completados, cierres de sesión y cambios de dispositivos registrados desde esta actualización.</p><Link href="/account/activity" className="button button--secondary">Ver actividad de seguridad</Link></section>
    <section className="account-security-section account-data-export" aria-labelledby="account-export-title" aria-describedby="account-export-description account-export-privacy">
      <h2 id="account-export-title">Descargar datos de mi cuenta</h2>
      <p id="account-export-description">La copia incluye tu perfil con correo electrónico, equipos, informes, evidencias, feedback privado, actividad de seguridad y las marcas mínimas de informes eliminados.</p>
      <p id="account-export-privacy">El archivo JSON está en texto claro. Revisalo antes de compartirlo y guardalo en un lugar que controles.</p>
      <a className="button button--secondary account-export-download" href="/account/export" download>Exportar mi cuenta (JSON)</a>
    </section>
    <AccountDeletionForm />
    <div className="account-page-footer"><Link href="/dashboard">Volver al dashboard</Link><form action={logoutAction}><LogoutButton secondary /></form></div>
  </AccountShell>;
}
