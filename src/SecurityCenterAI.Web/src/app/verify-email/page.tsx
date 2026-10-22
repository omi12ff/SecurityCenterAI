import type { Metadata } from "next";
import { AccountShell } from "@/components/account-shell";
import { RecoveryForm } from "@/components/account-forms";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Verificar correo", referrer: "no-referrer" };
export default async function VerifyEmailPage({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  const safeToken = typeof token === "string" && /^[A-Za-z0-9_-]{43,512}$/.test(token) ? token : "";
  return <AccountShell title="Confirmá tu correo" description="Confirmá que esta dirección te pertenece. El enlace vence a las 24 horas y se puede usar una sola vez."><RecoveryForm mode="verify" token={safeToken} /></AccountShell>;
}
