"use client";

import { useEffect, useRef, type ReactNode } from "react";

export function AnalysisHistoryDisclosure({ analysisId, expanded, summary, children }: { analysisId: string; expanded: boolean; summary: ReactNode; children: ReactNode }) {
  const ref = useRef<HTMLDetailsElement>(null);
  const id = `analysis-${analysisId}`;
  useEffect(() => {
    function openTarget() {
      if (window.location.hash !== `#${id}` || !ref.current) return;
      ref.current.open = true;
      ref.current.scrollIntoView({ block: "start" });
    }
    openTarget(); window.addEventListener("hashchange", openTarget);
    return () => window.removeEventListener("hashchange", openTarget);
  }, [id]);
  return <details ref={ref} id={id} open={expanded} className="section-card device-analysis history-disclosure"><summary>{summary}</summary>{children}</details>;
}
