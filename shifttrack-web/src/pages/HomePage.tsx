import { useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  BarChart3,
  Building2,
  CalendarDays,
  Clock3,
  Gauge,
  Landmark,
  Siren,
  TriangleAlert,
  UsersRound,
  Zap,
} from 'lucide-react';
import { apiFetch } from '../lib/api';
import { isAdminRole, isManagerRole, isTeamLeaderRole, roleLabelForValue } from '../lib/roles';
import type { ApiError, ReportsOverview, UserInfo } from '../types';

type HomeTab = 'home' | 'calendar' | 'employees' | 'requests' | 'reports' | 'super-admin';

const emptyReports: ReportsOverview = {
  selectedCompany: '',
  availableCompanies: [],
  weekStart: '',
  weekEnd: '',
  kpis: {
    totalActiveEmployees: 0,
    averageWeeklyCoverage: 0,
    riskDays: 0,
    pendingPtoRequests: 0,
    operations: 0,
  },
  coverageHeatmap: [],
  coverageTrend: [],
  expectedVsActual: [],
  ptoByStatus: [],
  ptoByType: [],
  headcountByOperation: [],
  topRiskOperations: [],
};

function pct(value: number) {
  return `${Number.isFinite(value) ? value.toFixed(1).replace('.0', '') : '0'}%`;
}

function firstName(user: UserInfo) {
  const source = user.displayName || user.email || 'there';
  return source.split(/\s+/)[0] || source;
}

function HomeMetric({
  label,
  value,
  note,
  tone = 'blue',
  icon,
}: {
  label: string;
  value: string | number;
  note: string;
  tone?: 'blue' | 'green' | 'amber' | 'red' | 'purple';
  icon: ReactNode;
}) {
  return (
    <article className={`home-metric ${tone}`}>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <p>{note}</p>
      </div>
      <div className="home-metric-icon">{icon}</div>
    </article>
  );
}

function HomePanel({
  title,
  subtitle,
  action,
  onAction,
  children,
  className = '',
}: {
  title: string;
  subtitle?: string;
  action?: string;
  onAction?: () => void;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`home-panel ${className}`}>
      <div className="home-panel-header">
        <div>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        {action && (
          <button type="button" onClick={onAction}>
            {action}
          </button>
        )}
      </div>
      {children}
    </section>
  );
}

export function HomePage({ user, onNavigate }: { user: UserInfo; onNavigate: (tab: HomeTab) => void }) {
  const [reports, setReports] = useState<ReportsOverview[]>([]);
  const [loading, setLoading] = useState(isAdminRole(user.role));
  const [error, setError] = useState<string | null>(null);

  const visibleCompanies = useMemo(() => {
    const companies = user.companies?.length ? user.companies : [user.company ?? ''];
    return companies.filter(Boolean);
  }, [user.companies, user.company]);

  useEffect(() => {
    if (!isAdminRole(user.role)) return;

    let canceled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const firstResponse = await apiFetch('/reports/overview');
        const firstJson = await firstResponse.json().catch(() => null) as ReportsOverview | ApiError | null;
        if (!firstResponse.ok) throw new Error((firstJson as ApiError | null)?.message ?? 'Unable to load home overview.');
        const firstReport = (firstJson ?? emptyReports) as ReportsOverview;
        const companies = firstReport.availableCompanies.length ? firstReport.availableCompanies : [firstReport.selectedCompany].filter(Boolean);
        const remaining = companies
          .filter((company) => company && company.toLowerCase() !== firstReport.selectedCompany.toLowerCase())
          .slice(0, 8);
        const otherReports = await Promise.all(remaining.map(async (company) => {
          const response = await apiFetch(`/reports/overview?company=${encodeURIComponent(company)}`);
          const json = await response.json().catch(() => null) as ReportsOverview | ApiError | null;
          if (!response.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to load ${company}.`);
          return (json ?? emptyReports) as ReportsOverview;
        }));
        if (!canceled) setReports([firstReport, ...otherReports].filter((item) => item.selectedCompany));
      } catch (err) {
        if (!canceled) setError(err instanceof Error ? err.message : 'Unable to load home overview.');
      } finally {
        if (!canceled) setLoading(false);
      }
    };

    void load();
    return () => {
      canceled = true;
    };
  }, [user.role]);

  const aggregate = useMemo(() => {
    if (!reports.length) return emptyReports.kpis;
    const totalActiveEmployees = reports.reduce((sum, report) => sum + report.kpis.totalActiveEmployees, 0);
    const riskDays = reports.reduce((sum, report) => sum + report.kpis.riskDays, 0);
    const pendingPtoRequests = reports.reduce((sum, report) => sum + report.kpis.pendingPtoRequests, 0);
    const operations = reports.reduce((sum, report) => sum + report.kpis.operations, 0);
    const averageWeeklyCoverage = reports.reduce((sum, report) => sum + report.kpis.averageWeeklyCoverage, 0) / reports.length;
    return { totalActiveEmployees, riskDays, pendingPtoRequests, operations, averageWeeklyCoverage };
  }, [reports]);

  const topRisks = useMemo(() => reports
    .flatMap((report) => report.topRiskOperations.map((risk) => ({ ...risk, company: report.selectedCompany })))
    .sort((a, b) => b.riskDays - a.riskDays || a.averageCoverage - b.averageCoverage)
    .slice(0, 5), [reports]);

  const roleLabel = user.isSystemHidden ? 'Global Administrator' : roleLabelForValue(user.role);
  const isAdmin = isAdminRole(user.role);
  const isManager = isManagerRole(user.role);
  const isEmployeeLike = !isAdmin && !isManager;

  return (
    <section className="home-page">
      <div className={`home-hero ${isAdmin ? 'executive' : isManager ? 'operations' : 'personal'}`}>
        <div>
          <p>{isAdmin ? 'Enterprise overview' : isManager ? 'Operations workspace' : 'Personal workspace'}</p>
          <h1>{isAdmin ? `Good to see you, ${firstName(user)}` : isManager ? "Today's workforce control center" : `Welcome back, ${firstName(user)}`}</h1>
          <span>
            {isAdmin
              ? `You are viewing ${reports.length || visibleCompanies.length || 1} company workspace(s) with ${aggregate.riskDays} coverage risk day(s).`
              : isManager
                ? 'Review schedule, requests and operational coverage before making staffing decisions.'
                : isTeamLeaderRole(user.role)
                  ? 'Jump into your calendar and requests without extra dashboard noise.'
                  : 'Your workspace is focused on calendar visibility and personal requests.'}
          </span>
        </div>
        <div className="home-hero-actions">
          <button type="button" onClick={() => onNavigate('calendar')}>Open Shift Calendar</button>
          {isAdmin && <button type="button" onClick={() => onNavigate('reports')}>Review Reports</button>}
          {!isEmployeeLike && <button type="button" onClick={() => onNavigate('requests')}>Review Requests</button>}
        </div>
      </div>

      {error && <div className="alert">{error}</div>}
      {loading && <div className="home-loading">Loading enterprise overview...</div>}

      {!loading && (
        <>
          {!isEmployeeLike && (
            <div className="home-metrics-grid">
              <HomeMetric label="Active employees" value={isAdmin ? aggregate.totalActiveEmployees : '-'} note={isAdmin ? 'Across visible companies' : 'Available in reports for admins'} icon={<UsersRound size={22} strokeWidth={2.4} />} />
              <HomeMetric label="Average coverage" value={isAdmin ? pct(aggregate.averageWeeklyCoverage) : '-'} note={isAdmin ? 'Current week average' : 'Open calendar for schedule detail'} tone={isAdmin && aggregate.averageWeeklyCoverage < 85 ? 'amber' : 'green'} icon={<Gauge size={22} strokeWidth={2.4} />} />
              <HomeMetric label="Risk days" value={isAdmin ? aggregate.riskDays : '-'} note={isAdmin ? 'Across selected portfolio' : 'Use requests/calendar views'} tone={isAdmin && aggregate.riskDays > 0 ? 'red' : 'green'} icon={<TriangleAlert size={22} strokeWidth={2.4} />} />
              <HomeMetric label="Pending PTO" value={isAdmin ? aggregate.pendingPtoRequests : '-'} note={isAdmin ? 'Awaiting review' : 'Open requests queue'} tone={isAdmin && aggregate.pendingPtoRequests > 0 ? 'purple' : 'green'} icon={<Clock3 size={22} strokeWidth={2.4} />} />
              <HomeMetric label={isAdmin ? 'Companies' : 'Role'} value={isAdmin ? (reports.length || visibleCompanies.length || 1) : roleLabel} note={isAdmin ? 'Visible to your scope' : 'Workspace permissions'} tone="green" icon={<Building2 size={22} strokeWidth={2.4} />} />
            </div>
          )}

          <div className="home-layout">
            {isAdmin && (
              <HomePanel className="home-panel-wide" title="Company performance" subtitle="Coverage and workforce health across your portfolio." action="Open reports" onAction={() => onNavigate('reports')}>
                {reports.length ? (
                  <div className="home-company-grid">
                    {reports.map((report) => {
                      const tone = report.kpis.riskDays > 2 ? 'red' : report.kpis.riskDays > 0 ? 'amber' : 'green';
                      return (
                        <button type="button" className={`home-company-card ${tone}`} key={report.selectedCompany} onClick={() => onNavigate('reports')}>
                          <div>
                            <strong>{report.selectedCompany}</strong>
                            <span>{report.kpis.totalActiveEmployees} active employees</span>
                          </div>
                          <div className="home-company-metrics">
                            <b>{pct(report.kpis.averageWeeklyCoverage)}</b>
                            <em>{report.kpis.riskDays} risk days</em>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  <div className="home-empty">No company reporting data available yet.</div>
                )}
              </HomePanel>
            )}

            <HomePanel title="Priority alerts" subtitle="Operational items requiring review." action={isAdmin ? 'View reports' : 'Open requests'} onAction={() => onNavigate(isAdmin ? 'reports' : 'requests')}>
              <div className="home-alert-list">
                {isAdmin && topRisks.length ? topRisks.map((risk) => (
                  <div className={`home-alert ${risk.riskDays > 2 ? 'red' : 'amber'}`} key={`${risk.company}-${risk.operation}`}>
                    <Siren size={18} strokeWidth={2.4} />
                    <div>
                      <strong>{risk.operation} coverage needs review</strong>
                      <span>{risk.company} · {pct(risk.averageCoverage)} average · {risk.riskDays} risk day(s)</span>
                    </div>
                  </div>
                )) : (
                  <>
                    <div className="home-alert blue">
                      <CalendarDays size={18} strokeWidth={2.3} />
                      <div>
                        <strong>Start with the live calendar</strong>
                        <span>Use the schedule view to inspect current week staffing.</span>
                      </div>
                    </div>
                    <div className="home-alert amber">
                      <Clock3 size={18} strokeWidth={2.3} />
                      <div>
                        <strong>Review pending requests</strong>
                        <span>PTO and swap decisions should be reviewed with coverage impact.</span>
                      </div>
                    </div>
                  </>
                )}
              </div>
            </HomePanel>

            <HomePanel title="Quick actions" subtitle="Common tasks across your workspace.">
              <div className="home-actions-grid">
                <button type="button" onClick={() => onNavigate('calendar')}>
                  <CalendarDays size={20} strokeWidth={2.3} />
                  <strong>Open Shift Calendar</strong>
                  <span>Manage live schedules</span>
                </button>
                {!isEmployeeLike && (
                  <button type="button" onClick={() => onNavigate('requests')}>
                    <Clock3 size={20} strokeWidth={2.3} />
                    <strong>Review Requests</strong>
                    <span>Approve PTO and swaps</span>
                  </button>
                )}
                {isAdmin && (
                  <button type="button" onClick={() => onNavigate('reports')}>
                    <BarChart3 size={20} strokeWidth={2.3} />
                    <strong>Open Reports</strong>
                    <span>Coverage and workforce</span>
                  </button>
                )}
                {user.isSystemHidden && (
                  <button type="button" onClick={() => onNavigate('super-admin')}>
                    <Landmark size={20} strokeWidth={2.3} />
                    <strong>Super Admin</strong>
                    <span>Manage admin access</span>
                  </button>
                )}
                {!isAdmin && (
                  <button type="button" onClick={() => onNavigate('calendar')}>
                    <Zap size={20} strokeWidth={2.3} />
                    <strong>Plan your week</strong>
                    <span>Check shifts and time off</span>
                  </button>
                )}
              </div>
            </HomePanel>
          </div>
        </>
      )}
    </section>
  );
}
