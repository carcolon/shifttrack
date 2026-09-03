import { createPortal } from 'react-dom';
import { useLayoutEffect, useRef, type ReactNode } from 'react';
import gsap from 'gsap';
import { Button } from './ui/Button';

type Variant = 'error' | 'info' | 'warning';

export function Modal({
  title,
  message,
  onClose,
  variant = 'error',
}: {
  title?: string;
  message: string;
  onClose: () => void;
  variant?: Variant;
}) {
  const icon = variant === 'info' ? 'i' : 'X';
  const cardRef = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    if (!cardRef.current) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        cardRef.current,
        { autoAlpha: 0, y: 18, scale: 0.96 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.28, ease: 'power2.out' },
      );
    }, cardRef);

    return () => ctx.revert();
  }, []);

  const content = (
    <div className="modal" role="alertdialog" aria-modal="true">
      <div ref={cardRef} className={`modal-card basic-modal-card tone-${variant}`}>
        <div className="modal-hero">
          <div className={`modal-icon ${variant}`}>{icon}</div>
          {title ? <h2>{title}</h2> : null}
          <p>{message}</p>
        </div>
        <div className="modal-actions">
          <Button variant="primary" onClick={onClose}>
            OK
          </Button>
        </div>
      </div>
    </div>
  );

  if (typeof document === 'undefined') return content;
  return createPortal(content, document.body);
}

type ConfirmProps = {
  title?: string;
  description?: string;
  message?: string;
  onCancel: () => void;
  onOk: () => void;
  icon?: 'warning' | 'info';
};

function renderConfirmMessage(message?: string) {
  if (!message?.trim()) return null;

  const sections = message
    .split(/\n{2,}/)
    .map((section) => section.split('\n').map((line) => line.trim()).filter(Boolean))
    .filter((section) => section.length > 0);

  return (
    <div className="confirm-message" aria-label="Confirmation details">
      {sections.map((section, sectionIndex) => {
        const text = section.join(' ');
        const isWarning = /warning|exceeds|limit|impact|affect coverage/i.test(text);
        const isSchedule = /^(Period|Shift:|Monday:|Tuesday:|Wednesday:|Thursday:|Friday:|Saturday:|Sunday:)/i.test(section[0] ?? '');
        return (
          <div
            key={`${sectionIndex}-${section[0]}`}
            className={[
              'confirm-message-section',
              isWarning ? 'warning' : '',
              isSchedule ? 'schedule' : '',
            ].filter(Boolean).join(' ')}
          >
            {section.map((line, lineIndex) => {
              const isTitle = lineIndex === 0 && /^(Warning:|Period|Employee:)/i.test(line);
              return (
                <div key={`${lineIndex}-${line}`} className={isTitle ? 'confirm-message-line title' : 'confirm-message-line'}>
                  {line}
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

export function ConfirmModal({
  title = 'Are you sure you want to delete this employee?',
  description = 'This action is permanent and cannot be undone.',
  message,
  onCancel,
  onOk,
  icon = 'warning',
}: ConfirmProps) {
  const cardRef = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    if (!cardRef.current) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        cardRef.current,
        { autoAlpha: 0, y: 18, scale: 0.96 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.28, ease: 'power2.out' },
      );
    }, cardRef);

    return () => ctx.revert();
  }, []);

  const content = (
    <div className="modal" role="alertdialog" aria-modal="true">
      <div ref={cardRef} className={`modal-card confirm-modal-card tone-${icon}`}>
        <div className="modal-hero">
          <div className={`modal-icon ${icon}`}>{icon === 'info' ? 'i' : '!'}</div>
          <h2>{title}</h2>
          {description && <p>{description}</p>}
        </div>
        {renderConfirmMessage(message)}
        <div className="modal-actions">
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="primary" onClick={onOk}>
            OK
          </Button>
        </div>
      </div>
    </div>
  );

  if (typeof document === 'undefined') return content;
  return createPortal(content, document.body);
}

export function Card({ children }: { children: ReactNode }) {
  return <div className="card">{children}</div>;
}

export function ModalShell({
  children,
  className = '',
  onBackdropClick,
  ariaLabel,
}: {
  children: ReactNode;
  className?: string;
  onBackdropClick?: () => void;
  ariaLabel?: string;
}) {
  const cardRef = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    if (!cardRef.current) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        cardRef.current,
        { autoAlpha: 0, y: 18, scale: 0.96 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.28, ease: 'power2.out' },
      );
    }, cardRef);

    return () => ctx.revert();
  }, []);

  const content = (
    <div className="modal" role="dialog" aria-modal="true" aria-label={ariaLabel} onClick={onBackdropClick}>
      <div ref={cardRef} className={`modal-card ${className}`.trim()} onClick={(e) => e.stopPropagation()}>
        {children}
      </div>
    </div>
  );

  if (typeof document === 'undefined') return content;
  return createPortal(content, document.body);
}
