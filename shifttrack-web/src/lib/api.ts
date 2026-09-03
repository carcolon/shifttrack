import { API_BASES, BEARER_FALLBACK_ENABLED } from './constants';

let preferredBase = API_BASES[0] ?? '';
let csrfTokenCache = '';
let legacyBearerCache = '';
const LEGACY_BEARER_STORAGE_KEY = 'shifttrack_legacy_bearer';

const normalizeBase = (base: string) => base.replace(/\/+$/, '');

const joinUrl = (base: string, path: string) => {
  const cleanBase = normalizeBase(base);
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  return `${cleanBase}${cleanPath}`;
};

const orderedBases = () => {
  const rest = API_BASES.filter((b) => b !== preferredBase);
  return [preferredBase, ...rest];
};

const shouldTryNextBase = (path: string, status: number) => {
  const normalized = path.toLowerCase();
  if (status >= 500 || status === 429) return true;
  // In mixed local/cloud dev, PTO ids may exist only in one backend datastore.
  if (status === 404 && normalized.includes('/pto/requests/')) return true;
  return false;
};

export const getPreferredApiBase = () => preferredBase;

export const apiUrl = (path: string) => joinUrl(preferredBase, path);

const readCookieValue = (name: string) => {
  if (typeof document === 'undefined') return '';
  const token = document.cookie
    .split(';')
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${name}=`));
  return token ? decodeURIComponent(token.split('=').slice(1).join('=')) : '';
};

const readStoredLegacyBearer = () => {
  if (typeof sessionStorage === 'undefined') return '';
  try {
    return sessionStorage.getItem(LEGACY_BEARER_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
};

const writeStoredLegacyBearer = (token: string) => {
  if (typeof sessionStorage === 'undefined') return;
  try {
    if (token) {
      sessionStorage.setItem(LEGACY_BEARER_STORAGE_KEY, token);
    } else {
      sessionStorage.removeItem(LEGACY_BEARER_STORAGE_KEY);
    }
  } catch {
    // Storage can be unavailable in private or hardened browser modes.
  }
};

const readLegacyBearer = () => {
  if (!BEARER_FALLBACK_ENABLED) return '';
  if (legacyBearerCache) return legacyBearerCache;
  legacyBearerCache = readStoredLegacyBearer();
  return legacyBearerCache;
};

export const setLegacyBearerToken = (token: string) => {
  legacyBearerCache = typeof token === 'string' ? token : '';
  if (BEARER_FALLBACK_ENABLED) {
    writeStoredLegacyBearer(legacyBearerCache);
  }
};

export const getRealtimeAccessToken = () => readLegacyBearer();

const updateCsrfCache = (response: Response) => {
  const token = response.headers.get('X-CSRF-Token');
  if (token && token.trim().length > 0) {
    csrfTokenCache = token.trim();
  }
};

const shouldAttachCsrf = (path: string, method: string) => {
  const normalizedPath = path.toLowerCase();
  if (normalizedPath.startsWith('/auth/')) return false;
  return method.toUpperCase() !== 'GET';
};

const ensureCsrfToken = async (requestInit: RequestInit) => {
  if (csrfTokenCache) return;
  try {
    const response = await fetch(joinUrl(preferredBase, '/auth/me'), requestInit);
    updateCsrfCache(response);
  } catch {
    // Ignore here; the original request will still run and surface the real error if needed.
  }
};

export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  let networkError: unknown = null;
  const headers = new Headers(init?.headers);
  if (BEARER_FALLBACK_ENABLED && !headers.has('Authorization')) {
    const legacyToken = readLegacyBearer();
    if (legacyToken) {
      headers.set('Authorization', `Bearer ${legacyToken}`);
    }
  }
  const requestMethod = (init?.method ?? 'GET').toUpperCase();
  const csrfEligible = shouldAttachCsrf(path, requestMethod);
  const requestInit: RequestInit = {
    ...init,
    headers,
    credentials: 'include',
  };

  if (csrfEligible && !headers.has('X-CSRF-Token')) {
    const cookieToken = readCookieValue('shifttrack_csrf');
    if (cookieToken) {
      csrfTokenCache = cookieToken;
    } else {
      await ensureCsrfToken(requestInit);
    }
    if (csrfTokenCache) {
      headers.set('X-CSRF-Token', csrfTokenCache);
    }
  }

  const bases = orderedBases();
  for (let i = 0; i < bases.length; i++) {
    const base = bases[i];
    try {
      const response = await fetch(joinUrl(base, path), requestInit);
      updateCsrfCache(response);
      const isLast = i === bases.length - 1;
      if (response.ok || isLast || !shouldTryNextBase(path, response.status)) {
        preferredBase = base;
        return response;
      }
      // Try the next backend base before returning this non-ok response.
    } catch (err) {
      networkError = err;
    }
  }

  throw networkError instanceof Error ? networkError : new Error('Unable to reach API endpoints.');
}
