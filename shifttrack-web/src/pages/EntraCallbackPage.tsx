import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { ApiError, UserInfo } from '../types';
import { apiFetch } from '../lib/api';
import { AuthAnimatedShell } from '../components/AuthAnimatedShell';
import { AuthBrand } from '../components/AuthBrand';
import { getBrowserCookie, removeBrowserCookie } from '../lib/browserState';
import { Button } from '../components/ui/Button';

const ENTRA_STATE_KEY = 'shifttrack-entra-state';
const ENTRA_VERIFIER_KEY = 'shifttrack-entra-code-verifier';

type Props = {
  onLogin: (user: UserInfo) => void;
};

export default function EntraCallbackPage({ onLogin }: Props) {
  const navigate = useNavigate();
  const [message, setMessage] = useState('Signing in with Microsoft...');
  const handledRef = useRef(false);

  useEffect(() => {
    if (handledRef.current) return;
    handledRef.current = true;

    const run = async () => {
      const query = new URLSearchParams(window.location.search);
      const code = query.get('code');
      const state = query.get('state');
      const error = query.get('error_description') ?? query.get('error');
      const expectedState = getBrowserCookie(ENTRA_STATE_KEY);
      const codeVerifier = getBrowserCookie(ENTRA_VERIFIER_KEY);

      if (error) {
        setMessage('Microsoft login failed. Please try again.');
        return;
      }
      if (!code || !state || !expectedState || state !== expectedState || !codeVerifier) {
        setMessage('Microsoft login response is invalid. Please try again.');
        return;
      }

      removeBrowserCookie(ENTRA_STATE_KEY);
      removeBrowserCookie(ENTRA_VERIFIER_KEY);

      try {
        const res = await apiFetch('/auth/entra-code-login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code,
            codeVerifier,
            redirectUri: `${window.location.origin}/entra-callback`,
          }),
        });
        const data = (await res.json().catch(() => null)) as ApiError | null;
        if (res.ok && typeof data?.email === 'string' && data.email.length > 0) {
          const user: UserInfo = {
            email: data.email ?? '',
            displayName: data.displayName ?? data.email ?? '',
            role: data.role ?? 0,
            permissions: data.permissions ?? [],
            token: '',
            isSystemHidden: data.isSystemHidden ?? false,
            company: data.company ?? '',
            companies: data.companies ?? [],
          };
          onLogin(user);
          navigate('/app', { replace: true });
          return;
        }

        setMessage(data?.message ?? 'Microsoft account is not authorized.');
      } catch {
        setMessage('Could not reach server while completing Microsoft login.');
      }
    };

    run();
  }, [navigate, onLogin]);

  return (
    <AuthAnimatedShell>
      <div className="login-card auth-card">
        <AuthBrand subtitle={message} />
        <Button type="button" variant="ghost" onClick={() => navigate('/', { replace: true })}>
          Back to login
        </Button>
      </div>
    </AuthAnimatedShell>
  );
}
