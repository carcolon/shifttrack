export const SESSION_ACTIVITY_EVENT = 'shifttrack:session-activity';

export function notifySessionActivity(source: string = 'user') {
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new CustomEvent(SESSION_ACTIVITY_EVENT, { detail: { source } }));
}
