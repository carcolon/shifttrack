import { Suspense, lazy, useEffect, useLayoutEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import gsap from 'gsap';
import { Sidebar } from '../components/Sidebar';
import { DashboardAmbientBackground } from '../components/DashboardAmbientBackground';
import { Modal, ConfirmModal, ModalShell } from '../components/Modals';
import { ShiftTrackLoaderOverlay } from '../components/ShiftTrackLoader';
import { Button } from '../components/ui/Button';
import { Field } from '../components/ui/Field';
import { Select } from '../components/ui/Select';
import { emailRegex, roleOptions, companyOptions, shiftTimeOptions } from '../lib/constants';
import { apiFetch } from '../lib/api';
import { canManageUsersForRole, isAdminRole, isManagerRole, roleLabelForValue } from '../lib/roles';
import type { ApiError, BulkUserUploadResponse, CompanyCatalogItem, CompanyOperationItem, CoverageRule, CreateEmployeeForm, Employee, UserInfo, ScheduleBlock, SchedulePeriod } from '../types';

const ShiftCalendarPage = lazy(() => import('./ShiftCalendarPage').then((module) => ({ default: module.ShiftCalendarPage })));
const RequestsPage = lazy(() => import('./RequestsPage').then((module) => ({ default: module.RequestsPage })));
const ReportsPage = lazy(() => import('./ReportsPage').then((module) => ({ default: module.ReportsPage })));
const HomePage = lazy(() => import('./HomePage').then((module) => ({ default: module.HomePage })));

function formatIsoDateToUs(value: string) {
  if (!value) return '';
  const [year, month, day] = value.split('-');
  if (!year || !month || !day) return value;
  return `${month}/${day}/${year}`;
}

function nextIsoDate(value: string) {
  const date = new Date(`${value}T00:00:00`);
  date.setDate(date.getDate() + 1);
  return date.toISOString().slice(0, 10);
}

function DateDisplayInput({
  value,
  min,
  onChange,
}: {
  value: string;
  min?: string;
  onChange: (nextValue: string) => void;
}) {
  const [isEditing, setIsEditing] = useState(false);

  return (
    <input
      type={isEditing ? 'date' : 'text'}
      value={isEditing ? value : formatIsoDateToUs(value)}
      min={isEditing ? min : undefined}
      placeholder="MM/DD/YYYY"
      inputMode="numeric"
      onFocus={() => setIsEditing(true)}
      onBlur={() => setIsEditing(false)}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}

type SearchOption = {
  value: string;
  label: string;
  disabled?: boolean;
};

const coverageDayLabels: Record<string, string> = {
  Monday: 'Mon',
  Tuesday: 'Tue',
  Wednesday: 'Wed',
  Thursday: 'Thu',
  Friday: 'Fri',
  Saturday: 'Sat',
  Sunday: 'Sun',
};

const defaultCoverageRules: CoverageRule[] = [
  { companyName: '', operationName: null, dayOfWeek: 'Monday', expectedCoverage: 95, greenThreshold: 91, yellowThreshold: 86, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Tuesday', expectedCoverage: 85, greenThreshold: 81, yellowThreshold: 71, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Wednesday', expectedCoverage: 80, greenThreshold: 76, yellowThreshold: 71, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Thursday', expectedCoverage: 80, greenThreshold: 76, yellowThreshold: 71, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Friday', expectedCoverage: 75, greenThreshold: 71, yellowThreshold: 66, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Saturday', expectedCoverage: 40, greenThreshold: 36, yellowThreshold: 31, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
  { companyName: '', operationName: null, dayOfWeek: 'Sunday', expectedCoverage: 35, greenThreshold: 31, yellowThreshold: 26, calculationScope: 'operation', isActive: true, updatedBy: '', updatedAtUtc: '' },
];

const fallbackRegionCodes = [
  'AF', 'AX', 'AL', 'DZ', 'AS', 'AD', 'AO', 'AI', 'AQ', 'AG', 'AR', 'AM', 'AW', 'AU', 'AT', 'AZ',
  'BS', 'BH', 'BD', 'BB', 'BY', 'BE', 'BZ', 'BJ', 'BM', 'BT', 'BO', 'BQ', 'BA', 'BW', 'BV', 'BR',
  'IO', 'BN', 'BG', 'BF', 'BI', 'CV', 'KH', 'CM', 'CA', 'KY', 'CF', 'TD', 'CL', 'CN', 'CX', 'CC',
  'CO', 'KM', 'CG', 'CD', 'CK', 'CR', 'CI', 'HR', 'CU', 'CW', 'CY', 'CZ', 'DK', 'DJ', 'DM', 'DO',
  'EC', 'EG', 'SV', 'GQ', 'ER', 'EE', 'SZ', 'ET', 'FK', 'FO', 'FJ', 'FI', 'FR', 'GF', 'PF', 'TF',
  'GA', 'GM', 'GE', 'DE', 'GH', 'GI', 'GR', 'GL', 'GD', 'GP', 'GU', 'GT', 'GG', 'GN', 'GW', 'GY',
  'HT', 'HM', 'VA', 'HN', 'HK', 'HU', 'IS', 'IN', 'ID', 'IR', 'IQ', 'IE', 'IM', 'IL', 'IT', 'JM',
  'JP', 'JE', 'JO', 'KZ', 'KE', 'KI', 'KP', 'KR', 'KW', 'KG', 'LA', 'LV', 'LB', 'LS', 'LR', 'LY',
  'LI', 'LT', 'LU', 'MO', 'MG', 'MW', 'MY', 'MV', 'ML', 'MT', 'MH', 'MQ', 'MR', 'MU', 'YT', 'MX',
  'FM', 'MD', 'MC', 'MN', 'ME', 'MS', 'MA', 'MZ', 'MM', 'NA', 'NR', 'NP', 'NL', 'NC', 'NZ', 'NI',
  'NE', 'NG', 'NU', 'NF', 'MK', 'MP', 'NO', 'OM', 'PK', 'PW', 'PS', 'PA', 'PG', 'PY', 'PE', 'PH',
  'PN', 'PL', 'PT', 'PR', 'QA', 'RE', 'RO', 'RU', 'RW', 'BL', 'SH', 'KN', 'LC', 'MF', 'PM', 'VC',
  'WS', 'SM', 'ST', 'SA', 'SN', 'RS', 'SC', 'SL', 'SG', 'SX', 'SK', 'SI', 'SB', 'SO', 'ZA', 'GS',
  'SS', 'ES', 'LK', 'SD', 'SR', 'SJ', 'SE', 'CH', 'SY', 'TW', 'TJ', 'TZ', 'TH', 'TL', 'TG', 'TK',
  'TO', 'TT', 'TN', 'TR', 'TM', 'TC', 'TV', 'UG', 'UA', 'AE', 'GB', 'US', 'UM', 'UY', 'UZ', 'VU',
  'VE', 'VN', 'VG', 'VI', 'WF', 'EH', 'YE', 'ZM', 'ZW',
];

function buildCountryOptions(): SearchOption[] {
  const displayNames = new Intl.DisplayNames(['en'], { type: 'region' });

  return fallbackRegionCodes
    .map((code) => {
      const label = displayNames.of(code) ?? code;
      return { value: label, label };
    })
    .filter((item, index, values) => values.findIndex((value) => value.label === item.label) === index)
    .sort((a, b) => a.label.localeCompare(b.label));
}

function SearchableSelect({
  value,
  options,
  placeholder,
  ariaLabel,
  onChange,
}: {
  value: string;
  options: SearchOption[];
  placeholder: string;
  ariaLabel: string;
  onChange: (nextValue: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const term = query.trim().toLowerCase();
  const filtered = options
    .filter((option) => option.label.toLowerCase().includes(term));

  return (
    <div className={`searchable-select ${open ? 'open' : ''}`}>
      <input
        value={open ? query : value}
        placeholder={placeholder}
        aria-label={ariaLabel}
        onFocus={() => {
          setOpen(true);
          setQuery('');
        }}
        onChange={(e) => {
          setOpen(true);
          setQuery(e.target.value);
        }}
        onBlur={() => window.setTimeout(() => setOpen(false), 120)}
      />
      {open && (
        <div className="searchable-menu" role="listbox">
          {filtered.length ? filtered.map((option) => (
            <button
              type="button"
              key={option.value}
              className={`searchable-option ${option.value === value ? 'selected' : ''}`}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                onChange(option.value);
                setQuery('');
                setOpen(false);
              }}
            >
              {option.label}
            </button>
          )) : <div className="searchable-empty">No results</div>}
        </div>
      )}
    </div>
  );
}

function SearchableMultiSelect({
  values,
  options,
  placeholder,
  ariaLabel,
  onToggle,
}: {
  values: string[];
  options: SearchOption[];
  placeholder: string;
  ariaLabel: string;
  onToggle: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const term = query.trim().toLowerCase();
  const filtered = options
    .filter((option) => option.label.toLowerCase().includes(term));

  return (
    <div className={`searchable-select multi ${open ? 'open' : ''}`}>
      <div className="selected-tags">
        {values.length ? values.map((value) => (
          <button type="button" key={value} className="selected-tag" onClick={() => onToggle(value)}>
            {value}
            <span aria-hidden="true">x</span>
          </button>
        )) : <span className="selected-placeholder">{placeholder}</span>}
      </div>
      <input
        value={query}
        placeholder={values.length ? 'Search companies' : placeholder}
        aria-label={ariaLabel}
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          setOpen(true);
          setQuery(e.target.value);
        }}
        onBlur={() => window.setTimeout(() => setOpen(false), 120)}
      />
      {open && (
        <div className="searchable-menu" role="listbox">
          {filtered.length ? filtered.map((option) => {
            const selected = values.some((value) => value.toLowerCase() === option.value.toLowerCase());
            return (
              <button
                type="button"
                key={option.value}
                disabled={option.disabled}
                className={`searchable-option ${selected ? 'selected' : ''}`}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  if (!option.disabled) onToggle(option.value);
                  setQuery('');
                }}
              >
                <span>{option.label}</span>
                {selected ? <strong>Selected</strong> : null}
              </button>
            );
          }) : <div className="searchable-empty">No results</div>}
        </div>
      )}
    </div>
  );
}

export function DashboardPage({ user, onLogout }: { user: UserInfo; onLogout: () => void }) {
  type AppTab = 'home' | 'calendar' | 'employees' | 'requests' | 'reports' | 'super-admin';
  type SortField = 'displayName' | 'email' | 'role' | 'operation' | 'company';
  type SortDirection = 'asc' | 'desc';
  const location = useLocation();
  const navigate = useNavigate();
  const activeTab = useMemo<AppTab>(() => {
    const segment = location.pathname.split('/')[2];
    return segment === 'home' || segment === 'employees' || segment === 'requests' || segment === 'calendar' || segment === 'reports' || segment === 'super-admin'
      ? segment
      : 'home';
  }, [location.pathname]);
  const [tabSwitching, setTabSwitching] = useState(false);
  const [navOverlay, setNavOverlay] = useState(false);
  const viewStageRef = useRef<HTMLDivElement | null>(null);
  const employeeFormRef = useRef<HTMLDivElement | null>(null);
  const bulkUploadInputRef = useRef<HTMLInputElement | null>(null);

  const [createForm, setCreateForm] = useState<CreateEmployeeForm>({
    firstName: '',
    lastName: '',
    email: '',
    role: '',
    location: '',
    company: '',
    companies: [],
    operation: '',
    isSystemHidden: false,
    appearsInSchedule: true,
  });
  const [companyCatalog, setCompanyCatalog] = useState<CompanyCatalogItem[]>([]);
  const [companyOperations, setCompanyOperations] = useState<CompanyOperationItem[]>([]);
  const [companyModalOpen, setCompanyModalOpen] = useState(false);
  const [companyDraft, setCompanyDraft] = useState('');
  const [selectedCompanyName, setSelectedCompanyName] = useState('');
  const [companyRenameDraft, setCompanyRenameDraft] = useState('');
  const [operationDraft, setOperationDraft] = useState('');
  const [operationRenameDrafts, setOperationRenameDrafts] = useState<Record<string, string>>({});
  const [companySaving, setCompanySaving] = useState(false);
  const [coverageModalOpen, setCoverageModalOpen] = useState(false);
  const [coverageCompany, setCoverageCompany] = useState(user.company ?? '');
  const [coverageOperation, setCoverageOperation] = useState('');
  const [coverageCalculationScope, setCoverageCalculationScope] = useState<'operation' | 'company'>('operation');
  const [coverageRules, setCoverageRules] = useState<CoverageRule[]>(defaultCoverageRules);
  const [coverageLoading, setCoverageLoading] = useState(false);
  const [coverageSaving, setCoverageSaving] = useState(false);
  const [coverageError, setCoverageError] = useState<string | null>(null);
  const [createSubmitting, setCreateSubmitting] = useState(false);
  const [bulkUploading, setBulkUploading] = useState(false);
  const [createModal, setCreateModal] = useState<{ title?: string; message: string; variant?: 'error' | 'info' } | null>(null);
  const [bulkUploadModalOpen, setBulkUploadModalOpen] = useState(false);
  const [unsavedModal, setUnsavedModal] = useState(false);
  const [hasUnsaved, setHasUnsaved] = useState(false);
  const [viewMode, setViewMode] = useState<'list' | 'create' | 'edit'>('list');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [memberScope, setMemberScope] = useState<'active' | 'inactive'>('active');
  const [superAdminScope, setSuperAdminScope] = useState<'admins' | 'super-admins'>('admins');
  const [employeesLoading, setEmployeesLoading] = useState(false);
  const [employeesError, setEmployeesError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Employee | null>(null);
  const [inactiveActionTarget, setInactiveActionTarget] = useState<{ emp: Employee; action: 'reactivate' | 'purge' } | null>(null);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [sortField, setSortField] = useState<SortField>('displayName');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [schedulePeriods, setSchedulePeriods] = useState<SchedulePeriod[]>([
    { effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: null, shiftTime: '', scheduleBlocks: [{ start: '', end: '', days: [] }], isRepeating: false },
  ]);
  const [confirmSchedule, setConfirmSchedule] = useState<string | null>(null);
const [pendingSave, setPendingSave] = useState<{
    payload: any;
    tempPassword: string | null;
    url: string;
    method: string;
  } | null>(null);

  const normalize = (value: string) =>
    value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase();
  const allDays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const roleLabel = roleLabelForValue;
  const dayLabel: Record<string, string> = {
    Mon: 'Monday',
    Tue: 'Tuesday',
    Wed: 'Wednesday',
    Thu: 'Thursday',
    Fri: 'Friday',
    Sat: 'Saturday',
    Sun: 'Sunday',
  };
  const todayIso = new Date().toISOString().slice(0, 10);
  const countryOptions = useMemo(() => buildCountryOptions(), []);
  const weeklyHoursLimit = 45;

  const calculateDurationHours = (start: string, end: string) => {
    const [startHour, startMinute] = start.split(':').map(Number);
    const [endHour, endMinute] = end.split(':').map(Number);
    if ([startHour, startMinute, endHour, endMinute].some((value) => Number.isNaN(value))) return 0;
    const startMinutes = startHour * 60 + startMinute;
    let endMinutes = endHour * 60 + endMinute;
    if (endMinutes <= startMinutes) endMinutes += 24 * 60;
    return Math.round(((endMinutes - startMinutes) / 60) * 100) / 100;
  };

  const calculateSchedulePeriodWeeklyHours = (period: SchedulePeriod) =>
    period.scheduleBlocks.reduce(
      (total, block) => total + calculateDurationHours(block.start, block.end) * block.days.length,
      0,
    );

  const buildWeeklyHoursWarning = () => {
    const exceeded = schedulePeriods
      .map((period, index) => ({
        index,
        period,
        hours: calculateSchedulePeriodWeeklyHours(period),
      }))
      .filter((item) => item.hours > weeklyHoursLimit);

    if (!exceeded.length) return '';

    const lines = exceeded.map(({ index, period, hours }) => {
      const range = period.effectiveTo ? `${period.effectiveFrom} to ${period.effectiveTo}` : `${period.effectiveFrom} onward`;
      return `Period ${index + 1} (${range}) totals ${hours} weekly hours.`;
    });

    return [
      `Warning: this schedule exceeds the ${weeklyHoursLimit}-hour weekly limit.`,
      ...lines,
      'You can still save it if this exception is intentional.',
    ].join('\n');
  };

  useEffect(() => {
    if (activeTab === 'calendar') {
      document.title = 'Shift Calendar';
      return;
    }
    if (activeTab === 'requests') {
      document.title = 'Requests';
      return;
    }
    if (activeTab === 'super-admin') {
      document.title = viewMode === 'edit' ? 'Edit Admin' : viewMode === 'create' ? 'Create Admin' : 'Super Admin';
      return;
    }
    document.title = viewMode === 'edit' ? 'Edit Employee' : 'Employees';
  }, [activeTab, viewMode]);

  const viewTransitionKey = useMemo(() => {
    if (activeTab === 'employees' || activeTab === 'super-admin') {
      return `${activeTab}-${viewMode}-${memberScope}`;
    }
    return activeTab;
  }, [activeTab, memberScope, viewMode]);

  useLayoutEffect(() => {
    if (!viewStageRef.current) return;

    const ctx = gsap.context(() => {
      gsap.killTweensOf(viewStageRef.current);
      gsap.fromTo(
        viewStageRef.current,
        {
          autoAlpha: 0,
          y: 18,
          filter: 'blur(8px)',
        },
        {
          autoAlpha: 1,
          y: 0,
          filter: 'blur(0px)',
          duration: 0.42,
          ease: 'power3.out',
          clearProps: 'filter',
        },
      );
    }, viewStageRef);

    return () => ctx.revert();
  }, [viewTransitionKey]);

  useLayoutEffect(() => {
    if ((activeTab !== 'employees' && activeTab !== 'super-admin') || viewMode === 'list' || !employeeFormRef.current) return;

    const ctx = gsap.context(() => {
      const targets = employeeFormRef.current?.querySelectorAll(
        'h2, .helper, .section-block, .schedule-block, .actions',
      );

      if (!targets?.length) return;

      gsap.fromTo(
        targets,
        { autoAlpha: 0, y: 14, filter: 'blur(6px)' },
        {
          autoAlpha: 1,
          y: 0,
          filter: 'blur(0px)',
          duration: 0.34,
          stagger: 0.05,
          ease: 'power2.out',
          clearProps: 'filter',
        },
      );
    }, employeeFormRef);

    return () => ctx.revert();
  }, [activeTab, viewMode, editingId]);

  useLayoutEffect(() => {
    if (!openMenuId) return;

    const menu = document.querySelector<HTMLElement>(`[data-actions-menu="${openMenuId}"]`);
    if (!menu) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        menu,
        { autoAlpha: 0, y: -8, scale: 0.96 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.18, ease: 'power2.out' },
      );
    }, menu);

    return () => ctx.revert();
  }, [openMenuId]);

  const allowedRoles = useMemo(() => {
    if (activeTab === 'super-admin') return roleOptions.filter((r) => r.value === '2');
    if (isAdminRole(user.role)) return roleOptions;
    if (isManagerRole(user.role)) return roleOptions.filter((r) => r.value !== '2');
    return [];
  }, [activeTab, user.role]);

  const allowedCompanyOptions = useMemo(() => {
    const fromUser = user.companies?.length ? user.companies : user.company ? [user.company] : [];
    const fromEmployees = employees.flatMap((emp) => emp.companies?.length ? emp.companies : [emp.company]);
    const activeCatalog = companyCatalog.filter((company) => company.isActive).map((company) => company.name);
    const source = user.isSystemHidden
      ? [...companyOptions, ...activeCatalog, ...fromEmployees, ...createForm.companies]
      : fromUser.length
        ? fromUser
        : companyOptions;

    return source
      .map((company) => company.trim())
      .filter(Boolean)
      .filter((company, index, values) => values.findIndex((item) => item.toLowerCase() === company.toLowerCase()) === index);
  }, [companyCatalog, createForm.companies, employees, user.companies, user.company, user.isSystemHidden]);

  const companySearchOptions = useMemo(
    () => allowedCompanyOptions.map((company) => ({ value: company, label: company })),
    [allowedCompanyOptions],
  );

  const operationsForCompany = (companyName: string, includeInactive = false) => companyOperations
    .filter((operation) => operation.companyName.toLowerCase() === companyName.trim().toLowerCase())
    .filter((operation) => includeInactive || operation.isActive)
    .map((operation) => operation.name)
    .filter((operation, index, values) => values.findIndex((item) => item.toLowerCase() === operation.toLowerCase()) === index)
    .sort((a, b) => a.localeCompare(b));

  const createOperationOptions = useMemo(() => {
    const primaryCompany = createForm.company || createForm.companies[0] || '';
    const catalogValues = primaryCompany ? operationsForCompany(primaryCompany) : [];
    const employeeValues = employees
      .filter((employee) => !primaryCompany || employee.company.toLowerCase() === primaryCompany.toLowerCase())
      .map((employee) => employee.operation);
    return [...catalogValues, ...employeeValues, createForm.operation]
      .map((value) => value?.trim())
      .filter(Boolean)
      .filter((value, index, values) => values.findIndex((item) => item.toLowerCase() === value.toLowerCase()) === index)
      .sort((a, b) => a.localeCompare(b));
  }, [companyOperations, createForm.company, createForm.companies, createForm.operation, employees]);

  const coverageOperationOptions = useMemo(() => {
    const catalogValues = coverageCompany ? operationsForCompany(coverageCompany) : [];
    const employeeValues = employees
      .filter((employee) => !coverageCompany || employee.company.toLowerCase() === coverageCompany.toLowerCase())
      .map((employee) => employee.operation);
    return [...catalogValues, ...employeeValues, createForm.operation]
      .map((value) => value?.trim())
      .filter(Boolean)
      .filter((value, index, values) => values.findIndex((item) => item.toLowerCase() === value.toLowerCase()) === index)
      .sort((a, b) => a.localeCompare(b));
  }, [companyOperations, coverageCompany, createForm.operation, employees]);

  const fetchCompanies = async () => {
    if (!user.isSystemHidden) return;
    try {
      const res = await apiFetch('/companies?includeInactive=true');
      if (!res.ok) return;
      const data = (await res.json()) as CompanyCatalogItem[];
      setCompanyCatalog(data);
    } catch {
      // Keep the inferred company list if the catalog endpoint is unavailable.
    }
  };

  const fetchCompanyOperations = async () => {
    try {
      const res = await apiFetch(`/companies/operations${user.isSystemHidden ? '?includeInactive=true' : ''}`);
      if (!res.ok) return;
      const data = (await res.json()) as CompanyOperationItem[];
      setCompanyOperations(data);
    } catch {
      // Keep operation options inferred from employees if the catalog endpoint is unavailable.
    }
  };

  const pulseOverlay = () => {
    setNavOverlay(true);
    setTimeout(() => setNavOverlay(false), 400);
  };

  const fetchEmployees = async (scope: 'active' | 'inactive' = memberScope) => {
    setEmployeesLoading(true);
    setEmployeesError(null);
    setTabSwitching(true);
    try {
      const endpoint =
        activeTab === 'super-admin' && superAdminScope === 'super-admins'
          ? '/users/system-hidden'
          : scope === 'inactive'
            ? '/users/inactive'
            : '/users';
      const res = await apiFetch(endpoint);
      if (!res.ok) {
        setEmployeesError('Unable to load employees.');
        return;
      }
      const data = (await res.json()) as Employee[];
      setEmployees(data);
      setPage(1);
      setOpenMenuId(null);
    } catch {
      setEmployeesError('Unable to load employees.');
    } finally {
      setEmployeesLoading(false);
      setTabSwitching(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'employees' || activeTab === 'super-admin') {
      const load = async () => {
        await fetchCompanyOperations();
        if (activeTab === 'super-admin') await fetchCompanies();
        await fetchEmployees(activeTab === 'super-admin' ? 'active' : memberScope);
        setViewMode((current) => (current === 'edit' || current === 'create' ? current : 'list'));
      };
      load();
    } else {
      setTabSwitching(false);
    }
  }, [activeTab, memberScope, superAdminScope]);

  useEffect(() => {
    fetchCompanyOperations();
  }, []);

  useEffect(() => {
    if (!companyModalOpen || selectedCompanyName) return;
    const first = companyCatalog[0]?.name ?? '';
    if (first) {
      setSelectedCompanyName(first);
      setCompanyRenameDraft(first);
    }
  }, [companyCatalog, companyModalOpen, selectedCompanyName]);

  useEffect(() => {
    const primaryCompany = createForm.company || createForm.companies[0] || '';
    if (!primaryCompany || !createForm.operation) return;
    const allowed = createOperationOptions.some((operation) => operation.toLowerCase() === createForm.operation.toLowerCase());
    if (!allowed) {
      setCreateForm((form) => ({ ...form, operation: '' }));
    }
  }, [createForm.company, createForm.companies, createForm.operation, createOperationOptions]);

  useEffect(() => {
    if (!coverageModalOpen) return;
    const fallbackCompany = coverageCompany || allowedCompanyOptions[0] || user.company || '';
    if (fallbackCompany !== coverageCompany) {
      setCoverageCompany(fallbackCompany);
      loadCoverageRules(fallbackCompany, coverageOperation);
      return;
    }
    if (fallbackCompany) {
      loadCoverageRules(fallbackCompany, coverageOperation);
    }
  }, [coverageModalOpen]);

  useEffect(() => {
    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as HTMLElement | null;
      if (!target?.closest('.actions-cell')) {
        setOpenMenuId(null);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    return () => document.removeEventListener('mousedown', handlePointerDown);
  }, []);

  const resetCreateForm = () => {
    setCreateForm({
      firstName: '',
      lastName: '',
      email: '',
      role: '',
      location: '',
      company: '',
      companies: [],
      operation: '',
      isSystemHidden: false,
      appearsInSchedule: true,
    });
    setHasUnsaved(false);
    setEditingId(null);
    setSchedulePeriods([{ effectiveFrom: todayIso, effectiveTo: null, shiftTime: '', scheduleBlocks: [{ start: '', end: '', days: [] }], isRepeating: false }]);
  };

  const startCreate = () => {
    setMemberScope('active');
    resetCreateForm();
    setViewMode('create');
    pulseOverlay();
  };

  const startCreateAdmin = () => {
    setMemberScope('active');
    resetCreateForm();
    setCreateForm((form) => ({
      ...form,
      role: '2',
      isSystemHidden: superAdminScope === 'super-admins',
      appearsInSchedule: superAdminScope !== 'super-admins',
    }));
    setViewMode('create');
    navigate('/app/super-admin');
    pulseOverlay();
  };

  const startCreateFromCalendar = () => {
    setMemberScope('active');
    resetCreateForm();
    setSearchTerm('');
    setOpenMenuId(null);
    setPage(1);
    setTabSwitching(true);
    setViewMode('create');
    pulseOverlay();
    navigate('/app/employees');
    fetchEmployees('active');
  };

  const formatBulkUploadMessage = (data: BulkUserUploadResponse | null, fallback: string) => {
    if (!data) return fallback;
    const errors = data.errors ?? [];
    if (!errors.length) return data.message || fallback;

    const details = errors
      .slice(0, 20)
      .map((error) => {
        const row = error.row > 0 ? `Row ${error.row}` : 'Workbook';
        const email = error.email ? ` - ${error.email}` : '';
        return `${row} - ${error.column}${email}: ${error.message}`;
      });
    const remaining = errors.length > details.length ? `\n...and ${errors.length - details.length} more error(s).` : '';
    return `${data.message || fallback}\n\n${details.join('\n')}${remaining}`;
  };

  const handleBulkUploadClick = () => {
    setBulkUploadModalOpen(true);
  };

  const handleChooseBulkUploadFile = () => {
    bulkUploadInputRef.current?.click();
  };

  const handleBulkUploadChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      setCreateModal({ title: 'Invalid file', message: 'Please upload the ShiftTrack .xlsx bulk template.', variant: 'error' });
      return;
    }

    setBulkUploading(true);
    try {
      const body = new FormData();
      body.append('file', file);
      const res = await apiFetch('/users/bulk-upload', {
        method: 'POST',
        body,
      });
      const data = (await res.json().catch(() => null)) as BulkUserUploadResponse | null;

      if (res.ok) {
        setCreateModal({
          title: 'Bulk upload completed',
          message: data?.message ?? 'Bulk upload completed.',
          variant: 'info',
        });
        setBulkUploadModalOpen(false);
        await fetchEmployees('active');
        setMemberScope('active');
      } else {
        setCreateModal({
          title: res.status === 403 ? 'Bulk upload not allowed' : 'Bulk upload failed',
          message: formatBulkUploadMessage(data, 'Bulk upload failed. No users were changed.'),
          variant: 'error',
        });
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setBulkUploading(false);
    }
  };

  const startEdit = (emp: Employee, targetTab: AppTab = activeTab) => {
    const parts = (emp.displayName ?? '').trim().split(' ').filter(Boolean);
    const first = parts.shift() ?? '';
    const last = parts.join(' ');
    setCreateForm({
      firstName: first,
      lastName: last,
      email: emp.email,
      role: String(emp.role),
      location: emp.location,
      company: emp.company,
      companies: emp.companies?.length ? emp.companies : emp.company ? [emp.company] : [],
      operation: emp.operation,
      isSystemHidden: !!emp.isSystemHidden,
      appearsInSchedule: !emp.isSystemHidden || !!emp.schedulePeriods?.length || !!emp.shiftTime,
    });
    setViewMode('edit');
    setEditingId(emp.id);
    setHasUnsaved(false);
    navigate(`/app/${targetTab}`);
    setSchedulePeriods(
      emp.schedulePeriods && emp.schedulePeriods.length
        ? emp.schedulePeriods.map((period) => ({
            effectiveFrom: period.effectiveFrom,
            effectiveTo: period.effectiveTo ?? null,
            shiftTime: period.shiftTime,
            isRepeating: !!period.isRepeating,
            scheduleBlocks: period.scheduleBlocks?.length
              ? period.scheduleBlocks.map((block) => ({ start: block.start, end: block.end, days: block.days ?? [] }))
              : [{ start: '', end: '', days: [] }],
          }))
        : [{ effectiveFrom: todayIso, effectiveTo: null, shiftTime: emp.shiftTime, scheduleBlocks: [{ start: '', end: '', days: [] }], isRepeating: false }]
    );
    pulseOverlay();
  };

  const generateTempPassword = () => {
    const specials = '!@#$%*';
    const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const lower = 'abcdefghijkmnopqrstuvwxyz';
    const digits = '23456789';
    const pick = (pool: string) => pool[Math.floor(Math.random() * pool.length)];
    let pwd = [pick(upper), pick(lower), pick(digits), pick(specials)].join('');
    const all = upper + lower + digits + specials;
    while (pwd.length < 10) pwd += pick(all);
    return pwd.split('').sort(() => Math.random() - 0.5).join('');
  };

  const validateCreateForm = (): string | null => {
    const f = createForm;
    const isSystemHiddenForm = activeTab === 'super-admin' && (superAdminScope === 'super-admins' || !!f.isSystemHidden);
    const requiresSchedule = !isSystemHiddenForm || !!f.appearsInSchedule;
    if (!f.firstName) return 'First Name';
    if (!f.lastName) return 'Last Name';
    if (!f.email) return 'Email';
    if (!emailRegex.test(f.email.trim())) return 'Email format';
    if (!f.role) return 'Role';
    if (!f.location) return 'Location';
    if (!isSystemHiddenForm && (!f.company || !f.companies.length)) return 'Company';
    if (activeTab !== 'super-admin' && !f.operation) return 'Operation';
    if (requiresSchedule) {
      const scheduleError = validateSchedulePeriods();
      if (scheduleError) return scheduleError;
    }
    return null;
  };

  const validateSchedulePeriods = () => {
    if (!schedulePeriods.length) return 'Please add at least one schedule period.';
    const sorted = [...schedulePeriods]
      .map((period, index) => ({ period, index }))
      .sort((a, b) => a.period.effectiveFrom.localeCompare(b.period.effectiveFrom));

    for (let i = 0; i < sorted.length; i++) {
      const { period, index } = sorted[i];
      if (!period.effectiveFrom) return `Effective from is required in period ${index + 1}.`;
      if (!period.shiftTime) return `Shift time is required in period ${index + 1}.`;
      if (!period.scheduleBlocks.length) return `Please add at least one schedule block in period ${index + 1}.`;
      if (period.isRepeating && !period.effectiveTo) {
        return `Valid until is required in period ${index + 1} when automatic repeat is enabled.`;
      }
      if (period.effectiveTo && period.effectiveTo < period.effectiveFrom) {
        return `Effective to must be after effective from in period ${index + 1}.`;
      }

      const used = new Set<string>();
      for (const block of period.scheduleBlocks) {
        if (!block.start) return `Start time is required in period ${index + 1}.`;
        if (!block.end) return `End time is required in period ${index + 1}.`;
        if (!block.days.length) return `Select at least one day in each block for period ${index + 1}.`;
        for (const day of block.days) {
          if (used.has(day)) return `Day ${day} is selected in multiple blocks for period ${index + 1}.`;
          used.add(day);
        }
      }

      const currentEnd = period.effectiveTo || '9999-12-31';
      for (let j = i + 1; j < sorted.length; j++) {
        const next = sorted[j].period;
        const nextEnd = next.effectiveTo || '9999-12-31';
        if (period.effectiveFrom <= nextEnd && next.effectiveFrom <= currentEnd) {
          return 'Schedule periods cannot overlap.';
        }
      }
    }

    const repeatingCount = schedulePeriods.filter((period) => period.isRepeating).length;
    if (repeatingCount > 0 && repeatingCount < 2) {
      return 'Automatically repeat shift period requires at least two schedule periods.';
    }
    if (repeatingCount > 0 && repeatingCount !== schedulePeriods.length) {
      return 'Automatically repeat shift period must include every schedule period in the cycle.';
    }

    return null;
  };

  const daysUsedElsewhere = (periodIndex: number, blockIndex: number) => {
    const used = new Set<string>();
    schedulePeriods[periodIndex]?.scheduleBlocks.forEach((block, index) => {
      if (index === blockIndex) return;
      block.days.forEach((day) => used.add(day));
    });
    return used;
  };

  const updatePeriod = (periodIndex: number, updater: (period: SchedulePeriod) => SchedulePeriod) => {
    setSchedulePeriods((periods) => periods.map((period, index) => (index === periodIndex ? updater(period) : period)));
    setHasUnsaved(true);
  };

  const updateBlock = (periodIndex: number, blockIndex: number, updater: (block: ScheduleBlock) => ScheduleBlock) => {
    updatePeriod(periodIndex, (period) => ({
      ...period,
      scheduleBlocks: period.scheduleBlocks.map((block, index) => (index === blockIndex ? updater(block) : block)),
    }));
  };

  const lastBlockComplete = (periodIndex: number) => {
    const blocks = schedulePeriods[periodIndex]?.scheduleBlocks ?? [];
    const last = blocks[blocks.length - 1];
    return !!last?.start && !!last?.end && (last?.days?.length ?? 0) > 0;
  };

  const addScheduleBlock = (periodIndex: number) => {
    updatePeriod(periodIndex, (period) => {
      if (period.scheduleBlocks.length >= 6) return period;
      return { ...period, scheduleBlocks: [...period.scheduleBlocks, { start: '', end: '', days: [] }] };
    });
  };

  const removeScheduleBlock = (periodIndex: number, blockIndex: number) => {
    updatePeriod(periodIndex, (period) => {
      if (period.scheduleBlocks.length <= 1) return period;
      const next = period.scheduleBlocks.filter((_, index) => index !== blockIndex);
      return {
        ...period,
        scheduleBlocks: next.length ? next : [{ start: '', end: '', days: [] }],
      };
    });
  };

  const removeSchedulePeriod = (periodIndex: number) => {
    setSchedulePeriods((periods) => {
      if (periods.length <= 1) return periods;
      const next = periods.filter((_, index) => index !== periodIndex);
      return next.length < 2 ? next.map((period) => ({ ...period, isRepeating: false })) : next;
    });
    setHasUnsaved(true);
  };

  const toggleRepeatingSchedule = (checked: boolean) => {
    setSchedulePeriods((periods) =>
      periods.map((period) => ({
        ...period,
        effectiveTo: checked ? period.effectiveTo || period.effectiveFrom : period.effectiveTo,
        isRepeating: checked,
      })),
    );
    setHasUnsaved(true);
  };

  const buildScheduleSummary = () => {
    return schedulePeriods
      .map((period, index) => {
        const map: Record<string, string> = {};
        allDays.forEach((day) => (map[day] = 'Day Off'));
        period.scheduleBlocks.forEach((block) => {
          block.days.forEach((day) => {
            map[day] = `${block.start} - ${block.end}`;
          });
        });

        const range = period.effectiveTo
          ? `${period.effectiveFrom} to ${period.effectiveTo}`
          : `${period.effectiveFrom} onward`;
        const daysSummary = allDays.map((day) => `${dayLabel[day]}: ${map[day]}`).join('\n');
        const repeatLabel = period.isRepeating ? '\nAutomatically repeats as part of the shift cycle' : '';
        return `Period ${index + 1} (${range})${repeatLabel}\nShift: ${period.shiftTime}\n${daysSummary}`;
      })
      .join('\n\n');
  };

  const addNextSchedulePeriod = (periodIndex: number) => {
    setSchedulePeriods((periods) => {
      const current = periods[periodIndex];
      if (!current?.effectiveTo) return periods;

      const nextFrom = nextIsoDate(current.effectiveTo);
      const next = [...periods];
      next.splice(periodIndex + 1, 0, {
        effectiveFrom: nextFrom,
        effectiveTo: null,
        shiftTime: current.shiftTime,
        isRepeating: false,
        scheduleBlocks: current.scheduleBlocks.map((block) => ({ ...block, days: [...block.days] })),
      });
      return next;
    });
    setHasUnsaved(true);
  };

  const performDelete = async (emp: Employee) => {
    if (user.role < 1) return;
    setDeleteLoading(true);
    try {
      const res = await apiFetch(`/users/${emp.id}`, {
        method: 'DELETE',
      });
      if (res.ok) {
        fetchEmployees();
      } else if (res.status === 403) {
        setCreateModal({ title: 'Not allowed', message: 'You do not have permission to delete this user.', variant: 'error' });
      } else {
        setCreateModal({ title: 'Could not delete', message: 'Unable to delete user.', variant: 'error' });
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setDeleteLoading(false);
    }
  };

  const performReactivate = async (emp: Employee) => {
    if (user.role < 1) return;
    setDeleteLoading(true);
    try {
      const res = await apiFetch(`/users/${emp.id}/reactivate`, {
        method: 'PUT',
      });
      if (res.ok) {
        fetchEmployees(memberScope);
      } else if (res.status === 403) {
        setCreateModal({ title: 'Not allowed', message: 'You do not have permission to reactivate this user.', variant: 'error' });
      } else {
        setCreateModal({ title: 'Could not reactivate', message: 'Unable to reactivate user.', variant: 'error' });
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setDeleteLoading(false);
    }
  };

  const performPurge = async (emp: Employee) => {
    if (user.role < 1) return;
    setDeleteLoading(true);
    try {
      const res = await apiFetch(`/users/${emp.id}/purge`, {
        method: 'DELETE',
        headers: {
          'X-Purge-Confirm': 'PURGE',
        },
      });
      if (res.ok) {
        fetchEmployees(memberScope);
      } else if (res.status === 400) {
        setCreateModal({ title: 'Could not delete', message: 'Only inactive users can be permanently deleted.', variant: 'error' });
      } else if (res.status === 403) {
        setCreateModal({ title: 'Not allowed', message: 'You do not have permission to permanently delete this user.', variant: 'error' });
      } else {
        setCreateModal({ title: 'Could not delete', message: 'Unable to permanently delete user.', variant: 'error' });
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleCreateSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const missing = validateCreateForm();
    if (missing) {
      setCreateModal({ title: 'Missing Data', message: 'Please complete the required field: ' + missing + '.', variant: 'error' });
      return;
    }
    const tempPassword = viewMode === 'edit' ? null : generateTempPassword();
    setCreateSubmitting(true);
    try {
      const isSystemHiddenPayload = activeTab === 'super-admin' && (superAdminScope === 'super-admins' || !!createForm.isSystemHidden);
      const shouldAppearInSchedule = !isSystemHiddenPayload || !!createForm.appearsInSchedule;
      if (shouldAppearInSchedule) {
        const scheduleError = validateSchedulePeriods();
        if (scheduleError) {
          setCreateModal({ title: 'Missing Data', message: scheduleError, variant: 'error' });
          setCreateSubmitting(false);
          return;
        }
      }
      const payload = {
        firstName: createForm.firstName.trim(),
        lastName: createForm.lastName.trim(),
        email: createForm.email.trim(),
        password: tempPassword ?? undefined,
        role: activeTab === 'super-admin' ? 2 : parseInt(createForm.role, 10),
        location: createForm.location,
        company: isSystemHiddenPayload ? createForm.company.trim() : createForm.company,
        companies: isSystemHiddenPayload ? createForm.companies.filter(Boolean) : createForm.companies,
        operation: activeTab === 'super-admin' ? (createForm.operation || 'Admin') : createForm.operation,
        isSystemHidden: isSystemHiddenPayload,
        schedulePeriods: shouldAppearInSchedule ? schedulePeriods : [],
      };

      const url = viewMode === 'edit' && editingId ? `/users/${editingId}` : '/users';
      const method = viewMode === 'edit' ? 'PUT' : 'POST';

      // Show confirmation modal with schedule summary before saving
      setPendingSave({ payload, tempPassword, url, method });
      if (shouldAppearInSchedule) {
        const weeklyHoursWarning = buildWeeklyHoursWarning();
        setConfirmSchedule([weeklyHoursWarning, buildScheduleSummary()].filter(Boolean).join('\n\n'));
      } else {
        setConfirmSchedule('This super admin will not appear in the schedule and no shift will be assigned.');
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCreateSubmitting(false);
    }
  };

  const executeSave = async () => {
    if (!pendingSave) return;
    setCreateSubmitting(true);
    const { payload, url, method } = pendingSave;
    try {
      const updatePayload = {
        firstName: payload.firstName,
        lastName: payload.lastName,
        role: payload.role,
        location: payload.location,
        company: payload.company,
        companies: payload.companies,
        operation: payload.operation,
        schedulePeriods: payload.schedulePeriods,
        ...(activeTab === 'super-admin' ? { isSystemHidden: payload.isSystemHidden } : {}),
      };
      const res = await apiFetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json',
        },
        body: method === 'PUT' ? JSON.stringify(updatePayload) : JSON.stringify(payload),
      });

      if (res.ok) {
        setCreateModal({
          title: method === 'PUT' ? 'Employee Updated' : 'Employee Created',
          message:
            method === 'PUT'
              ? 'The employee was updated successfully.'
              : 'Welcome email sent with temporary password.',
          variant: 'info',
        });
        fetchEmployees();
        resetCreateForm();
        setViewMode('list');
      } else {
        const data = (await res.json().catch(() => null)) as ApiError | null;
        setCreateModal({ title: 'Could not save', message: data?.message ?? 'Unable to save user.', variant: 'error' });
      }
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setConfirmSchedule(null);
      setPendingSave(null);
      setCreateSubmitting(false);
    }
  };

  const canManageUsers = canManageUsersForRole(user.role) || user.permissions?.includes('manageUsers');
  const canUseSuperAdmin = !!user.isSystemHidden;
  const canUseReports = isAdminRole(user.role);

  useEffect(() => {
    const segment = location.pathname.split('/')[2];

    if (!segment || (segment !== 'home' && segment !== 'calendar' && segment !== 'employees' && segment !== 'requests' && segment !== 'reports' && segment !== 'super-admin')) {
      navigate('/app/home', { replace: true });
      return;
    }

    if (segment === 'employees' && !canManageUsers) {
      navigate('/app/home', { replace: true });
    }
    if (segment === 'super-admin' && !canUseSuperAdmin) {
      navigate('/app/home', { replace: true });
    }
    if (segment === 'reports' && !canUseReports) {
      navigate('/app/home', { replace: true });
    }
  }, [canManageUsers, canUseReports, canUseSuperAdmin, location.pathname, navigate]);

  const filteredEmployees = useMemo(() => {
    const term = normalize(searchTerm.trim());
    const base = term
      ? employees.filter((e) =>
          [`${e.displayName}`, e.displayName, e.email, e.company, ...(e.companies ?? [])]
            .filter(Boolean)
            .some((field) => normalize(field!).includes(term))
        )
      : employees;
    const direction = sortDirection === 'asc' ? 1 : -1;
    return [...base].sort((a, b) => {
      const aValue =
        sortField === 'role'
          ? roleLabel(a.role)
          : sortField === 'operation'
            ? a.operation ?? ''
            : sortField === 'company'
              ? (a.companies?.length ? a.companies.join(', ') : a.company) ?? ''
            : a[sortField] ?? '';
      const bValue =
        sortField === 'role'
          ? roleLabel(b.role)
          : sortField === 'operation'
            ? b.operation ?? ''
            : sortField === 'company'
              ? (b.companies?.length ? b.companies.join(', ') : b.company) ?? ''
            : b[sortField] ?? '';
      return normalize(String(aValue)).localeCompare(normalize(String(bValue))) * direction;
    });
  }, [employees, searchTerm, sortDirection, sortField]);

  const superAdminRows = useMemo(
    () => superAdminScope === 'super-admins'
      ? filteredEmployees.filter((emp) => emp.isSystemHidden)
      : filteredEmployees.filter((emp) => emp.role === 2 && !emp.isSystemHidden),
    [filteredEmployees, superAdminScope],
  );
  const selectedCompany = useMemo(
    () => companyCatalog.find((company) => company.name.toLowerCase() === selectedCompanyName.toLowerCase()) ?? null,
    [companyCatalog, selectedCompanyName],
  );
  const selectedCompanyOperations = useMemo(
    () => companyOperations
      .filter((operation) => operation.companyName.toLowerCase() === selectedCompanyName.toLowerCase())
      .sort((a, b) => a.name.localeCompare(b.name)),
    [companyOperations, selectedCompanyName],
  );

  const handleTabChange = (tab: AppTab) => {
    if (tab === activeTab) return;
    if (tab !== 'employees' && tab !== 'super-admin') {
      setViewMode('list');
      setEditingId(null);
    }
    if (tab === 'super-admin') {
      setMemberScope('active');
    }
    navigate(`/app/${tab}`);
  };

  const toggleCompany = (company: string) => {
    const normalized = company.trim();
    if (!normalized) return;
    setCreateForm((form) => {
      const exists = form.companies.some((item) => item.toLowerCase() === normalized.toLowerCase());
      const companies = exists
        ? form.companies.filter((item) => item.toLowerCase() !== normalized.toLowerCase())
        : [...form.companies, normalized];

      const nextCompany = companies.includes(form.company) ? form.company : companies[0] ?? '';
      return {
        ...form,
        companies,
        company: nextCompany,
        operation: nextCompany === form.company ? form.operation : '',
      };
    });
    setHasUnsaved(true);
  };

  const saveCompany = async () => {
    const normalized = companyDraft.trim();
    if (!normalized || !user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: normalized }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not save company', message: 'Unable to save company.', variant: 'error' });
        return;
      }
      setCompanyDraft('');
      await fetchCompanies();
      await fetchCompanyOperations();
      setSelectedCompanyName(normalized);
      setCompanyRenameDraft(normalized);
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const setCompanyStatus = async (name: string, isActive: boolean) => {
    if (!user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies/status', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, isActive }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not update company', message: 'Unable to update company status.', variant: 'error' });
        return;
      }
      if (!isActive) {
        setCreateForm((form) => {
          const companies = form.companies.filter((company) => company.toLowerCase() !== name.toLowerCase());
          return { ...form, companies, company: companies.includes(form.company) ? form.company : companies[0] ?? '' };
        });
      }
      await fetchCompanies();
      await fetchCompanyOperations();
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const renameCompany = async () => {
    const currentName = selectedCompanyName.trim();
    const newName = companyRenameDraft.trim();
    if (!currentName || !newName || currentName.toLowerCase() === newName.toLowerCase() || !user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies/name', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currentName, newName }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not rename company', message: 'Unable to update company name.', variant: 'error' });
        return;
      }
      setSelectedCompanyName(newName);
      setCompanyRenameDraft(newName);
      await fetchCompanies();
      await fetchCompanyOperations();
      await fetchEmployees(memberScope);
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const saveOperation = async () => {
    const companyName = selectedCompanyName.trim();
    const name = operationDraft.trim();
    if (!companyName || !name || !user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies/operations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ companyName, name }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not save operation', message: 'Unable to save operation.', variant: 'error' });
        return;
      }
      setOperationDraft('');
      await fetchCompanyOperations();
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const setOperationStatus = async (companyName: string, name: string, isActive: boolean) => {
    if (!user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies/operations/status', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ companyName, name, isActive }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not update operation', message: 'Unable to update operation status.', variant: 'error' });
        return;
      }
      await fetchCompanyOperations();
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const renameOperation = async (companyName: string, currentName: string) => {
    const newName = (operationRenameDrafts[currentName] ?? currentName).trim();
    if (!companyName || !currentName || !newName || newName.toLowerCase() === currentName.toLowerCase() || !user.isSystemHidden) return;
    setCompanySaving(true);
    try {
      const res = await apiFetch('/companies/operations/name', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ companyName, currentName, newName }),
      });
      if (!res.ok) {
        setCreateModal({ title: 'Could not rename operation', message: 'Unable to update operation name.', variant: 'error' });
        return;
      }
      setOperationRenameDrafts((drafts) => {
        const next = { ...drafts };
        delete next[currentName];
        return next;
      });
      await fetchCompanyOperations();
      await fetchEmployees(memberScope);
    } catch {
      setCreateModal({ title: 'Network Error', message: 'We could not reach the server. Please try again.', variant: 'error' });
    } finally {
      setCompanySaving(false);
    }
  };

  const loadCoverageRules = async (company = coverageCompany, operation = coverageOperation) => {
    const normalizedCompany = company.trim();
    if (!normalizedCompany) {
      setCoverageError('Select a company.');
      return;
    }

    setCoverageLoading(true);
    setCoverageError(null);
    try {
      const params = new URLSearchParams({ company: normalizedCompany });
      if (operation.trim()) params.set('operation', operation.trim());
      const res = await apiFetch(`/coverage-rules?${params.toString()}`);
      const json = (await res.json().catch(() => null)) as ApiError | CoverageRule[] | null;
      if (!res.ok) {
        setCoverageError((json as ApiError | null)?.message ?? 'Unable to load coverage rules.');
        return;
      }
      const loadedRules = (json as CoverageRule[]).length ? json as CoverageRule[] : defaultCoverageRules;
      setCoverageRules(loadedRules);
      setCoverageCalculationScope(loadedRules.some((rule) => rule.calculationScope === 'company') ? 'company' : 'operation');
    } catch {
      setCoverageError('We could not reach the server. Please try again.');
    } finally {
      setCoverageLoading(false);
    }
  };

  const saveCoverageRules = async () => {
    const normalizedCompany = coverageCompany.trim();
    if (!normalizedCompany) {
      setCoverageError('Select a company.');
      return;
    }

    const invalid = coverageRules.find((rule) =>
      rule.expectedCoverage < 0 ||
      rule.expectedCoverage > 100 ||
      rule.greenThreshold < 0 ||
      rule.greenThreshold > 100 ||
      rule.yellowThreshold < 0 ||
      rule.yellowThreshold > 100 ||
      rule.greenThreshold < rule.yellowThreshold);
    if (invalid) {
      setCoverageError('Percentages must be 0-100 and green must be greater than or equal to yellow.');
      return;
    }

    setCoverageSaving(true);
    setCoverageError(null);
    try {
      const res = await apiFetch('/coverage-rules', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          companyName: normalizedCompany,
          operationName: coverageOperation.trim() || null,
          calculationScope: coverageCalculationScope,
          rules: coverageRules.map((rule) => ({
            dayOfWeek: rule.dayOfWeek,
            expectedCoverage: Number(rule.expectedCoverage),
            greenThreshold: Number(rule.greenThreshold),
            yellowThreshold: Number(rule.yellowThreshold),
            isActive: rule.isActive,
          })),
        }),
      });
      const json = (await res.json().catch(() => null)) as ApiError | CoverageRule[] | null;
      if (!res.ok) {
        setCoverageError((json as ApiError | null)?.message ?? 'Unable to save coverage rules.');
        return;
      }
      const savedRules = json as CoverageRule[];
      setCoverageRules(savedRules);
      setCoverageCalculationScope(savedRules.some((rule) => rule.calculationScope === 'company') ? 'company' : 'operation');
    } catch {
      setCoverageError('We could not reach the server. Please try again.');
    } finally {
      setCoverageSaving(false);
    }
  };

  const updateCoverageRule = (dayOfWeek: string, field: 'expectedCoverage' | 'greenThreshold' | 'yellowThreshold', value: string) => {
    const numericValue = Number(value);
    setCoverageRules((rules) => rules.map((rule) => (
      rule.dayOfWeek === dayOfWeek
        ? { ...rule, [field]: Number.isFinite(numericValue) ? numericValue : 0 }
        : rule
    )));
  };

  const hideRoleFieldForManagerEditingAdmin = viewMode === 'edit' && isManagerRole(user.role) && createForm.role === '2';
  const hideRoleFieldForSuperAdminTab = activeTab === 'super-admin';
  const isSystemHiddenForm = activeTab === 'super-admin' && (superAdminScope === 'super-admins' || !!createForm.isSystemHidden);
  const showScheduleFields = !isSystemHiddenForm || !!createForm.appearsInSchedule;

  const renderForm = (title: string) => (
    <div ref={employeeFormRef} className="card">
      <h2>{title}</h2>
      {viewMode === 'create' && (
        <p className="helper">
          A temporary password will be generated automatically; the user must change it on first login.
        </p>
      )}
      <form onSubmit={handleCreateSubmit} noValidate>
        <div className="section-block">
          <h3 className="section-title">General Information</h3>
          <section className="form-grid">
            <Field label="First Name*">
              <input
                type="text"
                value={createForm.firstName}
                onChange={(e) => {
                  setCreateForm((f) => ({ ...f, firstName: e.target.value }));
                  setHasUnsaved(true);
                }}
              />
            </Field>
            <Field label="Last Name*">
              <input
                type="text"
                value={createForm.lastName}
                onChange={(e) => {
                  setCreateForm((f) => ({ ...f, lastName: e.target.value }));
                  setHasUnsaved(true);
                }}
              />
            </Field>
            <Field label="Email*">
              <input
                type="email"
                value={createForm.email}
                onChange={(e) => {
                  setCreateForm((f) => ({ ...f, email: e.target.value }));
                  setHasUnsaved(true);
                }}
                disabled={viewMode === 'edit'}
              />
            </Field>
            {!hideRoleFieldForManagerEditingAdmin && !hideRoleFieldForSuperAdminTab && (
              <Field label="Role*">
                <Select
                  value={createForm.role}
                  onChange={(nextValue) => {
                    setCreateForm((f) => ({ ...f, role: nextValue }));
                    setHasUnsaved(true);
                  }}
                  ariaLabel="Role"
                >
                  <option value="">Select</option>
                  {allowedRoles.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </Select>
              </Field>
            )}
          </section>
        </div>

        <div className="section-block">
          <h3 className="section-title">Organizational Details</h3>
          <section className="form-grid">
            <Field label="Location*">
              <SearchableSelect
                value={createForm.location}
                options={countryOptions}
                placeholder="Search country"
                ariaLabel="Location"
                onChange={(nextValue) => {
                  setCreateForm((f) => ({ ...f, location: nextValue }));
                  setHasUnsaved(true);
                }}
              />
            </Field>
            {!isSystemHiddenForm && <Field label="Companies*">
              <div className="company-field-toolbar">
                <SearchableMultiSelect
                  values={createForm.companies}
                  options={companySearchOptions}
                  placeholder="Search companies"
                  ariaLabel="Companies"
                  onToggle={toggleCompany}
                />
                {user.isSystemHidden && (
                  <Button type="button" variant="ghost" size="sm" onClick={() => setCompanyModalOpen(true)}>
                    Edit
                  </Button>
                )}
              </div>
              {createForm.companies.length > 1 && (
                <Select
                  value={createForm.company}
                  onChange={(nextValue) => {
                    setCreateForm((f) => ({ ...f, company: nextValue, operation: '' }));
                    setHasUnsaved(true);
                  }}
                  ariaLabel="Primary Company"
                >
                  {createForm.companies.map((opt) => (
                    <option key={opt} value={opt}>
                      Primary: {opt}
                    </option>
                  ))}
                </Select>
              )}
            </Field>}
            {activeTab !== 'super-admin' && (
              <Field label="Operation*">
                <Select
                  value={createForm.operation}
                  onChange={(nextValue) => {
                    setCreateForm((f) => ({ ...f, operation: nextValue }));
                    setHasUnsaved(true);
                  }}
                  ariaLabel="Operation"
                >
                  <option value="">Select</option>
                  {createOperationOptions.map((opt) => (
                    <option key={opt} value={opt}>
                      {opt}
                    </option>
                  ))}
                </Select>
              </Field>
            )}
          </section>
        </div>

        <div className="section-block">
          <h3 className="section-title">Shift Periods</h3>
          {isSystemHiddenForm && (
            <label className="weekday-toggle schedule-visibility-toggle">
              <input
                type="checkbox"
                checked={!!createForm.appearsInSchedule}
                onChange={(e) => {
                  setCreateForm((f) => ({ ...f, appearsInSchedule: e.target.checked }));
                  setHasUnsaved(true);
                }}
              />
              <span>Show this super admin in the schedule</span>
            </label>
          )}
          {showScheduleFields ? (
            <p className="helper">
              Define when a schedule starts, whether it ends, and optionally chain a new schedule from the next day.
            </p>
          ) : (
            <p className="helper">
              This super admin will have access to all companies and will not receive a shift assignment.
            </p>
          )}

          {showScheduleFields && schedulePeriods.map((period, periodIndex) => {
            return (
              <div key={`${period.effectiveFrom}-${periodIndex}`} className="schedule-block">
                <div className="time-row">
                  <label className="weekday-toggle">
                    <input
                      type="radio"
                      name={`period-mode-${periodIndex}`}
                      checked={!period.effectiveTo}
                      onChange={() => {
                        toggleRepeatingSchedule(false);
                        updatePeriod(periodIndex, (current) => ({ ...current, effectiveTo: null, isRepeating: false }));
                      }}
                    />
                    <span>Fixed schedule</span>
                  </label>
                  <label className="weekday-toggle">
                    <input
                      type="radio"
                      name={`period-mode-${periodIndex}`}
                      checked={!!period.effectiveTo}
                      onChange={() =>
                        updatePeriod(periodIndex, (current) => ({
                          ...current,
                          effectiveTo: current.effectiveTo || current.effectiveFrom,
                        }))
                      }
                    />
                    <span>Valid until</span>
                  </label>
                </div>

                <div className="time-row">
                  <Field label="Effective From*">
                    <DateDisplayInput
                      value={period.effectiveFrom}
                      onChange={(nextValue) => updatePeriod(periodIndex, (current) => ({ ...current, effectiveFrom: nextValue }))}
                    />
                  </Field>
                  <Field label="Shift Time*">
                    <Select
                      value={period.shiftTime}
                      onChange={(nextValue) => updatePeriod(periodIndex, (current) => ({ ...current, shiftTime: nextValue }))}
                      ariaLabel="Shift Time"
                    >
                      <option value="">Select</option>
                      {shiftTimeOptions.map((opt) => (
                        <option key={opt} value={opt}>
                          {opt}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>

                {period.effectiveTo && (
                  <div className="time-row">
                    <Field label="Effective To*">
                      <DateDisplayInput
                        value={period.effectiveTo ?? ''}
                        min={period.effectiveFrom}
                        onChange={(nextValue) => updatePeriod(periodIndex, (current) => ({ ...current, effectiveTo: nextValue }))}
                      />
                    </Field>
                    <div className="time-row-spacer" aria-hidden="true" />
                  </div>
                )}

                {period.effectiveTo && schedulePeriods.length >= 2 && periodIndex === schedulePeriods.length - 1 && (
                  <label className="weekday-toggle schedule-visibility-toggle">
                    <input
                      type="checkbox"
                      checked={schedulePeriods.every((item) => !!item.isRepeating)}
                      onChange={(e) => toggleRepeatingSchedule(e.target.checked)}
                    />
                    <span>Automatically repeat shift period</span>
                  </label>
                )}

                {period.scheduleBlocks.map((block, blockIndex) => {
                  const used = daysUsedElsewhere(periodIndex, blockIndex);
                  const weekDays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'];
                  const weekDaysChecked = weekDays.every((d) => block.days.includes(d));
                  return (
                    <div key={`${periodIndex}-${blockIndex}`} className="schedule-block">
                      <div className="time-row">
                        <Field label="Start*">
                    <input
                      type="time"
                      value={block.start}
                      onChange={(e) => updateBlock(periodIndex, blockIndex, (b) => ({ ...b, start: e.target.value }))}
                    />
                        </Field>
                        <Field label="End*">
                    <input
                      type="time"
                      value={block.end}
                      onChange={(e) => updateBlock(periodIndex, blockIndex, (b) => ({ ...b, end: e.target.value }))}
                    />
                        </Field>
                        <label className="weekday-toggle">
                    <input
                      type="checkbox"
                      checked={weekDaysChecked}
                      onChange={(e) => {
                        if (e.target.checked) {
                          const available = weekDays.filter((d) => !used.has(d));
                          updateBlock(periodIndex, blockIndex, (b) => ({ ...b, days: Array.from(new Set([...b.days, ...available])) }));
                        } else {
                          updateBlock(periodIndex, blockIndex, (b) => ({ ...b, days: b.days.filter((d) => !weekDays.includes(d)) }));
                        }
                      }}
                    />
                    <span>Week Days</span>
                        </label>
                      </div>

                      <div className="days-row">
                        <span className="field-label">Days*</span>
                        <div className="days-list">
                          {allDays.map((d) => {
                      const selected = block.days.includes(d);
                      const disabled = used.has(d);
                      return (
                        <button
                          type="button"
                          key={d}
                          className={`day-chip ${selected ? 'selected' : ''}`}
                          disabled={disabled}
                          onClick={() => {
                            updateBlock(periodIndex, blockIndex, (b) => {
                              const exists = b.days.includes(d);
                              return {
                                ...b,
                                days: exists ? b.days.filter((x) => x !== d) : [...b.days, d],
                              };
                            });
                          }}
                        >
                          {d.toUpperCase()}
                        </button>
                      );
                          })}
                        </div>
                      </div>
                    </div>
                  );
                })}

                <div className="block-actions">
                  {period.scheduleBlocks.length < 6 && lastBlockComplete(periodIndex) && (
                    <Button type="button" className="link-button add-block" variant="ghost" size="sm" onClick={() => addScheduleBlock(periodIndex)}>
                      + Add Schedule Block
                </Button>
              )}
                  {period.scheduleBlocks.length > 1 && (
                    <Button
                      type="button"
                      className="link-button delete-block"
                      variant="ghost"
                      size="sm"
                      onClick={() => removeScheduleBlock(periodIndex, period.scheduleBlocks.length - 1)}
                    >
                  × Delete Schedule Block
                    </Button>
                  )}
                {period.effectiveTo && (
                  <Button type="button" className="link-button add-block" variant="ghost" size="sm" onClick={() => addNextSchedulePeriod(periodIndex)}>
                    + Add next schedule starting the next day
                  </Button>
                )}
                {schedulePeriods.length > 1 && (
                  <Button
                    type="button"
                    className="link-button delete-block"
                    variant="ghost"
                    size="sm"
                    onClick={() => removeSchedulePeriod(periodIndex)}
                  >
                    × Delete Schedule Period
                  </Button>
                )}
              </div>
            </div>
          );
        })}
        </div>

        <div className="actions">
          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              if (hasUnsaved) {
                setUnsavedModal(true);
              } else {
                resetCreateForm();
                setViewMode('list');
              }
            }}
          >
            Cancel
          </Button>
          <Button type="submit" variant="primary" disabled={createSubmitting}>
            {createSubmitting ? 'Saving…' : viewMode === 'edit' ? 'Update' : 'Save'}
          </Button>
        </div>
      </form>
    </div>
  );

  return (
    <div className="dashboard">
      <DashboardAmbientBackground />
      <div className="dashboard-watermark" aria-hidden="true">
        <img src="/branding/by-solvo.png" alt="" />
      </div>
      <Sidebar
        activeTab={activeTab}
        onTabChange={handleTabChange}
        showEmployeesTab={canManageUsers}
        showRequestsTab={true}
        showReportsTab={canUseReports}
        showSuperAdminTab={canUseSuperAdmin}
        userName={user.displayName || user.email}
        userEmail={user.email}
        onLogout={onLogout}
      />

      <div ref={viewStageRef} className="dashboard-view-stage">
        {activeTab === 'home' && (
          <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading home" />}>
            <HomePage user={user} onNavigate={handleTabChange} />
          </Suspense>
        )}

        {activeTab === 'calendar' && (
          <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading calendar" />}>
            <ShiftCalendarPage
              role={user.role}
              userEmail={user.email}
              userName={user.displayName || user.email}
              userCompany={user.company ?? ''}
              userCompanies={user.companies ?? []}
              isSystemHidden={user.isSystemHidden ?? false}
              onCreateEmployee={startCreateFromCalendar}
            />
          </Suspense>
        )}

        {activeTab === 'reports' && canUseReports && (
          <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading reports" />}>
            <ReportsPage />
          </Suspense>
        )}

        {activeTab === 'employees' && (
          <>
            {viewMode === 'list' && (
              <div className="card">
              <div className="card-header employee-card-header">
                <div className="member-scope-wrap">
                  <h2>{memberScope === 'inactive' ? 'Inactive Members' : 'Employees'}</h2>
                  {canManageUsers && (
                    <div className="member-scope-toggle">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        active={memberScope === 'active'}
                        onClick={() => {
                          setMemberScope('active');
                          setPage(1);
                          setOpenMenuId(null);
                        }}
                      >
                        Active Members
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        active={memberScope === 'inactive'}
                        onClick={() => {
                          setMemberScope('inactive');
                          setPage(1);
                          setOpenMenuId(null);
                        }}
                      >
                        Inactive Members
                      </Button>
                    </div>
                  )}
                </div>
                {canManageUsers && memberScope === 'active' && (
                  <Button
                    type="button"
                    className="bulk-upload-btn"
                    variant="ghost"
                    onClick={handleBulkUploadClick}
                    disabled={bulkUploading}
                  >
                    Bulk Load
                  </Button>
                )}
                <div className="header-actions">
                  <input
                    type="search"
                    className="search-input"
                    placeholder="Search by name or email"
                    value={searchTerm}
                    onChange={(e) => {
                      setSearchTerm(e.target.value);
                      setPage(1);
                    }}
                  />
                  <Select
                    className="sort-select"
                    value={sortField}
                    onChange={(nextValue) => {
                      setSortField(nextValue as SortField);
                      setPage(1);
                    }}
                    ariaLabel="Sort field"
                  >
                    <option value="displayName">Sort: Name</option>
                    <option value="email">Sort: Email</option>
                    <option value="role">Sort: Role</option>
                    <option value="company">Sort: Company</option>
                    <option value="operation">Sort: Operation</option>
                  </Select>
                  <Select
                    className="sort-select"
                    value={sortDirection}
                    onChange={(nextValue) => {
                      setSortDirection(nextValue as SortDirection);
                      setPage(1);
                    }}
                    ariaLabel="Sort direction"
                  >
                    <option value="asc">Ascending</option>
                    <option value="desc">Descending</option>
                  </Select>
                  {canManageUsers && memberScope === 'active' && (
                    <Button type="button" className="coverage-rules-btn" variant="ghost" onClick={() => setCoverageModalOpen(true)}>
                      Coverage Rules
                    </Button>
                  )}
                  {canManageUsers && memberScope === 'active' && (
                    <Button className="create-btn" onClick={startCreate}>
                      + Create Employee
                    </Button>
                  )}
                </div>
              </div>
              {employeesLoading && <p className="helper">Loading employees...</p>}
              {employeesError && <div className="alert">{employeesError}</div>}
              {!employeesLoading && !employeesError && (
                <div className="table-wrapper">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Employee Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Company</th>
                        <th>Operation</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredEmployees
                        .slice((page - 1) * pageSize, page * pageSize)
                        .map((emp) => (
                        <tr key={emp.id}>
                          <td>{emp.displayName}</td>
                          <td>{emp.email}</td>
                          <td>{roleLabel(emp.role)}</td>
                          <td>{(emp.companies?.length ? emp.companies : [emp.company]).filter(Boolean).join(', ')}</td>
                          <td>{emp.operation}</td>
                          <td>
                            {canManageUsers ? (
                              <div className="actions-cell">
                                <Button
                                  className="dots-btn"
                                  variant="ghost"
                                  size="sm"
                                  active={openMenuId === emp.id}
                                  aria-label={`Open actions for ${emp.displayName}`}
                                  onClick={() => setOpenMenuId(openMenuId === emp.id ? null : emp.id)}
                                >
                                  <span className="dots-btn-shell" aria-hidden="true">
                                    <span className="dots-btn-dot" />
                                    <span className="dots-btn-dot" />
                                    <span className="dots-btn-dot" />
                                  </span>
                                </Button>
                                {openMenuId === emp.id && (
                                  <div className="menu" data-actions-menu={emp.id}>
                                    {memberScope === 'active' ? (
                                      <>
                                        <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); startEdit(emp); }}>Edit</Button>
                                        <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); setDeleteTarget(emp); }}>Set as Inactive</Button>
                                      </>
                                    ) : (
                                      <>
                                        <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); setInactiveActionTarget({ emp, action: 'reactivate' }); }}>Set as active</Button>
                                        <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); setInactiveActionTarget({ emp, action: 'purge' }); }}>Delete permanently</Button>
                                      </>
                                    )}
                                  </div>
                                )}
                              </div>
                            ) : (
                              <span className="helper">View only</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="pagination-bar">
                    {(() => {
                      const total = filteredEmployees.length;
                      const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
                      const to = Math.min(page * pageSize, total);
                      const totalPages = Math.max(1, Math.ceil(total / pageSize));
                      return (
                        <>
                          <span className="pagination-text">
                            Showing {from} to {to} members (Page {page} of {totalPages})
                          </span>
                          <div className="pager-buttons">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setPage((p) => Math.max(1, p - 1))}
                              disabled={page <= 1}
                            >
                              ‹
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                              disabled={page >= totalPages}
                            >
                              ›
                            </Button>
                          </div>
                        </>
                      );
                    })()}
                  </div>
                </div>
              )}
              </div>
            )}

            {viewMode !== 'list' && (
              renderForm(viewMode === 'edit' ? 'Edit Employee' : 'Create Employee')
            )}
          </>
        )}

        {activeTab === 'super-admin' && canUseSuperAdmin && (
          <>
            {viewMode === 'list' && (
              <div className="card">
                <div className="card-header">
                  <div className="member-scope-wrap">
                    <h2>Super Admin</h2>
                    <div className="member-scope-toggle">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        active={superAdminScope === 'admins'}
                        onClick={() => {
                          setSuperAdminScope('admins');
                          setPage(1);
                          setOpenMenuId(null);
                        }}
                      >
                        Admins
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        active={superAdminScope === 'super-admins'}
                        onClick={() => {
                          setSuperAdminScope('super-admins');
                          setPage(1);
                          setOpenMenuId(null);
                        }}
                      >
                        Super Admins
                      </Button>
                    </div>
                  </div>
                  <div className="header-actions">
                    <input
                      type="search"
                      className="search-input"
                      placeholder="Search admin by name or email"
                      value={searchTerm}
                      onChange={(e) => {
                        setSearchTerm(e.target.value);
                        setPage(1);
                      }}
                    />
                    <Button type="button" variant="ghost" onClick={() => setCoverageModalOpen(true)}>
                      Coverage Rules
                    </Button>
                    <Button type="button" variant="ghost" onClick={() => setCompanyModalOpen(true)}>
                      Manage Companies
                    </Button>
                    <Button className="create-btn" onClick={startCreateAdmin}>
                      {superAdminScope === 'super-admins' ? '+ Create Super Admin' : '+ Create Admin'}
                    </Button>
                  </div>
                </div>
                {employeesLoading && <p className="helper">Loading {superAdminScope === 'super-admins' ? 'super admins' : 'admins'}...</p>}
                {employeesError && <div className="alert">{employeesError}</div>}
                {!employeesLoading && !employeesError && (
                  <div className="table-wrapper">
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Admin Name</th>
                          <th>Email</th>
                          <th>Companies</th>
                          <th>Operation</th>
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {superAdminRows
                          .slice((page - 1) * pageSize, page * pageSize)
                          .map((emp) => (
                          <tr key={emp.id}>
                            <td>{emp.displayName}</td>
                            <td>{emp.email}</td>
                            <td>{(emp.companies?.length ? emp.companies : [emp.company]).filter(Boolean).join(', ')}</td>
                            <td>{emp.operation}</td>
                            <td>
                              <div className="actions-cell">
                                <Button
                                  className="dots-btn"
                                  variant="ghost"
                                  size="sm"
                                  active={openMenuId === emp.id}
                                  aria-label={`Open actions for ${emp.displayName}`}
                                  onClick={() => setOpenMenuId(openMenuId === emp.id ? null : emp.id)}
                                >
                                  <span className="dots-btn-shell" aria-hidden="true">
                                    <span className="dots-btn-dot" />
                                    <span className="dots-btn-dot" />
                                    <span className="dots-btn-dot" />
                                  </span>
                                </Button>
                                {openMenuId === emp.id && (
                                  <div className="menu" data-actions-menu={emp.id}>
                                    <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); startEdit(emp, 'super-admin'); }}>Edit</Button>
                                    {superAdminScope === 'super-admins' && (
                                      <Button variant="ghost" size="sm" onClick={() => { setOpenMenuId(null); setDeleteTarget(emp); }}>Set as Inactive</Button>
                                    )}
                                  </div>
                                )}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    <div className="pagination-bar">
                      {(() => {
                        const total = superAdminRows.length;
                        const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
                        const to = Math.min(page * pageSize, total);
                        const totalPages = Math.max(1, Math.ceil(total / pageSize));
                        return (
                          <>
                            <span className="pagination-text">
                              Showing {from} to {to} {superAdminScope === 'super-admins' ? 'super admins' : 'admins'} (Page {page} of {totalPages})
                            </span>
                            <div className="pager-buttons">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setPage((p) => Math.max(1, p - 1))}
                                disabled={page <= 1}
                              >
                              Prev
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                                disabled={page >= totalPages}
                              >
                              Next
                              </Button>
                            </div>
                          </>
                        );
                      })()}
                    </div>
                  </div>
                )}
              </div>
            )}

            {viewMode !== 'list' && (
              renderForm(viewMode === 'edit' ? 'Edit Admin' : 'Create Admin')
            )}
          </>
        )}

      {createModal && (
        <Modal
          title={createModal.title}
          message={createModal.message}
          variant={createModal.variant}
          onClose={() => setCreateModal(null)}
        />
      )}
      {confirmSchedule && (
        <ConfirmModal
          title="Confirm Weekly Schedule"
          description="Please review the weekly schedule before saving:"
          message={confirmSchedule}
          onCancel={() => {
            setConfirmSchedule(null);
            setPendingSave(null);
          }}
          onOk={executeSave}
        />
      )}
      {deleteTarget && (
        <ConfirmModal
          title="Are you sure you want to set this member as Inactive?"
          description="You can manage them in the Inactive Members tab."
          message={`Employee: ${deleteTarget.displayName}`}
          onCancel={() => setDeleteTarget(null)}
          onOk={() => {
            if (deleteTarget) {
              performDelete(deleteTarget);
            }
            setDeleteTarget(null);
          }}
        />
      )}

        {activeTab === 'requests' && (
          <Suspense fallback={<ShiftTrackLoaderOverlay label="Loading requests" />}>
            <RequestsPage user={user} />
          </Suspense>
        )}
      </div>
      {inactiveActionTarget && (
        <ConfirmModal
          title={inactiveActionTarget.action === 'reactivate' ? 'Set user as active?' : 'Delete user permanently?'}
          description={
            inactiveActionTarget.action === 'reactivate'
              ? 'Are you sure you want to set this user as active again?'
              : 'Are you sure you want to permanently delete this user? This action cannot be undone.'
          }
          message={`Employee: ${inactiveActionTarget.emp.displayName}`}
          onCancel={() => setInactiveActionTarget(null)}
          onOk={() => {
            if (inactiveActionTarget.action === 'reactivate') {
              performReactivate(inactiveActionTarget.emp);
            } else {
              performPurge(inactiveActionTarget.emp);
            }
            setInactiveActionTarget(null);
          }}
        />
      )}
      {unsavedModal && (
        <ConfirmModal
          title="Unsaved Changes"
          description="You have unsaved changes. Are you sure you want to exit?"
          icon="info"
          message=""
          onCancel={() => setUnsavedModal(false)}
          onOk={() => {
            resetCreateForm();
            setUnsavedModal(false);
            setViewMode('list');
          }}
        />
      )}
      {bulkUploadModalOpen && (
        <ModalShell className="bulk-upload-modal" ariaLabel="Bulk load employees" onBackdropClick={() => !bulkUploading && setBulkUploadModalOpen(false)}>
          <div className="bulk-upload-modal-header">
            <div>
              <h2>Bulk Load Employees</h2>
              <p>Download the template, fill it, and upload the completed workbook.</p>
            </div>
            <Button type="button" variant="ghost" size="sm" onClick={() => setBulkUploadModalOpen(false)} disabled={bulkUploading}>
              Close
            </Button>
          </div>

          <div className="bulk-upload-modal-body">
            <div className="bulk-upload-step">
              <span className="bulk-upload-step-number">1</span>
              <div className="bulk-upload-step-content">
                <h3>Template</h3>
                <p>Use this exact file. Do not rename the sheet or headers.</p>
              </div>
              <a
                className="bulk-upload-download"
                href="/templates/ShiftTrack_Bulk_User_Upload_Template.xlsx"
                download="ShiftTrack_Bulk_User_Upload_Template.xlsx"
              >
                Download Template
              </a>
            </div>

            <div className="bulk-upload-step">
              <span className="bulk-upload-step-number">2</span>
              <div className="bulk-upload-step-content">
                <h3>Upload</h3>
                <p>The import validates every row first. If one row fails, no user is changed.</p>
              </div>
              <input
                ref={bulkUploadInputRef}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                className="bulk-upload-input"
                onChange={handleBulkUploadChange}
              />
              <Button
                type="button"
                className="bulk-upload-choose"
                variant="primary"
                onClick={handleChooseBulkUploadFile}
                disabled={bulkUploading}
              >
                {bulkUploading ? 'Uploading...' : 'Choose File'}
              </Button>
            </div>
          </div>
        </ModalShell>
      )}
      {coverageModalOpen && (
        <ModalShell className="coverage-rules-modal" ariaLabel="Manage coverage rules" onBackdropClick={() => setCoverageModalOpen(false)}>
          <div className="company-modal-header">
            <h2>Coverage Rules</h2>
            <Button type="button" variant="ghost" size="sm" onClick={() => setCoverageModalOpen(false)}>
              Close
            </Button>
          </div>
          <div className="coverage-rule-controls">
            <label>
              <span>Company</span>
              <Select
                value={coverageCompany}
                ariaLabel="Coverage company"
                onChange={(nextValue) => {
                  setCoverageCompany(nextValue);
                  loadCoverageRules(nextValue, coverageOperation);
                }}
              >
                {allowedCompanyOptions.map((company) => (
                  <option key={company} value={company}>{company}</option>
                ))}
              </Select>
            </label>
            <label>
              <span>Operation</span>
              <Select
                value={coverageOperation}
                ariaLabel="Coverage operation"
                onChange={(nextValue) => {
                  setCoverageOperation(nextValue);
                  loadCoverageRules(coverageCompany, nextValue);
                }}
              >
                <option value="">Company default</option>
                {coverageOperationOptions.map((operation) => (
                  <option key={operation} value={operation}>{operation}</option>
                ))}
              </Select>
            </label>
            <label>
              <span>Calculation Scope</span>
              <Select
                value={coverageCalculationScope}
                ariaLabel="Coverage calculation scope"
                onChange={(nextValue) => setCoverageCalculationScope(nextValue === 'company' ? 'company' : 'operation')}
              >
                <option value="operation">Operation</option>
                <option value="company">Company global</option>
              </Select>
            </label>
          </div>
          {coverageError && <div className="alert">{coverageError}</div>}
          {coverageLoading ? (
            <p className="helper">Loading coverage rules...</p>
          ) : (
            <div className="coverage-rule-board">
              <div className="coverage-rule-board-head">
                <span>Day</span>
                <span>Expected</span>
                <span>Green</span>
                <span>Yellow</span>
              </div>
              <div className="coverage-rule-list">
                {coverageRules.map((rule) => (
                  <div className="coverage-rule-row" key={rule.dayOfWeek}>
                    <div className="coverage-rule-day">
                      <span className="coverage-day-dot" aria-hidden="true" />
                      <strong>{coverageDayLabels[rule.dayOfWeek] ?? rule.dayOfWeek}</strong>
                    </div>
                    {([
                      ['expectedCoverage', 'target', rule.expectedCoverage],
                      ['greenThreshold', 'green', rule.greenThreshold],
                      ['yellowThreshold', 'yellow', rule.yellowThreshold],
                    ] as const).map(([field, tone, value]) => (
                      <label className={`coverage-rule-metric ${tone}`} key={field}>
                        <input
                          type="number"
                          min="0"
                          max="100"
                          value={value}
                          onChange={(e) => updateCoverageRule(rule.dayOfWeek, field, e.target.value)}
                          aria-label={`${rule.dayOfWeek} ${field}`}
                        />
                        <span>%</span>
                      </label>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          )}
          <div className="actions">
            <Button type="button" variant="ghost" onClick={() => loadCoverageRules()}>
              Reset
            </Button>
            <Button type="button" variant="primary" disabled={coverageSaving || coverageLoading} onClick={saveCoverageRules}>
              {coverageSaving ? 'Saving...' : 'Save Rules'}
            </Button>
          </div>
        </ModalShell>
      )}
      {companyModalOpen && (
        <ModalShell className="company-admin-modal" ariaLabel="Manage companies" onBackdropClick={() => setCompanyModalOpen(false)}>
          <div className="company-modal-header">
            <div>
              <span className="company-modal-kicker">Super Admin</span>
              <h2>Company Command Center</h2>
            </div>
            <Button type="button" variant="ghost" size="sm" onClick={() => setCompanyModalOpen(false)}>
              Close
            </Button>
          </div>
          <div className="company-modal-add">
            <input
              type="text"
              value={companyDraft}
              placeholder="Company name"
              onChange={(e) => setCompanyDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  saveCompany();
                }
              }}
            />
            <Button type="button" variant="primary" size="sm" disabled={companySaving || !companyDraft.trim()} onClick={saveCompany}>
              Add
            </Button>
          </div>
          <div className="company-command-layout">
            <aside className="company-command-list">
              {companyCatalog.length ? companyCatalog.map((company) => {
                const count = companyOperations.filter((operation) => operation.companyName.toLowerCase() === company.name.toLowerCase() && operation.isActive).length;
                const selected = company.name.toLowerCase() === selectedCompanyName.toLowerCase();
                return (
                  <button
                    type="button"
                    className={`company-command-item ${selected ? 'selected' : ''}`}
                    key={company.name}
                    onClick={() => {
                      setSelectedCompanyName(company.name);
                      setCompanyRenameDraft(company.name);
                    }}
                  >
                    <span>
                      <strong>{company.name}</strong>
                      <small>{count} active operations</small>
                    </span>
                    <em className={company.isActive ? 'status-active' : 'status-inactive'}>
                      {company.isActive ? 'Active' : 'Inactive'}
                    </em>
                  </button>
                );
              }) : <p className="helper">No companies found.</p>}
            </aside>
            <section className="company-command-detail">
              {selectedCompany ? (
                <>
                  <div className="company-detail-hero">
                    <div>
                      <span className={selectedCompany.isActive ? 'status-active' : 'status-inactive'}>
                        {selectedCompany.isActive ? 'Active company' : 'Inactive company'}
                      </span>
                      <h3>{selectedCompany.name}</h3>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={companySaving}
                      onClick={() => setCompanyStatus(selectedCompany.name, !selectedCompany.isActive)}
                    >
                      {selectedCompany.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                  </div>

                  <div className="company-editor-row">
                    <label>
                      <span>Company name</span>
                      <input
                        type="text"
                        value={companyRenameDraft}
                        onChange={(e) => setCompanyRenameDraft(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault();
                            renameCompany();
                          }
                        }}
                      />
                    </label>
                    <Button type="button" variant="primary" size="sm" disabled={companySaving || !companyRenameDraft.trim() || companyRenameDraft.trim() === selectedCompany.name} onClick={renameCompany}>
                      Save
                    </Button>
                  </div>

                  <div className="operation-composer">
                    <label>
                      <span>New operation for this company</span>
                      <input
                        type="text"
                        value={operationDraft}
                        placeholder="Operation name"
                        onChange={(e) => setOperationDraft(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault();
                            saveOperation();
                          }
                        }}
                      />
                    </label>
                    <Button type="button" variant="primary" size="sm" disabled={companySaving || !operationDraft.trim()} onClick={saveOperation}>
                      Create
                    </Button>
                  </div>

                  <div className="operation-stack">
                    {selectedCompanyOperations.length ? selectedCompanyOperations.map((operation) => {
                      const draft = operationRenameDrafts[operation.name] ?? operation.name;
                      return (
                        <div className="operation-card" key={`${operation.companyName}-${operation.name}`}>
                          <div className="operation-card-main">
                            <span className={operation.isActive ? 'status-active' : 'status-inactive'}>
                              {operation.isActive ? 'Active' : 'Inactive'}
                            </span>
                            <input
                              type="text"
                              value={draft}
                              onChange={(e) => setOperationRenameDrafts((drafts) => ({ ...drafts, [operation.name]: e.target.value }))}
                            />
                          </div>
                          <div className="operation-card-actions">
                            <Button type="button" variant="ghost" size="sm" disabled={companySaving || draft.trim() === operation.name || !draft.trim()} onClick={() => renameOperation(operation.companyName, operation.name)}>
                              Rename
                            </Button>
                            <Button type="button" variant="ghost" size="sm" disabled={companySaving} onClick={() => setOperationStatus(operation.companyName, operation.name, !operation.isActive)}>
                              {operation.isActive ? 'Disable' : 'Enable'}
                            </Button>
                          </div>
                        </div>
                      );
                    }) : <p className="helper">This company does not have operations yet.</p>}
                  </div>
                </>
              ) : (
                <p className="helper">Select or create a company to manage operations.</p>
              )}
            </section>
          </div>
        </ModalShell>
      )}

      {(tabSwitching || createSubmitting || deleteLoading || navOverlay) && <ShiftTrackLoaderOverlay label="Loading" />}
    </div>
  );
}

export default DashboardPage;

