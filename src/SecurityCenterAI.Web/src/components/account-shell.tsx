import Link from "next/link";
import type { ReactNode } from "react";
import { Icon } from "./icons";
import { InformationLinks } from "./information-links";

export function AccountShell({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return <main className="standalone-page"><section className="standalone-card account-card">
    <Link className="account-brand" href="/login"><Icon name="shield" /><span>Security Center AI</span></Link>
    <h1>{title}</h1><p>{description}</p>{children}<footer className="account-information-footer"><InformationLinks /></footer>
  </section></main>;
}
