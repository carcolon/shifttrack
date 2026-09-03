type CookieOptions = {
  maxAgeSeconds?: number;
};

const buildCookie = (name: string, value: string, options?: CookieOptions) => {
  const parts = [`${encodeURIComponent(name)}=${encodeURIComponent(value)}`, 'Path=/', 'SameSite=Lax'];
  if (typeof options?.maxAgeSeconds === 'number') {
    parts.push(`Max-Age=${Math.max(0, Math.floor(options.maxAgeSeconds))}`);
  }
  return parts.join('; ');
};

export const setBrowserCookie = (name: string, value: string, options?: CookieOptions) => {
  if (typeof document === 'undefined') return;
  document.cookie = buildCookie(name, value, options);
};

export const getBrowserCookie = (name: string) => {
  if (typeof document === 'undefined') return '';
  const prefix = `${encodeURIComponent(name)}=`;
  const entry = document.cookie
    .split(';')
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));
  if (!entry) return '';
  return decodeURIComponent(entry.slice(prefix.length));
};

export const removeBrowserCookie = (name: string) => {
  if (typeof document === 'undefined') return;
  document.cookie = buildCookie(name, '', { maxAgeSeconds: 0 });
};
