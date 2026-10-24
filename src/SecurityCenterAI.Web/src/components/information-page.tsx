import Link from "next/link";
import type { ReactNode } from "react";
import { Icon } from "./icons";
import { InformationLinks } from "./information-links";

export function InformationPage({ title, description, children, draft = false }: { title: string; description: string; children: ReactNode; draft?: boolean }) {
  return <div className="information-page"><a className="skip-link" href="#information-content">Ir al contenido</a>
    <header className="information-header"><Link className="account-brand" href="/"><Icon name="shield" /><span>Security Center AI</span></Link><Link href="/dashboard">Mi espacio</Link></header>
    <main className="information-main" id="information-content"><p className="landing-eyebrow">INFORMACIÓN Y AYUDA</p><h1>{title}</h1><p className="information-intro">{description}</p>
      {draft && <aside className="information-draft" aria-label="Estado del documento"><strong>Borrador para pruebas locales</strong><p>Faltan el nombre del responsable, el país de operación y un correo público de contacto. Este documento todavía no es una política ni un acuerdo aprobado para publicar el servicio.</p><p>Revisión del contenido: <time dateTime="2026-10-24">24 de octubre de 2026</time>.</p></aside>}
      <div className="information-body">{children}</div>
    </main>
    <footer className="information-footer"><InformationLinks /><p>Security Center AI · Proyecto en desarrollo.</p><Link href="/">Volver al inicio</Link></footer>
  </div>;
}
