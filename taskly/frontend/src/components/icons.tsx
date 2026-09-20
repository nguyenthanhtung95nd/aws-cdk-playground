// Inline SVG icons - no icon dependency, styled via currentColor.
const base = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.7,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  'aria-hidden': true,
};

export function EditIcon() {
  return (
    <svg {...base}>
      <path d="M4 20h4L18.5 9.5a2.12 2.12 0 0 0-3-3L5 17v3z" />
    </svg>
  );
}

export function TrashIcon() {
  return (
    <svg {...base}>
      <path d="M4 7h16M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2m1 0v13a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V7" />
    </svg>
  );
}

export function LogoutIcon() {
  return (
    <svg {...base}>
      <path d="M15 12H4m0 0 4-4m-4 4 4 4M13 4h5a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-5" />
    </svg>
  );
}
