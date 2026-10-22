"use client";

export default function ErrorPage({ reset }: { reset: () => void }) {
  return <main className="standalone-page"><section className="standalone-card"><p className="eyebrow">SECURITY CENTER AI</p><h1>No pudimos abrir esta pantalla</h1><p>Volvé a intentar en un momento para continuar.</p><button className="button button-primary" onClick={reset}>Volver a intentar</button></section></main>;
}
