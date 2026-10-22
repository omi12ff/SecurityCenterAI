"use client";

import { useFormStatus } from "react-dom";
import { Icon } from "./icons";

export function LogoutButton({ secondary = false }: { secondary?: boolean }) {
  const { pending } = useFormStatus();

  return (
    <button
      className={secondary ? "button button--secondary" : "logout-button"}
      type="submit"
      disabled={pending}
      aria-busy={pending}
      aria-label={pending ? "Cerrando sesión…" : "Cerrar sesión"}
    >
      {pending ? <span className="spinner" aria-hidden="true" /> : <Icon name="logout" />}
      <span>{pending ? "Cerrando sesión…" : "Cerrar sesión"}</span>
    </button>
  );
}
