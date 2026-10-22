import type { Metadata } from "next";
import { AccountShell } from "@/components/account-shell";
import { RecoveryForm } from "@/components/account-forms";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Nueva contraseña", referrer: "no-referrer" };
export default async function ResetPasswordPage({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  const safeToken = typeof token === "string" && /^[A-Za-z0-9_-]{43,512}$/.test(token) ? token : "";
  return <AccountShell title="Elegí una nueva contraseña" description="El enlace vence a los 30 minutos y se puede usar una sola vez."><RecoveryForm mode="reset" token={safeToken} /></AccountShell>;
}
