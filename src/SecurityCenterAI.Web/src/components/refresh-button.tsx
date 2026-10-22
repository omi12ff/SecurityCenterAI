"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { Icon } from "./icons";

export function RefreshButton({ label = "Volver a intentar" }: { label?: string }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <button
      className="button button--primary refresh-button"
      type="button"
      onClick={() => startTransition(() => router.refresh())}
      disabled={pending}
      aria-busy={pending}
    >
      {pending ? <span className="spinner" aria-hidden="true" /> : <Icon name="refresh" />}
      <span>{pending ? "Actualizando…" : label}</span>
    </button>
  );
}
