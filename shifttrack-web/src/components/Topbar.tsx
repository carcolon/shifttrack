import { useEffect, useLayoutEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from 'react';
import gsap from 'gsap';
import { AppsIcon, CaretDownIcon, ReportsIcon, RequestsIcon, ScheduleIcon, TeamIcon } from './Icons';
import { Button } from './ui/Button';

type TabKey = 'calendar' | 'employees' | 'requests' | 'reports' | 'super-admin';

type NavItem = {
  key: TabKey;
  label: string;
  icon: (color?: string) => ReactNode;
};

export function Topbar({
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
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement | null>(null);
  const menuPopoverRef = useRef<HTMLDivElement | null>(null);

  const items = useMemo<NavItem[]>(() => {
    const base: NavItem[] = [{ key: 'calendar', label: 'Shift Calendar', icon: (color) => <ScheduleIcon color={color} /> }];

    if (showEmployeesTab) {
      base.push({ key: 'employees', label: 'Employees', icon: (color) => <TeamIcon color={color} /> });
    }

    if (showRequestsTab) {
      base.push({ key: 'requests', label: 'Requests', icon: (color) => <RequestsIcon color={color} /> });
    }

    if (showReportsTab) {
      base.push({ key: 'reports', label: 'Reports', icon: (color) => <ReportsIcon color={color} /> });
    }

    if (showSuperAdminTab) {
      base.push({ key: 'super-admin', label: 'Super Admin', icon: (color) => <AppsIcon color={color} /> });
    }

    return base;
  }, [showEmployeesTab, showReportsTab, showRequestsTab, showSuperAdminTab]);

  const calendarItem = items.find((item) => item.key === 'calendar')!;
  const activeSecondaryItem = activeTab === 'calendar' ? null : items.find((item) => item.key === activeTab) ?? null;
  const hiddenItems = items.filter((item) => item.key !== 'calendar' && item.key !== activeSecondaryItem?.key);

  useEffect(() => {
    const handlePointerDown = (event: globalThis.MouseEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  useLayoutEffect(() => {
    if (!menuOpen || !menuPopoverRef.current) return;

    const ctx = gsap.context(() => {
      const popover = menuPopoverRef.current;
      if (!popover) return;
      const items = popover.querySelectorAll('.topbar-menu-item');

      gsap.fromTo(
        popover,
        { opacity: 0, y: 14, scale: 0.96, transformOrigin: 'top left' },
        { opacity: 1, y: 0, scale: 1, duration: 0.28, ease: 'power2.out' },
      );

      gsap.fromTo(
        items,
        { opacity: 0, x: -10 },
        { opacity: 1, x: 0, duration: 0.24, stagger: 0.05, ease: 'power2.out', delay: 0.06 },
      );
    }, menuPopoverRef);

    return () => ctx.revert();
  }, [menuOpen]);

  const handleTabLinkClick = (event: ReactMouseEvent<HTMLAnchorElement>, item: NavItem) => {
    if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    setMenuOpen(false);
    onTabChange(item.key);
  };

  const renderTab = (item: NavItem, active: boolean) => (
    <a
      key={item.key}
      href={`/app/${item.key}`}
      className={`topbar-tab ${active ? 'active' : ''}`}
      onClick={(event) => handleTabLinkClick(event, item)}
      style={{ background: active ? '#317EB5' : '#32425D' }}
    >
      <span className="btn-icon">{item.icon()}</span>
      {item.label}
    </a>
  );

  return (
    <nav className="topbar" style={{ background: '#32425D' }}>
      <div className="nav-left">
        <a
          href="/app/calendar"
          className="logo brand-inline brand-home topbar-brand-button"
          onClick={(event) => {
            if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            setMenuOpen(false);
            onTabChange('calendar');
          }}
        >
          <img src="/logo.svg" alt="ShiftTrack logo" className="logo-img" />
          <span>ShiftTrack</span>
        </a>

        <div className="tabs">
          {renderTab(calendarItem, activeTab === 'calendar')}
          {activeSecondaryItem && renderTab(activeSecondaryItem, true)}

          {!!hiddenItems.length && (
            <div className="topbar-menu" ref={menuRef}>
              <Button
                className={`topbar-tab topbar-menu-trigger ${menuOpen ? 'active' : ''}`}
                variant="ghost"
                onClick={() => setMenuOpen((open) => !open)}
                style={{ background: menuOpen ? '#2f587f' : '#32425D' }}
              >
                <span className="btn-icon"><AppsIcon /></span>
                More
                <span className={`topbar-menu-caret ${menuOpen ? 'open' : ''}`}><CaretDownIcon /></span>
              </Button>

              {menuOpen && (
                <div className="topbar-menu-popover" ref={menuPopoverRef}>
                  <div className="topbar-menu-header">Navigate</div>
                  {hiddenItems.map((item) => (
                    <a
                      key={item.key}
                      href={`/app/${item.key}`}
                      className="topbar-menu-item"
                      onClick={(event) => handleTabLinkClick(event, item)}
                    >
                      <span className="btn-icon topbar-menu-icon">{item.icon('#317eb5')}</span>
                      <span>{item.label}</span>
                    </a>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <div className="user-chip">
        <div className="user-info">
          <div className="user-name">{userName}</div>
          <div className="user-email">{userEmail}</div>
        </div>
        <Button className="logout-btn" variant="ghost" onClick={onLogout}>
          Log out
        </Button>
      </div>
    </nav>
  );
}
