import Link from "next/link";
import type { ReactNode } from "react";
import type { UserProfile } from "@/lib/contracts";
import { logoutAction } from "@/app/actions";
import { Icon } from "./icons";
import { LogoutButton } from "./logout-button";
import { InformationLinks } from "./information-links";
import { SidebarNav } from "./sidebar-nav";

export function DeviceShell({ user, children, activePage = "devices" }: { user: UserProfile; children: ReactNode; activePage?: "devices" | "getting-started" }) {
  return <div className="dashboard-shell">
    <a href="#contenido" className="skip-link">Ir al contenido</a>
    <aside className="sidebar" aria-label="Menú principal">
      <Link href="/dashboard" className="sidebar-brand"><span className="brand-mark"><Icon name="shield" /></span><span>Security Center<span className="brand-ai">AI</span></span></Link>
      <p className="nav-section-label">Tu espacio</p><SidebarNav>
        <Link href="/dashboard" className="nav-link"><Icon name="grid" className="nav-icon" /><span>Resumen</span></Link>
        <Link href="/getting-started" className={`nav-link${activePage === "getting-started" ? " nav-link--active" : ""}`} aria-current={activePage === "getting-started" ? "page" : undefined}><Icon name="info" className="nav-icon" /><span>Primeros pasos</span></Link>
        <Link href="/devices" className={`nav-link${activePage === "devices" ? " nav-link--active" : ""}`} aria-current={activePage === "devices" ? "page" : undefined}><Icon name="monitor" className="nav-icon" /><span>Dispositivos</span></Link>
        <Link href="/tools/scam-check" className="nav-link"><Icon name="mail" className="nav-icon" /><span>Enlaces y mensajes</span></Link>
        <Link href="/account" className="nav-link"><Icon name="user" className="nav-icon" /><span>Mi cuenta</span></Link>
      </SidebarNav>
      <div className="sidebar-bottom"><div className="sidebar-user"><span className="avatar" aria-hidden="true">{user.name.slice(0, 2).toLocaleUpperCase("es")}</span><div className="sidebar-user-meta"><strong>{user.name}</strong><span>{user.email}</span></div></div><form action={logoutAction}><LogoutButton /></form></div>
    </aside>
    <main className="dashboard-main" id="contenido"><header className="dashboard-topbar"><p className="breadcrumb">Mi espacio <span aria-hidden="true">/</span><Link href={activePage === "getting-started" ? "/getting-started" : "/devices"}>{activePage === "getting-started" ? "Primeros pasos" : "Dispositivos"}</Link></p><span className="environment-tag">Versión inicial</span></header><div className="dashboard-content devices-content">{children}<footer className="dashboard-footer"><span>Security Center AI</span><InformationLinks /><p>Información clara. Decisiones con evidencia.</p></footer></div></main>
  </div>;
}
