import type { DeviceRecord } from "@/lib/devices";
import { DeviceControls } from "./device-forms";
import { Icon } from "./icons";

export function deviceDate(value: string | null): string {
  return value ? new Intl.DateTimeFormat("es-PY", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Asuncion" }).format(new Date(value)) : "Sin registro";
}
export function DeviceCard({ device, verified, detail = false }: { device: DeviceRecord; verified: boolean; detail?: boolean }) {
  const status = { pending: "Pendiente de vinculación", linked: "Vinculado", revoked: "Acceso revocado" }[device.status];
  return <article className="section-card device-card">
    <header className="device-card-heading"><span className="device-icon"><Icon name="monitor" width="26" height="26" /></span><div>{detail ? <h2>{device.name}</h2> : <h3><a href={`/devices/${device.id}`}>{device.name}</a></h3>}<span className={`status-pill device-status--${device.status}`}>{status}</span></div></header>
    <dl className="device-facts"><div><dt>Registrado</dt><dd>{deviceDate(device.createdAtUtc)}</dd></div><div><dt>Último contacto</dt><dd>{deviceDate(device.lastSeenAtUtc)}</dd></div><div><dt>Sistema informado</dt><dd>{device.platform ?? "Sin información del sistema"}</dd></div><div><dt>Versión del agente</dt><dd>{device.agentVersion ?? "Sin registro"}</dd></div>{device.credentialExpiresAtUtc && <div><dt>Vencimiento del acceso</dt><dd>{deviceDate(device.credentialExpiresAtUtc)}</dd></div>}</dl>
    {device.status === "linked" && <p className="input-help">La vinculación confirma el acceso del agente. El último contacto no indica por sí solo que el equipo esté conectado ahora.</p>}
    {!detail && <a className="device-details-link" href={`/devices/${device.id}`}>Ver equipo e historial <span aria-hidden="true">→</span></a>}
    <DeviceControls device={device} verified={verified} />
  </article>;
}
