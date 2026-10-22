import { redirect } from "next/navigation";
import { AccountShell } from "@/components/account-shell";
import { SessionRenewal } from "@/components/session-renewal";
import { readRefreshToken } from "@/lib/session";

export default async function RenewSessionPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  if (!(await readRefreshToken())) redirect("/login?reason=session-expired");
  const next = (await searchParams).next;
  const destination = next === "/account" || next === "/devices" ? next : "/dashboard";
  return <AccountShell title="Continuá en tu cuenta" description="Estamos comprobando tu sesión para que puedas seguir."><SessionRenewal destination={destination} /></AccountShell>;
}
