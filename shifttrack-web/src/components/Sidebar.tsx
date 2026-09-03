import { useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  BarChart3,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Clock3,
  Home,
  LogOut,
  ShieldCheck,
  UsersRound,
} from 'lucide-react';
import { Button } from './ui/Button';

type TabKey = 'home' | 'calendar' | 'employees' | 'requests' | 'reports' | 'super-admin';

type NavItem = {
  key: TabKey;
  label: string;
  icon: ReactNode;
};

const COLLAPSED_KEY = 'shifttrack-sidebar-collapsed';

export function Sidebar({
  activeTab,
  onTabChange,
  showEmployeesTab,
  showRequestsTab,
  showReportsTab,
  showSuperAdminTab,
  userName,
  userEmail,
  onLogout,
}: {
  activeTab: TabKey;
  onTabChange: (tab: TabKey) => void;
  showEmployeesTab: boolean;
  showRequestsTab: boolean;
  showReportsTab: boolean;
  showSuperAdminTab: boolean;
  userName: string;
  userEmail: string;
  onLogout: () => void;
}) {
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(COLLAPSED_KEY) === 'true');

  useEffect(() => {
    localStorage.setItem(COLLAPSED_KEY, String(collapsed));
  }, [collapsed]);

  const items = useMemo<NavItem[]>(() => {
    const base: NavItem[] = [
      { key: 'home', label: 'Home', icon: <Home size={20} strokeWidth={2.3} /> },
      { key: 'calendar', label: 'Shift Calendar', icon: <CalendarDays size={20} strokeWidth={2.3} /> },
    ];

    if (showEmployeesTab) {
      base.push({ key: 'employees', label: 'Employees', icon: <UsersRound size={20} strokeWidth={2.3} /> });
    }

    if (showRequestsTab) {
      base.push({ key: 'requests', label: 'Requests', icon: <Clock3 size={20} strokeWidth={2.3} /> });
    }

    if (showReportsTab) {
      base.push({ key: 'reports', label: 'Reports', icon: <BarChart3 size={20} strokeWidth={2.3} /> });
    }

    if (showSuperAdminTab) {
      base.push({ key: 'super-admin', label: 'Super Admin', icon: <ShieldCheck size={20} strokeWidth={2.3} /> });
    }

    return base;
  }, [showEmployeesTab, showReportsTab, showRequestsTab, showSuperAdminTab]);

  const initials = (userName || userEmail || 'ST')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('') || 'ST';

  return (
    <aside className={`app-sidebar ${collapsed ? 'collapsed' : ''}`}>
      <div className="app-sidebar-brand">
        <a
          href="/app/home"
          className="app-sidebar-logo"
          onClick={(event) => {
            if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            onTabChange('home');
          }}
          aria-label="ShiftTrack home"
        >
          <img src="/logo.svg" alt="" />
          {!collapsed && (
            <span>
              <strong>ShiftTrack</strong>
              <em>Enterprise Workforce</em>
            </span>
          )}
        </a>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="app-sidebar-collapse"
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          onClick={() => setCollapsed((value) => !value)}
        >
          {collapsed ? <ChevronRight size={17} strokeWidth={2.5} /> : <ChevronLeft size={17} strokeWidth={2.5} />}
        </Button>
      </div>

      <nav className="app-sidebar-nav" aria-label="Primary navigation">
        {items.map((item) => (
          <a
            key={item.key}
            href={`/app/${item.key}`}
            className={`app-sidebar-link ${activeTab === item.key ? 'active' : ''}`}
            title={collapsed ? item.label : undefined}
            onClick={(event) => {
              if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
              event.preventDefault();
              onTabChange(item.key);
            }}
          >
            <span className="app-sidebar-icon">{item.icon}</span>
            {!collapsed && <span>{item.label}</span>}
          </a>
        ))}
      </nav>

      <div className="app-sidebar-footer">
        <div className="app-sidebar-user" title={collapsed ? `${userName} ${userEmail}` : undefined}>
          <div className="app-sidebar-avatar">{initials}</div>
          {!collapsed && (
            <div>
              <strong>{userName}</strong>
              <span>{userEmail}</span>
            </div>
          )}
        </div>
        <Button type="button" variant="ghost" className="app-sidebar-logout" onClick={onLogout}>
          <LogOut size={18} strokeWidth={2.4} />
          {!collapsed && <span>Log out</span>}
        </Button>
      </div>
    </aside>
  );
}
