const devApiBase = import.meta.env.DEV ? ['http://', 'localhost', ':5243'].join('') : '';
const primaryApiBase = import.meta.env.VITE_API_BASE ?? devApiBase;
const fallbackApiBase = import.meta.env.VITE_API_BASE_FALLBACK ?? '';

export const API_BASES = [primaryApiBase, fallbackApiBase]
  .map((value) => value.trim())
  .filter((value, index, arr) => value.length > 0 && arr.indexOf(value) === index);

export const API_BASE = API_BASES[0] ?? '';
export const ENTRA_TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID ?? '';
export const ENTRA_CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID ?? '';
export const MAINTENANCE_MODE_ENABLED = ['1', 'true', 'yes', 'on'].includes(
  (import.meta.env.VITE_MAINTENANCE_MODE ?? '').toString().trim().toLowerCase(),
);
export const MAINTENANCE_MESSAGE =
  (import.meta.env.VITE_MAINTENANCE_MESSAGE ?? '').toString().trim() ||
  'We are performing scheduled maintenance. Please check back soon.';
const bearerFallbackRaw = (import.meta.env.VITE_ENABLE_BEARER_FALLBACK ?? '').toString().trim().toLowerCase();
export const BEARER_FALLBACK_ENABLED =
  bearerFallbackRaw === '' || ['1', 'true', 'yes', 'on'].includes(bearerFallbackRaw);
const trackyRaw = (import.meta.env.VITE_ENABLE_TRACKY ?? '').toString().trim().toLowerCase();
export const TRACKY_ENABLED =
  trackyRaw === '' || ['1', 'true', 'yes', 'on'].includes(trackyRaw);

export const emailRegex = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
export const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%*]).{8,}$/;

export const roleOptions = [
  { value: '0', label: 'Employee' },
  { value: '1', label: 'Manager' },
  { value: '2', label: 'Admin' },
  { value: '3', label: 'Team Leader' },
];

export const locationOptions = ['ARG', 'COL', 'WPB'];
export const companyOptions = ['Esquire Law, LLC'];
export const operationOptions = ['ESQ', 'Leaders', 'Outbound', 'Referral', 'SGF'];
export const shiftTimeOptions = ['Morning', 'Late'];
