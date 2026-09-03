import type {
  ReportCoverageHeatmapPoint,
  ReportMetricPoint,
  ReportsOverview,
} from '../types';

type CellValue = string | number;

type Cell = {
  value: CellValue;
  type?: 'String' | 'Number';
  style?: string;
  mergeAcross?: number;
};

function xmlEscape(value: CellValue) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

function pct(value: number) {
  return `${Number.isFinite(value) ? value.toFixed(1).replace('.0', '') : '0'}%`;
}

function shortDate(value: string) {
  if (!value) return '';
  const [year, month, day] = value.split('-');
  return year && month && day ? `${month}/${day}/${year}` : value;
}

function dateColumnLabel(value: string) {
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) return value;
  return `${shortDate(value)} ${date.toLocaleDateString(undefined, { weekday: 'short' })}`;
}

function safeFilePart(value: string) {
  return value
    .trim()
    .replace(/[^a-z0-9]+/gi, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase() || 'company';
}

function sheetName(value: string) {
  return value.replace(/[\[\]:*?/\\]/g, '').slice(0, 31);
}

function statusStyle(status: string) {
  if (status.toLowerCase() === 'green') return 'GoodPct';
  if (status.toLowerCase() === 'yellow') return 'WarnPct';
  if (status.toLowerCase() === 'red') return 'RiskPct';
  return 'Data';
}

function priorityForRisk(riskDays: number) {
  if (riskDays >= 3) return 'Critical';
  if (riskDays >= 1) return 'Watch';
  return 'Stable';
}

function styleForPriority(riskDays: number) {
  if (riskDays >= 3) return 'RiskText';
  if (riskDays >= 1) return 'WarnText';
  return 'GoodText';
}

function cell({ value, type, style, mergeAcross }: Cell) {
  const resolvedType = type ?? (typeof value === 'number' ? 'Number' : 'String');
  const styleAttr = style ? ` ss:StyleID="${style}"` : '';
  const mergeAttr = mergeAcross ? ` ss:MergeAcross="${mergeAcross}"` : '';
  return `<Cell${styleAttr}${mergeAttr}><Data ss:Type="${resolvedType}">${xmlEscape(value)}</Data></Cell>`;
}

function row(cells: Cell[], height?: number) {
  const heightAttr = height ? ` ss:Height="${height}"` : '';
  return `<Row${heightAttr}>${cells.map(cell).join('')}</Row>`;
}

function blankRow() {
  return '<Row/>';
}

function columns(count: number, widths: number[] = []) {
  return Array.from({ length: count }, (_, index) => {
    const width = widths[index] ?? 110;
    return `<Column ss:Width="${width}"/>`;
  }).join('');
}

function worksheet(name: string, rows: string[], columnCount: number, widths?: number[]) {
  return `
    <Worksheet ss:Name="${xmlEscape(sheetName(name))}">
      <Table>
        ${columns(columnCount, widths)}
        ${rows.join('')}
      </Table>
      <WorksheetOptions xmlns="urn:schemas-microsoft-com:office:excel">
        <FreezePanes/>
        <FrozenNoSplit/>
        <SplitHorizontal>3</SplitHorizontal>
        <TopRowBottomPane>3</TopRowBottomPane>
        <ProtectObjects>False</ProtectObjects>
        <ProtectScenarios>False</ProtectScenarios>
      </WorksheetOptions>
    </Worksheet>`;
}

function titleRows(report: ReportsOverview, title: string, columnsCount: number) {
  return [
    row([{ value: 'ShiftTrack', style: 'Brand', mergeAcross: columnsCount - 1 }], 28),
    row([{ value: title, style: 'Title', mergeAcross: columnsCount - 1 }], 30),
    row([{ value: `${report.selectedCompany} | Week ${shortDate(report.weekStart)} - ${shortDate(report.weekEnd)} | Exported ${new Date().toLocaleString()}`, style: 'Subtitle', mergeAcross: columnsCount - 1 }], 22),
    blankRow(),
  ];
}

function sectionHeader(label: string, columnsCount: number) {
  return row([{ value: label, style: 'Section', mergeAcross: columnsCount - 1 }], 22);
}

function tableHeader(labels: string[]) {
  return row(labels.map((label) => ({ value: label, style: 'TableHeader' })));
}

function metricTotal(points: ReportMetricPoint[]) {
  return points.reduce((sum, item) => sum + item.value, 0);
}

function buildExecutiveSummary(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Executive Reporting Pack', 6),
    sectionHeader('Executive KPIs', 6),
    row([
      { value: 'Active employees', style: 'KpiLabel' },
      { value: report.kpis.totalActiveEmployees, style: 'KpiValue' },
      { value: 'Average weekly coverage', style: 'KpiLabel' },
      { value: pct(report.kpis.averageWeeklyCoverage), style: 'KpiValue' },
      { value: 'Days at risk', style: 'KpiLabel' },
      { value: report.kpis.riskDays, style: report.kpis.riskDays > 0 ? 'KpiRisk' : 'KpiValue' },
    ], 28),
    row([
      { value: 'Pending PTO', style: 'KpiLabel' },
      { value: report.kpis.pendingPtoRequests, style: 'KpiValue' },
      { value: 'Operations', style: 'KpiLabel' },
      { value: report.kpis.operations, style: 'KpiValue' },
      { value: 'Visible companies', style: 'KpiLabel' },
      { value: report.availableCompanies.length, style: 'KpiValue' },
    ], 28),
    blankRow(),
    sectionHeader('Management Notes', 6),
    tableHeader(['Signal', 'Interpretation', 'Recommended action', 'Priority', 'Owner', 'Time horizon']),
    row([
      { value: 'Coverage health', style: 'DataStrong' },
      { value: `${pct(report.kpis.averageWeeklyCoverage)} average coverage this week.`, style: 'Data' },
      { value: report.kpis.riskDays > 0 ? 'Review staffing, PTO approvals and weekend coverage rules.' : 'Maintain current staffing rhythm.', style: 'Data' },
      { value: report.kpis.riskDays > 0 ? 'High' : 'Normal', style: report.kpis.riskDays > 0 ? 'RiskText' : 'GoodText' },
      { value: 'Operations', style: 'Data' },
      { value: 'This week', style: 'Data' },
    ]),
    row([
      { value: 'PTO queue', style: 'DataStrong' },
      { value: `${report.kpis.pendingPtoRequests} pending request(s).`, style: 'Data' },
      { value: report.kpis.pendingPtoRequests > 0 ? 'Approve or deny pending requests with coverage preview.' : 'No immediate PTO backlog.', style: 'Data' },
      { value: report.kpis.pendingPtoRequests > 0 ? 'Medium' : 'Normal', style: report.kpis.pendingPtoRequests > 0 ? 'WarnText' : 'GoodText' },
      { value: 'Admin', style: 'Data' },
      { value: '24-48 hours', style: 'Data' },
    ]),
    row([
      { value: 'Operational scope', style: 'DataStrong' },
      { value: `${report.kpis.operations} operation(s) included in this report.`, style: 'Data' },
      { value: 'Use the Coverage Matrix sheet to inspect day-by-day risk.', style: 'Data' },
      { value: 'Normal', style: 'Data' },
      { value: 'Leadership', style: 'Data' },
      { value: 'Weekly', style: 'Data' },
    ]),
  ];

  return worksheet('Executive Summary', rows, 6, [145, 135, 165, 135, 145, 130]);
}

function buildCoverageMatrix(report: ReportsOverview) {
  const operations = Array.from(new Set(report.coverageHeatmap.map((item) => item.operation)))
    .sort((a, b) => a.localeCompare(b));
  const dates = Array.from(new Set(report.coverageHeatmap.map((item) => item.date)))
    .sort((a, b) => a.localeCompare(b));
  const byOperationDay = new Map<string, ReportCoverageHeatmapPoint>();
  report.coverageHeatmap.forEach((item) => byOperationDay.set(`${item.operation}|${item.date}`, item));

  const rows = [
    ...titleRows(report, 'Coverage Matrix', dates.length + 1),
    sectionHeader('Coverage by Operation and Day', dates.length + 1),
    tableHeader(['Operation', ...dates.map(dateColumnLabel)]),
    ...operations.map((operation) => row([
      { value: operation, style: 'DataStrong' },
      ...dates.map((date) => {
        const item = byOperationDay.get(`${operation}|${date}`);
        return {
          value: item ? `${pct(item.coverage)} / Exp ${item.expectedCoverage}%` : '-',
          style: item ? statusStyle(item.statusColor) : 'Data',
        };
      }),
    ])),
    blankRow(),
    row([{ value: 'Legend: green meets target, yellow is watch zone, red is below threshold.', style: 'Subtitle', mergeAcross: dates.length }]),
  ];

  return worksheet('Coverage Matrix', rows, dates.length + 1, [170, ...dates.map(() => 125)]);
}

function buildExpectedActual(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Expected vs Actual Coverage', 7),
    tableHeader(['Day', 'Date', 'Expected %', 'Actual %', 'Variance', 'Working agents', 'Status']),
    ...report.expectedVsActual.map((item) => {
      const variance = item.coverage - item.expectedCoverage;
      const style = variance >= 0 ? 'GoodText' : variance >= -5 ? 'WarnText' : 'RiskText';
      return row([
        { value: item.day, style: 'DataStrong' },
        { value: shortDate(item.date), style: 'Data' },
        { value: pct(item.expectedCoverage), style: 'Data' },
        { value: pct(item.coverage), style: 'Data' },
        { value: pct(variance), style },
        { value: item.totalAgents, style: 'Data' },
        { value: variance >= 0 ? 'On target' : 'Below target', style },
      ]);
    }),
  ];

  return worksheet('Expected vs Actual', rows, 7, [90, 120, 115, 105, 95, 125, 120]);
}

function buildTrend(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Coverage Trend', 5),
    tableHeader(['Week start', 'Average coverage', 'Risk days', 'Trend status', 'Executive readout']),
    ...report.coverageTrend.map((item) => row([
      { value: shortDate(item.weekStart), style: 'DataStrong' },
      { value: pct(item.averageCoverage), style: item.riskDays > 0 ? 'WarnText' : 'GoodText' },
      { value: item.riskDays, style: item.riskDays > 0 ? 'RiskText' : 'Data' },
      { value: item.riskDays > 0 ? 'Risk present' : 'Stable', style: item.riskDays > 0 ? 'RiskText' : 'GoodText' },
      { value: item.riskDays > 0 ? 'Inspect staffing and PTO overlap for this week.' : 'Coverage stayed within acceptable range.', style: 'Data' },
    ])),
  ];

  return worksheet('Coverage Trend', rows, 5, [120, 140, 90, 130, 270]);
}

function buildPto(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'PTO Analysis', 5),
    sectionHeader('PTO by Status', 5),
    tableHeader(['Status', 'Requests', 'Share', 'Operational note', 'Action']),
    ...report.ptoByStatus.map((item) => {
      const total = Math.max(1, metricTotal(report.ptoByStatus));
      return row([
        { value: item.label, style: 'DataStrong' },
        { value: item.value, style: 'Data' },
        { value: pct((item.value / total) * 100), style: 'Data' },
        { value: item.label.toLowerCase() === 'pending' ? 'Pending approval queue.' : 'Historical request state.', style: 'Data' },
        { value: item.label.toLowerCase() === 'pending' ? 'Review with coverage preview.' : 'Monitor trend.', style: 'Data' },
      ]);
    }),
    blankRow(),
    sectionHeader('PTO by Type', 5),
    tableHeader(['Type', 'Requests', 'Share', 'Operational note', 'Action']),
    ...report.ptoByType.map((item) => {
      const total = Math.max(1, metricTotal(report.ptoByType));
      return row([
        { value: item.label, style: 'DataStrong' },
        { value: item.value, style: 'Data' },
        { value: pct((item.value / total) * 100), style: 'Data' },
        { value: 'Absence category impact.', style: 'Data' },
        { value: 'Track against coverage matrix.', style: 'Data' },
      ]);
    }),
  ];

  return worksheet('PTO Analysis', rows, 5, [145, 90, 90, 210, 210]);
}

function buildWorkforce(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Workforce Composition', 6),
    tableHeader(['Operation', 'Active', 'Inactive', 'Total profiles', 'Inactive share', 'Capacity readout']),
    ...report.headcountByOperation.map((item) => {
      const total = item.active + item.inactive;
      const inactiveShare = total === 0 ? 0 : (item.inactive / total) * 100;
      return row([
        { value: item.operation, style: 'DataStrong' },
        { value: item.active, style: 'GoodText' },
        { value: item.inactive, style: item.inactive > 0 ? 'WarnText' : 'Data' },
        { value: total, style: 'Data' },
        { value: pct(inactiveShare), style: inactiveShare > 20 ? 'WarnText' : 'Data' },
        { value: inactiveShare > 20 ? 'Review attrition and backfill timing.' : 'Capacity mix is within normal range.', style: 'Data' },
      ]);
    }),
  ];

  return worksheet('Workforce', rows, 6, [170, 90, 90, 120, 120, 260]);
}

function buildRisk(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Risk Operations', 6),
    tableHeader(['Rank', 'Operation', 'Average coverage', 'Risk days', 'Priority', 'Recommended action']),
    ...report.topRiskOperations.map((item, index) => row([
      { value: index + 1, style: 'Data' },
      { value: item.operation, style: 'DataStrong' },
      { value: pct(item.averageCoverage), style: item.riskDays > 0 ? 'WarnText' : 'GoodText' },
      { value: item.riskDays, style: item.riskDays > 0 ? 'RiskText' : 'Data' },
      { value: priorityForRisk(item.riskDays), style: styleForPriority(item.riskDays) },
      { value: item.riskDays > 0 ? 'Rebalance schedule, review PTO, or adjust staffing threshold.' : 'Maintain operating model.', style: 'Data' },
    ])),
  ];

  return worksheet('Risk Operations', rows, 6, [70, 170, 130, 90, 110, 310]);
}

function buildRawData(report: ReportsOverview) {
  const rows = [
    ...titleRows(report, 'Raw Export Data', 7),
    sectionHeader('Coverage Heatmap Data', 7),
    tableHeader(['Dataset', 'Operation', 'Day', 'Date', 'Coverage %', 'Expected %', 'Status']),
    ...report.coverageHeatmap.map((item) => row([
      { value: 'Coverage Heatmap', style: 'Data' },
      { value: item.operation, style: 'Data' },
      { value: item.day, style: 'Data' },
      { value: item.date, style: 'Data' },
      { value: item.coverage, style: 'Data' },
      { value: item.expectedCoverage, style: 'Data' },
      { value: item.statusColor, style: statusStyle(item.statusColor) },
    ])),
    blankRow(),
    sectionHeader('Expected vs Actual Data', 7),
    tableHeader(['Dataset', 'Day', 'Date', 'Expected %', 'Actual %', 'Working agents', '']),
    ...report.expectedVsActual.map((item) => row([
      { value: 'Expected vs Actual', style: 'Data' },
      { value: item.day, style: 'Data' },
      { value: item.date, style: 'Data' },
      { value: item.expectedCoverage, style: 'Data' },
      { value: item.coverage, style: 'Data' },
      { value: item.totalAgents, style: 'Data' },
      { value: '', style: 'Data' },
    ])),
  ];

  return worksheet('Raw Data', rows, 7, [155, 150, 90, 110, 105, 105, 110]);
}

function styles() {
  return `
    <Styles>
      <Style ss:ID="Default" ss:Name="Normal">
        <Alignment ss:Vertical="Center"/>
        <Font ss:FontName="Calibri" ss:Size="11" ss:Color="#24354D"/>
      </Style>
      <Style ss:ID="Brand">
        <Alignment ss:Vertical="Center"/>
        <Font ss:FontName="Calibri" ss:Size="16" ss:Bold="1" ss:Color="#FFFFFF"/>
        <Interior ss:Color="#32425D" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="Title">
        <Alignment ss:Vertical="Center"/>
        <Font ss:FontName="Calibri" ss:Size="18" ss:Bold="1" ss:Color="#1D2D44"/>
        <Interior ss:Color="#DCEEFF" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="Subtitle">
        <Alignment ss:Vertical="Center"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#5D6C81"/>
      </Style>
      <Style ss:ID="Section">
        <Alignment ss:Vertical="Center"/>
        <Font ss:FontName="Calibri" ss:Size="12" ss:Bold="1" ss:Color="#FFFFFF"/>
        <Interior ss:Color="#317EB5" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="TableHeader">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Bold="1" ss:Color="#FFFFFF"/>
        <Interior ss:Color="#32425D" ss:Pattern="Solid"/>
        <Borders>
          <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#FFFFFF"/>
        </Borders>
      </Style>
      <Style ss:ID="Data">
        <Alignment ss:Vertical="Center" ss:WrapText="1"/>
        <Interior ss:Color="#FFFFFF" ss:Pattern="Solid"/>
        <Borders>
          <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#DCE6F2"/>
        </Borders>
      </Style>
      <Style ss:ID="DataStrong">
        <Alignment ss:Vertical="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="11" ss:Bold="1" ss:Color="#1D2D44"/>
        <Interior ss:Color="#F8FBFF" ss:Pattern="Solid"/>
        <Borders>
          <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#DCE6F2"/>
        </Borders>
      </Style>
      <Style ss:ID="KpiLabel">
        <Alignment ss:Vertical="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Bold="1" ss:Color="#5D6C81"/>
        <Interior ss:Color="#EFF7FF" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="KpiValue">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center"/>
        <Font ss:FontName="Calibri" ss:Size="14" ss:Bold="1" ss:Color="#1D2D44"/>
        <Interior ss:Color="#EFF7FF" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="KpiRisk">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center"/>
        <Font ss:FontName="Calibri" ss:Size="14" ss:Bold="1" ss:Color="#B3261E"/>
        <Interior ss:Color="#FDEDEC" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="GoodPct">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Bold="1" ss:Color="#1F5E32"/>
        <Interior ss:Color="#DDF3E2" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="WarnPct">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Bold="1" ss:Color="#7A4C00"/>
        <Interior ss:Color="#FFF0C2" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="RiskPct">
        <Alignment ss:Vertical="Center" ss:Horizontal="Center" ss:WrapText="1"/>
        <Font ss:FontName="Calibri" ss:Size="10" ss:Bold="1" ss:Color="#8F1D18"/>
        <Interior ss:Color="#FAD7D4" ss:Pattern="Solid"/>
      </Style>
      <Style ss:ID="GoodText">
        <Font ss:FontName="Calibri" ss:Size="11" ss:Bold="1" ss:Color="#1F7A3B"/>
      </Style>
      <Style ss:ID="WarnText">
        <Font ss:FontName="Calibri" ss:Size="11" ss:Bold="1" ss:Color="#A06400"/>
      </Style>
      <Style ss:ID="RiskText">
        <Font ss:FontName="Calibri" ss:Size="11" ss:Bold="1" ss:Color="#B3261E"/>
      </Style>
    </Styles>`;
}

export function exportReportsWorkbook(report: ReportsOverview) {
  const workbook = `<?xml version="1.0" encoding="UTF-8"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook
  xmlns="urn:schemas-microsoft-com:office:spreadsheet"
  xmlns:o="urn:schemas-microsoft-com:office:office"
  xmlns:x="urn:schemas-microsoft-com:office:excel"
  xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
  <DocumentProperties xmlns="urn:schemas-microsoft-com:office:office">
    <Author>ShiftTrack</Author>
    <Company>ShiftTrack</Company>
    <Title>${xmlEscape(report.selectedCompany)} Reporting Pack</Title>
    <Created>${new Date().toISOString()}</Created>
  </DocumentProperties>
  ${styles()}
  ${buildExecutiveSummary(report)}
  ${buildCoverageMatrix(report)}
  ${buildExpectedActual(report)}
  ${buildTrend(report)}
  ${buildPto(report)}
  ${buildWorkforce(report)}
  ${buildRisk(report)}
  ${buildRawData(report)}
</Workbook>`;

  const blob = new Blob([workbook], {
    type: 'application/vnd.ms-excel;charset=utf-8;',
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  const exportedAt = new Date().toISOString().slice(0, 10);
  link.href = url;
  link.download = `shifttrack-${safeFilePart(report.selectedCompany)}-reporting-pack-${exportedAt}.xls`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
