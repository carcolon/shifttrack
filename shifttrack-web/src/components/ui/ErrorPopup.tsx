import { Button } from './Button';
import { createPortal } from 'react-dom';

type Props = {
  message: string | null;
  onClose: () => void;
  title?: string;
  tone?: 'error' | 'warning' | 'info';
};

const toneContent = {
  error: { icon: '✕', kicker: 'ShiftTrack error' },
  warning: { icon: '!', kicker: 'ShiftTrack warning' },
  info: { icon: 'i', kicker: 'ShiftTrack info' },
} as const;

export function ErrorPopup({ message, onClose, title = 'Something went wrong', tone = 'error' }: Props) {
  if (!message) return null;

  const content = toneContent[tone];
  const popup = (
    <div className={`error-popup-backdrop tone-${tone}`} role="presentation" onClick={onClose}>
      <div className={`error-popup-orb error-popup-orb-a tone-${tone}`} aria-hidden="true" />
      <div className={`error-popup-orb error-popup-orb-b tone-${tone}`} aria-hidden="true" />
      <div
        className={`error-popup-card tone-${tone}`}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="error-popup-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="error-popup-header">
          <div className="error-popup-icon-wrap">
            <div className={`error-popup-icon-ring tone-${tone}`} aria-hidden="true" />
            <div className={`error-popup-icon tone-${tone}`} aria-hidden="true">
              {content.icon}
            </div>
          </div>
          <div className="error-popup-headline">
            <span className="error-popup-kicker">{content.kicker}</span>
            <h3 id="error-popup-title">{title}</h3>
          </div>
        </div>
        <p>{message}</p>
        <div className={`error-popup-accent tone-${tone}`} aria-hidden="true" />
        <div className="error-popup-actions">
          <Button variant="primary" onClick={onClose}>
            Close
          </Button>
        </div>
      </div>
    </div>
  );

  if (typeof document === 'undefined') {
    return popup;
  }

  return createPortal(popup, document.body);
}
