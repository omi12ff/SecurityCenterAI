"use client";

import { useEffect, useRef, type ReactNode } from "react";

export function SidebarNav({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLElement>(null);
  useEffect(() => {
    const nav = ref.current;
    if (!nav) return;
    const keepCurrentVisible = () => {
      const current = nav.querySelector<HTMLElement>("[aria-current]");
      if (!current || nav.scrollWidth <= nav.clientWidth) return;
      const offset = current.getBoundingClientRect().left - nav.getBoundingClientRect().left;
      nav.scrollLeft += offset - (nav.clientWidth - current.clientWidth) / 2;
    };
    keepCurrentVisible();
    const observer = new ResizeObserver(keepCurrentVisible);
    observer.observe(nav);
    return () => observer.disconnect();
  }, []);
  return <nav ref={ref} className="sidebar-nav" aria-label="Secciones de tu espacio">{children}</nav>;
}
