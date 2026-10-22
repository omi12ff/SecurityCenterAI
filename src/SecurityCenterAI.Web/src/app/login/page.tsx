import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthForm } from "@/components/auth-form";
import { AuthShell } from "@/components/auth-shell";
import { loadDashboard, readSession } from "@/lib/session";

export const metadata: Metadata = { title: "Iniciar sesión" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ reason?: string }> }) {
  const session = await readSession();
  if (session && (await loadDashboard(session)).kind === "success") redirect("/dashboard");
  const reason = (await searchParams).reason;
  const notice = reason === "session-expired" ? "Tu sesión venció o fue cerrada. Iniciá sesión para continuar."
    : reason === "all-sessions-closed" ? "Se cerraron todas las sesiones de tu cuenta."
    : reason === "logout-local" ? "Cerramos la sesión en este navegador. No pudimos confirmar el cierre en el servicio; cuando esté disponible podés cerrar todas las sesiones desde tu cuenta." : undefined;
  return <AuthShell mode="login"><AuthForm mode="login" notice={notice} /></AuthShell>;
}
