import { useLayoutEffect, useRef } from 'react';
import gsap from 'gsap';

type AuthTransitionCurtainProps = {
  mode?: 'login' | 'logout';
};

export function AuthTransitionCurtain({ mode = 'login' }: AuthTransitionCurtainProps) {
  const letters = 'ShiftTrack'.split('');
  const rootRef = useRef<HTMLDivElement | null>(null);
  const stageRef = useRef<HTMLDivElement | null>(null);
  const logoShellRef = useRef<HTMLDivElement | null>(null);
  const logoRef = useRef<HTMLImageElement | null>(null);
  const haloRef = useRef<HTMLDivElement | null>(null);
  const kickerRef = useRef<HTMLDivElement | null>(null);
  const wordmarkRef = useRef<HTMLDivElement | null>(null);
  const lettersRef = useRef<Array<HTMLSpanElement | null>>([]);
  const underlineRef = useRef<HTMLDivElement | null>(null);
  const shimmerRef = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    const ctx = gsap.context(() => {
      const isLogout = mode === 'logout';
      const tl = gsap.timeline({ defaults: { ease: 'power3.out' } });

      gsap.set(rootRef.current, { opacity: 1 });
      gsap.set(stageRef.current, { opacity: 0, yPercent: 5, scale: isLogout ? 0.94 : 0.9 });
      gsap.set(logoShellRef.current, { opacity: 0, scale: isLogout ? 0.82 : 0.74, rotate: isLogout ? -8 : -14 });
      gsap.set(logoRef.current, { opacity: 0, scale: isLogout ? 0.88 : 0.78, rotate: isLogout ? 8 : 14 });
      gsap.set(haloRef.current, { opacity: 0, scale: 0.72 });
      gsap.set(kickerRef.current, { opacity: 0, y: 18 });
      gsap.set(wordmarkRef.current, { opacity: 1 });
      gsap.set(lettersRef.current, {
        opacity: 0,
        y: isLogout ? 26 : 34,
        rotateX: isLogout ? -26 : -36,
        rotateY: isLogout ? 8 : 12,
        scale: isLogout ? 0.92 : 0.84,
        transformOrigin: '50% 100%',
      });
      gsap.set(underlineRef.current, { scaleX: 0, transformOrigin: 'left center' });
      gsap.set(shimmerRef.current, { xPercent: -135, opacity: 0 });

      tl.to(stageRef.current, { opacity: 1, yPercent: 0, scale: 1, duration: isLogout ? 0.45 : 0.56 })
        .to(haloRef.current, { opacity: 1, scale: 1.08, duration: isLogout ? 0.64 : 0.78 }, '<')
        .to(logoShellRef.current, { opacity: 1, scale: 1, rotate: 0, duration: isLogout ? 0.6 : 0.72 }, '<0.04')
        .to(logoRef.current, { opacity: 1, scale: 1, rotate: 0, duration: isLogout ? 0.6 : 0.72 }, '<')
        .to(kickerRef.current, { opacity: 1, y: 0, duration: 0.42 }, isLogout ? '<0.04' : '<0.08')
        .to(
          lettersRef.current,
          {
            opacity: 1,
            y: 0,
            rotateX: 0,
            rotateY: 0,
            scale: 1,
            duration: isLogout ? 0.5 : 0.62,
            stagger: 0.038,
            ease: 'back.out(1.45)',
          },
          isLogout ? '<0.06' : '<0.1',
        )
        .to(underlineRef.current, { scaleX: 1, duration: 0.42 }, '<0.02')
        .to(shimmerRef.current, { opacity: 0.82, xPercent: 125, duration: isLogout ? 0.86 : 1.05, ease: 'power2.inOut' }, '<-0.12');

      if (!isLogout) {
        tl.to(stageRef.current, { opacity: 0, scale: 1.05, yPercent: -3, duration: 0.46, ease: 'power2.in' }, '+=0.14')
          .to(rootRef.current, { opacity: 0, duration: 0.34, ease: 'power2.out' }, '<0.04');
      }
    }, rootRef);

    return () => ctx.revert();
  }, [mode]);

  return (
    <div ref={rootRef} className={`auth-transition-curtain auth-transition-curtain-${mode}`} aria-hidden="true">
      <div className="auth-transition-backdrop">
        <div className="auth-transition-grid" />
        <div className="auth-transition-glow auth-transition-glow-a" />
        <div className="auth-transition-glow auth-transition-glow-b" />
        <div className="auth-transition-glow auth-transition-glow-c" />
        <div ref={shimmerRef} className="auth-transition-shimmer" />
      </div>
      <div ref={stageRef} className="auth-transition-stage">
        <div ref={haloRef} className="auth-transition-halo" />
        <div ref={logoShellRef} className="auth-transition-logo-shell">
          <img ref={logoRef} src="/logo.svg" alt="" className="auth-transition-logo-mark" />
        </div>
        <div className="auth-transition-copy">
          <div ref={kickerRef} className="auth-transition-kicker">
            {mode === 'logout' ? 'See You Soon' : 'Welcome Back'}
          </div>
          <div ref={wordmarkRef} className="auth-transition-wordmark" aria-label="ShiftTrack">
            {letters.map((letter, index) => (
              <span
                key={`${letter}-${index}`}
                ref={(node) => {
                  lettersRef.current[index] = node;
                }}
                className="auth-transition-letter"
              >
                {letter}
              </span>
            ))}
          </div>
          <div ref={underlineRef} className="auth-transition-underline" />
        </div>
      </div>
    </div>
  );
}

export default AuthTransitionCurtain;
