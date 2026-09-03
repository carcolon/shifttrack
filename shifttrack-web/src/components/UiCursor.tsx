import { useEffect, useRef } from 'react';
import gsap from 'gsap';
import { HandPointing } from 'phosphor-react';

export function UiCursor() {
  const arrowRef = useRef<HTMLDivElement | null>(null);
  const hoverRef = useRef<HTMLDivElement | null>(null);
  const hoveringInteractiveRef = useRef(false);

  useEffect(() => {
    if (window.matchMedia('(pointer: coarse)').matches) return;

    const arrow = arrowRef.current;
    const hover = hoverRef.current;
    if (!arrow || !hover) return;

    const interactiveSelector = [
      'button',
      'a[href]',
      '[role="button"]',
      '.topbar-tab',
      '.topbar-menu-item',
      '.cell.cell-interactive',
      '.calendar-month-day:not(.blank)',
      '[data-ui-cursor="hand"]',
    ].join(', ');

    gsap.set(arrow, {
      autoAlpha: 1,
      scale: 1,
      rotate: -12,
    });
    gsap.set(hover, {
      autoAlpha: 0,
      scale: 0.78,
    });

    const moveArrowX = gsap.quickTo(arrow, 'x', { duration: 0.08, ease: 'power3.out' });
    const moveArrowY = gsap.quickTo(arrow, 'y', { duration: 0.08, ease: 'power3.out' });
    const moveHoverX = gsap.quickTo(hover, 'x', { duration: 0.14, ease: 'power3.out' });
    const moveHoverY = gsap.quickTo(hover, 'y', { duration: 0.14, ease: 'power3.out' });

    const setInteractiveState = (isInteractive: boolean) => {
      if (isInteractive === hoveringInteractiveRef.current) return;

      hoveringInteractiveRef.current = isInteractive;

      if (isInteractive) {
        gsap.to(arrow, { scale: 0.88, rotate: -12, duration: 0.2, ease: 'power2.out' });
        gsap.to(hover, { autoAlpha: 1, scale: 1, duration: 0.2, ease: 'power2.out' });
        return;
      }

      gsap.to(arrow, { scale: 1, rotate: -10, duration: 0.2, ease: 'power2.out' });
      gsap.to(hover, { autoAlpha: 0, scale: 0.78, duration: 0.18, ease: 'power2.out' });
    };

    const move = (event: MouseEvent) => {
      const target = document.elementFromPoint(event.clientX, event.clientY) as HTMLElement | null;
      const isInteractive = !!target?.closest(interactiveSelector);

      gsap.set(arrow, { autoAlpha: 1 });
      moveArrowX(event.clientX);
      moveArrowY(event.clientY);
      moveHoverX(event.clientX);
      moveHoverY(event.clientY);

      setInteractiveState(isInteractive);
    };

    const handleWindowLeave = () => {
      hoveringInteractiveRef.current = false;
      gsap.to(hover, { autoAlpha: 0, scale: 0.78, duration: 0.14, ease: 'power2.out' });
      gsap.to(arrow, { autoAlpha: 0, duration: 0.14, ease: 'power2.out' });
    };

    window.addEventListener('mousemove', move, { passive: true });
    window.addEventListener('mouseleave', handleWindowLeave);
    window.addEventListener('blur', handleWindowLeave);

    return () => {
      window.removeEventListener('mousemove', move);
      window.removeEventListener('mouseleave', handleWindowLeave);
      window.removeEventListener('blur', handleWindowLeave);
    };
  }, []);

  return (
    <>
      <div ref={arrowRef} className="ui-cursor-arrow" aria-hidden="true" />
      <div ref={hoverRef} className="ui-cursor-hover" aria-hidden="true">
        <HandPointing size={16} weight="light" />
      </div>
    </>
  );
}
