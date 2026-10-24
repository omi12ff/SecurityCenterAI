import type { Metadata } from "next";
import { AccountShell } from "@/components/account-shell";
import { EmailChangeConfirmation } from "@/components/account-email-change";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Confirmar cambio de correo", referrer: "no-referrer" };
export default async function ConfirmEmailChangePage({ searchParams }: { searchParams: Promise<{ token?: string; kind?: string }> }) {
  const values = await searchParams;
  const token = typeof values.token === "string" && /^[A-Za-z0-9_-]{43}$/.test(values.token) ? values.token : "";
  const kind = values.kind === "current" || values.kind === "new" ? values.kind : "";
  return <AccountShell title="Confirmar dirección de correo" description="Confirmá solo si solicitaste el cambio desde Mi cuenta."><EmailChangeConfirmation token={token} kind={kind} /></AccountShell>;
}
