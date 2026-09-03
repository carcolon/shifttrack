import { Suspense, lazy, useCallback, useEffect, useRef, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Button } from './components/ui/Button';
import { AuthTransitionCurtain } from './components/AuthTransitionCurtain';
import { ShiftTrackLoaderOverlay } from './components/ShiftTrackLoader';
import { SESSION_ACTIVITY_EVENT } from './lib/sessionActivity';
import { apiFetch, setLegacyBearerToken } from './lib/api';
import { setBrowserCookie } from './lib/browserState';
import { MAINTENANCE_MODE_ENABLED } from './lib/constants';
import type { UserInfo } from './types';

const DashboardPage = lazy(() => import('./pages/DashboardPage').then((module) => ({ default: module.DashboardPage })));
const EntraCallbackPage = lazy(() => import('./pages/EntraCallbackPage'));
const ForceChangePage = lazy(() => import('./pages/ForceChangePage'));
const LoginPage = lazy(() => import('./pages/LoginPage'));
const MaintenancePage = lazy(() => import('./pages/MaintenancePage'));
const PtoReviewPage = lazy(() => import('./pages/PtoReviewPage'));
const ResetPage = lazy(() => import('./pages/ResetPage'));
const SwapReviewPage = lazy(() => import('./pages/SwapReviewPage'));

const SESSION_WARNING_MS = 58 * 60 * 1000;
const SESSION_TIMEOUT_MS = 60 * 60 * 1000;
const SESSION_HEARTBEAT_MS = 5 * 60 * 1000;
const SESSION_CHECK_INTERVAL_MS = 15000;
const SESSION_FLASH_KEY = 'shifttrack-session-flash';
const LOGIN_TRANSITION_MS = 1750;
const LOGOUT_CURTAIN_CLOSE_MS = 1300;
const LOGOUT_CURTAIN_HOLD_MS = 320;

type AuthTransitionMode = 'login' | 'logout';

function AppShell() {
  const currentPath = window.location.pathname;
  const skipInitialSessionHydration = currentPath === '/entra-callback';
  const [user, setUser] = useState<UserInfo | null>(null);
  const [sessionHydrated, setSessionHydrated] = useState(false);
  const [forceEmail, setForceEmail] = useState('');
  const [forceCurrentPassword, setForceCurrentPassword] = useState('');
  const [sessionWarningOpen, setSessionWarningOpen] = useState(false);
  const [authTransitionMode, setAuthTransitionMode] = useState<AuthTransitionMode | null>(null);
  const lastActivityAtRef = useRef(Date.now());
  const lastHeartbeatAtRef = useRef(0);
  const expiringRef = useRef(false);
  const transitionTimerRef = useRef<number | null>(null);
  const transitionCleanupTimerRef = useRef<number | null>(null);
  const logoutInFlightRef = useRef(false);

  const finalizeLogin = useCallback((nextUser: UserInfo, forcePwd?: { email: string; currentPassword: string }) => {
    setUser(nextUser);
    lastActivityAtRef.current = Date.now();
    lastHeartbeatAtRef.current = 0;
    expiringRef.current = false;
    setSessionWarningOpen(false);
    setAuthTransitionMode('login');
    if (transitionTimerRef.current) {
      window.clearTimeout(transitionTimerRef.current);
    }
    if (transitionCleanupTimerRef.current) {
      window.clearTimeout(transitionCleanupTimerRef.current);
      transitionCleanupTimerRef.current = null;
    }
    transitionTimerRef.current = window.setTimeout(() => {
      setAuthTransitionMode(null);
      transitionTimerRef.current = null;
    }, LOGIN_TRANSITION_MS);
    if (forcePwd) {
      setForceEmail(forcePwd.email);
      setForceCurrentPassword(forcePwd.currentPassword);
    }
  }, []);

  const handleLogout = useCallback(async (reason?: 'expired') => {
    if (logoutInFlightRef.current) return;
    logoutInFlightRef.current = true;
    setSessionWarningOpen(false);
    setAuthTransitionMode('logout');
    if (transitionTimerRef.current) {
      window.clearTimeout(transitionTimerRef.current);
      transitionTimerRef.current = null;
    }
    if (transitionCleanupTimerRef.current) {
      window.clearTimeout(transitionCleanupTimerRef.current);
      transitionCleanupTimerRef.current = null;
    }

    try {
      await apiFetch('/auth/logout', { method: 'POST' });
    } catch {
      // Even if server logout fails, clear local state.
    } finally {
      transitionTimerRef.current = window.setTimeout(() => {
        if (reason === 'expired') {
          setBrowserCookie(SESSION_FLASH_KEY, JSON.stringify({
            title: 'Session expired',
            message: 'Your session ended after 60 minutes of inactivity. Please sign in again.',
          }), { maxAgeSeconds: 300 });
        }
        setUser(null);
        transitionTimerRef.current = null;
        transitionCleanupTimerRef.current = window.setTimeout(() => {
          setAuthTransitionMode(null);
          transitionCleanupTimerRef.current = null;
          logoutInFlightRef.current = false;
        }, LOGOUT_CURTAIN_HOLD_MS);
      }, LOGOUT_CURTAIN_CLOSE_MS);
    }
  }, []);

  const sendHeartbeat = useCallback(async (force = false) => {
    if (!user || expiringRef.current) return;
    const now = Date.now();
    if (!force && now - lastHeartbeatAtRef.current < SESSION_HEARTBEAT_MS) return;
    lastHeartbeatAtRef.current = now;
    try {
      const response = await apiFetch('/auth/ping', { method: 'POST' });
      if (!response.ok && response.status === 401) {
        expiringRef.current = true;
        await handleLogout('expired');
      }
    } catch {
      // Ignore transient ping failures.
    }
  }, [handleLogout, user]);

  const registerActivity = useCallback((forceHeartbeat = false) => {
    if (!user || expiringRef.current) return;
    lastActivityAtRef.current = Date.now();
    if (sessionWarningOpen) {
      setSessionWarningOpen(false);
    }
    void sendHeartbeat(forceHeartbeat);
  }, [sendHeartbeat, sessionWarningOpen, user]);

  useEffect(() => {
    if (user?.token) {
      setLegacyBearerToken(user.token);
      return;
    }

    if (sessionHydrated) {
      setLegacyBearerToken('');
    }
  }, [sessionHydrated, user]);

  useEffect(() => {
    if (skipInitialSessionHydration) {
      setSessionHydrated(true);
      return;
    }

    const hydrateSession = async () => {
      try {
        const response = await apiFetch('/auth/me');
        if (!response.ok) {
          setUser(null);
          setSessionHydrated(true);
          return;
        }

        const data = (await response.json()) as Omit<UserInfo, 'token'>;
        setUser({
          email: data.email,
          displayName: data.displayName,
          role: data.role,
          permissions: data.permissions ?? [],
          token: '',
          isSystemHidden: data.isSystemHidden ?? false,
          company: data.company ?? '',
          companies: data.companies ?? [],
        });
      } catch {
        setUser(null);
      } finally {
        setSessionHydrated(true);
      }
    };

    void hydrateSession();
  }, [skipInitialSessionHydration]);

  useEffect(() => {
    if (!user) {
      setSessionWarningOpen(false);
      return;
    }

    const onActivity = () => registerActivity(false);
    const onSignalrActivity = () => registerActivity(true);

    window.addEventListener('mousemove', onActivity, { passive: true });
    window.addEventListener('keydown', onActivity);
    window.addEventListener('click', onActivity);
    window.addEventListener('scroll', onActivity, { passive: true });
    window.addEventListener('touchstart', onActivity, { passive: true });
    window.addEventListener(SESSION_ACTIVITY_EVENT, onSignalrActivity as EventListener);

    void sendHeartbeat(true);

    const interval = window.setInterval(() => {
      const elapsed = Date.now() - lastActivityAtRef.current;
      if (elapsed >= SESSION_TIMEOUT_MS && !expiringRef.current) {
        expiringRef.current = true;
        void handleLogout('expired');
        return;
      }

      if (elapsed >= SESSION_WARNING_MS) {
        setSessionWarningOpen(true);
      }
    }, SESSION_CHECK_INTERVAL_MS);

    return () => {
      window.removeEventListener('mousemove', onActivity);
      window.removeEventListener('keydown', onActivity);
      window.removeEventListener('click', onActivity);
      window.removeEventListener('scroll', onActivity);
      window.removeEventListener('touchstart', onActivity);
      window.removeEventListener(SESSION_ACTIVITY_EVENT, onSignalrActivity as EventListener);
      window.clearInterval(interval);
    };
  }, [handleLogout, registerActivity, sendHeartbeat, user]);

  useEffect(() => () => {
    if (transitionTimerRef.current) {
      window.clearTimeout(transitionTimerRef.current);
    }
    if (transitionCleanupTimerRef.current) {
      window.clearTimeout(transitionCleanupTimerRef.current);
    }
  }, []);

  if (MAINTENANCE_MODE_ENABLED) {
    return (
      <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading maintenance" />}>
        <MaintenancePage />
      </Suspense>
    );
  }

  if (!sessionHydrated) {
    return null;
  }

  const loginRedirect = (path: string) => `/?redirect=${encodeURIComponent(path)}`;

  return (
    <BrowserRouter>
      <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading ShiftTrack" />}>
        <Routes>
          <Route
            path="/"
            element={<LoginPage onLogin={(u, forcePwd) => finalizeLogin(u, forcePwd)} />}
          />
          <Route
            path="/entra-callback"
            element={<EntraCallbackPage onLogin={(u) => finalizeLogin(u)} />}
          />
          <Route path="/reset" element={<ResetPage />} />
          <Route
            path="/force"
            element={
              <ForceChangePage
                email={forceEmail}
                currentPassword={forceCurrentPassword}
                onDone={() => {
                  setForceEmail('');
                  setForceCurrentPassword('');
                  setUser(null);
                }}
              />
            }
          />
          <Route
            path="/app"
            element={user ? <Navigate to="/app/home" replace /> : <Navigate to="/" replace />}
          />
          <Route
            path="/app/:tab"
            element={user ? <DashboardPage user={user} onLogout={() => void handleLogout()} /> : <Navigate to="/" replace />}
          />
          <Route
            path="/pto-review"
            element={user ? <PtoReviewPage user={user} /> : <Navigate to={loginRedirect(`${window.location.pathname}${window.location.search}`)} replace />}
          />
          <Route
            path="/swap-review"
            element={user ? <SwapReviewPage user={user} /> : <Navigate to={loginRedirect(`${window.location.pathname}${window.location.search}`)} replace />}
          />
          <Route path="*" element={<Navigate to={user ? '/app/home' : '/'} replace />} />
        </Routes>
        {sessionWarningOpen && user && (
          <div className="modal" role="alertdialog" aria-modal="true" aria-labelledby="session-warning-title">
            <div className="modal-card">
              <div className="modal-icon warning">!</div>
              <h2 id="session-warning-title">Session expiring soon</h2>
              <p>Your session will end after 60 minutes of inactivity. Stay signed in to keep working.</p>
              <div className="modal-actions">
                <Button variant="ghost" onClick={() => void handleLogout()}>
                  Log out
                </Button>
                <Button variant="primary" onClick={() => registerActivity(true)}>
                  Stay signed in
                </Button>
              </div>
            </div>
          </div>
        )}
        {authTransitionMode && <AuthTransitionCurtain mode={authTransitionMode} />}
      </Suspense>
    </BrowserRouter>
  );
}

export default AppShell;
