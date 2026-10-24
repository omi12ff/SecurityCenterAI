"use client";

import { useState } from "react";

export function AgentCommand({ command, label }: { command: string; label: string }) {
  const [message, setMessage] = useState("");
  async function copy() {
    try { await navigator.clipboard.writeText(command); setMessage("Comando copiado."); }
    catch { setMessage("Seleccioná y copiá el comando del recuadro."); }
  }
  return <div className="setup-command">
    <pre tabIndex={0} aria-label={label}><code>{command}</code></pre>
    <button className="button button--secondary" type="button" onClick={copy} aria-label={`Copiar comando: ${label}`}>Copiar comando</button>
    {message && <p className="input-help" role="status">{message}</p>}
  </div>;
}
