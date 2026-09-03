import type { ReactNode } from 'react';

export function Field({
  label,
  error,
  children,
}: {
  label: ReactNode;
  error?: ReactNode;
  children: ReactNode;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      <small className="error">{error}</small>
    </label>
  );
}
