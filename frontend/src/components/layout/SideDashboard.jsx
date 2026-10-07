import { useEffect, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  BookOpen,
  Bell,
  CalendarDays,
  CheckCheck,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  ClipboardList,
  GraduationCap,
  IdCard,
  LayoutDashboard,
  LogOut,
  MessageSquareText,
  ScanLine,
  School,
  Settings,
  UserRoundCog,
  Users,
  UsersRound,
  X,
} from 'lucide-react';
import { ROLES, useAuth } from '../../context/AuthContext';
import { useSchoolInfo } from '../../context/SchoolInfoContext';
import { notificationApi } from '../../services/endpoints';
import { API_BASE_URL } from '../../services/api';

const ADMIN_ITEMS = [
  { to: '/admin/analytics', label: 'Dashboard overview', Icon: LayoutDashboard },
  { to: '/admin/students', label: 'Student management', Icon: GraduationCap },
  { to: '/admin/accounts', label: 'Teacher & staff management', Icon: UserRoundCog },
  { to: '/admin/smart-id/print', label: 'Smart ID management & batch print', Icon: IdCard },
  { to: '/admin/scanner', label: 'ID scanner', Icon: ScanLine },
  { to: '/admin/attendance', label: 'Attendance & logs', Icon: ClipboardList },
  { to: '/admin/parents', label: 'Parents & guardians', Icon: UsersRound },
  { to: '/admin/subjects', label: 'Subjects', Icon: BookOpen },
  { to: '/admin/assignments', label: 'Teacher assignments', Icon: Users },
  { to: '/admin/assessments', label: 'Assessments', Icon: ClipboardCheck },
  { to: '/admin/feedback', label: 'Student feedback', Icon: MessageSquareText },
  { to: '/settings', label: 'System settings', Icon: Settings },
];

const ROLE_ITEMS = {
  [ROLES.teacher]: [
    { to: '/teacher/analytics', label: 'Class overview', Icon: LayoutDashboard },
    { to: '/teacher/students', label: 'Student roster', Icon: Users },
    { to: '/teacher/attendance', label: 'Attendance marker', Icon: ClipboardList },
    { to: '/teacher/marks', label: 'Gradebook & marks', Icon: ClipboardCheck },
    { to: '/staff/scanner', label: 'ID scanner', Icon: ScanLine },
    { to: '/my-smart-id', label: 'Digital Smart ID', Icon: IdCard },
    { to: '/settings', label: 'Profile & settings', Icon: Settings },
  ],
  [ROLES.staff]: [
    { to: '/staff/scanner', label: 'ID scanner', Icon: ScanLine },
    { to: '/my-smart-id', label: 'Digital Smart ID', Icon: IdCard },
    { to: '/settings', label: 'Profile & settings', Icon: Settings },
  ],
  [ROLES.student]: [
    { to: '/student/results', label: 'Academic overview & grades', Icon: GraduationCap },
    { to: '/student/feedback', label: 'Feedback & ideas', Icon: MessageSquareText },
    { to: '/my-smart-id', label: 'Digital Smart ID', Icon: IdCard },
    { to: '/settings', label: 'Profile & settings', Icon: Settings },
  ],
  [ROLES.guardian]: [
    { to: '/parent/dashboard', label: 'Children, attendance & grades', Icon: UsersRound },
    { to: '/settings', label: 'Profile & settings', Icon: Settings },
  ],
};

const ROLE_LABELS = {
  [ROLES.admin]: 'Administrator',
  [ROLES.teacher]: 'Teacher',
  [ROLES.staff]: 'Staff',
  [ROLES.student]: 'Student',
  [ROLES.guardian]: 'Parent / Guardian',
};

function photoSource(photoUrl) {
  if (!photoUrl) return null;
  try {
    return new URL(photoUrl, API_BASE_URL).toString();
  } catch {
    return null;
  }
}

export default function SideDashboard({
  collapsed,
  onToggleCollapsed,
  mobileOpen,
  onCloseMobile,
}) {
  const { user, roles, logout } = useAuth();
  const { schoolName } = useSchoolInfo();
  const navigate = useNavigate();
  const width = collapsed ? 'lg:w-20' : 'lg:w-72';
  const primaryRole = [ROLES.admin, ROLES.teacher, ROLES.staff, ROLES.student, ROLES.guardian]
    .find((role) => roles.includes(role));
  const items = [
    ...(primaryRole === ROLES.admin ? ADMIN_ITEMS : ROLE_ITEMS[primaryRole] ?? []),
    { to: '/events', label: 'Explore Events', Icon: CalendarDays },
  ];
  const roleLabel = ROLE_LABELS[primaryRole] ?? 'Portal user';
  const avatarUrl = photoSource(user?.photoUrl ?? user?.profileImageUrl);
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [notificationBusy, setNotificationBusy] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationError, setNotificationError] = useState('');
  const [savingNotificationId, setSavingNotificationId] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const refreshUnreadCount = async () => {
      try {
        const result = await notificationApi.unreadCount();
        if (!cancelled) {
          setUnreadCount(Number.isFinite(result?.unreadCount) ? result.unreadCount : 0);
          setNotificationError('');
        }
      } catch (error) {
        if (!cancelled) {
          setNotificationError(error.friendlyMessage ?? 'Notifications could not be refreshed.');
        }
      }
    };

    refreshUnreadCount();
    const interval = setInterval(refreshUnreadCount, 30000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  useEffect(() => {
    if (!notificationOpen) return undefined;
    let cancelled = false;
    setNotificationBusy(true);
    notificationApi.list()
      .then((result) => {
        if (!cancelled) {
          setNotifications(Array.isArray(result) ? result : result?.items ?? []);
          setNotificationError('');
        }
      })
      .catch((error) => {
        if (!cancelled) {
          setNotificationError(error.friendlyMessage ?? 'Notifications could not be loaded.');
        }
      })
      .finally(() => {
        if (!cancelled) setNotificationBusy(false);
      });
    return () => { cancelled = true; };
  }, [notificationOpen]);

  useEffect(() => {
    if (!mobileOpen) return undefined;
    const closeOnEscape = (event) => {
      if (event.key === 'Escape') onCloseMobile();
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [mobileOpen, onCloseMobile]);

  async function signOut() {
    await logout();
    navigate('/login', { replace: true });
  }

  async function openNotification(notification) {
    if (!notification.readAt) {
      setSavingNotificationId(notification.id);
      try {
        await notificationApi.markRead(notification.id);
        setNotifications((current) => current.map((item) =>
          item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item));
        setUnreadCount((count) => Math.max(0, count - 1));
        setNotificationError('');
      } catch (error) {
        setNotificationError(error.friendlyMessage ?? 'This notification could not be marked as read.');
        return;
      } finally {
        setSavingNotificationId(null);
      }
    }

    setNotificationOpen(false);
    if (notification.targetUrl) navigate(notification.targetUrl);
  }

  async function markAllNotificationsRead() {
    try {
      await notificationApi.markAllRead();
      setNotifications((current) => current.map((item) => ({
        ...item,
        readAt: item.readAt ?? new Date().toISOString(),
      })));
      setUnreadCount(0);
      setNotificationError('');
    } catch (error) {
      setNotificationError(error.friendlyMessage ?? 'Notifications could not be marked as read.');
    }
  }

  return (
    <>
      {mobileOpen && (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-slate-950/50 lg:hidden"
          onClick={() => {
            setNotificationOpen(false);
            onCloseMobile();
          }}
          aria-label="Close dashboard navigation"
        />
      )}
      <aside
        id="dashboard-sidebar"
        className={`fixed inset-y-0 left-0 z-50 flex w-72 flex-col border-r border-slate-200 bg-white shadow-xl transition-[transform,width] duration-200 lg:sticky lg:top-0 lg:z-auto lg:h-screen lg:translate-x-0 lg:shadow-none ${width} ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex h-16 shrink-0 items-center gap-3 border-b border-slate-100 px-4">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-brand-800 text-white">
            <School className="size-5" aria-hidden="true" />
          </span>
          {!collapsed && (
            <span className="min-w-0 flex-1 truncate text-sm font-bold text-slate-900">{schoolName}</span>
          )}
          <button
            type="button"
            className="ml-auto rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-900 lg:hidden"
            onClick={onCloseMobile}
            aria-label="Close dashboard navigation"
          >
            <X className="size-5" aria-hidden="true" />
          </button>
          <button
            type="button"
            className="ml-auto hidden rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-900 lg:inline-flex"
            onClick={onToggleCollapsed}
            aria-label={collapsed ? 'Expand dashboard navigation' : 'Collapse dashboard navigation'}
            title={collapsed ? 'Expand navigation' : 'Collapse navigation'}
          >
            {collapsed
              ? <ChevronRight className="size-4" aria-hidden="true" />
              : <ChevronLeft className="size-4" aria-hidden="true" />}
          </button>
        </div>

        <nav className="min-h-0 flex-1 space-y-1 overflow-y-auto p-3" aria-label={`${roleLabel} dashboard`}>
          {items.map(({ to, label, Icon }) => (
            <NavLink
              key={to}
              to={to}
              title={collapsed ? label : undefined}
              onClick={() => {
                setNotificationOpen(false);
                onCloseMobile();
              }}
              className={({ isActive }) => `flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${
                isActive
                  ? 'bg-brand-50 text-brand-800 ring-1 ring-inset ring-brand-100'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-950'
              } ${collapsed ? 'lg:justify-center lg:px-0' : ''}`}
            >
              <Icon className="size-5 shrink-0" aria-hidden="true" />
              <span className={collapsed ? 'lg:sr-only' : ''}>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className={`shrink-0 border-t border-slate-100 p-3 ${collapsed ? 'lg:flex lg:justify-center' : ''}`}>
          <div className="relative mb-2">
            <button
              type="button"
              className={`relative flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 ${collapsed ? 'lg:w-11 lg:justify-center lg:px-0' : ''}`}
              onClick={() => setNotificationOpen((open) => !open)}
              aria-expanded={notificationOpen}
              aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : 'Notifications'}
              title={collapsed ? 'Notifications' : undefined}
            >
              <Bell className="size-5 shrink-0" aria-hidden="true" />
              <span className={collapsed ? 'lg:sr-only' : ''}>Notifications</span>
              {unreadCount > 0 && (
                <span className="ml-auto grid min-w-5 place-items-center rounded-full bg-red-600 px-1.5 py-0.5 text-[10px] font-bold text-white">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </button>
            {notificationOpen && (
              <section className="absolute bottom-full left-0 z-[60] mb-2 w-[calc(100vw-1.5rem)] max-w-80 rounded-xl border border-slate-200 bg-white p-3 shadow-xl lg:left-0 lg:w-80" aria-label="Notifications">
                <div className="mb-2 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-semibold text-slate-900">Notifications</h2>
                  {unreadCount > 0 && (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1 text-xs font-medium text-brand-700 hover:underline"
                      onClick={markAllNotificationsRead}
                    >
                      <CheckCheck className="size-3.5" aria-hidden="true" />
                      Mark all read
                    </button>
                  )}
                </div>
                {notificationError && <p role="alert" className="mb-2 text-xs text-red-700">{notificationError}</p>}
                <div className="max-h-72 space-y-1 overflow-y-auto">
                  {notificationBusy ? (
                    <p className="py-4 text-center text-xs text-slate-500">Loading notifications…</p>
                  ) : notifications.length === 0 ? (
                    <p className="py-4 text-center text-xs text-slate-500">No notifications yet.</p>
                  ) : notifications.map((notification) => (
                    <button
                      key={notification.id}
                      type="button"
                      className={`block w-full rounded-lg p-2 text-left hover:bg-slate-50 ${notification.readAt ? 'text-slate-600' : 'bg-brand-50 text-slate-900'}`}
                      onClick={() => openNotification(notification)}
                      disabled={savingNotificationId === notification.id}
                    >
                      <span className="block text-xs font-semibold">{notification.title}</span>
                      <span className="mt-0.5 block text-xs leading-snug">{notification.message}</span>
                    </button>
                  ))}
                </div>
              </section>
            )}
          </div>
          {!collapsed && (
            <div className="mb-3 flex items-center gap-3 px-2">
              {avatarUrl ? (
                <img
                  src={avatarUrl}
                  alt=""
                  className="size-9 shrink-0 rounded-full border border-slate-200 object-cover"
                />
              ) : (
                <span className="grid size-9 shrink-0 place-items-center rounded-full bg-brand-100 text-sm font-bold text-brand-800">
                  {(user?.fullName ?? 'U').trim().slice(0, 1).toUpperCase()}
                </span>
              )}
              <span className="min-w-0">
                <span className="block truncate text-sm font-semibold text-slate-900">{user?.fullName ?? 'Portal user'}</span>
                <span className="block truncate text-xs text-slate-500">{roleLabel}</span>
              </span>
            </div>
          )}
          <button
            type="button"
            className={`flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 hover:bg-red-50 hover:text-red-700 ${collapsed ? 'lg:w-11 lg:justify-center lg:px-0' : ''}`}
            onClick={signOut}
            title={collapsed ? 'Sign out' : undefined}
          >
            <LogOut className="size-5 shrink-0" aria-hidden="true" />
            <span className={collapsed ? 'lg:sr-only' : ''}>Sign out</span>
          </button>
        </div>
      </aside>
    </>
  );
}
