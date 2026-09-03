export type ApiError = {
  message?: string;
  requirePasswordChange?: boolean;
  email?: string;
  displayName?: string;
  role?: number;
  token?: string;
  permissions?: string[];
  isSystemHidden?: boolean;
  company?: string;
  companies?: string[];
};

export type UserInfo = {
  email: string;
  displayName: string;
  role: number;
  permissions: string[];
  token: string;
  isSystemHidden?: boolean;
  company?: string;
  companies?: string[];
};

export type Employee = {
  id: string;
  displayName: string;
  email: string;
  role: number;
  isSystemHidden?: boolean;
  location: string;
  company: string;
  companies?: string[];
  operation: string;
  shiftTime: string;
  schedulePeriods?: SchedulePeriod[];
};

export type CreateEmployeeForm = {
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  location: string;
  company: string;
  companies: string[];
  operation: string;
  isSystemHidden?: boolean;
  appearsInSchedule?: boolean;
};

export type CompanyCatalogItem = {
  name: string;
  isActive: boolean;
};

export type CompanyOperationItem = {
  companyName: string;
  name: string;
  isActive: boolean;
};

export type CoverageRule = {
  companyName: string;
  operationName?: string | null;
  dayOfWeek: string;
  expectedCoverage: number;
  greenThreshold: number;
  yellowThreshold: number;
  calculationScope: 'operation' | 'company' | string;
  isActive: boolean;
  updatedBy: string;
  updatedAtUtc: string;
};

export type ReportMetricPoint = {
  label: string;
  value: number;
};

export type ReportCoverageHeatmapPoint = {
  operation: string;
  day: string;
  date: string;
  coverage: number;
  expectedCoverage: number;
  statusColor: 'green' | 'yellow' | 'red' | string;
};

export type ReportCoverageTrendPoint = {
  weekStart: string;
  averageCoverage: number;
  riskDays: number;
};

export type ReportCoverageDayPoint = {
  day: string;
  date: string;
  expectedCoverage: number;
  coverage: number;
  totalAgents: number;
};

export type ReportHeadcountPoint = {
  operation: string;
  active: number;
  inactive: number;
};

export type ReportOperationRiskPoint = {
  operation: string;
  riskDays: number;
  averageCoverage: number;
};

export type ReportsKpis = {
  totalActiveEmployees: number;
  averageWeeklyCoverage: number;
  riskDays: number;
  pendingPtoRequests: number;
  operations: number;
};

export type ReportsOverview = {
  selectedCompany: string;
  availableCompanies: string[];
  weekStart: string;
  weekEnd: string;
  kpis: ReportsKpis;
  coverageHeatmap: ReportCoverageHeatmapPoint[];
  coverageTrend: ReportCoverageTrendPoint[];
  expectedVsActual: ReportCoverageDayPoint[];
  ptoByStatus: ReportMetricPoint[];
  ptoByType: ReportMetricPoint[];
  headcountByOperation: ReportHeadcountPoint[];
  topRiskOperations: ReportOperationRiskPoint[];
};

export type ScheduleBlock = {
  start: string;
  end: string;
  days: string[];
};

export type SchedulePeriod = {
  effectiveFrom: string;
  effectiveTo?: string | null;
  shiftTime: string;
  scheduleBlocks: ScheduleBlock[];
  isRepeating?: boolean;
};

export type ScheduleEvent = {
  id: string;
  employeeId: string;
  employeeEmail: string;
  action: 'created' | 'updated' | 'deleted' | string;
  updatedByUserId: string;
  updatedByEmail: string;
  updatedByName: string;
  updatedByRole: number;
  occurredAtUtc: string;
  payloadJson: string;
};

export type BulkUserUploadError = {
  row: number;
  column: string;
  email: string;
  message: string;
};

export type BulkUserUploadResponse = {
  message: string;
  created: number;
  updated: number;
  rowsProcessed: number;
  errors?: BulkUserUploadError[];
};

export type PtoRequest = {
  id: string;
  userId: string;
  userEmail: string;
  userDisplayName: string;
  requestType: string;
  numberOfDays: number;
  startDate: string;
  endDate: string;
  comments?: string | null;
  reviewComments?: string | null;
  status: string;
  requestedByEmail: string;
  requestedByName: string;
  requestedByRole: number;
  reviewedByEmail?: string | null;
  reviewedByName?: string | null;
  reviewedByRole?: number | null;
  reviewedAtUtc?: string | null;
  createdAtUtc: string;
};

export type RequestExportJob = {
  id: string;
  status: 'pending' | 'queued' | 'processing' | 'completed' | 'failed' | string;
  fileName?: string | null;
  errorMessage?: string | null;
  createdAtUtc: string;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  downloadUrl?: string | null;
};

export type CoverageImpactWarning = {
  date: string;
  requiredAgents: number;
  currentWorkingAgents: number;
  projectedWorkingAgents: number;
  message: string;
};

export type PtoCoveragePreview = {
  hasImpact: boolean;
  warnings: CoverageImpactWarning[];
};

export type SwapCandidate = {
  id: string;
  displayName: string;
  email: string;
  shiftTime: string;
  shiftLabel: string;
};

export type SwapRequest = {
  id: string;
  requestedByUserId: string;
  requestedByEmail: string;
  requestedByDisplayName: string;
  requestedByRole: number;
  targetUserId: string;
  targetUserEmail: string;
  targetUserDisplayName: string;
  targetUserRole: number;
  swapDate: string;
  requestedDates: string[];
  targetDates: string[];
  appliedGroupId?: string | null;
  pairs: SwapPair[];
  requestType: string;
  comments?: string | null;
  reviewComments?: string | null;
  weeklyHours: Array<{
    weekStart: string;
    requesterHours: number;
    targetHours: number;
    limitHours: number;
  }>;
  exceedsWeeklyHoursLimit: boolean;
  status: string;
  reviewedByEmail?: string | null;
  reviewedByName?: string | null;
  reviewedByRole?: number | null;
  reviewedAtUtc?: string | null;
  createdAtUtc: string;
};

export type SwapScheduleEntry = {
  date: string;
  label: string;
  shiftTime: string;
  durationHours: number;
  type: string;
};

export type SwapPair = {
  requesterCurrent: SwapScheduleEntry;
  targetCurrent: SwapScheduleEntry;
  requesterResult: SwapScheduleEntry;
  targetResult: SwapScheduleEntry;
};
