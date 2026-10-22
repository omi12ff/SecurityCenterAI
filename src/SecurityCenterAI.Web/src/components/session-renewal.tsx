"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { renewSessionAction } from "@/app/actions";

let inFlight: ReturnType<typeof renewSessionAction> | undefined;
function renew(): ReturnType<typeof renewSessionAction> {
  if (inFlight) return inFlight;
  const execute = () => renewSessionAction();
  const executeLocked = async () => navigator.locks ? await navigator.locks.request("security-center-session", execute) : await execute();
  inFlight = executeLocked()
    .finally(() => { inFlight = undefined; });
  return inFlight;
}

export function SessionRenewal({ expiresAt, destination }: { expiresAt?: string; destination?: "/account" | "/dashboard" | "/devices" }) {
  const router = useRouter();
  const [message, setMessage] = useState("");
  const running = useRef(false);
  useEffect(() => {
    let active = true;
    let timer: ReturnType<typeof setTimeout>;
    async function update() {
      if (running.current) return;
      running.current = true;
      try {
        const result = await renew();
        if (!active) return;
        if (result.ok) {
          if (destination) router.replace(destination);
          else router.refresh();
        } else if (result.expired) router.replace("/login?reason=session-expired");
        else { setMessage(result.message ?? "No pudimos renovar tu sesión."); timer = setTimeout(update, 60_000); }
      } catch { if (active) { setMessage("No pudimos renovar tu sesión. Intentá de nuevo en un momento."); timer = setTimeout(update, 60_000); } }
      finally { running.current = false; }
    }
    const refreshAt = expiresAt ? Date.parse(expiresAt) - 60_000 : Date.now();
    timer = setTimeout(update, Math.max(0, refreshAt - Date.now()));
    const onVisible = () => { if (document.visibilityState === "visible" && Date.now() >= refreshAt) void update(); };
    document.addEventListener("visibilitychange", onVisible);
    return () => { active = false; clearTimeout(timer); document.removeEventListener("visibilitychange", onVisible); };
  }, [expiresAt, destination, router]);
  if (destination) return <div className="auth-form"><p role="status">{message || "Estamos renovando tu sesión…"}</p>{message && <button className="button button--secondary" onClick={() => window.location.reload()}>Reintentar</button>}</div>;
  return message ? <p className="form-alert" role="status">{message}</p> : null;
}
