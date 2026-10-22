import type { ReactNode } from "react";
import Link from "next/link";

type AuthShellProps = {
  mode: "login" | "register";
  children: ReactNode;
};

function ShieldMark() {
  return (
    <svg viewBox="0 0 28 32" fill="none" aria-hidden="true">
      <path d="M14 2 25 6.5v8.2c0 7-4.7 11.6-11 15.3C7.7 26.3 3 21.7 3 14.7V6.5L14 2Z" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" />
      <path d="m9 15 3.2 3.2L19 11.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function SecurityIllustration() {
  return (
    <div className="security-illustration" aria-hidden="true">
      <div className="illustration-grid" />
      <div className="illustration-orbit illustration-orbit--outer" />
      <div className="illustration-orbit illustration-orbit--inner" />
      <div className="illustration-node illustration-node--one" />
      <div className="illustration-node illustration-node--two" />
      <div className="illustration-node illustration-node--three" />
      <svg className="illustration-shield" viewBox="0 0 220 252" fill="none">
        <defs>
          <linearGradient id="shield-surface" x1="29" y1="21" x2="192" y2="226" gradientUnits="userSpaceOnUse">
            <stop stopColor="#22556A" />
            <stop offset="1" stopColor="#102D43" />
          </linearGradient>
          <linearGradient id="shield-edge" x1="34" y1="13" x2="185" y2="233" gradientUnits="userSpaceOnUse">
            <stop stopColor="#79E8D5" />
            <stop offset="1" stopColor="#235668" />
          </linearGradient>
        </defs>
        <path d="M110 11 202 48v69c0 57-38 95-92 124-54-29-92-67-92-124V48l92-37Z" fill="url(#shield-surface)" stroke="url(#shield-edge)" strokeWidth="2" />
        <path d="m110 30 75 30v57c0 45-27 78-75 105-48-27-75-60-75-105V60l75-30Z" stroke="#9AEADC" strokeOpacity=".15" />
        <rect x="77" y="104" width="66" height="53" rx="11" fill="#68D9C6" fillOpacity=".13" stroke="#72DCCB" strokeWidth="2" />
        <path d="M91 104V88a19 19 0 0 1 38 0v16" stroke="#8DE8D8" strokeWidth="3" strokeLinecap="round" />
        <circle cx="110" cy="128" r="5" fill="#9EEBDC" />
        <path d="M110 133v8" stroke="#9EEBDC" strokeWidth="3" strokeLinecap="round" />
      </svg>
      <div className="illustration-card illustration-card--one">
        <span className="illustration-card-icon"><ShieldMark /></span>
        <span className="illustration-card-lines"><i /><i /></span>
        <span className="illustration-card-dot" />
      </div>
      <div className="illustration-card illustration-card--two">
        <span className="illustration-bars"><i /><i /><i /><i /><i /></span>
        <span className="illustration-card-lines"><i /><i /></span>
      </div>
    </div>
  );
}

export function AuthShell({ mode, children }: AuthShellProps) {
  const isLogin = mode === "login";

  return (
    <div className="auth-layout">
      <aside className="auth-story" aria-label="Security Center AI">
        <Link href="/login" className="auth-brand" aria-label="Security Center AI, iniciar sesión">
          <span className="brand-symbol"><ShieldMark /></span>
          <span>Security Center <span className="brand-ai">AI</span></span>
        </Link>

        <div className="auth-story-content">
          <div className="story-eyebrow"><span /> TU CENTRO DE SEGURIDAD</div>
          <h2>Más claridad.<br /><span>Más tranquilidad.</span></h2>
          <p>Tu cuenta, tu dashboard y tu historial.<br />Todo en un mismo lugar.</p>
          <SecurityIllustration />
          <div className="story-note">
            <span className="story-note-icon"><ShieldMark /></span>
            <div><strong>Una base para lo que viene</strong><span>El agente de Windows está en preparación.</span></div>
          </div>
        </div>

        <div className="auth-story-footer"><span>Security Center AI</span><span>Tu espacio de trabajo</span></div>
      </aside>

      <main className="auth-main">
        <div className="auth-mobile-brand"><span className="brand-symbol"><ShieldMark /></span><span>Security Center <span className="brand-ai">AI</span></span></div>
        <div className="auth-main-top"><span>¿{isLogin ? "Aún no tenés" : "Ya tenés"} una cuenta?</span><Link href={isLogin ? "/register" : "/login"}>{isLogin ? "Crear cuenta" : "Iniciar sesión"}<span aria-hidden="true">↗</span></Link></div>
        <section className="auth-card" aria-labelledby="auth-title">
          <div className="auth-heading-icon"><ShieldMark /></div>
          <p className="auth-eyebrow">{isLogin ? "BIENVENIDO DE NUEVO" : "EMPECEMOS"}</p>
          <h1 id="auth-title">{isLogin ? "Iniciá sesión en tu cuenta" : "Creá tu cuenta"}</h1>
          <p className="auth-description">{isLogin ? "Accedé a tu dashboard y continuá donde lo dejaste." : "Configurá tu acceso a Security Center AI."}</p>
          {children}
        </section>
        <footer className="auth-main-footer"><span className="footer-shield"><ShieldMark /></span><span>Security Center AI · Tu centro de seguridad</span></footer>
      </main>
    </div>
  );
}
