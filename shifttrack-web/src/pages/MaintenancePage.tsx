import { useLayoutEffect, useMemo, useRef } from 'react';
import gsap from 'gsap';
import { MAINTENANCE_MESSAGE } from '../lib/constants';

const words = 'ShiftTrack'.split(' ');

export default function MaintenancePage() {
  const rootRef = useRef<HTMLElement | null>(null);
  const letters = useMemo(
    () =>
      words.map((word) =>
        word.split('').map((letter, index) => ({
          letter,
          wobble: index % 2 === 0 ? -1 : 1,
        })),
      ),
    [],
  );

  useLayoutEffect(() => {
    if (!rootRef.current) return;

    const ctx = gsap.context(() => {
      const q = gsap.utils.selector(rootRef);
      const core = q('.maintenance-core');
      const rings = q('.maintenance-orbit-ring');
      const shards = q('.maintenance-shard');
      const particles = q('.maintenance-node');
      const letters = q('.maintenance-title-letter');
      const wordLines = q('.maintenance-title-word');
      const subtitle = q('.maintenance-subtitle');
      const status = q('.maintenance-status');
      const panel = q('.maintenance-panel');
      const beams = q('.maintenance-energy-beam');

      gsap.fromTo(
        panel,
        { autoAlpha: 0, y: 30, scale: 0.96 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.9, ease: 'power3.out' },
      );

      gsap.fromTo(
        wordLines,
        { autoAlpha: 0, y: 22 },
        { autoAlpha: 1, y: 0, duration: 0.45, stagger: 0.08, ease: 'power2.out', delay: 0.08 },
      );

      gsap.fromTo(
        letters,
        {
          yPercent: 120,
          xPercent: () => gsap.utils.random(-22, 22),
          rotateZ: () => gsap.utils.random(-18, 18),
          rotateX: -88,
          scale: 0.82,
          autoAlpha: 0,
        },
        {
          yPercent: 0,
          xPercent: 0,
          rotateZ: 0,
          rotateX: 0,
          scale: 1,
          autoAlpha: 1,
          duration: 0.9,
          stagger: 0.028,
          ease: 'back.out(1.5)',
          delay: 0.16,
        },
      );

      gsap.to(letters, {
        yPercent: 'random(-6, 6)',
        rotateZ: 'random(-3, 3)',
        duration: 'random(2.8, 4.6)',
        ease: 'sine.inOut',
        repeat: -1,
        yoyo: true,
        stagger: {
          each: 0.03,
          from: 'center',
        },
        delay: 1.1,
      });

      gsap.fromTo(
        subtitle,
        { autoAlpha: 0, y: 18 },
        { autoAlpha: 1, y: 0, duration: 0.7, ease: 'power2.out', delay: 0.35 },
      );

      gsap.fromTo(
        status,
        { autoAlpha: 0, y: 14 },
        { autoAlpha: 1, y: 0, duration: 0.55, ease: 'power2.out', delay: 0.45 },
      );

      gsap.fromTo(
        core,
        { scale: 0.86, rotate: -10, autoAlpha: 0 },
        { scale: 1, rotate: 0, autoAlpha: 1, duration: 0.85, ease: 'power3.out' },
      );

      rings.forEach((ring, index) => {
        gsap.to(ring, {
          rotate: index % 2 === 0 ? 360 : -360,
          duration: 10 + index * 2.5,
          repeat: -1,
          ease: 'none',
        });
        gsap.to(ring, {
          scale: index % 2 === 0 ? 1.05 : 0.94,
          duration: 3 + index * 0.7,
          repeat: -1,
          yoyo: true,
          ease: 'sine.inOut',
        });
      });

      shards.forEach((shard, index) => {
        gsap.to(shard, {
          rotate: index % 2 === 0 ? 24 : -24,
          x: index % 2 === 0 ? 28 : -28,
          y: index % 3 === 0 ? -18 : 18,
          duration: 3.2 + index * 0.35,
          repeat: -1,
          yoyo: true,
          ease: 'sine.inOut',
        });
      });

      particles.forEach((particle, index) => {
        gsap.to(particle, {
          x: (index % 2 === 0 ? 1 : -1) * (26 + (index % 3) * 14),
          y: (index % 3 === 0 ? -1 : 1) * (22 + (index % 4) * 12),
          scale: 0.7 + (index % 3) * 0.25,
          duration: 2.8 + index * 0.3,
          repeat: -1,
          yoyo: true,
          ease: 'sine.inOut',
        });
      });

      beams.forEach((beam, index) => {
        gsap.fromTo(
          beam,
          { xPercent: -140, autoAlpha: 0 },
          {
            xPercent: 220,
            autoAlpha: 0.92,
            duration: 3.8 + index * 0.45,
            repeat: -1,
            ease: 'none',
            delay: index * 0.35,
          },
        );
      });
    }, rootRef);

    return () => ctx.revert();
  }, []);

  return (
    <main ref={rootRef} className="maintenance-page">
      <div className="maintenance-backdrop">
        <div className="maintenance-grid" />
        <div className="maintenance-aura maintenance-aura-a" />
        <div className="maintenance-aura maintenance-aura-b" />
        <div className="maintenance-aura maintenance-aura-c" />
        <div className="maintenance-energy-beam maintenance-energy-beam-a" />
        <div className="maintenance-energy-beam maintenance-energy-beam-b" />
        <div className="maintenance-energy-beam maintenance-energy-beam-c" />
        <span className="maintenance-node maintenance-node-a" />
        <span className="maintenance-node maintenance-node-b" />
        <span className="maintenance-node maintenance-node-c" />
        <span className="maintenance-node maintenance-node-d" />
        <span className="maintenance-node maintenance-node-e" />
        <span className="maintenance-node maintenance-node-f" />
      </div>

      <section className="maintenance-panel">
        <div className="maintenance-scene">
          <div className="maintenance-core">
            <div className="maintenance-orbit-ring maintenance-orbit-ring-outer" />
            <div className="maintenance-orbit-ring maintenance-orbit-ring-mid" />
            <div className="maintenance-orbit-ring maintenance-orbit-ring-inner" />
            <span className="maintenance-shard maintenance-shard-a" />
            <span className="maintenance-shard maintenance-shard-b" />
            <span className="maintenance-shard maintenance-shard-c" />
            <div className="maintenance-logo-shell">
              <img src="/logo.svg" alt="ShiftTrack logo" className="maintenance-logo" />
            </div>
          </div>
        </div>

        <div className="maintenance-copy">
          <div className="maintenance-kicker">Live platform upgrade</div>
          <h1 className="maintenance-title" aria-label="ShiftTrack Reawakens">
            {letters.map((word, wordIndex) => (
              <span key={`word-${wordIndex}`} className="maintenance-title-word">
                {word.map(({ letter, wobble }, letterIndex) => (
                  <span
                    key={`${wordIndex}-${letterIndex}-${letter}`}
                    className="maintenance-title-letter"
                    style={{ ['--maintenance-letter-tilt' as string]: wobble }}
                  >
                    {letter}
                  </span>
                ))}
              </span>
            ))}
          </h1>
          <p className="maintenance-subtitle">{MAINTENANCE_MESSAGE}</p>
          <div className="maintenance-status">
            <span className="maintenance-status-dot" />
            We are tuning the ShiftTrack for you. Please stay tuned!
          </div>
        </div>
      </section>
    </main>
  );
}
