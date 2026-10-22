import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthForm } from "@/components/auth-form";
import { AuthShell } from "@/components/auth-shell";
import { loadDashboard, readSession } from "@/lib/session";

export const metadata: Metadata = { title: "Crear cuenta" };

export default async function RegisterPage() {
  const session = await readSession();
  if (session && (await loadDashboard(session)).kind === "success") redirect("/dashboard");
  return <AuthShell mode="register"><AuthForm mode="register" /></AuthShell>;
}
