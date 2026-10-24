import Link from "next/link";

export function InformationLinks() {
  return <nav className="information-links" aria-label="Información y ayuda">
    <Link href="/privacy">Privacidad</Link><Link href="/terms">Términos</Link><Link href="/report-problem">Ayuda y errores</Link>
  </nav>;
}
