import { cn } from '../../lib/cn';

const toneClasses: Record<string, string> = {
  pending: 'status-badge status-pending tw:rounded-full tw:px-2.5 tw:py-1 tw:text-xs tw:font-bold',
  approved: 'status-badge status-approved tw:rounded-full tw:px-2.5 tw:py-1 tw:text-xs tw:font-bold',
  denied: 'status-badge status-denied tw:rounded-full tw:px-2.5 tw:py-1 tw:text-xs tw:font-bold',
  canceled: 'status-badge status-canceled tw:rounded-full tw:px-2.5 tw:py-1 tw:text-xs tw:font-bold',
};

export function StatusBadge({ status, className }: { status: string; className?: string }) {
  const normalized = status.trim().toLowerCase();
  const label = normalized
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');

  return <span className={cn(toneClasses[normalized] ?? 'status-badge', className)}>{label}</span>;
}
