import { motion } from 'motion/react';
import type { ReactNode } from 'react';

const shards = Array.from({ length: 22 }, (_, i) => ({
  id: i,
  left: `${(i * 9 + 5) % 100}%`,
  top: `${(i * 13 + 11) % 100}%`,
  delay: (i % 7) * 0.25,
  duration: 5 + (i % 5) * 0.8,
}));

export function AuthAnimatedShell({ children }: { children: ReactNode }) {
  return (
    <div className="page auth-animated-page">
      <div className="auth-bg-grid" />
      <div className="auth-bg-glow auth-bg-glow-a" />
      <div className="auth-bg-glow auth-bg-glow-b" />
      <div className="auth-bg-glow auth-bg-glow-c" />

      {shards.map((shard) => (
        <motion.span
          key={shard.id}
          className="auth-particle"
          style={{ left: shard.left, top: shard.top }}
          initial={{ opacity: 0, scale: 0.6, y: 16, rotate: 0 }}
          animate={{ opacity: [0, 0.65, 0], scale: [0.6, 1, 0.65], y: [16, -26, 16], rotate: [0, 90, 180] }}
          transition={{ duration: shard.duration, repeat: Infinity, delay: shard.delay, ease: 'easeInOut' }}
        />
      ))}

      <motion.div
        className="auth-card-shell"
        initial={{ opacity: 0, y: 26, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.55, ease: 'easeOut' }}
      >
        <div className="auth-card-rings auth-card-rings-a" />
        <div className="auth-card-rings auth-card-rings-b" />
        {children}
      </motion.div>
    </div>
  );
}

export default AuthAnimatedShell;
