import type { Metadata } from "next";
import { AccountShell } from "@/components/account-shell";
import { RecoveryForm } from "@/components/account-forms";

export const metadata: Metadata = { title: "Recuperar contraseña" };
export default function ForgotPasswordPage() {
  return <AccountShell title="Recuperá tu acceso" description="Ingresá tu correo y te ayudaremos a elegir una nueva contraseña."><RecoveryForm mode="forgot" /></AccountShell>;
}
