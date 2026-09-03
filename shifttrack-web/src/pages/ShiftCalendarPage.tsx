import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import gsap from 'gsap';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { Button } from '../components/ui/Button';
import { ErrorPopup } from '../components/ui/ErrorPopup';
import { Field } from '../components/ui/Field';
import { Select } from '../components/ui/Select';
import { ConfirmModal, ModalShell } from '../components/Modals';
import { ScheduleAssistant } from '../components/ScheduleAssistant';
import { companyOptions, shiftTimeOptions, TRACKY_ENABLED } from '../lib/constants';
import { apiFetch, getPreferredApiBase, getRealtimeAccessToken } from '../lib/api';
import {
  ROLE_EMPLOYEE,
  canManageUsersForRole,
  canExportCalendarForRole,
  canRequestForOthersForRole,
  canViewCoverageForRole,
  canViewLiveUpdatesForRole,
  isAdminRole,
  isManagerRole,
  isEmployeeLikeRole,
  roleInitialsForValue,
  roleLabelForValue,
} from '../lib/roles';
import { notifySessionActivity } from '../lib/sessionActivity';
import type { CompanyCatalogItem, CompanyOperationItem, PtoCoveragePreview, ScheduleEvent } from '../types';

type CalendarCell = {
  date: string;
  label: string;
  durationHours: number;
  type: string;
  shiftTime: string;
  ptoGroupId?: string;
  ptoRequestType?: string;
  ptoComments?: string;
  isPtoStart?: boolean;
  isDailyScheduleOverride?: boolean;
  scheduleOverrideComments?: string;
};

type CalendarRow = {
  id: string;
  displayName: string;
  email: string;
  role: number;
  location: string;
  company: string;
  operation: string;
  shiftTime: string;
  cells: CalendarCell[];
};

type DayDescriptor = { date: string; label: string };

type HolidayItem = {
  id: string;
  date: string;
  name: string;
  isManual: boolean;
};

type CalendarResponse = {
  weekStart: string;
  weekEnd: string;
  days: DayDescriptor[];
  coverage: CoverageSummary[];
  items: CalendarRow[];
};

type HolidaysResponse = {
  countryCode: string;
  startDate?: string;
  endDate?: string;
  year?: number;
  items: HolidayItem[];
};

type CoverageSummary = {
  date: string;
  dayCode: string;
  expectedCoverage: number;
  coverage: number;
  totalAgents: number;
  statusColor: 'red' | 'yellow' | 'green' | string;
};

const expectedCoverageByDay: Record<string, number> = {
  Mon: 95,
  Tue: 85,
  Wed: 80,
  Thu: 80,
  Fri: 75,
  Sat: 40,
  Sun: 35,
};

const resolveCoverageColor = (dayCode: string, coverage: number): 'red' | 'yellow' | 'green' => {
  if (dayCode === 'Mon') return coverage >= 91 ? 'green' : coverage >= 86 ? 'yellow' : 'red';
  if (dayCode === 'Tue') return coverage >= 81 ? 'green' : coverage >= 71 ? 'yellow' : 'red';
  if (dayCode === 'Wed') return coverage >= 76 ? 'green' : coverage >= 71 ? 'yellow' : 'red';
  if (dayCode === 'Thu') return coverage >= 76 ? 'green' : coverage >= 71 ? 'yellow' : 'red';
  if (dayCode === 'Fri') return coverage >= 71 ? 'green' : coverage >= 66 ? 'yellow' : 'red';
  if (dayCode === 'Sat') return coverage >= 36 ? 'green' : coverage >= 31 ? 'yellow' : 'red';
  return coverage >= 31 ? 'green' : coverage >= 26 ? 'yellow' : 'red';
};

const ptoRequestTypeOptions = [
  { value: 'sick_leave', label: 'Sick Leave' },
  { value: 'maternity_leave', label: 'Maternity Leave' },
  { value: 'birthday', label: 'Birthday' },
  { value: 'holiday', label: 'Holiday' },
  { value: 'family_day', label: 'Family Day' },
  { value: 'fmla', label: 'FMLA' },
  { value: 'vacations', label: 'Vacations' },
  { value: 'unpaid_leave', label: 'Unpaid Leave' },
] as const;

const formatSwapDate = (value?: string) => {
  if (!value) return 'Not selected';
  const parsed = new Date(`${value}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
};

type PtoModalState = {
  open: boolean;
  activeTab: 'pto' | 'swap' | 'dayoff' | 'schedule';
  userId: string;
  userEmail: string;
  userName: string;
  targetRole: number;
  startDate: string;
  numberOfDays: number;
  requestType: string;
  comments: string;
  dailyStartTime: string;
  dailyEndTime: string;
  canChangeDailySchedule: boolean;
  existingGroupId?: string;
  existingRequestId?: string;
  canCancelApproved: boolean;
  swapTargetUserId?: string;
  swapTargetUserEmail?: string;
  swapTargetUserName?: string;
  swapTargetRole?: number;
  swapTargetDate?: string;
  swapRequesterDate?: string;
  swapAvailableRequesterDates?: string[];
  swapRequesterShiftOnTargetDate?: string;
  swapTargetShiftByRequesterDate?: Record<string, string>;
};

type PtoRequestListItem = {
  id: string;
  userId: string;
  status: string;
  overrideGroupId?: string | null;
};

type DragSelectionState = {
  rowId: string;
  startIndex: number;
  endIndex: number;
};

type ToastMessage = {
  id: string;
  tone: 'info' | 'success' | 'error';
  text: string;
};

type PendingPtoPayload = {
  userId: string;
  startDate: string;
  numberOfDays: number;
  requestType: string;
  comments: string | null;
  existingGroupId: string | null;
  employeeFilter?: string | null;
  roleFilter?: string | null;
  shiftFilter?: string | null;
  operationFilter?: string | null;
  companyFilter?: string | null;
};

type PendingDailySchedulePayload = {
  userId: string;
  date: string;
  startTime: string;
  endTime: string;
  comments: string;
};

type Props = {
  role: number;
  userEmail: string;
  userName: string;
  userCompany: string;
  userCompanies: string[];
  isSystemHidden?: boolean;
  onCreateEmployee: () => void;
};

const parseDateOnly = (value: string) => {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, (m || 1) - 1, d || 1);
};

const formatDateKeyLocal = (value: Date) =>
  `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;

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

const weekStartForDate = (value: string) => {
  const date = parseDateOnly(value);
  const diff = (7 + date.getDay() - 1) % 7;
  date.setDate(date.getDate() - diff);
  return date.toISOString().slice(0, 10);
};

const colorFor = (cell: CalendarCell) => {
  if (cell.type === 'shiftLate') return 'late';
  if (cell.type === 'shiftMorning') return 'morning';
  if (cell.type === 'leave') return 'leave';
  return 'dayoff';
};

const isWorkingCalendarCell = (cell?: CalendarCell | null) => cell?.type === 'shiftMorning' || cell?.type === 'shiftLate';

const describeWorkingShift = (cell?: CalendarCell | null) => {
  if (!isWorkingCalendarCell(cell)) return '';
  const label = cell?.label?.trim();
  if (label && label !== cell?.date) {
    return label;
  }
  return cell?.shiftTime?.trim() || 'Working shift';
};

const shiftPriority = (shiftTime: string) => {
  const normalized = shiftTime.trim().toLowerCase();
  if (normalized === 'morning') return 0;
  if (normalized === 'late') return 1;
  return 2;
};

const tryReadDownloadFileName = (contentDisposition: string | null) => {
  if (!contentDisposition) return '';
  const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const basicMatch = contentDisposition.match(/filename="?([^\";]+)"?/i);
  return basicMatch?.[1]?.trim() ?? '';
};

export function ShiftCalendarPage({
  role,
  userEmail,
  userName,
  userCompany,
  userCompanies,
  isSystemHidden = false,
  onCreateEmployee,
}: Props) {
  const canViewCoverage = canViewCoverageForRole(role);
  const canViewLiveUpdates = canViewLiveUpdatesForRole(role);
  const canRequestForOthers = canRequestForOthersForRole(role);
  const canExportCalendar = canExportCalendarForRole(role);
  const today = new Date();
  const startOfWeek = new Date(today);
  const diff = (7 + today.getDay() - 1) % 7; // Monday=1
  startOfWeek.setDate(today.getDate() - diff);
  const [weekStart, setWeekStart] = useState<string>(startOfWeek.toISOString().slice(0, 10));
  const [employeeFilter, setEmployeeFilter] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [shiftFilter, setShiftFilter] = useState('');
  const [operationFilter, setOperationFilter] = useState('');
  const [companyFilter, setCompanyFilter] = useState('');
  const [companyCatalog, setCompanyCatalog] = useState<CompanyCatalogItem[]>([]);
  const [companyOperations, setCompanyOperations] = useState<CompanyOperationItem[]>([]);
  const [data, setData] = useState<CalendarResponse | null>(null);
  const [holidays, setHolidays] = useState<HolidayItem[]>([]);
  const [monthHolidays, setMonthHolidays] = useState<HolidayItem[]>([]);
  const [monthOverviewYear, setMonthOverviewYear] = useState(startOfWeek.getFullYear());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [realtimeStatus, setRealtimeStatus] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting');
  const [lastRealtimeAt, setLastRealtimeAt] = useState<string>('');
  const [updatesOpen, setUpdatesOpen] = useState(false);
  const [eventsLoading, setEventsLoading] = useState(false);
  const [events, setEvents] = useState<ScheduleEvent[]>([]);
  const ptoCloseTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const ptoModalBackdropRef = useRef<HTMLDivElement | null>(null);
  const ptoModalCardRef = useRef<HTMLDivElement | null>(null);
  const ptoModalPanelRef = useRef<HTMLDivElement | null>(null);
  const calendarViewRef = useRef<HTMLDivElement | null>(null);
  const [ptoModal, setPtoModal] = useState<PtoModalState>({
    open: false,
    activeTab: 'pto',
    userId: '',
    userEmail: '',
    userName: '',
    targetRole: 0,
    startDate: '',
    numberOfDays: 1,
    requestType: '',
    comments: '',
    dailyStartTime: '08:00',
    dailyEndTime: '17:00',
    canChangeDailySchedule: false,
    canCancelApproved: false,
    swapAvailableRequesterDates: [],
    swapTargetShiftByRequesterDate: {},
  });
  const [ptoModalClosing, setPtoModalClosing] = useState(false);
  const [swapDatesLoading, setSwapDatesLoading] = useState(false);
  const [savingPto, setSavingPto] = useState(false);
  const [savingSwap, setSavingSwap] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [coverageConfirm, setCoverageConfirm] = useState<{ message: string; payload: PendingPtoPayload | null }>({
    message: '',
    payload: null,
  });
  const [dailyScheduleConfirm, setDailyScheduleConfirm] = useState<{ message: string; payload: PendingDailySchedulePayload | null }>({
    message: '',
    payload: null,
  });
  const [selectedEmployee, setSelectedEmployee] = useState<CalendarRow | null>(null);
  const [dragSelection, setDragSelection] = useState<DragSelectionState | null>(null);
  const [viewMode, setViewMode] = useState<'grid' | 'month' | 'list'>('grid');
  const [updatedEmployeeIds, setUpdatedEmployeeIds] = useState<string[]>([]);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const hubRef = useRef<HubConnection | null>(null);
  const realtimeRefreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const joinedWeekRef = useRef<string>('');
  const weekStartRef = useRef<string>(weekStart);
  const fetchSequenceRef = useRef(0);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const suppressCellClickRef = useRef(false);
  const trackyPromptRef = useRef<((prompt: string) => Promise<void>) | null>(null);

  const setPtoDays = useCallback((nextValue: number) => {
    const safeValue = Math.max(1, Math.min(90, nextValue));
    setPtoModal((prev) => ({
      ...prev,
      numberOfDays: safeValue,
    }));
  }, []);

  const actionLabel = (action: string) => {
    if (action === 'created') return 'Created';
    if (action === 'updated') return 'Updated';
    if (action === 'pto_requested') return 'PTO Requested';
    if (action === 'pto_updated') return 'PTO Updated';
    if (action === 'pto_approved') return 'PTO Approved';
    if (action === 'pto_denied') return 'PTO Denied';
    if (action === 'pto_canceled') return 'PTO Canceled';
    if (action === 'inactivated' || action === 'deleted') return 'Inactive';
    if (action === 'purged') return 'Purged';
    return action;
  };
  const formatET = (isoDate: string) => {
    const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(isoDate);
    const normalized = hasTimezone ? isoDate : `${isoDate}Z`;
    const d = new Date(normalized);
    const datePart = d.toLocaleDateString('en-US', { timeZone: 'America/New_York' });
    const timePart = d.toLocaleTimeString('en-US', {
      timeZone: 'America/New_York',
      hour: 'numeric',
      minute: '2-digit',
      second: '2-digit',
      hour12: true,
    });
    return `${datePart}, ${timePart} ET`;
  };

  const roleLabelFor = roleLabelForValue;
  const normalizedCurrentEmail = userEmail.trim().toLowerCase();
  const normalizedRoleFilter = roleFilter.trim().toLowerCase();

  const pushToast = useCallback((tone: ToastMessage['tone'], text: string) => {
    const id = `${tone}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
    setToasts((current) => [...current, { id, tone, text }]);
    window.setTimeout(() => {
      setToasts((current) => current.filter((item) => item.id !== id));
    }, 2600);
  }, []);

  const askTracky = useCallback(
    async (prompt: string) => {
      if (!TRACKY_ENABLED) return;
      if (!trackyPromptRef.current) return;
      await trackyPromptRef.current(prompt);
    },
    [],
  );

  useEffect(() => {
    let ignore = false;

    const loadCompaniesAndOperations = async () => {
      try {
        const [companiesRes, operationsRes] = await Promise.all([
          apiFetch('/companies'),
          apiFetch('/companies/operations'),
        ]);

        if (companiesRes.ok && !ignore) {
          const json = (await companiesRes.json()) as CompanyCatalogItem[];
          setCompanyCatalog(json);
        }

        if (operationsRes.ok && !ignore) {
          const json = (await operationsRes.json()) as CompanyOperationItem[];
          setCompanyOperations(json);
        }
      } catch {
        if (!ignore) {
          setCompanyCatalog([]);
          setCompanyOperations([]);
        }
      }
    };

    loadCompaniesAndOperations();
    return () => {
      ignore = true;
    };
  }, []);

  const loadRecentEvents = useCallback(async () => {
    setEventsLoading(true);
    try {
      const res = await apiFetch('/schedule/events?take=20');
      if (!res.ok) return;
      const json = (await res.json()) as ScheduleEvent[];
      setEvents(json);
    } finally {
      setEventsLoading(false);
    }
  }, [role]);

  const fetchData = useCallback(async (start: string, opts?: { silent?: boolean }) => {
    const fetchSequence = ++fetchSequenceRef.current;
    if (!opts?.silent) setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.set('weekStart', start);
      if (employeeFilter.trim()) params.set('employee', employeeFilter.trim());
      if (roleFilter) params.set('role', roleFilter);
      if (shiftFilter) params.set('shift', shiftFilter);
      if (operationFilter) params.set('operation', operationFilter);
      if (companyFilter) params.set('company', companyFilter);
      const res = await apiFetch(`/calendar?${params.toString()}`);
      if (!res.ok) throw new Error('Unable to load calendar');
      const json = (await res.json()) as CalendarResponse;
      if (fetchSequence !== fetchSequenceRef.current) return;
      const normalizedCurrentEmail = userEmail.trim().toLowerCase();
      const hasCurrentUserRow = json.items.some((item) => item.email.trim().toLowerCase() === normalizedCurrentEmail);

      if (!hasCurrentUserRow && normalizedCurrentEmail) {
        const selfParams = new URLSearchParams();
        selfParams.set('weekStart', start);
        selfParams.set('employee', userEmail.trim());
        const selfRes = await apiFetch(`/calendar?${selfParams.toString()}`);
        if (selfRes.ok) {
          const selfJson = (await selfRes.json()) as CalendarResponse;
          if (fetchSequence !== fetchSequenceRef.current) return;
          const selfRow = selfJson.items.find((item) => item.email.trim().toLowerCase() === normalizedCurrentEmail);
          if (selfRow) {
            json.items = [selfRow, ...json.items];
          }
        }
      }

      if (fetchSequence !== fetchSequenceRef.current) return;
      setData(json);
      const holidaysRes = await apiFetch(`/holidays?startDate=${json.weekStart}&endDate=${json.weekEnd}`);
      if (fetchSequence !== fetchSequenceRef.current) return;
      if (holidaysRes.ok) {
        const holidaysJson = (await holidaysRes.json()) as HolidaysResponse;
        if (fetchSequence !== fetchSequenceRef.current) return;
        setHolidays(holidaysJson.items ?? []);
      } else {
        setHolidays([]);
      }
    } catch (e: any) {
      if (fetchSequence !== fetchSequenceRef.current) return;
      setHolidays([]);
      setError(e.message ?? 'Unable to load calendar');
    } finally {
      if (!opts?.silent) setLoading(false);
    }
  }, [employeeFilter, roleFilter, shiftFilter, operationFilter, companyFilter, userEmail]);

  useEffect(() => {
    weekStartRef.current = weekStart;
  }, [weekStart]);

  useEffect(() => {
    fetchData(weekStart);
  }, [weekStart, fetchData]);

  useEffect(() => {
    // auto-aplica al cambiar filtros
    fetchData(weekStart, { silent: true });
  }, [employeeFilter, roleFilter, shiftFilter, operationFilter, companyFilter, weekStart, fetchData]);

  useEffect(() => {
    if (viewMode !== 'month') return;

    const yearStart = formatDateKeyLocal(new Date(monthOverviewYear, 0, 1));
    const yearEnd = formatDateKeyLocal(new Date(monthOverviewYear, 11, 31));
    let ignore = false;

    const loadMonthHolidays = async () => {
      const res = await apiFetch(`/holidays?startDate=${yearStart}&endDate=${yearEnd}`);
      if (!res.ok || ignore) {
        if (!ignore) setMonthHolidays([]);
        return;
      }

      const json = (await res.json()) as HolidaysResponse;
      if (!ignore) {
        setMonthHolidays(json.items ?? []);
      }
    };

    loadMonthHolidays().catch(() => {
      if (!ignore) setMonthHolidays([]);
    });

    return () => {
      ignore = true;
    };
  }, [monthOverviewYear, viewMode]);

  useEffect(() => {
    if (viewMode !== 'month') return;
    const sourceYear = parseDateOnly(data?.weekStart ?? weekStart).getFullYear();
    setMonthOverviewYear(sourceYear);
  }, [data?.weekStart, viewMode, weekStart]);

  useLayoutEffect(() => {
    if (!calendarViewRef.current) return;

    const ctx = gsap.context(() => {
      const view = calendarViewRef.current;
      if (!view) return;

      const animatedChildren = view.querySelectorAll(
        '.calendar-grid-scroll, .calendar-month-view, .calendar-footer',
      );

      gsap.fromTo(
        animatedChildren,
        { opacity: 0, y: 14, filter: 'blur(8px)' },
        {
          opacity: 1,
          y: 0,
          filter: 'blur(0px)',
          duration: 0.42,
          stagger: 0.06,
          ease: 'power2.out',
          clearProps: 'filter,transform,opacity',
        },
      );
    }, calendarViewRef);

    return () => ctx.revert();
  }, [viewMode, data?.weekStart]);

  useEffect(() => {
    const hubUrl = `${getPreferredApiBase().replace(/\/+$/, '')}/hubs/schedule`;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        withCredentials: true,
        accessTokenFactory: () => getRealtimeAccessToken(),
      })
      .withAutomaticReconnect([0, 1500, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    hubRef.current = connection;
    setRealtimeStatus('connecting');

    connection.onreconnecting(() => setRealtimeStatus('reconnecting'));
    connection.onreconnected(async () => {
      setRealtimeStatus('connected');
      if (joinedWeekRef.current) {
        await connection.invoke('JoinWeek', joinedWeekRef.current).catch(() => undefined);
      }
      await fetchData(weekStartRef.current, { silent: true });
    });
    connection.onclose(() => setRealtimeStatus('disconnected'));

    connection.on('schedule.updated', (incoming: Partial<ScheduleEvent>) => {
      notifySessionActivity('signalr');
      setLastRealtimeAt(new Date().toLocaleTimeString());
      if (incoming.employeeId) {
        setUpdatedEmployeeIds((prev) => {
          const next = [incoming.employeeId!, ...prev.filter((id) => id !== incoming.employeeId)].slice(0, 12);
          return next;
        });
        window.setTimeout(() => {
          setUpdatedEmployeeIds((prev) => prev.filter((id) => id !== incoming.employeeId));
        }, 3200);
      }
      const now = new Date().toISOString();
      setEvents((prev) => [
        {
          id: `live-${now}`,
          employeeId: incoming.employeeId ?? '',
          employeeEmail: incoming.employeeEmail ?? '',
          action: incoming.action ?? 'updated',
          updatedByUserId: incoming.updatedByUserId ?? '',
          updatedByEmail: incoming.updatedByEmail ?? userEmail,
          updatedByName: incoming.updatedByName ?? userName,
          updatedByRole: incoming.updatedByRole ?? role,
          occurredAtUtc: incoming.occurredAtUtc ?? now,
          payloadJson: '{}',
        },
        ...prev,
      ].slice(0, 20));
      if (realtimeRefreshTimer.current) {
        clearTimeout(realtimeRefreshTimer.current);
      }
      // Debounce burst updates when several users are edited in sequence.
      realtimeRefreshTimer.current = setTimeout(() => {
        fetchData(weekStartRef.current, { silent: true });
      }, 350);
    });

    const start = async () => {
      try {
        await connection.start();
        setRealtimeStatus('connected');
        joinedWeekRef.current = weekStartRef.current;
        await connection.invoke('JoinWeek', joinedWeekRef.current).catch(() => undefined);
      } catch {
        setRealtimeStatus('disconnected');
      }
    };
    start();

    return () => {
      if (realtimeRefreshTimer.current) {
        clearTimeout(realtimeRefreshTimer.current);
      }
      connection.off('schedule.updated');
      connection.stop().catch(() => undefined);
    };
  }, [fetchData]);

  useEffect(() => {
    const connection = hubRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;

    const previousWeek = joinedWeekRef.current;
    joinedWeekRef.current = weekStart;

    const rejoin = async () => {
      if (previousWeek && previousWeek !== weekStart) {
        await connection.invoke('LeaveWeek', previousWeek).catch(() => undefined);
      }
      await connection.invoke('JoinWeek', weekStart).catch(() => undefined);
    };
    rejoin();
  }, [weekStart]);

  useEffect(() => {
    if (!updatesOpen) return;
    loadRecentEvents();
  }, [updatesOpen, loadRecentEvents]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      const isTypingTarget = !!target && (
        target.tagName === 'INPUT' ||
        target.tagName === 'TEXTAREA' ||
        target.tagName === 'SELECT' ||
        target.isContentEditable
      );

      if (event.key === '/' && !isTypingTarget) {
        event.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
        return;
      }

      if (event.key === 'Escape') {
        setSelectedEmployee(null);
        setUpdatesOpen(false);
        if (ptoModal.open) {
          closePtoModal();
        }
        return;
      }

      if (isTypingTarget) return;

      if (event.key === 'ArrowLeft') {
        event.preventDefault();
        goWeek(-1);
      } else if (event.key === 'ArrowRight') {
        event.preventDefault();
        goWeek(1);
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [ptoModal.open]);

  const resetFilters = () => {
    setEmployeeFilter('');
    setRoleFilter('');
    setShiftFilter('');
    setOperationFilter('');
    setCompanyFilter('');
    fetchData(weekStart, { silent: true });
  };

  const goWeek = (delta: number) => {
    const d = parseDateOnly(weekStart);
    d.setDate(d.getDate() + delta * 7);
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    setWeekStart(`${yyyy}-${mm}-${dd}`);
  };

  const jumpToCurrentWeek = () => {
    const d = new Date();
    const monday = new Date(d);
    const offset = (7 + d.getDay() - 1) % 7;
    monday.setDate(d.getDate() - offset);
    setWeekStart(monday.toISOString().slice(0, 10));
  };

  const startDragSelection = (row: CalendarRow, index: number) => {
    suppressCellClickRef.current = false;
    setDragSelection({ rowId: row.id, startIndex: index, endIndex: index });
  };

  const extendDragSelection = (row: CalendarRow, index: number) => {
    setDragSelection((current) => {
      if (!current || current.rowId !== row.id) return current;
      if (current.endIndex !== index) {
        suppressCellClickRef.current = true;
      }
      return { ...current, endIndex: index };
    });
  };

  const clearDragSelection = () => {
    setDragSelection(null);
    window.setTimeout(() => {
      suppressCellClickRef.current = false;
    }, 0);
  };

  const exportCalendar = async () => {
    if (!canExportCalendar) return;

    setExporting(true);
    setError(null);
    pushToast('info', 'Preparing export...');
    try {
      const params = new URLSearchParams();
      params.set('weekStart', weekStart);
      if (employeeFilter.trim()) params.set('employee', employeeFilter.trim());
      if (roleFilter) params.set('role', roleFilter);
      if (shiftFilter) params.set('shift', shiftFilter);
      if (operationFilter) params.set('operation', operationFilter);
      if (companyFilter) params.set('company', companyFilter);

      const res = await apiFetch(`/calendar/export?${params.toString()}`);
      if (!res.ok) {
        const payload = (await res.json().catch(() => null)) as { message?: string } | null;
        throw new Error(payload?.message ?? 'Unable to export calendar.');
      }

      const blob = await res.blob();
      if (blob.size === 0) {
        pushToast('error', 'No records were returned for the current filters.');
        return;
      }
      const downloadName = tryReadDownloadFileName(res.headers.get('Content-Disposition')) || 'shifttrack-calendar-export.xlsx';
      const objectUrl = window.URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = objectUrl;
      anchor.download = downloadName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.URL.revokeObjectURL(objectUrl);
      pushToast('success', 'Downloaded export file.');
    } catch (e: any) {
      setError(e.message ?? 'Unable to export calendar.');
      pushToast('error', e.message ?? 'Unable to export calendar.');
    } finally {
      setExporting(false);
    }
  };

  const normalizedUserEmail = userEmail.trim().toLowerCase();
  const currentUserRow = useMemo(
    () => data?.items.find((item) => item.email.trim().toLowerCase() === normalizedUserEmail) ?? null,
    [data, normalizedUserEmail],
  );

  useLayoutEffect(() => {
    if (!ptoModal.open || ptoModalClosing || !ptoModalCardRef.current) return;

    const ctx = gsap.context(() => {
      gsap.set(ptoModalBackdropRef.current, { opacity: 0 });
      gsap.set(ptoModalCardRef.current, { opacity: 0, y: 22, scale: 0.96, filter: 'blur(10px)' });

      const tl = gsap.timeline({ defaults: { ease: 'power3.out' } });
      tl.to(ptoModalBackdropRef.current, { opacity: 1, duration: 0.18 }).to(
        ptoModalCardRef.current,
        {
          opacity: 1,
          y: 0,
          scale: 1,
          filter: 'blur(0px)',
          duration: 0.32,
          clearProps: 'filter',
        },
        '<0.02',
      );
    }, ptoModalCardRef);

    return () => ctx.revert();
  }, [ptoModal.open, ptoModalClosing, ptoModal.activeTab, ptoModal.userId]);

  useLayoutEffect(() => {
    if (!ptoModal.open || ptoModalClosing || !ptoModalPanelRef.current) return;

    const ctx = gsap.context(() => {
      const panel = ptoModalPanelRef.current;
      if (!panel) return;
      const animatedChildren = panel.querySelectorAll(
        '.swap-summary-card, .grid, .field, .field-help, .swap-summary-note',
      );

      gsap.fromTo(
        panel,
        { opacity: 0, y: 10, filter: 'blur(6px)' },
        {
          opacity: 1,
          y: 0,
          filter: 'blur(0px)',
          duration: 0.22,
          ease: 'power2.out',
          clearProps: 'filter',
        },
      );

      if (animatedChildren.length) {
        gsap.fromTo(
          animatedChildren,
          { opacity: 0, y: 10 },
          {
            opacity: 1,
            y: 0,
            duration: 0.24,
            stagger: 0.045,
            ease: 'power2.out',
            delay: 0.06,
            clearProps: 'opacity,transform',
          },
        );
      }
    }, ptoModalPanelRef);

    return () => ctx.revert();
  }, [ptoModal.open, ptoModalClosing, ptoModal.activeTab]);

  const openPtoModal = async (row: CalendarRow, cell: CalendarCell, selectedDays?: number) => {
    const loadEligibleRequesterDayOffDates = async (targetDate: string) => {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      const endDate = new Date(today);
      endDate.setDate(endDate.getDate() + 29);
      const todayIso = formatDateKeyLocal(today);
      const endDateIso = formatDateKeyLocal(endDate);

      if (targetDate < todayIso) {
        return {
          canSwapSelectedDay: false,
          eligibleDates: [] as string[],
          requesterShiftOnTargetDate: '',
          targetShiftByEligibleDate: {} as Record<string, string>,
        };
      }

      const weekStarts = new Set<string>();
      for (const cursor = new Date(today); cursor <= endDate; cursor.setDate(cursor.getDate() + 1)) {
        weekStarts.add(weekStartForDate(formatDateKeyLocal(cursor)));
      }
      weekStarts.add(weekStartForDate(targetDate));

      const selfCells = new Map<string, CalendarCell>();
      const targetCells = new Map<string, CalendarCell>();

      (currentUserRow?.cells ?? []).forEach((item) => selfCells.set(item.date, item));
      row.cells.forEach((item) => targetCells.set(item.date, item));

      for (const weekStart of weekStarts) {
        const requesterParams = new URLSearchParams();
        requesterParams.set('weekStart', weekStart);
        requesterParams.set('employee', userEmail.trim());
        const requesterRes = await apiFetch(`/calendar?${requesterParams.toString()}`);
        if (requesterRes.ok) {
          const requesterJson = (await requesterRes.json()) as CalendarResponse;
          const selfRow =
            requesterJson.items.find((item) => item.email.trim().toLowerCase() === normalizedUserEmail) ??
            requesterJson.items.find((item) => item.displayName.trim().toLowerCase() === userName.trim().toLowerCase());
          (selfRow?.cells ?? []).forEach((item) => selfCells.set(item.date, item));
        }

        const targetParams = new URLSearchParams();
        targetParams.set('weekStart', weekStart);
        targetParams.set('employee', row.email.trim());
        const targetRes = await apiFetch(`/calendar?${targetParams.toString()}`);
        if (targetRes.ok) {
          const targetJson = (await targetRes.json()) as CalendarResponse;
          const targetRow =
            targetJson.items.find((item) => item.email.trim().toLowerCase() === row.email.trim().toLowerCase()) ??
            targetJson.items.find((item) => item.displayName.trim().toLowerCase() === row.displayName.trim().toLowerCase());
          (targetRow?.cells ?? []).forEach((item) => targetCells.set(item.date, item));
        }
      }

      const requesterCellOnTargetDate = selfCells.get(targetDate);
      const targetCellOnTargetDate = targetCells.get(targetDate);
      const canSwapSelectedDay =
        isWorkingCalendarCell(requesterCellOnTargetDate) &&
        targetCellOnTargetDate?.type === 'dayOff';

      const eligibleDates = Array.from(selfCells.entries())
        .filter(([date, requesterCell]) => {
          if (date < todayIso || date > endDateIso) return false;
          if (requesterCell.type !== 'dayOff') return false;
          return isWorkingCalendarCell(targetCells.get(date));
        })
        .map(([date]) => date)
        .sort();

      const targetShiftByEligibleDate = Object.fromEntries(
        eligibleDates.map((date) => [date, describeWorkingShift(targetCells.get(date))]),
      );

      return {
        canSwapSelectedDay,
        eligibleDates,
        requesterShiftOnTargetDate: describeWorkingShift(requesterCellOnTargetDate),
        targetShiftByEligibleDate,
      };
    };

    if (!canRequestForOthers && row.email.toLowerCase() !== userEmail.toLowerCase()) {
      // Employees can still initiate a change request with a same-role coworker from their schedule cell.
      const canSwapWithRow =
        row.email.trim().toLowerCase() !== normalizedUserEmail &&
        row.role === role &&
        cell.type === 'dayOff';
      if (!canSwapWithRow) {
        setError('You can only create a change request from a same-role coworker day off.');
        return;
      }
    }

    if (cell.type === 'leave' && cell.ptoGroupId && !cell.isPtoStart) {
      setError('To edit a PTO request, double click the first PTO day.');
      return;
    }

    const isEdit = !!(cell.ptoGroupId && cell.isPtoStart && (cell.type === 'leave' || cell.ptoRequestType === 'day_off'));
    const canCancelApproved = !!(isEdit && canRequestForOthers && (isAdminRole(role) || !isAdminRole(row.role)));
    let startDate = cell.date;
    let numberOfDays = Math.max(1, selectedDays ?? 1);
    let requestType = isEdit ? cell.ptoRequestType ?? '' : '';
    let comments = isEdit ? cell.ptoComments ?? '' : '';
    let existingRequestId: string | undefined;
    const canChangeDailySchedule =
      !cell.ptoGroupId &&
      (isAdminRole(role) || (isManagerRole(role) && !isAdminRole(row.role)));
    const timeMatch = cell.label.match(/^(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$/);
    const dailyStartTime = timeMatch?.[1]?.padStart(5, '0') ?? '08:00';
    const dailyEndTime = timeMatch?.[2]?.padStart(5, '0') ?? '17:00';

    if (isEdit && cell.ptoGroupId) {
      const res = await apiFetch(`/pto/requests/${cell.ptoGroupId}`);
      if (res.ok) {
        const request = (await res.json()) as {
          id: string;
          startDate: string;
          numberOfDays: number;
          requestType: string;
          comments?: string | null;
        };
        startDate = request.startDate;
        numberOfDays = Math.max(1, request.numberOfDays);
        requestType = request.requestType ?? '';
        comments = request.comments ?? '';
        existingRequestId = request.id;
      }
    }

    if (ptoCloseTimerRef.current) {
      clearTimeout(ptoCloseTimerRef.current);
      ptoCloseTimerRef.current = null;
    }
    setPtoModalClosing(false);
    const isSameRoleCoworkerDayOff =
      row.email.trim().toLowerCase() !== normalizedUserEmail &&
      row.role === role &&
      cell.type === 'dayOff';
    const requesterCellOnTargetDate = currentUserRow?.cells.find((item) => item.date === cell.date);
    const requesterShiftOnTargetDate = describeWorkingShift(requesterCellOnTargetDate);
    const canOpenSwapTab = isSameRoleCoworkerDayOff && isWorkingCalendarCell(requesterCellOnTargetDate);

    if (
      !canRequestForOthers &&
      isSameRoleCoworkerDayOff &&
      !canOpenSwapTab
    ) {
      setError('You can only request that day off if you are scheduled to work on that same day.');
      return;
    }

    setPtoModal({
      open: true,
      activeTab: canOpenSwapTab ? 'swap' : canChangeDailySchedule ? 'schedule' : requestType === 'day_off' ? 'dayoff' : 'pto',
      userId: row.id,
      userEmail: row.email,
      userName: row.displayName,
      targetRole: row.role,
      startDate,
      numberOfDays,
      requestType,
      comments: cell.isDailyScheduleOverride ? cell.scheduleOverrideComments ?? '' : comments,
      dailyStartTime,
      dailyEndTime,
      canChangeDailySchedule,
      existingGroupId: isEdit ? cell.ptoGroupId : undefined,
      existingRequestId,
      canCancelApproved,
      swapTargetUserId: isSameRoleCoworkerDayOff ? row.id : undefined,
      swapTargetUserEmail: isSameRoleCoworkerDayOff ? row.email : undefined,
      swapTargetUserName: isSameRoleCoworkerDayOff ? row.displayName : undefined,
      swapTargetRole: isSameRoleCoworkerDayOff ? row.role : undefined,
      swapTargetDate: isSameRoleCoworkerDayOff ? cell.date : undefined,
      swapRequesterDate: undefined,
      swapAvailableRequesterDates: [],
      swapRequesterShiftOnTargetDate: isSameRoleCoworkerDayOff ? requesterShiftOnTargetDate : undefined,
      swapTargetShiftByRequesterDate: {},
    });

    if (!isSameRoleCoworkerDayOff) {
      setSwapDatesLoading(false);
      return;
    }

    setSwapDatesLoading(true);
    try {
      const swapState = await loadEligibleRequesterDayOffDates(cell.date);
      const canOpenSwapTabAfterLoad =
        isSameRoleCoworkerDayOff && swapState.canSwapSelectedDay;

      setPtoModal((prev) => {
        if (!prev.open || prev.userId !== row.id || prev.startDate !== startDate) return prev;
        return {
          ...prev,
          activeTab: canOpenSwapTabAfterLoad ? 'swap' : prev.activeTab,
          swapRequesterDate: canOpenSwapTabAfterLoad ? swapState.eligibleDates[0] ?? undefined : undefined,
          swapAvailableRequesterDates: canOpenSwapTabAfterLoad ? swapState.eligibleDates : [],
          swapRequesterShiftOnTargetDate: canOpenSwapTabAfterLoad ? swapState.requesterShiftOnTargetDate : undefined,
          swapTargetShiftByRequesterDate: canOpenSwapTabAfterLoad ? swapState.targetShiftByEligibleDate : {},
        };
      });
    } finally {
      setSwapDatesLoading(false);
    }
  };

  const canInteractWithEmployeeRow = (row: CalendarRow) =>
    canRequestForOthers ||
    row.email.trim().toLowerCase() === normalizedUserEmail ||
    row.role === role;

  const closePtoModal = (force = false) => {
    if ((savingPto || savingSwap) && !force) return;
    if (ptoCloseTimerRef.current) {
      clearTimeout(ptoCloseTimerRef.current);
      ptoCloseTimerRef.current = null;
    }
    setPtoModalClosing(true);
    ptoCloseTimerRef.current = setTimeout(() => {
      setPtoModal((prev) => ({ ...prev, open: false }));
      setPtoModalClosing(false);
      setSwapDatesLoading(false);
      ptoCloseTimerRef.current = null;
    }, 180);
  };

  const cancelApprovedPto = async () => {
    if (!ptoModal.existingGroupId || !ptoModal.canCancelApproved) return;

    setSavingPto(true);
    setError(null);
    try {
      let cancelRequestId = ptoModal.existingRequestId;
      if (!cancelRequestId) {
        const listRes = await apiFetch('/pto/requests?status=approved&take=500');
        if (listRes.ok) {
          const requests = (await listRes.json()) as PtoRequestListItem[];
          const matched = requests.find((item) => {
            if (item.userId !== ptoModal.userId) return false;
            if (item.id === ptoModal.existingGroupId) return true;
            return item.overrideGroupId === ptoModal.existingGroupId;
          });
          cancelRequestId = matched?.id;
          if (cancelRequestId) {
            setPtoModal((prev) => ({ ...prev, existingRequestId: cancelRequestId }));
          }
        }
      }

      const targetId = cancelRequestId ?? ptoModal.existingGroupId;
      const res = await apiFetch(`/pto/requests/${targetId}/cancel`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });
      const data = (await res.json().catch(() => null)) as { message?: string } | null;
      if (!res.ok) {
        throw new Error(data?.message ?? 'Unable to cancel PTO.');
      }

      closePtoModal(true);
      await fetchData(weekStartRef.current, { silent: true });
    } catch (e: any) {
      setError(e.message ?? 'Unable to cancel PTO.');
    } finally {
      setSavingPto(false);
    }
  };

  const validatePtoLikeRequest = (requestType: string) => {
    if (!ptoModal.userId || !ptoModal.startDate) return false;
    if (!requestType) {
      setError('Please select a request type.');
      return false;
    }
    if (!Number.isFinite(ptoModal.numberOfDays) || ptoModal.numberOfDays < 1) {
      setError('Please enter a valid number of days.');
      return false;
    }
    if (!ptoModal.comments.trim()) {
      setError('Comments are required.');
      return false;
    }

    if (isEmployeeLikeRole(role)) {
      const requestedStart = parseDateOnly(ptoModal.startDate);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      const maxDate = new Date(today);
      maxDate.setDate(maxDate.getDate() + 60);

      if (requestedStart < today) {
        setError('Employees cannot request PTO for past dates.');
        return false;
      }

      if (requestedStart > maxDate) {
        setError('Employees can only request PTO up to 60 days from today.');
        return false;
      }
    }

    return true;
  };

  const buildPtoLikePayload = (requestType: string): PendingPtoPayload => {
    const payload: PendingPtoPayload = {
      userId: ptoModal.userId,
      startDate: ptoModal.startDate,
      numberOfDays: ptoModal.numberOfDays,
      requestType,
      comments: ptoModal.comments.trim() || null,
      existingGroupId: ptoModal.existingGroupId ?? null,
    };

    if (employeeFilter.trim()) payload.employeeFilter = employeeFilter.trim();
    if (roleFilter) payload.roleFilter = roleFilter;
    if (shiftFilter) payload.shiftFilter = shiftFilter;
    if (operationFilter) payload.operationFilter = operationFilter;
    if (companyFilter) payload.companyFilter = companyFilter;
    return payload;
  };

  const loadCoveragePreview = async (payload: PendingPtoPayload) => {
    const res = await apiFetch('/calendar/pto/coverage-preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    const data = (await res.json().catch(() => null)) as { message?: string } | PtoCoveragePreview | null;
    if (!res.ok) {
      throw new Error((data as { message?: string } | null)?.message ?? 'Unable to validate coverage.');
    }

    return (data as PtoCoveragePreview) ?? { hasImpact: false, warnings: [] };
  };

  const persistPtoLikeRequest = async (payload: PendingPtoPayload) => {
    setSavingPto(true);
    setError(null);
    try {
      const res = await apiFetch('/calendar/pto', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      const data = (await res.json().catch(() => null)) as { message?: string } | null;
      if (!res.ok) {
        throw new Error(data?.message ?? 'Unable to save PTO.');
      }

      closePtoModal(true);
      await fetchData(weekStartRef.current, { silent: true });
    } catch (e: any) {
      setError(e.message ?? 'Unable to save PTO.');
    } finally {
      setSavingPto(false);
    }
  };

  const submitPto = async () => {
    if (!validatePtoLikeRequest(ptoModal.requestType)) return;
    const payload = buildPtoLikePayload(ptoModal.requestType);

    if (isAdminRole(role)) {
      try {
        const preview = await loadCoveragePreview(payload);
        if (preview.warnings.length > 0) {
          setCoverageConfirm({
            message: preview.warnings.map((item) => item.message).join('\n'),
            payload,
          });
          return;
        }
      } catch (e: any) {
        setError(e.message ?? 'Unable to validate coverage.');
        return;
      }
    }

    await persistPtoLikeRequest(payload);
  };

  const submitDayOff = async () => {
    const requestType = 'day_off';
    if (!validatePtoLikeRequest(requestType)) return;
    const payload = buildPtoLikePayload(requestType);

    if (isAdminRole(role)) {
      try {
        const preview = await loadCoveragePreview(payload);
        if (preview.warnings.length > 0) {
          setCoverageConfirm({
            message: preview.warnings.map((item) => item.message).join('\n'),
            payload,
          });
          return;
        }
      } catch (e: any) {
        setError(e.message ?? 'Unable to validate coverage.');
        return;
      }
    }

    await persistPtoLikeRequest(payload);
  };

  const submitSwap = async () => {
    if (!ptoModal.swapTargetUserId || !ptoModal.swapTargetDate || !ptoModal.swapRequesterDate) {
      setError('Please choose your day off and the employee date for the change request.');
      return;
    }
    if (!ptoModal.comments.trim()) {
      setError('Observations are required.');
      return;
    }

    setSavingSwap(true);
    setError(null);
    try {
      const res = await apiFetch('/swap/requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          targetUserId: ptoModal.swapTargetUserId,
          requestedDates: [ptoModal.swapRequesterDate],
          targetDates: [ptoModal.swapTargetDate],
          requestType: 'swap_shift',
          comments: ptoModal.comments.trim() || null,
        }),
      });

      const data = (await res.json().catch(() => null)) as { message?: string } | null;
      if (!res.ok) {
        throw new Error(data?.message ?? 'Unable to create change request.');
      }

      pushToast('success', 'Change request submitted.');
      closePtoModal(true);
      await fetchData(weekStartRef.current, { silent: true });
    } catch (e: any) {
      setError(e.message ?? 'Unable to create change request.');
    } finally {
      setSavingSwap(false);
    }
  };

  const submitDailySchedule = async () => {
    if (!ptoModal.dailyStartTime || !ptoModal.dailyEndTime) {
      setError('Start time and end time are required.');
      return;
    }
    if (ptoModal.dailyStartTime === ptoModal.dailyEndTime) {
      setError('Start time and end time must be different.');
      return;
    }
    if (!ptoModal.comments.trim()) {
      setError('Comments are required.');
      return;
    }

    const payload: PendingDailySchedulePayload = {
      userId: ptoModal.userId,
      date: ptoModal.startDate,
      startTime: ptoModal.dailyStartTime,
      endTime: ptoModal.dailyEndTime,
      comments: ptoModal.comments.trim(),
    };
    const targetRow = data?.items.find((row) => row.id === payload.userId);
    const targetCell = targetRow?.cells.find((cell) => cell.date === payload.date);
    const currentWeeklyHours = targetRow?.cells.reduce((total, cell) => total + (cell.durationHours || 0), 0) ?? 0;
    const projectedDailyHours = calculateDurationHours(payload.startTime, payload.endTime);
    const projectedWeeklyHours = Math.round((currentWeeklyHours - (targetCell?.durationHours || 0) + projectedDailyHours) * 100) / 100;

    if (projectedWeeklyHours > weeklyHoursLimit) {
      setDailyScheduleConfirm({
        payload,
        message: [
          `Warning: this one-day schedule change will put ${ptoModal.userName} at ${projectedWeeklyHours} weekly hours.`,
          `The weekly limit is ${weeklyHoursLimit} hours.`,
          'You can still continue if this exception is intentional.',
        ].join('\n'),
      });
      return;
    }

    await persistDailySchedule(payload);
  };

  const persistDailySchedule = async (payload: PendingDailySchedulePayload) => {
    setSavingPto(true);
    setError(null);
    try {
      const res = await apiFetch('/calendar/day-schedule', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const json = (await res.json().catch(() => null)) as { message?: string } | null;
      if (!res.ok) throw new Error(json?.message ?? 'Unable to change the schedule for this day.');

      pushToast('success', `Schedule updated for ${formatSwapDate(payload.date)}.`);
      closePtoModal(true);
      await fetchData(weekStartRef.current, { silent: true });
    } catch (e: any) {
      setError(e.message ?? 'Unable to change the schedule for this day.');
    } finally {
      setSavingPto(false);
    }
  };

  const formattedRange = useMemo(() => {
    if (!data) return '';
    const start = parseDateOnly(data.weekStart);
    const end = parseDateOnly(data.weekEnd);
    const fmt = (d: Date) =>
      d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    return `${fmt(start)} - ${fmt(end)}, ${start.getFullYear()}`;
  }, [data]);

  const baseSortedItems = useMemo(() => {
    if (!data) return [];
    return [...data.items].sort((a, b) => {
      const aIsCurrent = a.email.trim().toLowerCase() === normalizedCurrentEmail;
      const bIsCurrent = b.email.trim().toLowerCase() === normalizedCurrentEmail;
      if (aIsCurrent !== bIsCurrent) return aIsCurrent ? -1 : 1;

      const shiftOrder = shiftPriority(a.shiftTime) - shiftPriority(b.shiftTime);
      if (shiftOrder !== 0) return shiftOrder;
      return a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' });
    });
  }, [data, normalizedCurrentEmail]);
  const effectiveCoverage = useMemo<CoverageSummary[]>(() => {
    if (!data) return [];
    if (Array.isArray(data.coverage) && data.coverage.length === data.days.length) return data.coverage;

    const totalAccountAgents = data.items.length;
    return data.days.map((d) => {
      const [dayCode] = d.label.split(' ');
      const agentsWorking = data.items.reduce((count, row) => {
        const cell = row.cells.find((c) => c.date === d.date);
        if (!cell) return count;
        return cell.type === 'shiftMorning' || cell.type === 'shiftLate' ? count + 1 : count;
      }, 0);
      const coverage = totalAccountAgents === 0 ? 0 : Number(((agentsWorking * 100) / totalAccountAgents).toFixed(1));
      return {
        date: d.date,
        dayCode,
        expectedCoverage: expectedCoverageByDay[dayCode] ?? 0,
        coverage,
        totalAgents: agentsWorking,
        statusColor: resolveCoverageColor(dayCode, coverage),
      };
    });
  }, [data]);
  const coverageByDate = useMemo(() => {
    const map = new Map<string, CoverageSummary>();
    effectiveCoverage.forEach((item) => map.set(item.date, item));
    return map;
  }, [effectiveCoverage]);
  const holidayNamesByDate = useMemo(() => {
    const map = new Map<string, string>();
    holidays.forEach((item) => map.set(item.date, item.name));
    return map;
  }, [holidays]);
  const filteredItems = useMemo(() => {
    if (!roleFilter) return baseSortedItems;
    return baseSortedItems.filter((row) => {
      if (row.email.trim().toLowerCase() === normalizedCurrentEmail) return true;

      const numericRole = Number(row.role);
      if (!Number.isNaN(numericRole) && String(numericRole) === roleFilter) return true;

      const safeRole = Number.isNaN(numericRole) ? ROLE_EMPLOYEE : numericRole;
      if (roleLabelForValue(safeRole).toLowerCase() === normalizedRoleFilter) return true;
      return roleInitialsForValue(safeRole).toLowerCase() === normalizedRoleFilter;
    });
  }, [baseSortedItems, normalizedCurrentEmail, normalizedRoleFilter, roleFilter]);
  const pinnedCurrentUserRow = useMemo(
    () => filteredItems.find((row) => row.email.trim().toLowerCase() === normalizedCurrentEmail) ?? null,
    [filteredItems, normalizedCurrentEmail],
  );
  const additionalItems = useMemo(
    () => filteredItems.filter((row) => row.email.trim().toLowerCase() !== normalizedCurrentEmail),
    [filteredItems, normalizedCurrentEmail],
  );
  const isPastWeek = useMemo(() => {
    if (!data) return false;
    const selectedStart = parseDateOnly(data.weekStart);
    const todayLocal = new Date();
    const currentWeekStart = new Date(todayLocal);
    const diff = (7 + todayLocal.getDay() - 1) % 7;
    currentWeekStart.setDate(todayLocal.getDate() - diff);
    currentWeekStart.setHours(0, 0, 0, 0);
    return selectedStart.getTime() < currentWeekStart.getTime();
  }, [data]);

  const summaryStats = useMemo(() => {
    const ptoCount = filteredItems.filter((row) => row.cells.some((cell) => cell.type === 'leave')).length;
    const dayOffCount = filteredItems.filter((row) => row.cells.some((cell) => cell.type === 'dayOff')).length;
    const riskCount = effectiveCoverage.filter((item) => item.statusColor === 'red').length;
    return { ptoCount, dayOffCount, riskCount };
  }, [effectiveCoverage, filteredItems]);

  useEffect(() => {
    return () => {
      if (ptoCloseTimerRef.current) {
        clearTimeout(ptoCloseTimerRef.current);
        ptoCloseTimerRef.current = null;
      }
    };
  }, []);
  const visibleItems = pinnedCurrentUserRow ? [pinnedCurrentUserRow, ...additionalItems] : additionalItems;
  const localToday = new Date();
  const localTodayKey = `${localToday.getFullYear()}-${String(localToday.getMonth() + 1).padStart(2, '0')}-${String(localToday.getDate()).padStart(2, '0')}`;
  const currentYearDate = useMemo(() => new Date(monthOverviewYear, 0, 1), [monthOverviewYear]);
  const currentYearLabel = useMemo(
    () => currentYearDate.toLocaleDateString('en-US', { year: 'numeric' }),
    [currentYearDate],
  );
  const actualCurrentYear = today.getFullYear();
  const monthHolidayNamesByDate = useMemo(() => {
    const map = new Map<string, string>();
    monthHolidays.forEach((item) => map.set(item.date, item.name));
    return map;
  }, [monthHolidays]);
  const yearMonths = useMemo(() => {
    return Array.from({ length: 12 }, (_, monthIndex) => {
      const monthStart = new Date(currentYearDate.getFullYear(), monthIndex, 1);
      const monthEnd = new Date(currentYearDate.getFullYear(), monthIndex + 1, 0);
      const cells: Array<{ key: string; date?: string; dayNumber?: number; holidayName?: string; isToday?: boolean }> = [];
      const leadingBlanks = (7 + monthStart.getDay() - 1) % 7;

      for (let index = 0; index < leadingBlanks; index += 1) {
        cells.push({ key: `blank-start-${monthIndex}-${index}` });
      }

      for (let day = 1; day <= monthEnd.getDate(); day += 1) {
        const date = new Date(currentYearDate.getFullYear(), monthIndex, day);
        const dateKey = formatDateKeyLocal(date);
        cells.push({
          key: `${monthIndex}-${dateKey}`,
          date: dateKey,
          dayNumber: day,
          holidayName: monthHolidayNamesByDate.get(dateKey),
          isToday: dateKey === localTodayKey,
        });
      }

      const trailingBlanks = (7 - (cells.length % 7)) % 7;
      for (let index = 0; index < trailingBlanks; index += 1) {
        cells.push({ key: `blank-end-${monthIndex}-${index}` });
      }

      return {
        key: `month-${monthIndex}`,
        label: monthStart.toLocaleDateString('en-US', { month: 'long' }),
        cells,
      };
    });
  }, [currentYearDate, localTodayKey, monthHolidayNamesByDate]);
  const assistantDocked = Boolean(selectedEmployee || ptoModal.open);
  const canTargetSelectedPtoUser =
    canRequestForOthers || ptoModal.userEmail.trim().toLowerCase() === normalizedCurrentEmail;
  const scopedCompanies = useMemo(() => {
    const values = (userCompanies.length ? userCompanies : [userCompany])
      .map((company) => company.trim())
      .filter(Boolean);

    return values.filter((company, index, all) =>
      all.findIndex((item) => item.toLowerCase() === company.toLowerCase()) === index);
  }, [userCompanies, userCompany]);
  const companyFilterOptions = useMemo(() => {
    const values = isSystemHidden
      ? [
          ...companyOptions,
          ...companyCatalog.filter((company) => company.isActive).map((company) => company.name),
          ...(data?.items ?? []).map((item) => item.company),
          companyFilter,
        ]
      : scopedCompanies;

    const unique = values
      .map((company) => company?.trim())
      .filter(Boolean)
      .filter((company, index, all) =>
      all.findIndex((item) => item.toLowerCase() === company.toLowerCase()) === index);

    return [
      { value: '', label: 'All' },
      ...unique.sort((a, b) => a.localeCompare(b)).map((company) => ({ value: company, label: company })),
    ];
  }, [companyCatalog, companyFilter, data?.items, isSystemHidden, scopedCompanies]);

  const operationFilterOptions = useMemo(() => {
    const companyMatches = (companyName: string) =>
      !companyFilter || companyName.trim().toLowerCase() === companyFilter.trim().toLowerCase();
    const values = [
      ...companyOperations
        .filter((operation) => operation.isActive && companyMatches(operation.companyName))
        .map((operation) => operation.name),
      ...(data?.items ?? [])
        .filter((item) => companyMatches(item.company))
        .map((item) => item.operation),
      operationFilter,
    ]
      .map((operation) => operation?.trim())
      .filter(Boolean)
      .filter((operation, index, all) =>
        all.findIndex((item) => item.toLowerCase() === operation.toLowerCase()) === index)
      .sort((a, b) => a.localeCompare(b));

    return [{ value: '', label: 'All' }, ...values.map((operation) => ({ value: operation, label: operation }))];
  }, [companyFilter, companyOperations, data?.items, operationFilter]);

  useEffect(() => {
    if (!companyFilter || isSystemHidden) return;
    const isAllowed = scopedCompanies.some((company) => company.toLowerCase() === companyFilter.toLowerCase());
    if (!isAllowed) {
      setCompanyFilter('');
    }
  }, [companyFilter, isSystemHidden, scopedCompanies]);

  useEffect(() => {
    if (!operationFilter) return;
    const isAllowed = operationFilterOptions.some((option) => option.value.toLowerCase() === operationFilter.toLowerCase());
    if (!isAllowed) {
      setOperationFilter('');
    }
  }, [operationFilter, operationFilterOptions]);

  return (
    <div className="calendar-wrapper">
      <div className="card calendar-card">
        <div className="calendar-controls-shell">
          <div className="calendar-filter-grid">
            <div className="filter full scheduling">
              <span>Scheduling Date</span>
              <div className="week-picker">
                <Button className="pill-btn" variant="ghost" size="sm" onClick={() => goWeek(-1)} aria-label="Previous week">
                  &lt;
                </Button>
                <span className="range-label">{formattedRange || `${weekStart}`}</span>
                <Button className="pill-btn" variant="ghost" size="sm" onClick={() => goWeek(1)} aria-label="Next week">
                  &gt;
                </Button>
              </div>
            </div>
            <div className="filter">
              <span>Employee</span>
              <input
                ref={searchInputRef}
                className="pill-input"
                type="search"
                value={employeeFilter}
                onChange={(e) => setEmployeeFilter(e.target.value)}
                placeholder="Search by name or email"
              />
            </div>
            <div className="filter">
              <span>Role</span>
              <Select
                className="pill-input"
                value={roleFilter}
                onChange={setRoleFilter}
                options={[
                  { value: '', label: 'All' },
                  { value: '0', label: 'Employee' },
                  { value: '1', label: 'Manager' },
                  { value: '2', label: 'Admin' },
                  { value: '3', label: 'Team Leader' },
                ]}
                placeholder="All"
                ariaLabel="Role"
              />
            </div>
            <div className="filter">
              <span>Shift</span>
              <Select
                className="pill-input"
                value={shiftFilter}
                onChange={setShiftFilter}
                options={[{ value: '', label: 'All' }, ...shiftTimeOptions.map((s) => ({ value: s, label: s }))]}
                placeholder="All"
                ariaLabel="Shift"
              />
            </div>
            <div className="filter">
              <span>Operation</span>
              <Select
                className="pill-input"
                value={operationFilter}
                onChange={setOperationFilter}
                options={operationFilterOptions}
                placeholder="All"
                ariaLabel="Operation"
              />
            </div>
            <div className="filter">
              <span>Company</span>
              <Select
                className="pill-input"
                value={companyFilter}
                onChange={setCompanyFilter}
                options={companyFilterOptions}
                placeholder="All"
                ariaLabel="Company"
                searchable
                searchPlaceholder="Search company"
              />
            </div>
          </div>

          <div className="calendar-toolbar-band">
            {canViewCoverage && (
              <div className="calendar-summary-inline">
                <span><strong>{filteredItems.length}</strong> visible</span>
                <span><strong>{summaryStats.ptoCount}</strong> on PTO</span>
                <span className="risk"><strong>{summaryStats.riskCount}</strong> risk days</span>
              </div>
            )}
            <div className="calendar-toolbar-clusters">
              <div className="calendar-filter-actions">
                <button type="button" className="calendar-link-action" onClick={resetFilters} disabled={loading || exporting}>
                  Reset
                </button>
                <button type="button" className="calendar-link-action" onClick={jumpToCurrentWeek} disabled={loading}>
                  Today
                </button>
              </div>
              <div className="calendar-toolbar-actions">
                <div className="view-toggle">
                  <Button
                    className="calendar-toolbar-btn"
                    variant={viewMode === 'grid' ? 'primary' : 'ghost'}
                    size="sm"
                    onClick={() => setViewMode('grid')}
                  >
                    Grid
                  </Button>
                  <Button
                    className="calendar-toolbar-btn"
                    variant={viewMode === 'month' ? 'primary' : 'ghost'}
                    size="sm"
                    onClick={() => setViewMode('month')}
                  >
                    Month
                  </Button>
                </div>
                {canExportCalendar && (
                  <Button
                    className="calendar-toolbar-btn calendar-export-btn"
                    variant="ghost"
                    size="sm"
                    onClick={exportCalendar}
                    disabled={loading || exporting}
                  >
                    {exporting ? 'Exporting...' : 'Export'}
                  </Button>
                )}
                {canViewLiveUpdates && (
                  <button type="button" className={`realtime-pill ${realtimeStatus}`} onClick={() => setUpdatesOpen((v) => !v)}>
                    <span className="dot" />
                    <span>
                      {realtimeStatus === 'connected' ? 'Live' : realtimeStatus === 'reconnecting' ? 'Reconnecting' : realtimeStatus === 'connecting' ? 'Connecting' : 'Offline'}
                    </span>
                    {lastRealtimeAt && <small>Updated {lastRealtimeAt}</small>}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>

        {canViewLiveUpdates && updatesOpen && (
          <div className="updates-dropdown">
            <div className="updates-header">
              <strong>Live updates</strong>
              <button type="button" className="link-button" onClick={loadRecentEvents}>
                Refresh
              </button>
            </div>
            {eventsLoading && <div className="helper">Loading updates...</div>}
            {!eventsLoading && events.length === 0 && <div className="helper">No updates yet.</div>}
            {!eventsLoading && events.length > 0 && (
              <ul className="updates-list">
                {events.map((evt) => (
                  <li key={evt.id} className="updates-item">
                    <div className="updates-line">
                      <span className={`tag ${evt.action}`}>{actionLabel(evt.action)}</span>
                      <strong>{evt.employeeEmail || 'Employee'}</strong>
                    </div>
                    <div className="updates-meta">
                      By {evt.updatedByName || evt.updatedByEmail || 'Unknown'} · {formatET(evt.occurredAtUtc)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {coverageConfirm.payload && (
          <ConfirmModal
            title="Coverage impact warning"
            description="This action will affect coverage."
            message={coverageConfirm.message}
            onCancel={() => setCoverageConfirm({ message: '', payload: null })}
            onOk={() => {
              const payload = coverageConfirm.payload;
              setCoverageConfirm({ message: '', payload: null });
              if (payload) {
                void persistPtoLikeRequest(payload);
              }
            }}
          />
        )}

        {dailyScheduleConfirm.payload && (
          <ConfirmModal
            title="Weekly hours warning"
            description="This daily schedule change exceeds the weekly hours limit."
            message={dailyScheduleConfirm.message}
            onCancel={() => setDailyScheduleConfirm({ message: '', payload: null })}
            onOk={() => {
              const payload = dailyScheduleConfirm.payload;
              setDailyScheduleConfirm({ message: '', payload: null });
              if (payload) {
                void persistDailySchedule(payload);
              }
            }}
          />
        )}

        <ErrorPopup message={error} onClose={() => setError(null)} title="Calendar error" />

        {!error && loading && !data && (
          <div className="calendar-grid-scroll">
            <div className="calendar-grid skeleton">
              <div className="coverage-label-cell skeleton-box" />
              {Array.from({ length: 7 }).map((_, idx) => (
                <div key={`sk-cov-${idx}`} className="coverage-cell skeleton-box" />
              ))}
              <div className="calendar-header empty skeleton-box" />
              {Array.from({ length: 7 }).map((_, idx) => (
                <div key={`sk-head-${idx}`} className="calendar-header skeleton-box" />
              ))}
              {Array.from({ length: 5 }).map((_, rowIdx) => (
                <div className="calendar-row" key={`sk-row-${rowIdx}`}>
                  <div className="employee-cell skeleton-box" />
                  {Array.from({ length: 7 }).map((_, cellIdx) => (
                    <div key={`sk-cell-${rowIdx}-${cellIdx}`} className="cell skeleton-box" />
                  ))}
                </div>
              ))}
            </div>
          </div>
        )}
        <div ref={calendarViewRef}>
          {!error && !loading && viewMode === 'grid' && (
            <div className="calendar-grid-scroll">
              <div className="calendar-grid">
                {canViewCoverage && (
                  <>
                    <div className="coverage-label-cell sticky-left sticky-coverage">
                      <div>Expected Coverage</div>
                      <div>Coverage</div>
                      <div>Total Agents</div>
                    </div>
                    {data?.days.map((d) => {
                      const c = coverageByDate.get(d.date);
                      return (
                        <div key={`cov-${d.date}`} className={`coverage-cell sticky-coverage ${c?.statusColor ?? 'red'} ${isPastWeek ? 'locked' : ''} ${d.date === localTodayKey ? 'today-column' : ''}`}>
                          <div>{c?.expectedCoverage ?? 0}%</div>
                          <div>{c ? `${c.coverage}%` : '0%'}</div>
                          <div>{c?.totalAgents ?? 0}</div>
                        </div>
                      );
                    })}
                  </>
                )}

                <div className={`calendar-header empty sticky-left ${canViewCoverage ? 'sticky-day' : 'sticky-top'}`}>Employee Name</div>
                {data?.days.map((d) => {
                  const [dayPart, ...rest] = d.label.split(' ');
                  const datePart = rest.join(' ');
                  return (
                    <div key={d.date} className={`calendar-header ${canViewCoverage ? 'sticky-day' : 'sticky-top'} ${d.date === localTodayKey ? 'today-column' : ''}`}>
                      <div className="day-label">{dayPart.toUpperCase()}</div>
                      <div className="day-sub">{datePart}</div>
                    </div>
                  );
                })}

                {visibleItems.map((row) => {
                  const canInteractWithRow = canInteractWithEmployeeRow(row);
                  const roleInitials = roleInitialsForValue(row.role);
                  return (
                    <div className={`calendar-row ${updatedEmployeeIds.includes(row.id) ? 'recently-updated' : ''}`} key={row.id}>
                      <div className="employee-cell sticky-left">
                        <div className="emp-head">
                          <div className="emp-avatar">{roleInitials}</div>
                          <div>
                            <button type="button" className="emp-name emp-name-button" onClick={() => setSelectedEmployee(row)}>
                              {row.displayName}
                            </button>
                            <div className="emp-meta-grid">
                              <div className="emp-meta micro">Location: {row.location}</div>
                              <div className="emp-meta micro">Shift: {row.shiftTime}</div>
                              <div className="emp-meta micro">Company: {row.company}</div>
                              <div className="emp-meta micro">Operation: {row.operation}</div>
                            </div>
                          </div>
                        </div>
                      </div>
                      {row.cells.map((cell) => {
                        const holidayName = holidayNamesByDate.get(cell.date);
                        const isWorkingHoliday = !!holidayName && (cell.type === 'shiftMorning' || cell.type === 'shiftLate');
                        return (
                          <div
                            key={row.id + cell.date}
                            className={`cell ${colorFor(cell)} ${canInteractWithRow ? 'cell-interactive' : 'cell-readonly'} ${cell.date === localTodayKey ? 'today-column' : ''} ${dragSelection && dragSelection.rowId === row.id &&
                                Math.min(dragSelection.startIndex, dragSelection.endIndex) <= row.cells.findIndex((item) => item.date === cell.date) &&
                                Math.max(dragSelection.startIndex, dragSelection.endIndex) >= row.cells.findIndex((item) => item.date === cell.date)
                                ? 'cell-selected'
                                : ''
                              }`}
                            onClick={() => {
                              if (!canInteractWithRow) return;
                              if (suppressCellClickRef.current) return;
                              openPtoModal(row, cell);
                            }}
                            onDoubleClick={() => {
                              if (!canInteractWithRow) return;
                              openPtoModal(row, cell);
                            }}
                            onMouseDown={() => {
                              if (!canInteractWithRow) return;
                              startDragSelection(row, row.cells.findIndex((item) => item.date === cell.date));
                            }}
                            onMouseEnter={() => {
                              if (!canInteractWithRow) return;
                              extendDragSelection(row, row.cells.findIndex((item) => item.date === cell.date));
                            }}
                            onMouseUp={() => {
                              if (!canInteractWithRow) return;
                              const current = dragSelection;
                              if (current && current.rowId === row.id) {
                                const from = Math.min(current.startIndex, current.endIndex);
                                const to = Math.max(current.startIndex, current.endIndex);
                                if (to > from) {
                                  openPtoModal(row, row.cells[from], to - from + 1);
                                }
                              }
                              clearDragSelection();
                            }}
                            title={canInteractWithRow ? [
                              `${row.displayName} · ${cell.date}`,
                              `Type: ${cell.type}`,
                              cell.shiftTime ? `Shift: ${cell.shiftTime}` : '',
                              holidayName ? `Holiday: ${holidayName}` : '',
                              cell.durationHours > 0 ? `Duration: ${cell.durationHours}h` : '',
                              cell.ptoRequestType ? `PTO: ${cell.ptoRequestType}` : '',
                              cell.ptoComments ? `Comments: ${cell.ptoComments}` : '',
                              cell.isDailyScheduleOverride && cell.scheduleOverrideComments ? `Daily schedule comments: ${cell.scheduleOverrideComments}` : '',
                              updatedEmployeeIds.includes(row.id) ? 'Recently updated' : '',
                            ].filter(Boolean).join('\n') : undefined}
                          >
                            <div className="cell-label">
                              <span className={`cell-icon ${isWorkingHoliday ? 'holiday-work-icon' : ''}`} aria-hidden="true">
                                {cell.type === 'leave' ? 'P' : cell.type === 'dayOff' ? 'O' : cell.type === 'shiftLate' ? 'L' : 'M'}
                              </span>
                              <span>{cell.label}</span>
                            </div>
                            {cell.durationHours > 0 && <div className="cell-sub">{cell.durationHours}h</div>}
                            {cell.isDailyScheduleOverride && cell.scheduleOverrideComments && (
                              <div className="cell-comment" aria-label={`Daily schedule comments: ${cell.scheduleOverrideComments}`}>
                                {cell.scheduleOverrideComments}
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
          {!error && !loading && viewMode === 'month' && (
            <div className="calendar-month-view">
              <div className="calendar-month-head">
                <div>
                  <div className="calendar-month-kicker">Year Overview</div>
                  <h3>{currentYearLabel}</h3>
                </div>
                <div className="calendar-month-head-side">
                  <div className="calendar-year-actions">
                    <Button
                      type="button"
                      className="calendar-toolbar-btn"
                      variant="ghost"
                      size="sm"
                      onClick={() => setMonthOverviewYear((year) => year - 1)}
                    >
                      Previous Year
                    </Button>
                    <Button
                      type="button"
                      className="calendar-toolbar-btn"
                      variant={monthOverviewYear === actualCurrentYear ? 'primary' : 'ghost'}
                      size="sm"
                      onClick={() => setMonthOverviewYear(actualCurrentYear)}
                    >
                      Current Year
                    </Button>
                    <Button
                      type="button"
                      className="calendar-toolbar-btn"
                      variant="ghost"
                      size="sm"
                      onClick={() => setMonthOverviewYear((year) => year + 1)}
                    >
                      Next Year
                    </Button>
                  </div>
                  <p>Click any day to jump into its week. Colombian holidays are shown in red.</p>
                </div>
              </div>
              <div className="calendar-year-grid">
                {yearMonths.map((month) => (
                  <section key={month.key} className="calendar-year-month">
                    <h4>{month.label}</h4>
                    <div className="calendar-year-weekdays">
                      {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map((day) => (
                        <div key={`${month.key}-${day}`} className="calendar-month-weekday">
                          {day.slice(0, 2)}
                        </div>
                      ))}
                    </div>
                    <div className="calendar-month-grid">
                      {month.cells.map((cell) =>
                        cell.date ? (
                          <button
                            key={cell.key}
                            type="button"
                            className={`calendar-month-day ${cell.holidayName ? 'holiday' : ''} ${cell.isToday ? 'today' : ''}`}
                            title={cell.holidayName ? `${cell.date}\nHoliday: ${cell.holidayName}` : cell.date}
                            onClick={() => {
                              setWeekStart(weekStartForDate(cell.date!));
                              setViewMode('grid');
                            }}
                          >
                            <span className="calendar-month-day-number">{cell.dayNumber}</span>
                          </button>
                        ) : (
                          <div key={cell.key} className="calendar-month-day blank" aria-hidden="true" />
                        ),
                      )}
                    </div>
                  </section>
                ))}
              </div>
            </div>
          )}
          {!error && !loading && viewMode === 'list' && (
            <div className="calendar-list">
              {visibleItems.map((row) => {
                const canInteractWithRow = canInteractWithEmployeeRow(row);
                const roleInitials = roleInitialsForValue(row.role);
                return (
                  <article key={`list-${row.id}`} className={`calendar-list-card ${updatedEmployeeIds.includes(row.id) ? 'recently-updated' : ''}`}>
                    <header className="calendar-list-head">
                      <div className="emp-head">
                        <div className="emp-avatar">{roleInitials}</div>
                        <div>
                          <button type="button" className="emp-name emp-name-button" onClick={() => setSelectedEmployee(row)}>
                            {row.displayName}
                          </button>
                          <div className="calendar-list-meta">{row.operation} · {row.company} · {row.shiftTime}</div>
                        </div>
                      </div>
                      {TRACKY_ENABLED && (
                        <Button
                          type="button"
                          className="quick-filter-chip"
                          variant="ghost"
                          size="sm"
                          onClick={() => askTracky(`Schedule of ${row.displayName} this week`)}
                        >
                          Ask Tracky
                        </Button>
                      )}
                    </header>
                    <div className="calendar-list-days">
                      {row.cells.map((cell) => {
                        const holidayName = holidayNamesByDate.get(cell.date);
                        const isWorkingHoliday = !!holidayName && (cell.type === 'shiftMorning' || cell.type === 'shiftLate');
                        return (
                          <button
                            key={`list-cell-${row.id}-${cell.date}`}
                            type="button"
                            className={`calendar-list-day ${colorFor(cell)} ${canInteractWithRow ? '' : 'readonly'} ${cell.date === localTodayKey ? 'today-column' : ''} ${isWorkingHoliday ? 'holiday-work-day' : ''}`}
                            onClick={() => {
                              if (!canInteractWithRow) return;
                              openPtoModal(row, cell);
                            }}
                            onDoubleClick={() => {
                              if (!canInteractWithRow) return;
                              openPtoModal(row, cell);
                            }}
                            title={canInteractWithRow ? [cell.label, holidayName ? `Holiday: ${holidayName}` : ''].filter(Boolean).join('\n') : undefined}
                          >
                            <span>{data?.days.find((day) => day.date === cell.date)?.label.split(' ')[0] ?? cell.date}</span>
                            <strong>{cell.label}</strong>
                          </button>
                        );
                      })}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
          <div className="calendar-footer">
            <div className="calendar-footer-actions">
              {canManageUsersForRole(role) && (
                <Button className="link-button" variant="ghost" size="sm" onClick={onCreateEmployee}>
                  + Create Employee
                </Button>
              )}
            </div>
            {viewMode === 'grid' ? (
              <div className="calendar-visible-count">
                Showing all {filteredItems.length} visible employees
              </div>
            ) : (
              <div className="calendar-month-footer-note">Showing the full year. Holidays are highlighted in red.</div>
            )}
          </div>
        </div>
        {selectedEmployee && (
            <ModalShell className="employee-details-modal" ariaLabel="Employee details" onBackdropClick={() => setSelectedEmployee(null)}>
                <div className="employee-details-head">
                  <div className="employee-details-avatar">
                    {roleInitialsForValue(selectedEmployee.role)}
                  </div>
                  <div className="employee-details-identity">
                    <h2>{selectedEmployee.displayName}</h2>
                    <p>{selectedEmployee.email}</p>
                  </div>
                </div>

                <div className="employee-details-grid">
                  {canViewCoverage && (
                    <div className="employee-detail-item">
                      <span>Role</span>
                      <strong>{roleLabelFor(selectedEmployee.role)}</strong>
                    </div>
                  )}
                  <div className="employee-detail-item">
                    <span>Shift</span>
                    <strong>{selectedEmployee.shiftTime}</strong>
                  </div>
                  <div className="employee-detail-item">
                    <span>Location</span>
                    <strong>{selectedEmployee.location}</strong>
                  </div>
                  <div className="employee-detail-item">
                    <span>Company</span>
                    <strong>{selectedEmployee.company}</strong>
                  </div>
                  <div className="employee-detail-item">
                    <span>Operation</span>
                    <strong>{selectedEmployee.operation}</strong>
                  </div>
                </div>

                <div className="modal-actions">
                  {TRACKY_ENABLED && (
                    <Button variant="ghost" onClick={() => askTracky(`Schedule of ${selectedEmployee.displayName} this week`)}>
                      Ask Tracky
                    </Button>
                  )}
                  <Button variant="primary" onClick={() => setSelectedEmployee(null)}>
                    Close
                  </Button>
                </div>
            </ModalShell>
          )}
          {ptoModal.open && typeof document !== 'undefined' && createPortal(
            <div ref={ptoModalBackdropRef} className="modal" role="dialog" aria-modal="true">
              <div
                ref={ptoModalCardRef}
                className={`modal-card pto-modal pto-modal-animated ${ptoModalClosing ? 'is-closing' : ''}`}
              >
                <h2>
                  {ptoModal.activeTab === 'swap'
                    ? 'Change Request'
                    : ptoModal.activeTab === 'schedule'
                      ? 'Change Schedule for This Day'
                    : ptoModal.activeTab === 'dayoff'
                      ? ptoModal.existingGroupId ? 'Day Off Request' : 'New PTO'
                      : ptoModal.existingGroupId
                        ? 'PTO Request'
                        : 'New PTO'}
                </h2>
                <div className="member-scope-toggle">
                  {ptoModal.canChangeDailySchedule && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      active={ptoModal.activeTab === 'schedule'}
                      onClick={() => setPtoModal((prev) => ({ ...prev, activeTab: 'schedule' }))}
                    >
                      Daily Schedule
                    </Button>
                  )}
                  {canTargetSelectedPtoUser && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      active={ptoModal.activeTab === 'pto'}
                      onClick={() => setPtoModal((prev) => ({ ...prev, activeTab: 'pto' }))}
                    >
                      PTO
                    </Button>
                  )}
                  {ptoModal.swapTargetUserId && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      active={ptoModal.activeTab === 'swap'}
                      onClick={() => setPtoModal((prev) => ({ ...prev, activeTab: 'swap' }))}
                    >
                      Change Request
                    </Button>
                  )}
                  {canTargetSelectedPtoUser && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      active={ptoModal.activeTab === 'dayoff'}
                      onClick={() => setPtoModal((prev) => ({ ...prev, activeTab: 'dayoff' }))}
                    >
                      Day Off Request
                    </Button>
                  )}
                </div>

                {ptoModal.activeTab === 'schedule' && (
                  <div ref={ptoModalPanelRef} className="modal-form">
                    <p className="helper">This changes only the selected date and does not modify the employee's weekly schedule.</p>
                    <div className="grid two">
                      <Field label="Employee">
                        <input type="text" value={ptoModal.userName} readOnly />
                      </Field>
                      <Field label="Date">
                        <input type="date" value={ptoModal.startDate} readOnly />
                      </Field>
                    </div>
                    <div className="grid two">
                      <Field label="Start Time*">
                        <input
                          type="time"
                          value={ptoModal.dailyStartTime}
                          onChange={(event) => setPtoModal((prev) => ({ ...prev, dailyStartTime: event.target.value }))}
                        />
                      </Field>
                      <Field label="End Time*">
                        <input
                          type="time"
                          value={ptoModal.dailyEndTime}
                          onChange={(event) => setPtoModal((prev) => ({ ...prev, dailyEndTime: event.target.value }))}
                        />
                      </Field>
                    </div>
                    <Field label="Comments*">
                      <textarea
                        value={ptoModal.comments}
                        onChange={(event) => setPtoModal((prev) => ({ ...prev, comments: event.target.value }))}
                        placeholder="Explain why this day's schedule is changing..."
                        rows={3}
                      />
                    </Field>
                  </div>
                )}

                {ptoModal.activeTab === 'pto' && (
                  <div ref={ptoModalPanelRef} className="modal-form">
                    <Field label="Request Type*">
                      <Select
                        value={ptoModal.requestType}
                        onChange={(nextValue) => setPtoModal((prev) => ({ ...prev, requestType: nextValue }))}
                        options={[{ value: '', label: 'Select' }, ...ptoRequestTypeOptions.map((option) => ({ value: option.value, label: option.label }))]}
                        placeholder="Select"
                        ariaLabel="Request Type"
                      />
                    </Field>

                    <div className="grid two">
                      <Field label="Days*">
                        <div className="number-stepper">
                          <button
                            type="button"
                            className="number-stepper-btn"
                            onClick={() => setPtoDays(ptoModal.numberOfDays - 1)}
                            disabled={ptoModal.numberOfDays <= 1}
                            aria-label="Decrease days"
                          >
                            -
                          </button>
                          <input
                            type="text"
                            inputMode="numeric"
                            pattern="[0-9]*"
                            value={String(ptoModal.numberOfDays)}
                            onChange={(e) => {
                              const digitsOnly = e.target.value.replace(/\D/g, '');
                              if (!digitsOnly) {
                                setPtoModal((prev) => ({ ...prev, numberOfDays: 1 }));
                                return;
                              }
                              setPtoDays(Number(digitsOnly));
                            }}
                            onKeyDown={(e) => {
                              if (e.key === 'ArrowUp') {
                                e.preventDefault();
                                setPtoDays(ptoModal.numberOfDays + 1);
                              } else if (e.key === 'ArrowDown') {
                                e.preventDefault();
                                setPtoDays(ptoModal.numberOfDays - 1);
                              }
                            }}
                            placeholder="Number of days"
                            aria-label="Number of PTO days"
                          />
                          <button
                            type="button"
                            className="number-stepper-btn"
                            onClick={() => setPtoDays(ptoModal.numberOfDays + 1)}
                            disabled={ptoModal.numberOfDays >= 90}
                            aria-label="Increase days"
                          >
                            +
                          </button>
                        </div>
                      </Field>
                      <Field label="Start Date*">
                        <input type="date" value={ptoModal.startDate} readOnly />
                      </Field>
                    </div>

                    <Field label="Comments*">
                      <textarea
                        value={ptoModal.comments}
                        onChange={(e) => setPtoModal((prev) => ({ ...prev, comments: e.target.value }))}
                        placeholder="Add a comment..."
                        rows={3}
                      />
                    </Field>
                  </div>
                )}

                {ptoModal.activeTab === 'dayoff' && (
                  <div ref={ptoModalPanelRef} className="modal-form">
                    <div className="grid two">
                      <Field label="Days*">
                        <div className="number-stepper">
                          <button
                            type="button"
                            className="number-stepper-btn"
                            onClick={() => setPtoDays(ptoModal.numberOfDays - 1)}
                            disabled={ptoModal.numberOfDays <= 1}
                            aria-label="Decrease days"
                          >
                            -
                          </button>
                          <input
                            type="text"
                            inputMode="numeric"
                            pattern="[0-9]*"
                            value={String(ptoModal.numberOfDays)}
                            onChange={(e) => {
                              const digitsOnly = e.target.value.replace(/\D/g, '');
                              if (!digitsOnly) {
                                setPtoModal((prev) => ({ ...prev, numberOfDays: 1 }));
                                return;
                              }
                              setPtoDays(Number(digitsOnly));
                            }}
                            onKeyDown={(e) => {
                              if (e.key === 'ArrowUp') {
                                e.preventDefault();
                                setPtoDays(ptoModal.numberOfDays + 1);
                              } else if (e.key === 'ArrowDown') {
                                e.preventDefault();
                                setPtoDays(ptoModal.numberOfDays - 1);
                              }
                            }}
                            placeholder="Number of day off days"
                            aria-label="Number of day off days"
                          />
                          <button
                            type="button"
                            className="number-stepper-btn"
                            onClick={() => setPtoDays(ptoModal.numberOfDays + 1)}
                            disabled={ptoModal.numberOfDays >= 90}
                            aria-label="Increase days"
                          >
                            +
                          </button>
                        </div>
                      </Field>
                      <Field label="Start Date*">
                        <input type="date" value={ptoModal.startDate} readOnly />
                      </Field>
                    </div>

                    <Field label="Comments*">
                      <textarea
                        value={ptoModal.comments}
                        onChange={(e) => setPtoModal((prev) => ({ ...prev, comments: e.target.value }))}
                        placeholder="Add a comment..."
                        rows={3}
                      />
                    </Field>
                  </div>
                )}

                {ptoModal.activeTab === 'swap' && (
                  <div ref={ptoModalPanelRef} className="modal-form">
                    <div className="swap-summary-card">
                      <div className="swap-summary-intro">
                        Request a change with a same-role coworker by taking one of their day off dates and offering one
                        of your own day off dates from the next 30 calendar days when they are scheduled to work.
                      </div>
                      <div className="swap-summary-grid">
                        <div className="swap-summary-block">
                          <span className="swap-summary-label">You want</span>
                          <strong>{formatSwapDate(ptoModal.swapTargetDate)}</strong>
                          <span>
                            {ptoModal.swapTargetUserName ? `${ptoModal.swapTargetUserName}'s day off` : 'Coworker day off'}
                          </span>
                          <span>
                            {ptoModal.swapRequesterShiftOnTargetDate
                              ? `Your working shift to trade: ${ptoModal.swapRequesterShiftOnTargetDate}`
                              : 'Loading your working shift for this day...'}
                          </span>
                        </div>
                        <div className="swap-summary-block">
                          <span className="swap-summary-label">You offer</span>
                          <strong>{formatSwapDate(ptoModal.swapRequesterDate)}</strong>
                          <span>Your selected day off to offer</span>
                          <span>
                            {ptoModal.swapRequesterDate && ptoModal.swapTargetShiftByRequesterDate?.[ptoModal.swapRequesterDate]
                              ? `Coworker working shift to trade: ${ptoModal.swapTargetShiftByRequesterDate[ptoModal.swapRequesterDate]}`
                              : swapDatesLoading
                                ? 'Loading coworker working shift for this day...'
                                : 'Select a day off to see the coworker shift being exchanged'}
                          </span>
                        </div>
                      </div>
                    </div>
                    <div className="grid two">
                      <Field label="Coworker*">
                        <input type="text" value={ptoModal.swapTargetUserName ?? ''} readOnly />
                      </Field>
                      <Field label="Coworker Day Off Requested*">
                        <input type="date" value={ptoModal.swapTargetDate ?? ''} readOnly />
                      </Field>
                    </div>
                    <div className="grid two">
                      <Field label="Your Day Off To Offer*">
                        <Select
                          value={ptoModal.swapRequesterDate ?? ''}
                          onChange={(nextValue) => setPtoModal((prev) => ({ ...prev, swapRequesterDate: nextValue }))}
                          disabled={swapDatesLoading}
                          ariaLabel="Your Day Off To Offer"
                        >
                          <option value="">
                            {swapDatesLoading
                              ? 'Loading eligible day off dates...'
                              : 'Select one of your day off dates in the next 30 calendar days'}
                          </option>
                          {(ptoModal.swapAvailableRequesterDates ?? []).map((date) => (
                            <option key={date} value={date}>
                              {`${formatSwapDate(date)}${ptoModal.swapTargetShiftByRequesterDate?.[date]
                                  ? ` · Coworker works ${ptoModal.swapTargetShiftByRequesterDate[date]}`
                                  : ''
                                }`}
                            </option>
                          ))}
                        </Select>
                      </Field>
                      <div className="swap-summary-note">
                        The request stays pending until a manager or admin reviews it. Once approved, the schedule is
                        updated automatically.
                      </div>
                    </div>
                    {swapDatesLoading && <p className="field-help">Checking your eligible dates for this request...</p>}
                    {!swapDatesLoading && (ptoModal.swapAvailableRequesterDates ?? []).length === 0 && (
                      <p className="field-help error">
                        You do not have an eligible day off to offer in the next 30 calendar days where this coworker is
                        working that same day.
                      </p>
                    )}
                    <Field label="Observations*">
                      <textarea
                        value={ptoModal.comments}
                        onChange={(e) => setPtoModal((prev) => ({ ...prev, comments: e.target.value }))}
                        placeholder="Add context for the change request..."
                        rows={3}
                      />
                    </Field>
                  </div>
                )}

                <div className="modal-actions">
                  <Button variant="ghost" onClick={() => closePtoModal()} disabled={savingPto || savingSwap}>
                    Cancel
                  </Button>
                  {ptoModal.activeTab === 'pto' && ptoModal.canCancelApproved && (
                    <Button variant="dangerGhost" onClick={cancelApprovedPto} disabled={savingPto || savingSwap}>
                      Cancel PTO
                    </Button>
                  )}
                  <Button
                    variant="primary"
                    onClick={ptoModal.activeTab === 'schedule' ? submitDailySchedule : ptoModal.activeTab === 'swap' ? submitSwap : ptoModal.activeTab === 'dayoff' ? submitDayOff : submitPto}
                    disabled={
                      savingPto ||
                      savingSwap ||
                      swapDatesLoading ||
                      (ptoModal.activeTab === 'swap' &&
                        (!(ptoModal.swapAvailableRequesterDates ?? []).length || !ptoModal.swapRequesterDate))
                    }
                  >
                    {savingPto || savingSwap ? 'Saving...' : 'Save'}
                  </Button>
                </div>
              </div>
            </div>,
            document.body,
          )}
          {TRACKY_ENABLED && (
            <ScheduleAssistant
              weekStart={data?.weekStart ?? weekStart}
              docked={assistantDocked}
              onPromptReady={(submit) => {
                trackyPromptRef.current = submit;
              }}
            />
          )}
          <div className="toast-stack" aria-live="polite" aria-atomic="true">
            {toasts.map((toast) => (
              <div key={toast.id} className={`toast ${toast.tone}`}>
                {toast.text}
              </div>
            ))}
          </div>
        </div>
      </div>
      );
}

      export default ShiftCalendarPage;
