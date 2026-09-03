import { useEffect, useState, type FormEvent } from 'react';
import { emailRegex, passwordRegex } from '../lib/constants';
import { apiFetch } from '../lib/api';
import type { ApiError } from '../types';
import { AuthAnimatedShell } from '../components/AuthAnimatedShell';
import { AuthBrand } from '../components/AuthBrand';
import { Button } from '../components/ui/Button';
import { Field } from '../components/ui/Field';

export function ResetPage() {
  const [resetCodeFromUrl] = useState(() => {
    const searchParams = new URLSearchParams(window.location.search);
    return searchParams.get('code') ?? '';
  });
  const [legacyTokenFromUrl] = useState(() => {
    const hashParams = new URLSearchParams(window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash);
    const searchParams = new URLSearchParams(window.location.search);
    return hashParams.get('token') ?? searchParams.get('token') ?? '';
  });
  const [legacyEmailFromUrl] = useState(() => {
    const hashParams = new URLSearchParams(window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash);
    const searchParams = new URLSearchParams(window.location.search);
    return hashParams.get('email') ?? searchParams.get('email') ?? '';
  });
  const [resetSession, setResetSession] = useState<{ email: string; exchangeToken: string } | null>(null);
  const [legacyReset, setLegacyReset] = useState<{ email: string; token: string } | null>(() => {
    if (!legacyTokenFromUrl || !legacyEmailFromUrl) return null;
    return { email: legacyEmailFromUrl, token: legacyTokenFromUrl };
  });
  const [exchangeError, setExchangeError] = useState<string | null>(null);
  const [exchangeLoading, setExchangeLoading] = useState(false);
  const hasResetContext = !!resetSession || !!legacyReset;

  const [resetPassword, setResetPassword] = useState('');
  const [resetConfirm, setResetConfirm] = useState('');
  const [resetErrors, setResetErrors] = useState<{ password?: string; confirm?: string; general?: string }>({});
  const [resetSubmitting, setResetSubmitting] = useState(false);
  const [resetResult, setResetResult] = useState<string | null>(null);

  const [requestEmail, setRequestEmail] = useState('');
  const [requestMessage, setRequestMessage] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<string | null>(null);
  const [requestSubmitting, setRequestSubmitting] = useState(false);

  useEffect(() => {
    document.title = hasResetContext ? 'Reset password' : 'Forgot password';
  }, [hasResetContext]);

  useEffect(() => {
    if (hasResetContext && (window.location.search || window.location.hash)) {
      window.history.replaceState({}, '', '/reset');
    }
  }, [hasResetContext]);

  useEffect(() => {
    const exchangeResetCode = async () => {
      if (!resetCodeFromUrl) return;
      setExchangeLoading(true);
      setExchangeError(null);
      try {
        const res = await apiFetch('/auth/reset-password/exchange', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: resetCodeFromUrl }),
        });
        if (!res.ok) {
          const data = (await res.json().catch(() => null)) as ApiError | null;
          setExchangeError(data?.message ?? 'Reset link is invalid or expired.');
          return;
        }

        const data = (await res.json()) as { email?: string; exchangeToken?: string };
        if (!data.email || !data.exchangeToken) {
          setExchangeError('Reset link is invalid or expired.');
          return;
        }

        setLegacyReset(null);
        setResetSession({ email: data.email, exchangeToken: data.exchangeToken });
      } catch {
        setExchangeError('We could not reach the server. Please try again.');
      } finally {
        setExchangeLoading(false);
      }
    };

    void exchangeResetCode();
  }, [resetCodeFromUrl]);

  const validateReset = () => {
    const next: typeof resetErrors = {};
    if (!resetPassword) next.password = 'The Password field is required.';
    if (resetPassword && !passwordRegex.test(resetPassword.trim())) next.password = 'Password must meet policy.';
    if (resetConfirm !== resetPassword) next.confirm = 'Passwords do not match.';
    if (resetPassword !== resetPassword.trim()) next.general = 'Credentials for the entered email are not valid. Please check and try again.';
    setResetErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleReset = async (e: FormEvent) => {
    e.preventDefault();
    setResetResult(null);
    if (!hasResetContext || !validateReset()) return;
    setResetSubmitting(true);
    try {
      const res = await apiFetch(
        resetSession ? '/auth/reset-password/complete' : '/auth/reset-password',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(
            resetSession
              ? {
                  email: resetSession.email,
                  exchangeToken: resetSession.exchangeToken,
                  newPassword: resetPassword.trim(),
                }
              : {
                  email: legacyReset?.email ?? '',
                  token: legacyReset?.token ?? '',
                  newPassword: resetPassword.trim(),
                },
          ),
        },
      );
      if (res.ok) {
        setResetErrors({});
        setResetResult('Password reset successful. Redirecting to login...');
        setTimeout(() => {
          window.location.href = '/';
        }, 1500);
      } else {
        const data = (await res.json().catch(() => null)) as ApiError | null;
        setResetResult(data?.message ?? 'Unable to reset password.');
      }
    } catch {
      setResetResult('We could not reach the server. Please try again.');
    } finally {
      setResetSubmitting(false);
    }
  };

  const handleRequestLink = async (e: FormEvent) => {
    e.preventDefault();
    setRequestMessage(null);
    setRequestError(null);
    const email = requestEmail.trim();
    if (!email) {
      setRequestError('The Email field is required.');
      return;
    }
    if (!emailRegex.test(email)) {
      setRequestError('Credentials for the entered email are not valid. Please check and try again.');
      return;
    }
    setRequestSubmitting(true);
    try {
      const res = await apiFetch('/auth/forgot-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      });
      if (res.ok) {
        setRequestMessage('Reset link sent if the email exists.');
      } else {
        const data = (await res.json().catch(() => null)) as ApiError | null;
        setRequestError(data?.message ?? 'Unable to process request.');
      }
    } catch {
      setRequestError('We could not reach the server. Please try again.');
    } finally {
      setRequestSubmitting(false);
    }
  };

  return (
    <AuthAnimatedShell>
      <div className="login-card auth-card">
        <AuthBrand subtitle={hasResetContext ? 'Reset your password' : 'Forgot your password?'} />
        {!hasResetContext && (
          <Button className="link-button back-link" variant="ghost" type="button" onClick={() => (window.location.href = '/')}>
            {'<- Back to login'}
          </Button>
        )}
        {hasResetContext ? (
          <>
            {exchangeLoading && <div className="alert">Validating reset link...</div>}
            {exchangeError && <div className="alert">{exchangeError}</div>}
            {resetResult && <div className="alert success">{resetResult}</div>}
            <form onSubmit={handleReset} noValidate>
              <small className="helper">Password must be at least 8 chars and include uppercase, lowercase, number, and special (!@#$%*).</small>
              <Field label="New Password" error={resetErrors.password}>
                <input
                  type="password"
                  value={resetPassword}
                  onChange={(e) => setResetPassword(e.target.value)}
                  placeholder="New password"
                  autoComplete="new-password"
                  required
                />
              </Field>
              <Field label="Confirm Password" error={resetErrors.confirm}>
                <input
                  type="password"
                  value={resetConfirm}
                  onChange={(e) => setResetConfirm(e.target.value)}
                  placeholder="Confirm password"
                  autoComplete="new-password"
                  required
                />
              </Field>
              <small className="error">{resetErrors.general}</small>
              <Button type="submit" variant="primary" disabled={resetSubmitting || exchangeLoading || !!exchangeError}>
                {resetSubmitting ? 'Saving...' : 'Reset password'}
              </Button>
            </form>
          </>
        ) : (
          <>
            {requestMessage && <div className="alert success">{requestMessage}</div>}
            {requestError && <div className="alert">{requestError}</div>}
            <form onSubmit={handleRequestLink} noValidate>
              <Field label="Corporate Email">
                <input
                  type="email"
                  value={requestEmail}
                  onChange={(e) => setRequestEmail(e.target.value)}
                  placeholder="Corporate Email"
                  autoComplete="email"
                  required
                />
              </Field>
              <Button type="submit" variant="primary" disabled={requestSubmitting}>
                {requestSubmitting ? 'Sending...' : 'Send reset link'}
              </Button>
            </form>
          </>
        )}
      </div>
    </AuthAnimatedShell>
  );
}

export default ResetPage;
