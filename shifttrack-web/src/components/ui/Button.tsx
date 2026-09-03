import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn } from '../../lib/cn';

type ButtonVariant = 'primary' | 'ghost' | 'dangerGhost';
type ButtonSize = 'sm' | 'md';

const legacyVariantClass: Record<ButtonVariant, string> = {
  primary: 'primary',
  ghost: 'ghost',
  dangerGhost: 'ghost danger-ghost',
};

const tailwindVariantClass: Record<ButtonVariant, string> = {
  primary: 'tw:rounded-lg tw:border tw:border-brand-600 tw:bg-brand-600 tw:text-white',
  ghost: 'tw:rounded-lg tw:border tw:border-brand-600 tw:bg-transparent tw:text-brand-600',
  dangerGhost: 'tw:rounded-lg tw:border tw:border-danger-700 tw:bg-transparent tw:text-danger-700',
};

const sizeClass: Record<ButtonSize, string> = {
  sm: 'small tw:min-h-9 tw:px-3 tw:text-sm',
  md: 'tw:min-h-10 tw:px-4 tw:text-sm',
};

export function Button({
  children,
  className,
  variant = 'primary',
  size = 'md',
  active = false,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
  variant?: ButtonVariant;
  size?: ButtonSize;
  active?: boolean;
}) {
  return (
    <button
      type={type}
      data-ui-cursor="hand"
      data-ui-button-variant={variant}
      className={cn(
        'st-shine-button',
        legacyVariantClass[variant],
        tailwindVariantClass[variant],
        sizeClass[size],
        active && 'active',
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
}
