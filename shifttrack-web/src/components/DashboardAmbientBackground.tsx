import { useLayoutEffect, useRef } from 'react';
import gsap from 'gsap';

const ambientNodes = [
  { top: '8%', left: '12%', size: 18 },
  { top: '16%', left: '28%', size: 14 },
  { top: '11%', left: '52%', size: 12 },
  { top: '22%', left: '74%', size: 20 },
  { top: '34%', left: '18%', size: 16 },
  { top: '41%', left: '61%', size: 12 },
  { top: '56%', left: '82%', size: 18 },
  { top: '68%', left: '24%', size: 14 },
  { top: '76%', left: '48%', size: 16 },
  { top: '84%', left: '72%', size: 12 },
];

export function DashboardAmbientBackground() {
  const rootRef = useRef<HTMLDivElement | null>(null);
  const beamARef = useRef<HTMLDivElement | null>(null);
  const beamBRef = useRef<HTMLDivElement | null>(null);
  const glowARef = useRef<HTMLDivElement | null>(null);
  const glowBRef = useRef<HTMLDivElement | null>(null);
  const glowCRef = useRef<HTMLDivElement | null>(null);
  const coreRef = useRef<HTMLDivElement | null>(null);
  const ringOuterRef = useRef<HTMLDivElement | null>(null);
  const ringInnerRef = useRef<HTMLDivElement | null>(null);
  const traceRefs = useRef<Array<HTMLDivElement | null>>([]);
  const nodeRefs = useRef<Array<HTMLDivElement | null>>([]);

  useLayoutEffect(() => {
    if (!rootRef.current) return;

    const ctx = gsap.context(() => {
      gsap.set(nodeRefs.current, { transformOrigin: '50% 50%' });
      gsap.set(traceRefs.current, { transformOrigin: '0% 50%' });

      if (glowARef.current) {
        gsap.to(glowARef.current, {
          x: 180,
          y: 72,
          scale: 1.22,
          rotation: 18,
          duration: 8.4,
          ease: 'sine.inOut',
          repeat: -1,
          yoyo: true,
        });
      }

      if (glowBRef.current) {
        gsap.to(glowBRef.current, {
          x: -210,
          y: 108,
          scale: 1.26,
          rotation: -24,
          duration: 9.6,
          ease: 'sine.inOut',
          repeat: -1,
          yoyo: true,
        });
      }

      if (glowCRef.current) {
        gsap.to(glowCRef.current, {
          x: 120,
          y: -84,
          scale: 1.18,
          rotation: 14,
          duration: 10.2,
          ease: 'sine.inOut',
          repeat: -1,
          yoyo: true,
        });
      }

      if (beamARef.current) {
        gsap.fromTo(
          beamARef.current,
          { xPercent: -60, yPercent: -10, autoAlpha: 0.25, rotation: -14 },
          {
            xPercent: 45,
            yPercent: 10,
            autoAlpha: 0.9,
            rotation: 10,
            duration: 6.8,
            ease: 'power1.inOut',
            repeat: -1,
            yoyo: true,
          },
        );
      }

      if (beamBRef.current) {
        gsap.fromTo(
          beamBRef.current,
          { xPercent: 55, yPercent: 8, autoAlpha: 0.18, rotation: 18 },
          {
            xPercent: -42,
            yPercent: -12,
            autoAlpha: 0.72,
            rotation: -12,
            duration: 8.1,
            ease: 'power1.inOut',
            repeat: -1,
            yoyo: true,
          },
        );
      }

      if (coreRef.current) {
        gsap.to(coreRef.current, {
          scale: 1.1,
          autoAlpha: 0.95,
          duration: 2.6,
          ease: 'sine.inOut',
          repeat: -1,
          yoyo: true,
        });
      }

      if (ringOuterRef.current) {
        gsap.to(ringOuterRef.current, {
          rotation: 360,
          scale: 1.08,
          duration: 18,
          ease: 'none',
          repeat: -1,
        });
      }

      if (ringInnerRef.current) {
        gsap.to(ringInnerRef.current, {
          rotation: -360,
          scale: 1.14,
          duration: 14,
          ease: 'none',
          repeat: -1,
        });
      }

      nodeRefs.current.forEach((node, index) => {
        if (!node) return;
        gsap.fromTo(
          node,
          {
            y: 0,
            x: 0,
            scale: 0.85,
            rotation: -8,
            autoAlpha: 0.45,
          },
          {
            y: index % 2 === 0 ? -34 : 30,
            x: index % 3 === 0 ? 18 : -14,
            scale: 1.24,
            rotation: index % 2 === 0 ? 16 : -14,
            autoAlpha: 0.95,
            duration: 2.8 + index * 0.22,
            ease: 'sine.inOut',
            repeat: -1,
            yoyo: true,
            delay: index * 0.08,
          },
        );
      });

      traceRefs.current.forEach((trace, index) => {
        if (!trace) return;
        gsap.fromTo(
          trace,
          {
            xPercent: -30,
            scaleX: 0.4,
            autoAlpha: 0.18,
          },
          {
            xPercent: 30,
            scaleX: 1,
            autoAlpha: 0.88,
            duration: 4.8 + index * 0.7,
            ease: 'sine.inOut',
            repeat: -1,
            yoyo: true,
            delay: index * 0.4,
          },
        );
      });
    }, rootRef);

    return () => ctx.revert();
  }, []);

  return (
    <div ref={rootRef} className="dashboard-ambient" aria-hidden="true">
      <div className="dashboard-ambient-grid" />
      <div ref={glowARef} className="dashboard-ambient-glow glow-a" />
      <div ref={glowBRef} className="dashboard-ambient-glow glow-b" />
      <div ref={glowCRef} className="dashboard-ambient-glow glow-c" />
      <div ref={beamARef} className="dashboard-ambient-beam beam-a" />
      <div ref={beamBRef} className="dashboard-ambient-beam beam-b" />

      <div className="dashboard-ambient-orbit">
        <div ref={ringOuterRef} className="dashboard-ambient-ring ring-outer" />
        <div ref={ringInnerRef} className="dashboard-ambient-ring ring-inner" />
        <div ref={coreRef} className="dashboard-ambient-core" />
      </div>

      <div className="dashboard-ambient-traces">
        {[0, 1, 2].map((trace) => (
          <div
            key={`trace-${trace}`}
            ref={(element) => {
              traceRefs.current[trace] = element;
            }}
            className={`dashboard-ambient-trace trace-${trace + 1}`}
          />
        ))}
      </div>

      {ambientNodes.map((node, index) => (
        <div
          key={`node-${index}`}
          ref={(element) => {
            nodeRefs.current[index] = element;
          }}
          className="dashboard-ambient-node"
          style={{
            top: node.top,
            left: node.left,
            width: `${node.size}px`,
            height: `${node.size}px`,
          }}
        />
      ))}
    </div>
  );
}
