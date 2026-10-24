import { redirect } from "next/navigation";
import Link from "next/link";
import { readSession, readRefreshToken } from "@/lib/session";
import { Icon } from "@/components/icons";
import { InformationLinks } from "@/components/information-links";

export default async function HomePage() {
  if (await readSession()) redirect("/dashboard");
  if (await readRefreshToken()) redirect("/session/renew");
  return <div className="landing-page">
    <a href="#landing-content" className="skip-link">Ir al contenido</a>
    <header className="landing-header">
      <Link href="/" className="account-brand" aria-label="Security Center AI, inicio"><Icon name="shield" /><span>Security Center AI</span></Link>
      <nav aria-label="Navegación pública"><a href="#como-funciona">Cómo funciona</a><a href="#tus-datos">Tus datos</a><Link href="/login">Iniciar sesión</Link></nav>
    </header>
    <main id="landing-content">
      <section className="landing-hero" aria-labelledby="landing-title">
        <div><p className="landing-eyebrow">PARA ENTENDER TU PC · EN ESPAÑOL</p><h1 id="landing-title">Tu seguridad empieza por entender qué revisar.</h1>
          <p className="landing-intro">Consultá comprobaciones de Windows, entendé sus resultados y encontrá pasos claros para revisarlos. También podés buscar señales visibles en un enlace o mensaje.</p>
          <div className="tool-actions"><Link href="/register" className="button button--primary">Crear cuenta gratuita <Icon name="arrow-right" /></Link><a href="#como-funciona" className="button button--secondary">Conocer el recorrido</a></div>
          <p className="landing-preview">Proyecto en desarrollo. Agente portátil en vista previa para Windows 11 x64.</p>
        </div>
        <aside className="landing-overview" aria-label="Alcance de las comprobaciones">
          <span className="landing-overview-icon"><Icon name="monitor" width="42" height="42" /></span><h2>Observaciones que podés consultar</h2>
          <ul><li>Estado del firewall y protecciones de Windows</li><li>Estado del servicio de actualizaciones</li><li>Cifrado, arranque seguro y otras comprobaciones</li></ul>
          <p>Diez reglas iniciales. Cada resultado diferencia lo comprobado de lo que no se pudo determinar.</p>
        </aside>
      </section>
      <section className="landing-section" aria-labelledby="landing-tools-title">
        <p className="landing-eyebrow">TU ESPACIO</p><h2 id="landing-tools-title">Información clara para dar el siguiente paso</h2>
        <div className="landing-grid">
          <article className="section-card landing-feature"><Icon name="monitor" /><h3>Revisá tu equipo</h3><p>Vinculá un dispositivo, solicitá un análisis desde el agente y consultá puntos, cobertura y evidencias. El envío es manual.</p></article>
          <article className="section-card landing-feature"><Icon name="history" /><h3>Entendé los cambios</h3><p>Consultá tu historial y compará dos informes del mismo equipo. Las guías en español explican qué significa cada comprobación.</p></article>
          <article className="section-card landing-feature"><Icon name="mail" /><h3>Revisá enlaces y mensajes</h3><p>Buscá señales de urgencia, referencias a claves y características de la dirección. Cuenta verificada, con hasta 25 consultas diarias por defecto.</p><Link href="/tools/scam-check">Ir a la herramienta →</Link></article>
        </div>
      </section>
      <section className="landing-section landing-walkthrough" id="como-funciona" aria-labelledby="landing-steps-title">
        <div><p className="landing-eyebrow">PASO A PASO</p><h2 id="landing-steps-title">De una comprobación a una explicación</h2><p>La web organiza tus resultados. El agente portátil realiza las consultas locales de Windows desde una consola.</p></div>
        <ol><li><h3>Creá y verificá tu cuenta</h3><p>Tu espacio admite un equipo activo. Podés administrar sesiones y revocar el acceso del dispositivo.</p></li>
          <li><h3>Vinculá y analizá tu PC</h3><p>Usá el código temporal para vincular el agente. Ejecutá las comprobaciones y elegí cuándo enviar el informe.</p></li>
          <li><h3>Revisá el resultado y sus límites</h3><p>Consultá evidencia, cobertura y pasos de revisión. Podés volver a analizar para comparar lo observado.</p></li></ol>
      </section>
      <section className="landing-section" id="tus-datos" aria-labelledby="landing-data-title">
        <p className="landing-eyebrow">DATOS Y CONTROL</p><h2 id="landing-data-title">Vos elegís qué enviar</h2>
        <div className="landing-grid"><article className="section-card landing-feature"><h3>Informes acotados</h3><p>El agente envía observaciones de las comprobaciones solicitadas. No sube documentos personales o tu historial de navegación.</p></article>
          <article className="section-card landing-feature"><h3>Contenido de consulta temporal</h3><p>La revisión de enlaces y mensajes procesa el contenido en nuestro servidor y conserva solo el contador de consultas. No abre el destino ni usa un proveedor de IA externo.</p></article>
          <article className="section-card landing-feature"><h3>Acceso y borrado</h3><p>Desde Mi cuenta podés exportar tus datos o eliminar tu cuenta. También podés borrar análisis individuales y revocar equipos.</p></article></div>
      </section>
      <section className="landing-section landing-faq" id="preguntas" aria-labelledby="landing-faq-title"><h2 id="landing-faq-title">Antes de empezar</h2>
        <details><summary>¿Reemplaza a mi antivirus?</summary><p>No. Ayuda a interpretar comprobaciones y configuraciones. Mantené las protecciones y actualizaciones del equipo.</p></details>
        <details><summary>¿Un puntaje alto garantiza que mi PC está segura?</summary><p>No. Los puntos corresponden a diez reglas iniciales, con cobertura y estados desconocidos explícitos. No certifican el equipo.</p></details>
        <details><summary>¿Puede confirmar si un mensaje es una estafa?</summary><p>Las reglas muestran observaciones para contrastar; pueden pasar por alto engaños o marcar contenido legítimo. No se consulta reputación ni se verifica al remitente.</p></details>
        <details><summary>¿Qué Windows admite el agente actual?</summary><p>La vista previa está orientada a Windows 11 x64. La compatibilidad en otras PC y versiones continúa pendiente de pruebas; no se anuncia soporte para Windows 10 todavía.</p></details>
      </section>
      <section className="landing-cta"><h2>Empezá por conocer tu equipo</h2><p>Creá tu espacio y revisá cada resultado con su evidencia.</p><Link href="/register" className="button button--primary">Crear mi cuenta</Link></section>
    </main>
    <footer className="landing-footer"><span>Security Center AI</span><p>Información clara. Decisiones con evidencia.</p><InformationLinks /><Link href="/login">Entrar a mi cuenta</Link></footer>
  </div>;
}
