import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Modal } from '../components/Modals';
import { EyeIcon, EyeOffIcon } from '../components/Icons';
import { AuthAnimatedShell } from '../components/AuthAnimatedShell';
import { AuthBrand } from '../components/AuthBrand';
import { ShiftTrackLoaderOverlay } from '../components/ShiftTrackLoader';
import { Button } from '../components/ui/Button';
import { Field } from '../components/ui/Field';
import { getBrowserCookie, removeBrowserCookie, setBrowserCookie } from '../lib/browserState';
import { emailRegex, ENTRA_CLIENT_ID, ENTRA_TENANT_ID } from '../lib/constants';
import { apiFetch } from '../lib/api';
import type { ApiError, UserInfo } from '../types';

const ENTRA_STATE_KEY = 'shifttrack-entra-state';
const ENTRA_VERIFIER_KEY = 'shifttrack-entra-code-verifier';
const SESSION_FLASH_KEY = 'shifttrack-session-flash';

const toBase64Url = (bytes: Uint8Array) =>
  btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');

const generateCodeVerifier = () => {
  const bytes = new Uint8Array(32);
  crypto.getRandomValues(bytes);
  return toBase64Url(bytes);
};

const buildCodeChallenge = async (verifier: string) => {
  const encoded = new TextEncoder().encode(verifier);
  const digest = await crypto.subtle.digest('SHA-256', encoded);
  return toBase64Url(new Uint8Array(digest));
};

export function LoginPage({ onLogin }: { onLogin: (user: UserInfo, force?: { email: string; currentPassword: string }) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string; general?: string }>({});
  const [modalMessage, setModalMessage] = useState<string | null>(null);
  const [modalTitle, setModalTitle] = useState('Login Failed');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const redirectPath = params.get('redirect') || '/app';

  useEffect(() => {
    document.title = 'ShiftTrack | Login';
  }, []);

  useEffect(() => {
    const raw = getBrowserCookie(SESSION_FLASH_KEY);
    if (!raw) return;
    removeBrowserCookie(SESSION_FLASH_KEY);
    try {
      const parsed = JSON.parse(raw) as { title?: string; message?: string };
      if (parsed.message) {
        setModalTitle(parsed.title ?? 'Session expired');
        setModalMessage(parsed.message);
      }
    } catch {
      setModalTitle('Session expired');
      setModalMessage('Your session ended. Please sign in again.');
    }
  }, []);

  const hasTrimIssue = useMemo(() => email !== email.trim() || password !== password.trim(), [email, password]);
  const canUseEntra = Boolean(ENTRA_CLIENT_ID && ENTRA_TENANT_ID);

  const validateLogin = () => {
    const next: typeof errors = {};
    if (!email) next.email = 'The Email field is required.';
    if (!password) next.password = 'The Password field is required.';
    if (hasTrimIssue || (email && !emailRegex.test(email.trim()))) {
      next.general = 'Credentials for the entered email are not valid. Please check and try again.';
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault();
    setModalTitle('Login Failed');
    setModalMessage(null);
    if (!validateLogin()) return;

    setIsSubmitting(true);
    try {
      const res = await apiFetch('/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim(), password: password.trim() }),
      });
      const data = (await res.json().catch(() => null)) as ApiError | null;
      if (res.ok && typeof data?.email === 'string' && data.email.length > 0) {
        const user: UserInfo = {
          email: data.email,
          displayName: data.displayName ?? data.email,
          role: data.role ?? 0,
          permissions: data.permissions ?? [],
          token: '',
          isSystemHidden: data.isSystemHidden ?? false,
          company: data.company ?? '',
          companies: data.companies ?? [],
        };
        onLogin(user);
        navigate(redirectPath, { replace: true });
        return;
      }

      if (data?.requirePasswordChange) {
        const user: UserInfo = {
          email: data.email ?? email.trim(),
          displayName: data.displayName ?? data.email ?? email.trim(),
          role: data.role ?? 0,
          permissions: data.permissions ?? [],
          token: '',
          isSystemHidden: data.isSystemHidden ?? false,
          company: data.company ?? '',
          companies: data.companies ?? [],
        };
        onLogin(user, { email: user.email, currentPassword: password.trim() });
        navigate('/force');
        return;
      }

      setModalTitle('Login Failed');
      setModalMessage(data?.message ?? 'Credentials for the entered email are not valid. Please check and try again.');
    } catch {
      setModalTitle('Login Failed');
      setModalMessage('We could not reach the server. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEntraLogin = async () => {
    if (!canUseEntra) {
      setModalMessage('Microsoft Entra login is not configured for this environment.');
      return;
    }

    const state = crypto.randomUUID();
    const codeVerifier = generateCodeVerifier();
    const codeChallenge = await buildCodeChallenge(codeVerifier);
    setBrowserCookie(ENTRA_STATE_KEY, state, { maxAgeSeconds: 900 });
    setBrowserCookie(ENTRA_VERIFIER_KEY, codeVerifier, { maxAgeSeconds: 900 });

    const redirectUri = `${window.location.origin}/entra-callback`;
    const params = new URLSearchParams({
      client_id: ENTRA_CLIENT_ID,
      response_type: 'code',
      redirect_uri: redirectUri,
      response_mode: 'query',
      scope: 'openid profile email',
      state,
      code_challenge: codeChallenge,
      code_challenge_method: 'S256',
    });

    window.location.assign(`https://login.microsoftonline.com/${ENTRA_TENANT_ID}/oauth2/v2.0/authorize?${params.toString()}`);
  };

  return (
    <AuthAnimatedShell>
      <div className="login-card auth-card">
        <AuthBrand subtitle="Welcome to your ShiftTrack account" />

        {errors.general && <div className="alert">{errors.general}</div>}

        <form onSubmit={handleLogin} noValidate>
          <Field label="Corporate Email" error={errors.email}>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Corporate Email"
              autoComplete="email"
              required
            />
          </Field>

          <Field label="Password" error={errors.password}>
            <div className="password">
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Password"
                autoComplete="current-password"
                required
              />
              <Button
                type="button"
                className="eye"
                variant="ghost"
                size="sm"
                onClick={() => setShowPassword((s) => !s)}
                aria-label="Toggle password visibility"
              >
                {showPassword ? <EyeOffIcon /> : <EyeIcon />}
              </Button>
            </div>
          </Field>

          <Button type="submit" variant="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Log in'}
          </Button>

          <Button type="button" className="entra-btn" variant="ghost" onClick={handleEntraLogin} disabled={isSubmitting}>
            <span className="entra-logo" aria-hidden="true">
              <i />
              <i />
              <i />
              <i />
            </span>
            <span>Continue with Microsoft</span>
          </Button>

          <Button type="button" className="text-button" variant="ghost" onClick={() => navigate('/reset')}>
            Forgot password? Reset it
          </Button>
        </form>
      </div>

      {modalMessage && (
        <Modal
          title={modalTitle}
          message={modalMessage}
          onClose={() => setModalMessage(null)}
          variant={modalTitle === 'Session expired' ? 'warning' : 'error'}
        />
      )}

      {isSubmitting && <ShiftTrackLoaderOverlay label="Signing in" />}
    </AuthAnimatedShell>
  );
}

export default LoginPage;
