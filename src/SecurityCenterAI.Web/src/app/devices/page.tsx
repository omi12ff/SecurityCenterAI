import type { Metadata } from "next";
import Link from "next/link";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchDevices } from "@/lib/device-api";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { DeviceShell } from "@/components/device-shell";
import { DeviceCard } from "@/components/device-card";
import { AddDeviceForm } from "@/components/device-forms";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { SessionRenewal } from "@/components/session-renewal";
import { Icon } from "@/components/icons";
import { RefreshButton } from "@/components/refresh-button";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Mis dispositivos", referrer: "no-referrer" };
export default async function DevicesPage() {
  const session = await requireSession("/devices");
  let result;
  try { result = await Promise.all([fetchProfile(session.token), fetchDevices(session.token)]); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    return <DashboardUnavailable />;
  }
  const [user, devices] = result;
  const active = devices.find(device => device.status !== "revoked");
  return <DeviceShell user={user}><SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <div className="page-heading"><div><p className="page-greeting">TU ESPACIO</p><h1>Mis dispositivos</h1><p className="page-description">Registrá tu equipo y administrá su acceso a Security Center AI.</p></div><RefreshButton label="Actualizar" /></div>
    <section className="section-card device-registration" aria-labelledby="device-register-title"><h2 id="device-register-title">Preparar la vinculación</h2><p>Elegí un nombre para identificar tu PC y vinculala con el agente Windows. Cuando envíes sus comprobaciones desde el agente, podrás consultar aquí sus resultados e historial.</p><p><Link className="device-back-link" href="/getting-started">Descargar el agente y seguir la guía de primeros pasos →</Link></p><AddDeviceForm verified={user.emailVerified} activeDeviceId={active?.id} /></section>
    <section aria-labelledby="device-list-title"><div className="section-heading"><div><h2 id="device-list-title">Tus equipos</h2><p>Los últimos 100 equipos registrados, con su acceso e historial.</p></div><span className="status-pill">{devices.length} {devices.length === 1 ? "equipo" : "equipos"}</span></div>
      {devices.length ? <div className="device-list">{devices.map(device => <DeviceCard key={device.id} device={device} verified={user.emailVerified} />)}</div> : <div className="section-card empty-state"><span className="empty-state-icon"><Icon name="monitor" width="30" height="30" /></span><h3>Todavía no registraste equipos</h3><p className="empty-state-copy">Cuando agregues uno, podrás preparar su vinculación y consultar aquí sus futuros resultados.</p></div>}
    </section>
  </DeviceShell>;
}
