import type { Metadata } from "next";
import Link from "next/link";
import { ApiError, fetchProfile } from "@/lib/api";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { AccountShell } from "@/components/account-shell";
import { AccountActionForm } from "@/components/account-forms";
import { SessionRenewal } from "@/components/session-renewal";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { LogoutButton } from "@/components/logout-button";
import { logoutAction } from "@/app/actions";

export const metadata: Metadata = { title: "Seguridad de tu cuenta" };
export const dynamic = "force-dynamic";
export default async function AccountPage() {
  const session = await requireSession("/account");
  let user;
  try { user = await fetchProfile(session.token); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    return <DashboardUnavailable />;
  }
  return <AccountShell title="Seguridad de tu cuenta" description="Verificá tu correo y controlá el acceso a tu espacio.">
    <SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <div className="account-security-section"><h2>Correo electrónico</h2><p className="account-email">{user.email}</p><p className={`status-pill ${user.emailVerified ? "email-verified" : ""}`}>{user.emailVerified ? "Correo verificado" : "Verificación pendiente"}</p>{!user.emailVerified && <><p>Podés usar tu dashboard mientras verificás tu dirección.</p><AccountActionForm mode="resend" /></>}</div>
    <div className="account-security-section"><h2>Contraseña</h2><p>Si necesitás cambiarla, enviá un enlace de recuperación a tu correo.</p><Link href="/forgot-password" className="button button--secondary">Cambiar mi contraseña</Link></div>
    <div className="account-security-section"><h2>Sesiones</h2><p>Tu sesión se renueva mientras usás la aplicación, hasta un máximo de 30 días. Podés cerrar todas las sesiones web, incluido este navegador. El acceso de tus equipos se administra desde <Link href="/devices">Dispositivos</Link>.</p><AccountActionForm mode="logout-all" /></div>
    <div className="account-page-footer"><Link href="/dashboard">Volver al dashboard</Link><form action={logoutAction}><LogoutButton secondary /></form></div>
  </AccountShell>;
}
