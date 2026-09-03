import { createPortal } from 'react-dom';
import { useLayoutEffect, useRef } from 'react';
import gsap from 'gsap';

export function ShiftTrackLoader({ label = 'Loading ShiftTrack' }: { label?: string }) {
  const rootRef = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    if (!rootRef.current) return;

    const ctx = gsap.context(() => {
      const q = gsap.utils.selector(rootRef);
      const logoShell = q('.st-loader-logo-shell');
      const logo = q('.st-loader-logo');
      const rings = q('.st-loader-ring');
      const arcs = q('.st-loader-arc');
      const dots = q('.st-loader-dot');
      const glow = q('.st-loader-core-glow');

      gsap.fromTo(
        logoShell,
        { scale: 0.72, autoAlpha: 0 },
        { scale: 1, autoAlpha: 1, duration: 0.55, ease: 'power3.out' },
      );

      gsap.to(logoShell, {
        y: -8,
        duration: 1.8,
        repeat: -1,
        yoyo: true,
        ease: 'sine.inOut',
      });

      gsap.to(logo, {
        rotate: 360,
        duration: 7,
        repeat: -1,
        ease: 'none',
      });

      gsap.to(glow, {
        scale: 1.15,
        opacity: 0.95,
        duration: 1.5,
        repeat: -1,
        yoyo: true,
        ease: 'sine.inOut',
      });

      rings.forEach((ring, index) => {
        gsap.to(ring, {
          rotate: index % 2 === 0 ? 360 : -360,
          duration: 6 + index * 1.8,
          repeat: -1,
          ease: 'none',
        });
      });

      arcs.forEach((arc, index) => {
        gsap.to(arc, {
          rotate: index % 2 === 0 ? 360 : -360,
          duration: 2.8 + index * 0.45,
          repeat: -1,
          ease: 'none',
        });
      });

      dots.forEach((dot, index) => {
        gsap.to(dot, {
          x: (index - 1) * 16,
          y: index % 2 === 0 ? -12 : 12,
          scale: 0.82 + index * 0.08,
          duration: 1.9 + index * 0.2,
          repeat: -1,
          yoyo: true,
          ease: 'sine.inOut',
        });
      });
    }, rootRef);

    return () => ctx.revert();
  }, []);

  return (
    <div ref={rootRef} className="st-loader-minimal" role="status" aria-label={label}>
      <div className="st-loader-core-glow" />
      <div className="st-loader-ring st-loader-ring-outer" />
      <div className="st-loader-ring st-loader-ring-mid" />
      <div className="st-loader-ring st-loader-ring-inner" />
      <div className="st-loader-arc st-loader-arc-a" />
      <div className="st-loader-arc st-loader-arc-b" />
      <div className="st-loader-arc st-loader-arc-c" />
      <span className="st-loader-dot st-loader-dot-a" />
      <span className="st-loader-dot st-loader-dot-b" />
      <span className="st-loader-dot st-loader-dot-c" />

      <div className="st-loader-logo-shell">
        <img src="/logo.svg" alt="" className="st-loader-logo" />
      </div>
    </div>
  );
}

export function ShiftTrackLoaderOverlay({ label = 'Loading ShiftTrack' }: { label?: string }) {
  const content = (
    <div className="spinner-overlay">
      <ShiftTrackLoader label={label} />
    </div>
  );

  if (typeof document === 'undefined') return content;
  return createPortal(content, document.body);
}
