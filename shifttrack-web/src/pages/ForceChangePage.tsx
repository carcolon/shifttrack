import { useEffect, useState, type FormEvent } from 'react';
import { passwordRegex } from '../lib/constants';
import { apiFetch } from '../lib/api';
import type { ApiError, UserInfo } from '../types';
import { AuthAnimatedShell } from '../components/AuthAnimatedShell';
import { AuthBrand } from '../components/AuthBrand';
import { Button } from '../components/ui/Button';
import { Field } from '../components/ui/Field';

export function ForceChangePage({ email, currentPassword, onDone }: { email: string; currentPassword: string; onDone: (user?: UserInfo) => void }) {
  const [forcePassword, setForcePassword] = useState('');
  const [forceConfirm, setForceConfirm] = useState('');
  const [forceSubmitting, setForceSubmitting] = useState(false);
  const [forceError, setForceError] = useState<string | null>(null);
  const [forceSuccess, setForceSuccess] = useState<string | null>(null);

  useEffect(() => {
    document.title = 'Change password';
  }, []);

  const handleForceChange = async (e: FormEvent) => {
    e.preventDefault();
    setForceError(null);
    setForceSuccess(null);
    if (!forcePassword || !forceConfirm) {
      setForceError('Both fields are required.');
      return;
    }
    if (!passwordRegex.test(forcePassword.trim())) {
      setForceError('Password must meet policy.');
      return;
    }
    if (forcePassword !== forceConfirm) {
      setForceError('Passwords do not match.');
      return;
    }
    setForceSubmitting(true);
    try {
      const res = await apiFetch('/auth/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, token: currentPassword, newPassword: forcePassword.trim() }),
      });
      if (res.ok) {
        setForceSuccess('Password updated. Redirecting...');
        setTimeout(() => onDone(), 1000);
      } else {
        const data = (await res.json().catch(() => null)) as ApiError | null;
        setForceError(data?.message ?? 'Unable to change password.');
      }
    } catch {
      setForceError('We could not reach the server. Please try again.');
    } finally {
      setForceSubmitting(false);
    }
  };

  return (
    <AuthAnimatedShell>
      <div className="login-card auth-card">
        <AuthBrand subtitle="For security reasons, you are required to change your password before accessing your account." />
        {forceError && <div className="alert">{forceError}</div>}
        {forceSuccess && <div className="alert success">{forceSuccess}</div>}
        <form onSubmit={handleForceChange} noValidate>
          <small className="helper">Password must be at least 8 chars and include uppercase, lowercase, number, and special (!@#$%*).</small>
          <Field label="Enter your new password">
            <input type="password" value={forcePassword} onChange={(e) => setForcePassword(e.target.value)} required />
          </Field>
          <Field label="Confirm your new password">
            <input type="password" value={forceConfirm} onChange={(e) => setForceConfirm(e.target.value)} required />
          </Field>
          <Button type="submit" variant="primary" disabled={forceSubmitting}>
            {forceSubmitting ? 'Saving...' : 'Update Password'}
          </Button>
        </form>
      </div>
    </AuthAnimatedShell>
  );
}

export default ForceChangePage;
