import type { Metadata } from "next";
import Link from "next/link";
import { AccountShell } from "@/components/account-shell";
import { LinkMessageForm } from "@/components/link-message-form";
import { SessionRenewal } from "@/components/session-renewal";
import { ApiError, fetchProfile, request } from "@/lib/api";
import { parseLinkMessageQuota } from "@/lib/link-message";
import { requireSession, redirectToRenewal } from "@/lib/session";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Revisar enlaces y mensajes", referrer: "no-referrer" };

export default async function ScamCheckPage() {
  const session = await requireSession("/tools/scam-check");
  let profile; let quota;
  try {
    const [user, usage] = await Promise.all([fetchProfile(session.token), request("/api/tools/link-message", { token: session.token })]);
    profile = user; quota = parseLinkMessageQuota(usage);
    if (!quota) throw new ApiError(502, "Contador inválido.");
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/tools/scam-check");
    return <AccountShell title="Revisar enlaces y mensajes" description="Observaciones para ayudarte a decidir qué revisar."><p role="alert">No pudimos cargar la herramienta ni comprobar tu límite.</p><a href="/tools/scam-check">Volver a intentar</a><p><Link href="/dashboard">Volver al resumen</Link></p></AccountShell>;
  }
  return <AccountShell title="Revisar enlaces y mensajes" description="Una revisión básica de señales visibles, con pasos para contrastarlas.">
    <SessionRenewal expiresAt={session.expiresAt.toISOString()} />
    <LinkMessageForm initialQuota={quota} verified={profile.emailVerified} />
    <section className="section-card tool-panel"><h2>Cómo interpretar la revisión</h2>
      <p>Usamos reglas locales sobre la dirección y palabras del texto. No calculamos un puntaje de confianza ni declaramos que un enlace es seguro. Un mensaje educativo también puede mencionar claves, premios o pagos.</p>
      <p>Si tenés dudas, buscá el contacto oficial por un medio que ya conozcas y contrastá allí el mensaje.</p>
      <p>Más información: <a href="https://consumidor.ftc.gov/articulos/como-reconocer-y-evitar-las-estafas-de-phishing" target="_blank" rel="noopener noreferrer">guía de phishing de la FTC</a> y <a href="https://www.incibe.es/aprendeciberseguridad/phishing" target="_blank" rel="noopener noreferrer">guía de INCIBE</a>.</p>
    </section>
    <p><Link href="/dashboard">← Volver al resumen</Link> · <Link href="/account">Mi cuenta</Link></p>
  </AccountShell>;
}
