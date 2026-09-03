import { useEffect, useMemo, useState, type ReactNode } from 'react';
import ReactEChartsCore from 'echarts-for-react/lib/core';
import * as echarts from 'echarts/core';
import { BarChart, HeatmapChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TooltipComponent, VisualMapComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import type { EChartsOption } from 'echarts';
import {
  ArrowsClockwise,
  Buildings,
  CalendarCheck,
  ChartBar,
  Clock,
  DownloadSimple,
  FunnelSimple,
  Lightning,
  UsersThree,
  WarningCircle,
  type IconProps,
} from 'phosphor-react';
import { Button } from '../components/ui/Button';
import { Select } from '../components/ui/Select';
import { apiFetch } from '../lib/api';
import type { ApiError, ReportsOverview } from '../types';

echarts.use([
  BarChart,
  HeatmapChart,
  LineChart,
  PieChart,
  GridComponent,
  LegendComponent,
  TooltipComponent,
  VisualMapComponent,
  CanvasRenderer,
]);

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

const chartPalette = ['#317eb5', '#25a9e0', '#7bc96f', '#f2c94c', '#eb5757', '#6f7f95'];
const riskColorMap: Record<string, string> = {
  green: '#33a852',
  yellow: '#f2b84b',
  red: '#d94d4d',
};

type ReportPeriod = 'current-week' | 'previous-week' | 'month' | 'custom';

function shortDate(value: string) {
  if (!value) return '';
  const [, month, day] = value.split('-');
  return month && day ? `${month}/${day}` : value;
}

function isoDate(date: Date) {
  return date.toISOString().slice(0, 10);
}

function resolveMonday(date: Date) {
  const copy = new Date(date);
  const diff = (copy.getDay() + 6) % 7;
  copy.setDate(copy.getDate() - diff);
  return copy;
}

function defaultCustomRange() {
  const start = resolveMonday(new Date());
  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  return { startDate: isoDate(start), endDate: isoDate(end) };
}

function dateAxisLabel(value: string) {
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) return value;
  return `${shortDate(value)} ${date.toLocaleDateString(undefined, { weekday: 'short' })}`;
}

function pct(value: number) {
  return `${Number.isFinite(value) ? value.toFixed(1).replace('.0', '') : '0'}%`;
}

function chartBase(): EChartsOption {
  return {
    color: chartPalette,
    textStyle: { fontFamily: 'Nunito Sans, system-ui, sans-serif', color: '#26364d' },
    tooltip: {
      trigger: 'item',
      backgroundColor: 'rgba(20, 35, 58, 0.94)',
      borderWidth: 0,
      textStyle: { color: '#ffffff' },
      padding: [10, 12],
    },
    grid: { left: 38, right: 24, top: 34, bottom: 34, containLabel: true },
  };
}

function ReportsEmpty({ label = 'No report data for this scope yet.' }: { label?: string }) {
  return <div className="reports-empty">{label}</div>;
}

function ReportPanel({
  title,
  subtitle,
  children,
  className = '',
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <article className={`reports-panel ${className}`}>
      <div className="reports-panel-header">
        <div>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
      </div>
      {children}
    </article>
  );
}

function KpiCard({
  label,
  value,
  note,
  tone = 'blue',
  icon: Icon,
}: {
  label: string;
  value: string | number;
  note: string;
  tone?: 'blue' | 'green' | 'amber' | 'red' | 'purple';
  icon: React.ComponentType<IconProps>;
}) {
  return (
    <div className={`reports-kpi-card ${tone}`}>
      <div className="reports-kpi-topline">
        <div>
          <span>{label}</span>
          <strong>{value}</strong>
        </div>
        <div className="reports-kpi-icon">
          <Icon size={21} weight="bold" />
        </div>
      </div>
      <p>{note}</p>
    </div>
  );
}

export function ReportsPage() {
  const [data, setData] = useState<ReportsOverview>(emptyReports);
  const [selectedCompany, setSelectedCompany] = useState('');
  const [selectedOperation, setSelectedOperation] = useState('all');
  const [selectedPeriod, setSelectedPeriod] = useState<ReportPeriod>('current-week');
  const [customRange, setCustomRange] = useState(defaultCustomRange);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadReports = async (
    company = selectedCompany,
    period: ReportPeriod = selectedPeriod,
    range = customRange,
  ) => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (company) params.set('company', company);
      params.set('period', period);
      if (period === 'custom') {
        params.set('startDate', range.startDate);
        params.set('endDate', range.endDate);
      }
      const query = `?${params.toString()}`;
      const response = await apiFetch(`/reports/overview${query}`);
      const json = await response.json().catch(() => null) as ReportsOverview | ApiError | null;
      if (!response.ok) {
        throw new Error((json as ApiError | null)?.message ?? 'Unable to load reports.');
      }
      const report = (json ?? emptyReports) as ReportsOverview;
      setData(report);
      setSelectedCompany(report.selectedCompany);
      setSelectedOperation('all');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load reports.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadReports('');
  }, []);

  const operationOptions = useMemo(() => {
    const operations = Array.from(new Set(data.coverageHeatmap.map((item) => item.operation)))
      .sort((a, b) => a.localeCompare(b));
    return [
      { value: 'all', label: 'All operations' },
      ...operations.map((operation) => ({ value: operation, label: operation })),
    ];
  }, [data.coverageHeatmap]);

  const filteredHeatmap = useMemo(
    () => selectedOperation === 'all'
      ? data.coverageHeatmap
      : data.coverageHeatmap.filter((item) => item.operation === selectedOperation),
    [data.coverageHeatmap, selectedOperation],
  );

  const filteredRiskOperations = useMemo(
    () => selectedOperation === 'all'
      ? data.topRiskOperations
      : data.topRiskOperations.filter((item) => item.operation === selectedOperation),
    [data.topRiskOperations, selectedOperation],
  );

  const filteredHeadcount = useMemo(
    () => selectedOperation === 'all'
      ? data.headcountByOperation
      : data.headcountByOperation.filter((item) => item.operation === selectedOperation),
    [data.headcountByOperation, selectedOperation],
  );

  const displayedExpectedVsActual = useMemo(() => {
    if (selectedOperation === 'all') return data.expectedVsActual;
    return filteredHeatmap.map((item) => ({
      day: item.day,
      date: item.date,
      expectedCoverage: item.expectedCoverage,
      coverage: item.coverage,
      totalAgents: 0,
    }));
  }, [data.expectedVsActual, filteredHeatmap, selectedOperation]);

  const filteredRiskDays = useMemo(() => {
    if (selectedOperation === 'all') return data.kpis.riskDays;
    return new Set(filteredHeatmap
      .filter((item) => item.statusColor.toLowerCase() === 'red')
      .map((item) => item.date)).size;
  }, [data.kpis.riskDays, filteredHeatmap, selectedOperation]);

  const filteredAverageCoverage = useMemo(() => {
    if (selectedOperation === 'all') return data.kpis.averageWeeklyCoverage;
    if (!filteredHeatmap.length) return 0;
    return Math.round((filteredHeatmap.reduce((sum, item) => sum + item.coverage, 0) / filteredHeatmap.length) * 10) / 10;
  }, [data.kpis.averageWeeklyCoverage, filteredHeatmap, selectedOperation]);

  const selectedHeadcountTotal = useMemo(() => (
    filteredHeadcount.reduce((sum, item) => sum + item.active, 0)
  ), [filteredHeadcount]);

  const coverageActions = useMemo(() => {
    const actions: Array<{ title: string; detail: string; tone: 'red' | 'amber' | 'blue' | 'green' }> = [];
    const critical = filteredRiskOperations.find((item) => item.riskDays >= 3);
    const watch = filteredRiskOperations.find((item) => item.riskDays > 0 && item.riskDays < 3);

    if (critical) {
      actions.push({
        title: `Review ${critical.operation} staffing`,
        detail: `${critical.operation} has ${critical.riskDays} risk days and ${pct(critical.averageCoverage)} average coverage.`,
        tone: 'red',
      });
    } else if (watch) {
      actions.push({
        title: `Monitor ${watch.operation} coverage`,
        detail: `${watch.operation} is in watch range with ${watch.riskDays} risk day(s).`,
        tone: 'amber',
      });
    }

    if (data.kpis.pendingPtoRequests > 0) {
      actions.push({
        title: 'Resolve pending PTO queue',
        detail: `${data.kpis.pendingPtoRequests} pending request(s) should be reviewed with coverage preview.`,
        tone: 'amber',
      });
    }

    actions.push({
      title: 'Export weekly snapshot',
      detail: 'Share this reporting pack before weekly staffing and PTO planning.',
      tone: 'blue',
    });

    if (!filteredRiskDays && data.kpis.pendingPtoRequests === 0) {
      actions.unshift({
        title: 'Coverage is stable',
        detail: 'No immediate coverage risk detected for the selected scope.',
        tone: 'green',
      });
    }

    return actions.slice(0, 3);
  }, [data.kpis.pendingPtoRequests, filteredRiskDays, filteredRiskOperations]);

  const heatmapOption = useMemo<EChartsOption>(() => {
    const days = Array.from(new Set(filteredHeatmap.map((item) => item.date)));
    const dayLabels = days.map(dateAxisLabel);
    const operations = Array.from(new Set(filteredHeatmap.map((item) => item.operation)));
    const points = filteredHeatmap.map((item) => [
      days.indexOf(item.date),
      operations.indexOf(item.operation),
      item.coverage,
      item.expectedCoverage,
      item.statusColor,
      item.day,
    ]);

    return {
      ...chartBase(),
      tooltip: {
        ...chartBase().tooltip,
        formatter: (params) => {
          const raw = Array.isArray((params as { data?: unknown[] }).data) ? (params as { data: unknown[] }).data : [];
          const day = dayLabels[Number(raw[0])];
          const operation = operations[Number(raw[1])];
          return `${operation}<br/>${day}: <strong>${pct(Number(raw[2]))}</strong><br/>Expected: ${raw[3]}%`;
        },
      },
      visualMap: {
        min: 0,
        max: 100,
        show: false,
        inRange: { color: ['#f4c7c3', '#f8e7a3', '#b9e7bf'] },
      },
      xAxis: { type: 'category', data: dayLabels, axisTick: { show: false }, axisLine: { show: false }, axisLabel: { interval: 0, rotate: dayLabels.length > 10 ? 35 : 0 } },
      yAxis: { type: 'category', data: operations, axisTick: { show: false }, axisLine: { show: false } },
      series: [{
        type: 'heatmap',
        data: points,
        label: {
          show: true,
          formatter: (params) => pct(Number((params as { value?: unknown[] }).value?.[2] ?? 0)),
          color: '#1f2a3d',
          fontWeight: 700,
        },
        itemStyle: {
          borderColor: '#ffffff',
          borderWidth: 4,
          borderRadius: 6,
        },
      }],
    };
  }, [filteredHeatmap]);

  const expectedOption = useMemo<EChartsOption>(() => {
    const labels = displayedExpectedVsActual.map((item) => dateAxisLabel(item.date));
    return {
      ...chartBase(),
      tooltip: { ...chartBase().tooltip, trigger: 'axis' },
      legend: { top: 0, right: 0, data: ['Actual', 'Expected'] },
      xAxis: { type: 'category', data: labels, axisTick: { show: false }, axisLine: { lineStyle: { color: '#d6e3f1' } } },
      yAxis: { type: 'value', max: 100, axisLabel: { formatter: '{value}%' }, splitLine: { lineStyle: { color: '#edf3f9' } } },
      series: [
        {
          name: 'Actual',
          type: 'bar',
          data: displayedExpectedVsActual.map((item) => item.coverage),
          barWidth: 24,
          itemStyle: {
            borderRadius: [6, 6, 0, 0],
            color: '#25a9e0',
          },
        },
        {
          name: 'Expected',
          type: 'line',
          data: displayedExpectedVsActual.map((item) => item.expectedCoverage),
          smooth: true,
          symbolSize: 8,
          lineStyle: { width: 3, color: '#32425d' },
          itemStyle: { color: '#32425d' },
        },
      ],
    };
  }, [displayedExpectedVsActual]);

  const trendOption = useMemo<EChartsOption>(() => ({
    ...chartBase(),
    tooltip: { ...chartBase().tooltip, trigger: 'axis' },
    xAxis: {
      type: 'category',
      data: data.coverageTrend.map((item) => shortDate(item.weekStart)),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d6e3f1' } },
    },
    yAxis: { type: 'value', max: 100, axisLabel: { formatter: '{value}%' }, splitLine: { lineStyle: { color: '#edf3f9' } } },
    series: [{
      name: 'Coverage',
      type: 'line',
      data: data.coverageTrend.map((item) => item.averageCoverage),
      smooth: true,
      areaStyle: { color: 'rgba(37, 169, 224, 0.16)' },
      lineStyle: { width: 3, color: '#317eb5' },
      itemStyle: { color: '#317eb5' },
    }],
  }), [data.coverageTrend]);

  const ptoStatusOption = useMemo<EChartsOption>(() => ({
    ...chartBase(),
    legend: { bottom: 0, left: 'center' },
    series: [{
      type: 'pie',
      radius: ['48%', '72%'],
      center: ['50%', '45%'],
      data: data.ptoByStatus.map((item) => ({ name: item.label, value: item.value })),
      label: { formatter: '{b}: {c}' },
      itemStyle: { borderColor: '#ffffff', borderWidth: 4 },
    }],
  }), [data.ptoByStatus]);

  const ptoTypeOption = useMemo<EChartsOption>(() => ({
    ...chartBase(),
    xAxis: {
      type: 'category',
      data: data.ptoByType.map((item) => item.label),
      axisLabel: { interval: 0, rotate: data.ptoByType.length > 4 ? 24 : 0 },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d6e3f1' } },
    },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#edf3f9' } } },
    series: [{
      type: 'bar',
      data: data.ptoByType.map((item) => item.value),
      barWidth: 26,
      itemStyle: { borderRadius: [6, 6, 0, 0], color: '#317eb5' },
    }],
  }), [data.ptoByType]);

  const headcountOption = useMemo<EChartsOption>(() => ({
    ...chartBase(),
    tooltip: { ...chartBase().tooltip, trigger: 'axis' },
    legend: { top: 0, right: 0, data: ['Active', 'Inactive'] },
    xAxis: {
      type: 'category',
      data: filteredHeadcount.map((item) => item.operation),
      axisLabel: { interval: 0, rotate: filteredHeadcount.length > 5 ? 20 : 0 },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d6e3f1' } },
    },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#edf3f9' } } },
    series: [
      {
        name: 'Active',
        type: 'bar',
        stack: 'headcount',
        data: filteredHeadcount.map((item) => item.active),
        itemStyle: { color: '#317eb5' },
      },
      {
        name: 'Inactive',
        type: 'bar',
        stack: 'headcount',
        data: filteredHeadcount.map((item) => item.inactive),
        itemStyle: { color: '#bac7d6' },
      },
    ],
  }), [filteredHeadcount]);

  const companyOptions = data.availableCompanies.map((company) => ({ value: company, label: company }));
  const hasMultipleCompanies = data.availableCompanies.length > 1;
  const periodOptions = [
    { value: 'current-week', label: 'Current week' },
    { value: 'previous-week', label: 'Previous week' },
    { value: 'month', label: '1 month' },
    { value: 'custom', label: 'Custom' },
  ];

  const rangeLabel = data.weekStart && data.weekEnd
    ? `${shortDate(data.weekStart)} - ${shortDate(data.weekEnd)}`
    : 'Date range';

  const changePeriod = (nextPeriod: string) => {
    const period = nextPeriod as ReportPeriod;
    setSelectedPeriod(period);
    if (period !== 'custom') {
      loadReports(selectedCompany, period, customRange);
    }
  };

  const applyCustomRange = () => {
    if (!customRange.startDate || !customRange.endDate) {
      setError('Select a valid custom date range.');
      return;
    }
    if (customRange.endDate < customRange.startDate) {
      setError('Custom end date must be after the start date.');
      return;
    }
    loadReports(selectedCompany, 'custom', customRange);
  };

  const buildExportData = (): ReportsOverview => {
    if (selectedOperation === 'all') return data;
    return {
      ...data,
      kpis: {
        ...data.kpis,
        totalActiveEmployees: selectedHeadcountTotal,
        averageWeeklyCoverage: filteredAverageCoverage,
        riskDays: filteredRiskDays,
        operations: 1,
      },
      coverageHeatmap: filteredHeatmap,
      expectedVsActual: displayedExpectedVsActual,
      headcountByOperation: filteredHeadcount,
      topRiskOperations: filteredRiskOperations,
    };
  };

  const exportExcel = async () => {
    setExporting(true);
    setError(null);
    try {
      const { exportReportsWorkbook } = await import('../lib/reportExcelExport');
      exportReportsWorkbook(buildExportData());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to export Excel report.');
    } finally {
      setExporting(false);
    }
  };

  return (
    <section className="reports-page">
      <div className="reports-hero">
        <div>
          <p className="reports-eyebrow"><ChartBar size={15} weight="bold" /> Reporting dashboard</p>
          <h1>Company performance</h1>
          <p className="reports-subtitle">
            Coverage, workforce and PTO visibility across your selected operating scope.
          </p>
        </div>
        <div className="reports-toolbar-actions">
          <Button type="button" variant="ghost" onClick={() => loadReports(selectedCompany, selectedPeriod, customRange)}>
            <ArrowsClockwise size={16} weight="bold" />
            Refresh
          </Button>
          <Button type="button" variant="primary" disabled={loading || exporting || !data.selectedCompany} onClick={exportExcel}>
            <DownloadSimple size={16} weight="bold" />
            {exporting ? 'Exporting...' : 'Export Excel'}
          </Button>
        </div>
      </div>

      <div className="reports-filter-bar">
        <div className="reports-filter-copy">
          <FunnelSimple size={18} weight="bold" />
          <div>
            <strong>Global filters</strong>
            <span>Apply the same scope to every metric and chart available in this report.</span>
          </div>
        </div>
        <div className="reports-filter-controls">
          {hasMultipleCompanies && (
            <Select
              className="reports-company-select"
              value={selectedCompany}
              options={companyOptions}
              searchable
              searchPlaceholder="Search company"
              ariaLabel="Reports company"
              onChange={(nextCompany) => {
                setSelectedCompany(nextCompany);
                loadReports(nextCompany, selectedPeriod, customRange);
              }}
            />
          )}
          {!hasMultipleCompanies && data.selectedCompany && (
            <div className="reports-company-chip">{data.selectedCompany}</div>
          )}
          <Select
            className="reports-company-select"
            value={selectedOperation}
            options={operationOptions}
            searchable
            searchPlaceholder="Search operation"
            ariaLabel="Reports operation"
            onChange={setSelectedOperation}
          />
          <Select
            className="reports-period-select"
            value={selectedPeriod}
            options={periodOptions}
            ariaLabel="Reports date period"
            onChange={changePeriod}
          />
          <div className="reports-period-chip" title={rangeLabel}>
            <CalendarCheck size={16} weight="bold" />
            {rangeLabel}
          </div>
        </div>
      </div>

      {selectedPeriod === 'custom' && (
        <div className="reports-custom-range">
          <label>
            <span>Start date</span>
            <input
              type="date"
              value={customRange.startDate}
              onChange={(event) => setCustomRange((range) => ({ ...range, startDate: event.target.value }))}
            />
          </label>
          <label>
            <span>End date</span>
            <input
              type="date"
              value={customRange.endDate}
              onChange={(event) => setCustomRange((range) => ({ ...range, endDate: event.target.value }))}
            />
          </label>
          <Button type="button" variant="primary" onClick={applyCustomRange} disabled={loading}>
            Apply range
          </Button>
        </div>
      )}

      {loading && <div className="reports-loading">Loading reports...</div>}
      {error && <div className="alert">{error}</div>}

      {!loading && !error && (
        <>
          <div className="reports-kpi-grid">
            <KpiCard label="Total active employees" value={selectedHeadcountTotal} note="Across selected scope" icon={UsersThree} />
            <KpiCard label="Average coverage" value={pct(filteredAverageCoverage)} note="Current week coverage" icon={ChartBar} tone={filteredAverageCoverage >= 85 ? 'green' : 'amber'} />
            <KpiCard label="Days at risk" value={filteredRiskDays} note={filteredRiskDays > 0 ? 'Require operational review' : 'No red days detected'} icon={WarningCircle} tone={filteredRiskDays > 0 ? 'red' : 'green'} />
            <KpiCard label="Pending PTO" value={data.kpis.pendingPtoRequests} note="Awaiting admin decision" icon={Clock} tone={data.kpis.pendingPtoRequests > 0 ? 'purple' : 'green'} />
            <KpiCard label="Operations" value={selectedOperation === 'all' ? data.kpis.operations : 1} note={selectedOperation === 'all' ? 'Across company scope' : selectedOperation} icon={Buildings} tone="green" />
          </div>

          <div className="reports-layout">
            <ReportPanel className="reports-panel-heatmap" title="Coverage heatmap" subtitle="Weekly coverage by operation and day.">
              {filteredHeatmap.length ? <ReactEChartsCore echarts={echarts} option={heatmapOption} className="reports-chart tall" /> : <ReportsEmpty />}
            </ReportPanel>

            <ReportPanel className="reports-panel-risk" title="Top risk operations" subtitle="Prioritized by lowest coverage.">
              <div className="reports-risk-list">
                {filteredRiskOperations.length ? filteredRiskOperations.map((item) => {
                  const color = item.riskDays > 0 ? riskColorMap.red : riskColorMap.green;
                  const label = item.riskDays >= 3 ? 'Critical' : item.riskDays > 0 ? 'At risk' : 'Stable';
                  return (
                    <div className="reports-risk-row" key={item.operation}>
                      <div>
                        <strong>{item.operation}</strong>
                        <span>{pct(item.averageCoverage)} average</span>
                      </div>
                      <div className="reports-risk-right">
                        <b style={{ color }}>{item.riskDays} risk days</b>
                        <em className={`reports-risk-badge ${item.riskDays >= 3 ? 'critical' : item.riskDays > 0 ? 'watch' : 'stable'}`}>{label}</em>
                      </div>
                    </div>
                  );
                }) : <ReportsEmpty />}
              </div>
            </ReportPanel>

            <ReportPanel title="Expected vs actual coverage" subtitle="Selected range by day.">
              {displayedExpectedVsActual.length ? <ReactEChartsCore echarts={echarts} option={expectedOption} className="reports-chart" /> : <ReportsEmpty />}
            </ReportPanel>

            <ReportPanel title="Coverage trend" subtitle="Last 12 weeks.">
              {data.coverageTrend.length ? <ReactEChartsCore echarts={echarts} option={trendOption} className="reports-chart" /> : <ReportsEmpty />}
            </ReportPanel>

            <ReportPanel title="PTO by status" subtitle="Requests active, updated, reviewed or pending in the selected range.">
              {data.ptoByStatus.length ? <ReactEChartsCore echarts={echarts} option={ptoStatusOption} className="reports-chart" /> : <ReportsEmpty label="No PTO requests found." />}
            </ReportPanel>

            <ReportPanel title="PTO by type" subtitle="Absence mix for relevant requests in the selected range.">
              {data.ptoByType.length ? <ReactEChartsCore echarts={echarts} option={ptoTypeOption} className="reports-chart" /> : <ReportsEmpty label="No PTO requests found." />}
            </ReportPanel>

            <ReportPanel className="reports-panel-workforce" title="Headcount by operation" subtitle="Active and inactive members.">
              {filteredHeadcount.length ? <ReactEChartsCore echarts={echarts} option={headcountOption} className="reports-chart" /> : <ReportsEmpty />}
            </ReportPanel>

            <ReportPanel title="Coverage actions" subtitle="Recommended operational follow-ups.">
              <div className="reports-actions-list">
                {coverageActions.map((item) => (
                  <div className={`reports-action-card ${item.tone}`} key={item.title}>
                    <div className="reports-action-icon"><Lightning size={18} weight="bold" /></div>
                    <div>
                      <strong>{item.title}</strong>
                      <span>{item.detail}</span>
                    </div>
                  </div>
                ))}
              </div>
            </ReportPanel>
          </div>
        </>
      )}
    </section>
  );
}
