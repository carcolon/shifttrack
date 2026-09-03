# ShiftTrack Web

React + Vite frontend for ShiftTrack. This app handles authentication, the authenticated dashboard shell, employee administration, requests and approvals, weekly and yearly calendar views, realtime schedule refresh, and the current shared UI foundation.

## Tech Stack

- React 19 + TypeScript
- Vite
- React Router
- GSAP
- Motion
- SignalR client (`@microsoft/signalr`)

## App Areas

- `Login`
- `Reset password`
- `Force password change`
- `Entra callback`
- `Dashboard`
  - `Shift Calendar`
  - `Employees`
  - `Requests`
- `PTO review`
- `Swap review`
- `Maintenance`

## Main Features

### Auth and session UX

- Cookie-backed auth with `/auth/me` hydration
- Entra ID callback flow
- 60-minute inactivity session timeout
- Warning modal before expiration
- Logout/login transition animation
- Local session flash message after expiration

### Dashboard shell

- Shared topbar and authenticated app layout
- Ambient animated background across authenticated screens
- Shared custom cursor
- Shared button/select/modal primitives
- GSAP transitions for tabs, views, dropdowns and modal entry

### Shift Calendar

- Weekly grid view
- Year overview / month-style annual calendar
- Jump from year overview day to its week
- Filters:
  - scheduling date
  - employee
  - role
  - shift
  - operation
  - company
- Logged-in user stays pinned first in calendar results
- Pagination for additional employees
- Team Leader support
- Coverage visible for Admin, Manager and Team Leader
- Colombian holidays loaded from backend
- Holiday days shown in red in year overview
- Working shifts on holidays highlighted in the weekly view
- SignalR-driven silent refresh

### PTO and change requests

- PTO creation and cancellation flows
- Change Request flow for same-role coworkers
- Request and review screens
- Local-time rendering for request timestamps
- PTO business rules aligned with backend validation
- Vacation requests respect Colombian business-day counting rules

### Employees

- Create, edit, deactivate, reactivate and purge flows
- Shared schedule-period editor
- Fixed/valid-until scheduling blocks
- Up to multiple schedule periods with next-period generation
- Role-aware visibility and management

## Routes

- `/`
- `/reset`
- `/force`
- `/entra-callback`
- `/app`
- `/pto-review`
- `/swap-review`

## Environment Variables

Create `.env.development`, `.env.local`, or `.env.production` as needed.

```env
VITE_API_BASE=http://localhost:5243
VITE_API_BASE_FALLBACK=https://your-deployed-api.azurewebsites.net
VITE_ENABLE_BEARER_FALLBACK=true
VITE_ENTRA_TENANT_ID=
VITE_ENTRA_CLIENT_ID=
VITE_MAINTENANCE_MODE=false
VITE_MAINTENANCE_MESSAGE=
VITE_ENABLE_TRACKY=true
```

### API base behavior

The web app is designed to:

- try `VITE_API_BASE` first
- fall back to `VITE_API_BASE_FALLBACK` only when the primary backend is unavailable
- avoid switching to cloud just because local auth returned `401`

That lets you run:

- local frontend + local backend
- local frontend + deployed backend fallback

## Local Development

```bash
cd shifttrack-web
npm install
npm run dev
```

Default local frontend URL: `http://localhost:5173`

## Build

```bash
cd shifttrack-web
npm run build
```

## Important Frontend Files

- `src/App.tsx`
- `src/pages/DashboardPage.tsx`
- `src/pages/ShiftCalendarPage.tsx`
- `src/pages/RequestsPage.tsx`
- `src/pages/PtoReviewPage.tsx`
- `src/pages/SwapReviewPage.tsx`
- `src/components/Topbar.tsx`
- `src/components/Modals.tsx`
- `src/components/ui/Button.tsx`
- `src/components/ui/Field.tsx`
- `src/components/ui/Select.tsx`
- `src/components/UiCursor.tsx`

## Realtime

- Hub path: `/hubs/schedule`
- Event: `schedule.updated`
- Used for silent schedule refresh and live-status indicator

## Deployment Notes

- Set `VITE_API_BASE` to the deployed API URL in the target environment
- Keep `VITE_API_BASE_FALLBACK` empty in environments where fallback is not desired
- Ensure API CORS includes the deployed frontend origin
- Ensure Entra redirect/callback URLs match the deployed frontend
- Ensure the SignalR hub is reachable from the deployed Static Web App

## Troubleshooting

- Calendar not loading holidays:
  - verify backend `/holidays` exists in the target environment
- Local frontend hitting cloud unexpectedly:
  - check `VITE_API_BASE`
  - check `VITE_API_BASE_FALLBACK`
  - restart Vite after env changes
- Realtime status stuck offline:
  - confirm `/hubs/schedule` is reachable
- Auth loop after timeout:
  - clear session storage and sign in again

## Recommended PR Checklist

- `npm run build` passes
- screenshots or recordings added for UI changes
- env var additions documented
- no secrets committed
