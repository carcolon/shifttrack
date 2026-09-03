import { motion } from 'motion/react';

export function AuthBrand({ subtitle }: { subtitle: string }) {
  return (
    <header className="brand auth-brand">
      <div className="auth-brand-logo-shell">
        <div className="auth-brand-ring auth-brand-ring-a" />
        <div className="auth-brand-ring auth-brand-ring-b" />
        <motion.img
          src="/logo.svg"
          alt="ShiftTrack logo"
          className="auth-brand-logo"
          animate={{ y: [0, -4, 0], rotate: [0, -1.25, 1.25, 0] }}
          transition={{ duration: 3.1, repeat: Infinity, ease: 'easeInOut' }}
        />
      </div>
      <div>
        <div className="brand-name">ShiftTrack</div>
        <div className="brand-subtitle">{subtitle}</div>
      </div>
    </header>
  );
}

export default AuthBrand;
