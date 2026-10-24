"use client";

import { useActionState, useEffect, useState } from "react";
import Link from "next/link";
import { checkLinkMessageAction } from "@/app/link-message-actions";
import type { LinkMessageQuota, LinkMessageState } from "@/lib/link-message";
import { deviceDate } from "./device-card";

export function LinkMessageForm({ initialQuota, verified }: { initialQuota: LinkMessageQuota; verified: boolean }) {
  const [state, action, pending] = useActionState<LinkMessageState, FormData>(checkLinkMessageAction, {});
  const [kind, setKind] = useState("link"); const [content, setContent] = useState("");
  const [confirmed, setConfirmed] = useState(false); const [visible, setVisible] = useState(false);
  useEffect(() => { setVisible(true); }, [state]);
  const quota = state.response?.quota ?? state.quota ?? initialQuota;
  const report = visible ? state.response?.report : undefined;
  return <>
    <section className="section-card tool-panel" aria-labelledby="tool-quota-title">
      <h2 id="tool-quota-title">Tu límite diario</h2>
      <p role="status">{quota.remaining} de {quota.limit} consultas disponibles para enlaces y mensajes.</p>
      <p>Se renueva el próximo día UTC: <time dateTime={quota.resetsAtUtc}>{deviceDate(quota.resetsAtUtc)}</time>.</p>
      <a href="/tools/scam-check">Actualizar límite</a>
    </section>
    <section className="section-card tool-panel" aria-labelledby="tool-input-title">
      <h2 id="tool-input-title">Qué querés revisar</h2>
      <p id="tool-data-help">El contenido se procesa en el servidor de Security Center AI. Guardamos solo el día y la cantidad de consultas de tu cuenta. No guardamos el mensaje, el enlace o el resultado, ni los enviamos a un servicio externo.</p>
      <p>No pegues contraseñas, códigos, datos bancarios o información privada. Eliminá esos datos antes de continuar.</p>
      {!verified && <p role="alert">Primero <Link href="/account">verificá tu correo desde Mi cuenta</Link>.</p>}
      <form action={action} className="auth-form tool-check-form" aria-label="Revisar enlace o mensaje" aria-describedby="tool-data-help" aria-busy={pending} onSubmit={() => setVisible(false)}>
        <div><label htmlFor="tool-kind">Tipo de contenido</label><select id="tool-kind" name="kind" className="text-input" value={kind} disabled={pending} onChange={event => { setKind(event.target.value); setContent(""); setConfirmed(false); setVisible(false); }}>
          <option value="link">Enlace</option><option value="message">Mensaje</option></select></div>
        <div><label htmlFor="tool-content">{kind === "link" ? "Enlace completo (HTTP o HTTPS)" : "Texto del mensaje"}</label>
          <textarea id="tool-content" name="content" className="text-input" rows={kind === "link" ? 3 : 7} maxLength={kind === "link" ? 2048 : 6000}
            required spellCheck={false} autoComplete="off" value={content} disabled={pending} onChange={event => { setContent(event.target.value); setVisible(false); }} />
          <p className="field-help">{content.length}/{kind === "link" ? 2048 : 6000} caracteres. Se revisan hasta cinco enlaces HTTP/HTTPS por mensaje.</p></div>
        <label className="checkbox-label"><input type="checkbox" name="confirmed" value="yes" required checked={confirmed} disabled={pending} onChange={event => setConfirmed(event.target.checked)} />Revisé el contenido y confirmo enviarlo a Security Center AI para esta consulta.</label>
        <div className="tool-actions"><button className="button button--primary" disabled={pending || !verified || quota.remaining === 0}>{pending ? "Revisando…" : "Revisar contenido"}</button>
          <button className="button button--secondary" type="button" disabled={pending} onClick={() => { setContent(""); setConfirmed(false); setVisible(false); }}>Limpiar contenido</button></div>
      </form>
      {visible && state.message && <p role="alert">{state.message}</p>}
    </section>
    {report && <section className="section-card tool-panel" aria-labelledby="tool-result-title" role="region">
      <h2 id="tool-result-title">{report.outcome === "signals-found" ? "Señales para revisar" : "Sin señales evidentes en estas reglas"}</h2>
      <p>Este resultado no confirma una estafa ni garantiza seguridad. Las reglas son limitadas y pueden marcar contenido legítimo o pasar por alto engaños.</p>
      <p>No se abrieron enlaces ni se comprobaron redirecciones o reputación. No se verifica la identidad del remitente ni se analizan archivos, imágenes o adjuntos.</p>
      {report.hosts.length > 0 && <div><h3>Dominios o direcciones observados</h3><ul>{report.hosts.map(host => <li key={host}><code>{host}</code></li>)}</ul><p>Se muestran como texto, sin abrirlos. Rutas, parámetros y datos de usuario no se devuelven.</p></div>}
      <p>Enlaces revisados: {report.linksChecked}. Enlaces no revisados: {report.linksOmitted}.</p>
      {report.signals.length > 0 && <ul className="tool-signals">{report.signals.map(signal => <li key={signal.code}><h3>{signal.title}</h3><p>{signal.explanation}</p><p>{signal.advice}</p></li>)}</ul>}
      <p>Comprobación con reglas {report.rulesVersion}.</p>
    </section>}
  </>;
}
