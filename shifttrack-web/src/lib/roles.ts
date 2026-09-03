export const ROLE_EMPLOYEE = 0;
export const ROLE_MANAGER = 1;
export const ROLE_ADMIN = 2;
export const ROLE_TEAM_LEADER = 3;

export const isEmployeeRole = (role: number) => role === ROLE_EMPLOYEE;
export const isManagerRole = (role: number) => role === ROLE_MANAGER;
export const isAdminRole = (role: number) => role === ROLE_ADMIN;
export const isTeamLeaderRole = (role: number) => role === ROLE_TEAM_LEADER;
export const isEmployeeLikeRole = (role: number) => isEmployeeRole(role) || isTeamLeaderRole(role);

export const canViewCoverageForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role) || isTeamLeaderRole(role);

export const canManageUsersForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role);

export const canReviewPtoForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role);

export const canRequestForOthersForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role);

export const canExportCalendarForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role);

export const canViewLiveUpdatesForRole = (role: number) =>
  isManagerRole(role) || isAdminRole(role);

export const roleLabelForValue = (role: number) => {
  if (isAdminRole(role)) return 'Admin';
  if (isManagerRole(role)) return 'Manager';
  if (isTeamLeaderRole(role)) return 'Team Leader';
  return 'Employee';
};

export const roleInitialsForValue = (role: number) => {
  if (isAdminRole(role)) return 'AD';
  if (isManagerRole(role)) return 'MA';
  if (isTeamLeaderRole(role)) return 'TL';
  return 'EM';
};
