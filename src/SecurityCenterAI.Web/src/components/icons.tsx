import type { ReactNode, SVGProps } from "react";

type IconName =
  | "shield"
  | "grid"
  | "history"
  | "user"
  | "logout"
  | "arrow-right"
  | "mail"
  | "lock"
  | "eye"
  | "eye-off"
  | "monitor"
  | "info"
  | "refresh"
  | "warning";

export function Icon({
  name,
  ...props
}: SVGProps<SVGSVGElement> & { name: IconName }) {
  const paths: Record<IconName, ReactNode> = {
    shield: <><path d="M12 3 4.5 6v5.2c0 4.5 3 7.5 7.5 9.8 4.5-2.3 7.5-5.3 7.5-9.8V6L12 3Z" /><path d="m8.5 11.7 2.3 2.3 4.8-4.9" /></>,
    grid: <><rect x="3.5" y="3.5" width="6.5" height="6.5" rx="1.5" /><rect x="14" y="3.5" width="6.5" height="6.5" rx="1.5" /><rect x="3.5" y="14" width="6.5" height="6.5" rx="1.5" /><rect x="14" y="14" width="6.5" height="6.5" rx="1.5" /></>,
    history: <><path d="M3.5 10a8.5 8.5 0 1 1 .7 6.1M3.5 5.5V10H8" /><path d="M12 7v5l3.3 2" /></>,
    user: <><circle cx="12" cy="8" r="3.5" /><path d="M4.5 21v-2a7.5 7.5 0 0 1 15 0v2" /></>,
    logout: <><path d="M10 4H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h5M9 12h12m-4-4 4 4-4 4" /></>,
    "arrow-right": <><path d="M4 12h16m-6-6 6 6-6 6" /></>,
    mail: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m3 7 9 6 9-6" /></>,
    lock: <><rect x="5" y="10" width="14" height="11" rx="2" /><path d="M8 10V7a4 4 0 0 1 8 0v3m-4 5v2" /></>,
    eye: <><path d="M2 12s3.7-7 10-7 10 7 10 7-3.7 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></>,
    "eye-off": <><path d="m3 3 18 18M10.6 5.1A10.5 10.5 0 0 1 12 5c6.3 0 10 7 10 7a20.3 20.3 0 0 1-3.3 4.1M6.2 6.2A20.1 20.1 0 0 0 2 12s3.7 7 10 7a11.4 11.4 0 0 0 5.1-1.3M9.9 9.9a3 3 0 0 0 4.2 4.2" /></>,
    monitor: <><rect x="3" y="4" width="18" height="13" rx="2" /><path d="M8 21h8m-4-4v4" /></>,
    info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6m0-10v.1" /></>,
    refresh: <><path d="M20.5 10a8.5 8.5 0 0 0-14.6-4L3 9m0-5v5h5M3.5 14a8.5 8.5 0 0 0 14.6 4l2.9-3m0 5v-5h-5" /></>,
    warning: <><path d="M10.2 4.2 2.8 17a2.1 2.1 0 0 0 1.8 3.2h14.8a2.1 2.1 0 0 0 1.8-3.2L13.8 4.2a2.1 2.1 0 0 0-3.6 0Z" /><path d="M12 9v4m0 3v.1" /></>,
  };

  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      {paths[name]}
    </svg>
  );
}
