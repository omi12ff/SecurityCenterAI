"use client";

import { useEffect, useRef, type ReactNode } from "react";

/** Keep native disclosure controls and open the evidence reached from a report link. */
export function GuidanceDetails({ anchorId, summary, initiallyOpen, children }: {
  anchorId: string; summary: ReactNode; initiallyOpen: boolean; children: ReactNode;
}) {
  const ref = useRef<HTMLDetailsElement>(null);
  useEffect(() => {
    const reveal = (hash: string) => {
      if (hash === `#${anchorId}` && ref.current) ref.current.open = true;
    };
    const onHash = () => reveal(window.location.hash);
    const onLink = (event: MouseEvent) => {
      const target = event.target instanceof Element ? event.target.closest("a[href]") : null;
      if (!(target instanceof HTMLAnchorElement)) return;
      const url = new URL(target.href, window.location.href);
      if (url.origin === window.location.origin && url.pathname === window.location.pathname) reveal(url.hash);
    };
    onHash();
    window.addEventListener("hashchange", onHash);
    document.addEventListener("click", onLink, true);
    return () => {
      window.removeEventListener("hashchange", onHash);
      document.removeEventListener("click", onLink, true);
    };
  }, [anchorId]);
  return <details ref={ref} open={initiallyOpen} className="guidance-disclosure">
    <summary>{summary}</summary><div className="guidance-disclosure-body">{children}</div>
  </details>;
}
