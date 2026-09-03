import { useCallback, useEffect, useState } from 'react';

export type AppToastTone = 'info' | 'success' | 'error';

export type AppToastMessage = {
  id: string;
  tone: AppToastTone;
  title?: string;
  message: string;
  actionLabel?: string;
  onAction?: () => void | Promise<void>;
  autoDismissMs?: number | null;
};

export function useToastStack() {
  const [toasts, setToasts] = useState<AppToastMessage[]>([]);

  const dismissToast = useCallback((id: string) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const pushToast = useCallback((toast: Omit<AppToastMessage, 'id'>) => {
    const id = `${toast.tone}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
    setToasts((current) => [...current, { ...toast, id }]);
    return id;
  }, []);

  return { toasts, pushToast, dismissToast };
}

export function ToastStack({
  toasts,
  onDismiss,
}: {
  toasts: AppToastMessage[];
  onDismiss: (id: string) => void;
}) {
  return (
    <div className="app-toast-stack" aria-live="polite" aria-atomic="true">
      {toasts.map((toast) => (
        <ToastItem key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  );
}

function ToastItem({
  toast,
  onDismiss,
}: {
  toast: AppToastMessage;
  onDismiss: (id: string) => void;
}) {
  useEffect(() => {
    if (toast.autoDismissMs === null) return;
    const timeout = window.setTimeout(() => onDismiss(toast.id), toast.autoDismissMs ?? 3600);
    return () => window.clearTimeout(timeout);
  }, [onDismiss, toast.autoDismissMs, toast.id]);

  const runAction = async () => {
    try {
      await toast.onAction?.();
      onDismiss(toast.id);
    } catch {
      // Action handlers are responsible for surfacing their own errors.
    }
  };

  return (
    <div className={`app-toast ${toast.tone}`} role="status">
      <div className="app-toast-copy">
        {toast.title && <strong>{toast.title}</strong>}
        <span>{toast.message}</span>
      </div>
      <div className="app-toast-actions">
        {toast.actionLabel && toast.onAction && (
          <button type="button" className="app-toast-action" onClick={runAction}>
            {toast.actionLabel}
          </button>
        )}
        <button type="button" className="app-toast-close" aria-label="Dismiss notification" onClick={() => onDismiss(toast.id)}>
          x
        </button>
      </div>
    </div>
  );
}
