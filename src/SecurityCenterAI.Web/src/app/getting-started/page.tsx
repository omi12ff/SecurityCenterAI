import type { Metadata } from "next";
import Link from "next/link";
import { ApiError, fetchProfile } from "@/lib/api";
import { fetchDevice, fetchDevices } from "@/lib/device-api";
import { agentPackageInfo } from "@/lib/agent-package";
import { agentPairingCommand } from "@/lib/agent-endpoint";
import { requireSession, redirectToRenewal } from "@/lib/session";
import { DeviceShell } from "@/components/device-shell";
import { DashboardUnavailable } from "@/components/dashboard-view";
import { SessionRenewal } from "@/components/session-renewal";
import { RefreshButton } from "@/components/refresh-button";
import { AgentCommand } from "@/components/agent-command";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Primeros pasos", referrer: "no-referrer" };

export default async function GettingStartedPage() {
  const session = await requireSession("/getting-started");
  let loaded;
  try {
    const [user, devices] = await Promise.all([fetchProfile(session.token), fetchDevices(session.token)]);
    const active = devices.find(device => device.status !== "revoked");
    const detail = active ? await fetchDevice(active.id, session.token) : null;
    loaded = { user, device: detail?.device ?? null, analyses: detail?.analyses ?? [] };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/getting-started");
    return <DashboardUnavailable />;
  }
  const { user, device, analyses } = loaded;
  const linked = device?.status === "linked" && !!device.credentialExpiresAtUtc && Date.parse(device.credentialExpiresAtUtc) > Date.now();
  const latest = analyses.find(analysis => analysis.source === "windows-agent");
  const download = await agentPackageInfo();
  const pairingCommand = agentPairingCommand(process.env.AGENT_API_URL ?? (process.env.NODE_ENV === "development" ? "http://localhost:5169" : undefined));
  const next = !user.emailVerified ? { title: "Verificá tu correo", href: "/account", action: "Ir a verificar mi correo" }
    : !device ? { title: "Registrá tu equipo", href: "/devices", action: "Registrar mi equipo" }
    : !linked ? { title: "Vinculá tu equipo con el agente", href: "#vincular", action: "Ver cómo vincularlo" }
    : !latest ? { title: "Enviá tu primer análisis", href: "#enviar", action: "Ver cómo enviar el análisis" }
    : { title: "Tu primer análisis ya está disponible", href: `/devices/${device.id}/analyses/${latest.id}`, action: "Entender mi análisis" };
  const steps = [
    { label: "Correo verificado", done: user.emailVerified },
    { label: "Equipo registrado", done: !!device },
    { label: "Acceso del agente vigente", done: linked },
    { label: "Análisis recibido de este equipo", done: !!latest },
  ];
  return <DeviceShell user={user} activePage="getting-started"><SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <div className="page-heading"><div><p className="page-greeting">DE TU CUENTA AL PRIMER RESULTADO</p><h1>Primeros pasos</h1><p className="page-description">Prepará el agente Windows y decidí cuándo enviar las comprobaciones de tu equipo.</p></div><RefreshButton label="Actualizar progreso" /></div>
    <section className="section-card setup-panel setup-next" aria-labelledby="setup-next-title">
      <p className="eyebrow">TU PRÓXIMO PASO</p><h2 id="setup-next-title">{next.title}</h2>
      {device && <p>Equipo: <strong>{device.name}</strong>. {device.status === "linked" && !linked ? "Su acceso venció; generá un código nuevo para volver a vincularlo." : ""}</p>}
      <Link href={next.href} className="button button--primary">{next.action}</Link>
      <ol className="setup-progress" aria-label="Progreso de la preparación">{steps.map(step => <li key={step.label} className={step.done ? "setup-step-done" : undefined}><strong>{step.label}</strong><span>{step.done ? "Confirmado" : "Pendiente"}</span></li>)}</ol>
      <p className="input-help">El progreso refleja lo guardado en tu cuenta. Una vinculación no confirma que el equipo esté conectado ahora; descargar el ZIP no inicia un análisis.</p>
    </section>

    <section className="section-card setup-panel" id="agente" aria-labelledby="setup-agent-title">
      <h2 id="setup-agent-title">1. Prepará el agente Windows</h2>
      <p>Paquete portátil en vista previa para <strong>Windows 11 x64</strong>. Incluye .NET: para usarlo no necesitás instalar SDK, Docker ni Node.js. Windows 10, ARM64 y otros equipos todavía no están validados.</p>
      {download ? <div className="setup-download">
        <p><strong>Versión {download.version}</strong> · ZIP · {(download.bytes / (1024 * 1024)).toLocaleString("es-PY", { maximumFractionDigits: 1 })} MB</p>
        {user.emailVerified ? <a className="button button--primary" href="/getting-started/agent/download" download>Descargar agente Windows (ZIP)</a> : <p className="form-alert">Para descargarlo, <Link href="/account">verificá tu correo desde Mi cuenta</Link>.</p>}
        <details className="setup-details"><summary>Comprobar la integridad del ZIP</summary>
          <p>Antes de extraerlo, abrí PowerShell en la carpeta donde lo descargaste y compará el resultado con este SHA-256:</p><p className="setup-hash"><code>{download.sha256}</code></p>
          <AgentCommand label="Comprobar SHA-256" command={`Get-FileHash '.\\${download.fileName}' -Algorithm SHA256`} />
          <p>Si el valor no coincide, no lo ejecutes. El hash permite comparar el archivo; no es una firma de editor ni certifica su seguridad.</p>
        </details>
      </div> : <p className="form-alert" role="status">El paquete todavía no está disponible desde esta web. Podés preparar tu cuenta y registrar el equipo; volvé a intentar más tarde.</p>}
      <ol className="setup-instructions"><li>Descargá el ZIP y elegí <strong>Extraer todo</strong> en una carpeta nueva. Conservá juntos el ejecutable, las DLL y los demás archivos.</li><li>Abrí PowerShell en la carpeta que contiene <code>SecurityCenterAI.Agent.exe</code>, con permisos normales.</li><li>Comprobá los requisitos básicos:</li></ol>
      <AgentCommand label="Comprobar requisitos" command=".\SecurityCenterAI.Agent.exe doctor" />
      <p>Un diagnóstico correcto permite seguir con la prueba; no confirma que Windows esté protegido. El agente no solicita permisos de administrador. Si Windows o tu organización bloquean el programa, detené la prueba y consultá al responsable, sin desactivar las protecciones.</p>
    </section>

    <section className="section-card setup-panel" id="vincular" aria-labelledby="setup-pair-title">
      <h2 id="setup-pair-title">2. Registrá y vinculá tu equipo</h2>
      <p>En <Link className="device-back-link" href="/devices">Mis dispositivos</Link>, elegí un nombre para tu PC. Tu cuenta admite un equipo activo. Si ya lo registraste, usá su tarjeta; el historial de equipos revocados se conserva.</p>
      <p>El código se muestra solo al generarlo y tiene vencimiento. Con el agente listo, copialo y ejecutá:</p>
      {pairingCommand ? <AgentCommand label="Vincular el equipo" command={pairingCommand} /> : <p className="form-alert">La dirección para vincular el agente todavía no está configurada en esta web. Pedísela a quien administra la aplicación antes de continuar.</p>}
      <p>Pegá el código cuando la consola lo pida: la entrada queda oculta. No lo pongas en el comando, capturas o mensajes. El agente no pide la contraseña de tu cuenta.</p>
      <p>Si venció el código, generá uno nuevo desde la tarjeta. Si el agente dice que ya tiene una credencial, ejecutá <code>.\SecurityCenterAI.Agent.exe status</code> y revisá el equipo antes de sustituirla con <code>pair --replace</code> y un código nuevo.</p>
      <p className="input-help">Si la dirección dice localhost, la API y el agente deben ejecutarse en esta misma PC. Mantené abiertas las terminales de la web y la API; usá otra terminal para el agente. Los destinos externos requieren HTTPS válido.</p>
      <Link className="button button--secondary" href={device ? `/devices/${device.id}` : "/devices"}>{device ? "Abrir mi equipo y sus códigos" : "Ir a Mis dispositivos"}</Link>
    </section>

    <section className="section-card setup-panel" id="enviar" aria-labelledby="setup-send-title">
      <h2 id="setup-send-title">3. Analizá, revisá y elegí si enviar</h2>
      <p>Primero realizá las diez comprobaciones de solo lectura. Este comando guarda un informe en tu PC y <strong>no lo envía</strong>:</p>
      <AgentCommand label="Analizar solo en esta PC" command=".\SecurityCenterAI.Agent.exe scan" />
      <p>La consola indica dónde guardó el JSON. Revisalo antes de compartirlo: contiene estados de seguridad y evidencia seleccionada. Si querés incorporarlo a tu cuenta, enviá el último informe con esta acción explícita:</p>
      <AgentCommand label="Enviar el último informe" command=".\SecurityCenterAI.Agent.exe send" />
      <p>Después, pulsá <strong>Actualizar progreso</strong> o abrí el historial del equipo. Si solo ejecutaste <code>doctor</code>, <code>status</code> o <code>heartbeat</code>, todavía no enviaste un análisis.</p>
      <p>Reintentar el mismo envío no duplica el resultado. Si el informe fue eliminado y el agente responde <code>410</code>, generá uno nuevo con <code>scan</code> antes de decidir si enviarlo.</p>
      {device && <Link className="button button--secondary" href={latest ? `/devices/${device.id}/analyses/${latest.id}` : `/devices/${device.id}`}>{latest ? "Abrir el análisis y sus guías" : "Ver equipo e historial"}</Link>}
    </section>

    <section className="section-card setup-panel" aria-labelledby="setup-data-title"><h2 id="setup-data-title">Qué esperar y cómo retirar el acceso</h2>
      <p>Los puntos muestran comprobaciones confirmadas; revisá también la cobertura. Un estado <strong>Desconocido</strong> indica falta de información, y requiere revisión manual. Las guías se preparan localmente, sin IA externa. El agente no cambia configuraciones ni reemplaza el antivirus.</p>
      <p>La credencial queda protegida para tu usuario Windows y los informes quedan en texto claro en <code>%LOCALAPPDATA%\SecurityCenterAI\Agent</code>. No los copies dentro del ZIP. No hay análisis ni envíos automáticos.</p>
      <p>Para cortar el acceso, usá <strong>Revocar acceso</strong> en Mis dispositivos. Después podés ejecutar <code>.\SecurityCenterAI.Agent.exe forget</code> para quitar la credencial local. Borrar el programa no revoca el acceso ni borra los informes guardados.</p>
    </section>
    <p><Link className="device-back-link" href="/dashboard">← Volver al resumen</Link></p>
  </DeviceShell>;
}
