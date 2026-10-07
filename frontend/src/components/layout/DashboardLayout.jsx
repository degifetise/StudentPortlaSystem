import { useEffect, useState } from 'react';
import { Menu } from 'lucide-react';
import { Outlet, useLocation } from 'react-router-dom';
import SideDashboard from './SideDashboard';
import SiteFooter from './SiteFooter';
import { ROLES, useAuth } from '../../context/AuthContext';
import { pageHeadingFor } from './navigation';
import DashboardEventsWidget from '../events/DashboardEventsWidget';

/** Shared responsive sidebar shell for every signed-in role. */
export default function DashboardLayout() {
  const { roles } = useAuth();
  const location = useLocation();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const current = pageHeadingFor(location.pathname, roles);

  useEffect(() => {
    setMobileSidebarOpen(false);
  }, [location.pathname]);

  return (
    <div className={`min-h-screen bg-slate-50 lg:grid ${sidebarCollapsed ? 'lg:grid-cols-[5rem_minmax(0,1fr)]' : 'lg:grid-cols-[18rem_minmax(0,1fr)]'}`}>
      <SideDashboard
        collapsed={sidebarCollapsed}
        onToggleCollapsed={() => setSidebarCollapsed((collapsed) => !collapsed)}
        mobileOpen={mobileSidebarOpen}
        onCloseMobile={() => setMobileSidebarOpen(false)}
      />
      <div className="flex min-h-screen min-w-0 flex-col">
        <header className="sticky top-0 z-30 flex min-h-16 items-center gap-3 border-b border-slate-200 bg-white px-4 shadow-sm sm:px-6">
          <button
            type="button"
            className="rounded-lg p-2 text-slate-600 hover:bg-slate-100 lg:hidden"
            onClick={() => setMobileSidebarOpen(true)}
            aria-label="Open dashboard navigation"
            aria-controls="dashboard-sidebar"
            aria-expanded={mobileSidebarOpen}
          >
            <Menu className="size-5" aria-hidden="true" />
          </button>
          <div>
            <p className="text-sm font-semibold text-slate-900">{current?.label ?? 'Dashboard'}</p>
            {current?.description && <p className="hidden text-xs text-slate-500 sm:block">{current.description}</p>}
          </div>
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8">
          {roles.includes(ROLES.admin) && <DashboardEventsWidget />}
          <Outlet />
        </main>
        <SiteFooter />
      </div>
    </div>
  );
}
