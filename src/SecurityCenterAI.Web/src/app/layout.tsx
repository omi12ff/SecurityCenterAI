import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";
import "./design-system.css";

export const metadata: Metadata = {
  title: { default: "Security Center AI", template: "%s · Security Center AI" },
  description: "Tu espacio para entender la seguridad y el estado de tu PC, paso a paso.",
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return <html lang="es"><body>{children}</body></html>;
}
