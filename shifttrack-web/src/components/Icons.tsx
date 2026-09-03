import { BarChart3, CalendarDays, ChevronDown, Eye, EyeOff, Grid2X2, Hourglass, RefreshCw, UsersRound } from 'lucide-react';

export function ScheduleIcon({ color = 'white', size = 18 }: { color?: string; size?: number } = {}) {
  return <CalendarDays size={size} strokeWidth={2.4} color={color} />;
}

export function TeamIcon({ color = 'white', size = 18 }: { color?: string; size?: number } = {}) {
  return <UsersRound size={size} strokeWidth={2.4} color={color} />;
}

export function RequestsIcon({ color = 'white', size = 18 }: { color?: string; size?: number } = {}) {
  return <Hourglass size={size} strokeWidth={2.4} color={color} />;
}

export function SwapIcon({ size = 18, color = 'white' }: { size?: number; color?: string }) {
  return <RefreshCw size={size} strokeWidth={2.4} color={color} />;
}

export function AppsIcon({ color = 'white', size = 18 }: { color?: string; size?: number } = {}) {
  return <Grid2X2 size={size} strokeWidth={2.4} color={color} />;
}

export function ReportsIcon({ color = 'white', size = 18 }: { color?: string; size?: number } = {}) {
  return <BarChart3 size={size} strokeWidth={2.4} color={color} />;
}

export function CaretDownIcon({ color = 'white', size = 16 }: { color?: string; size?: number } = {}) {
  return <ChevronDown size={size} strokeWidth={2.4} color={color} />;
}

export function EyeIcon() {
  return <Eye size={18} strokeWidth={2.1} />;
}

export function EyeOffIcon() {
  return <EyeOff size={18} strokeWidth={2.1} />;
}
