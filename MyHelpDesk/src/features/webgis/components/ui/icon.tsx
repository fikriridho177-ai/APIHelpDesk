import type { ReactNode, SVGProps } from "react";

const shapes = {
  location: <><circle cx="12" cy="12" r="6" /><circle cx="12" cy="12" r="2" /><path d="M12 2v4M12 18v4M2 12h4M18 12h4" /></>,
  map: <><path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2Z" /><path d="M9 3v16M15 5v16" /></>,
  layers: <><path d="m12 3 10 6-10 6L2 9Z" /><path d="m3 13 9 5 9-5M3 17l9 5 9-5" /></>,
  users: <><circle cx="9" cy="8" r="3" /><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M21 21v-3a6 6 0 0 0-3-5.2" /></>,
  upload: <><path d="M12 16V3m-5 5 5-5 5 5M4 16v4a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-4" /></>,
  settings: <><path d="m10 3-.6 2.3-2 .9-2.1-.7-2 3.5 1.6 1.7v2.6L3.3 15l2 3.5 2.1-.7 2 .9.6 2.3h4l.6-2.3 2-.9 2.1.7 2-3.5-1.6-1.7v-2.6L20.7 9l-2-3.5-2.1.7-2-.9L14 3Z" /><circle cx="12" cy="12" r="3" /></>,
  search: <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>,
  "chevron-down": <path d="m6 9 6 6 6-6" />,
  "chevron-right": <path d="m9 6 6 6-6 6" />,
  "arrow-up": <path d="M12 20V4m-6 6 6-6 6 6" />,
  "arrow-down": <path d="M12 4v16m-6-6 6 6 6-6" />,
  "arrow-right": <path d="M4 12h16m-6-6 6 6-6 6" />,
  logout: <><path d="M9 4H4v16h5M9 12h12m-5-5 5 5-5 5" /></>,
  shield: <><path d="m12 3 8 3v6c0 4-3 7-8 10-5-3-8-6-8-10V6Z" /><path d="m8 12 3 3 5-6" /></>,
  check: <path d="m5 12 4 4L19 6" />,
  clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
  history: <><path d="M3 11a9 9 0 1 1 2.6 7M3 4v7h7" /><path d="M12 7v5l3 2" /></>,
  x: <path d="m6 6 12 12M6 18 18 6" />,
  refresh: <><path d="M20 7a9 9 0 0 0-15-2L2 8m0-6v6h6M4 17a9 9 0 0 0 15 2l3-3m0 6v-6h-6" /></>,
  eye: <><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></>,
  "eye-off": <><path d="m3 3 18 18M10 5a12 12 0 0 1 2 0c6.5 0 10 7 10 7a17 17 0 0 1-3 4M6 6a19 19 0 0 0-4 6s3.5 7 10 7a12 12 0 0 0 5-1M10 10a3 3 0 0 0 4 4" /></>,
  sliders: <><path d="M4 3v4m0 4v10M12 3v10m0 4v4M20 3v2m0 4v12M1 7h6v4H1ZM9 13h6v4H9ZM17 5h6v4h-6Z" /></>,
  ruler: <><path d="m3 16 13-13 5 5L8 21Z" /><path d="m13 6 2 2M9 10l2 2M5 14l2 2" /></>,
  polygon: <><path d="m5 5 14 2-2 13-14-4Z" /><path d="M3 3h4v4H3ZM17 5h4v4h-4ZM15 18h4v4h-4ZM1 14h4v4H1Z" fill="var(--surface, white)" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v6M12 7h.01" /></>,
  trash: <><path d="M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7" /></>,
  edit: <><path d="m15 4 5 5M3 21l5-1L21 7l-4-4L4 16Z" /></>,
  "zoom-in": <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5M7 10.5h7M10.5 7v7" /></>,
  menu: <path d="M4 6h16M4 12h16M4 18h16" />,
  folder: <path d="M3 5h6l2 3h10v12H3Z" />,
  pin: <><path d="M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 1 1 14 0Z" /><circle cx="12" cy="10" r="2.5" /></>,
  home: <><path d="m2 10 10-8 10 8M5 8v13h14V8M9 21v-8h6v8" /></>,
  globe: <><circle cx="12" cy="12" r="9" /><ellipse cx="12" cy="12" rx="4" ry="9" /><path d="M3 12h18" /></>,
} satisfies Record<string, ReactNode>;

export type IconName = keyof typeof shapes;

type IconProps = Omit<SVGProps<SVGSVGElement>, "name"> & {
  name: IconName;
  size?: number;
};

/** Decorative by default; give an icon-only control its own accessible name. */
export function Icon({ name, size = 18, ...props }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {shapes[name]}
    </svg>
  );
}
