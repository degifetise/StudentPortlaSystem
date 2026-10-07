import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';
import {
  BadgeCheck,
  Bell,
  ChevronDown,
  CheckCheck,
  GraduationCap,
  IdCard,
  LogOut,
  Menu,
  School,
  ShieldCheck,
  UsersRound,
  X,
} from 'lucide-react';
import { ROLES, useAuth } from '../../context/AuthContext';
import { useSchoolInfo } from '../../context/SchoolInfoContext';
import { notificationApi } from '../../services/endpoints';
import { navItemsFor } from './navigation';


const ROLE_STYLE = {
  [ROLES.admin]: { chip: 'bg-amber-400/20 text-amber-100 ring-amber-300/30', Icon: ShieldCheck },
  [ROLES.teacher]: { chip: 'bg-emerald-400/20 text-emerald-100 ring-emerald-300/30', Icon: BadgeCheck },
  [ROLES.staff]: { chip: 'bg-indigo-400/20 text-indigo-100 ring-indigo-300/30', Icon: IdCard },
  [ROLES.student]: { chip: 'bg-sky-400/20 text-sky-100 ring-sky-300/30', Icon: GraduationCap },
  [ROLES.guardian]: { chip: 'bg-violet-400/20 text-violet-100 ring-violet-300/30', Icon: UsersRound },
};

const FALLBACK_STYLE = { chip: 'bg-white/15 text-white ring-white/20', Icon: IdCard };

function initialsOf(name = '') {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || '?'
  );
}

/** Coloured chip naming a role. Rendered on the dark bar, hence the light-on-dark palette. */
export function RoleBadge({ role, className = '' }) {
  const { chip, Icon } = ROLE_STYLE[role] ?? FALLBACK_STYLE;

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ring-inset ${chip} ${className}`}
    >
      <Icon className="size-3" aria-hidden="true" />
      {role}
    </span>
  );
}


function IdentityLine({ user, roles }) {
  const identifier = user?.studentIdNumber ?? user?.employeeId;

  return (
    <span className="flex items-center gap-1.5">
      {roles.map((role) => (
        <RoleBadge key={role} role={role} />
      ))}
      {identifier && (
        <span className="font-mono text-[11px] text-brand-200" title="Identifier">
          {identifier}
        </span>
      )}
    </span>
  );
}


export default function TopNavBar() {
  const { user, roles, isAuthenticated, logout } = useAuth();
  const { schoolName, academicYear } = useSchoolInfo();
  const location = useLocation();
  const navigate = useNavigate();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState([]);
  const [notificationBusy, setNotificationBusy] = useState(false);
  const [readingNotificationIds, setReadingNotificationIds] = useState(() => new Set());
  const [notificationError, setNotificationError] = useState('');
  const menuRef = useRef(null);
  const notificationRef = useRef(null);
  const notificationCountVersion = useRef(0);

  const items = navItemsFor(roles);
  const isStudent = roles.includes(ROLES.student);

  useEffect(() => {
    if (!isAuthenticated) {
      setUnreadCount(0);
      return undefined;
    }

    let cancelled = false;
    const refreshCount = async () => {
      const version = notificationCountVersion.current;
      try {
        const result = await notificationApi.unreadCount();
        if (!cancelled && version === notificationCountVersion.current) {
          setUnreadCount(Number.isFinite(result?.unreadCount) ? result.unreadCount : 0);
          setNotificationError('');
        }
      } catch (error) {
        if (!cancelled) {
          setNotificationError(error.friendlyMessage ?? 'Notifications could not be refreshed.');
        }
      }
    };

    refreshCount();
    const interval = setInterval(refreshCount, 30000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [isAuthenticated, location.pathname]);

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
        if (!cancelled) setNotificationError(error.friendlyMessage ?? 'Notifications could not be loaded.');
      })
      .finally(() => {
        if (!cancelled) setNotificationBusy(false);
      });

    return () => { cancelled = true; };
  }, [notificationOpen]);

  // Any navigation closes both overlays, including a click on the link you are already on.
  useEffect(() => {
    setMobileOpen(false);
    setMenuOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!menuOpen) return undefined;

    const onPointerDown = (event) => {
      if (!menuRef.current?.contains(event.target)) setMenuOpen(false);
    };
    const onKeyDown = (event) => event.key === 'Escape' && setMenuOpen(false);

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [menuOpen]);

  useEffect(() => {
    if (!notificationOpen) return undefined;
    const onPointerDown = (event) => {
      if (!notificationRef.current?.contains(event.target)) setNotificationOpen(false);
    };
    const onKeyDown = (event) => event.key === 'Escape' && setNotificationOpen(false);
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [notificationOpen]);

  async function markNotificationRead(notification) {
    if (notification.readAt) {
      if (notification.targetUrl) navigate(notification.targetUrl);
      setNotificationOpen(false);
      return;
    }

    if (readingNotificationIds.has(notification.id)) return;
    setReadingNotificationIds((current) => new Set(current).add(notification.id));
    try {
      await notificationApi.markRead(notification.id);
      notificationCountVersion.current += 1;
      setNotifications((current) => current.map((item) =>
        item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item));
      setUnreadCount((count) => Math.max(0, count - 1));
      setNotificationError('');
      if (notification.targetUrl) navigate(notification.targetUrl);
      setNotificationOpen(false);
    } catch (error) {
      setNotificationError(error.friendlyMessage ?? 'This notification could not be marked as read.');
    } finally {
      setReadingNotificationIds((current) => {
        const next = new Set(current);
        next.delete(notification.id);
        return next;
      });
    }
  }

  async function markAllNotificationsRead() {
    try {
      await notificationApi.markAllRead();
      notificationCountVersion.current += 1;
      setNotifications((current) => current.map((item) => ({
        ...item,
        readAt: item.readAt ?? new Date().toISOString(),
      })));
      setUnreadCount(0);
      setNotificationError('');
      setNotificationOpen(false);
    } catch (error) {
      setNotificationError(error.friendlyMessage ?? 'Notifications could not be marked as read.');
    }
  }

  useEffect(() => {
    if (!mobileOpen) return undefined;
    const onKeyDown = (event) => event.key === 'Escape' && setMobileOpen(false);
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [mobileOpen]);

  /* The active pill is the route indicator. NavLink resolves `isActive` from the current
     location, and `end` keeps "/" from matching every path below it. */
  const deskLink = ({ isActive }) =>
    `relative rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
      isActive
        ? 'bg-white text-brand-800 shadow-sm'
        : 'text-brand-100 hover:bg-white/10 hover:text-white'
    }`;

  const sheetLink = ({ isActive }) =>
    `flex items-start gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${
      isActive ? 'bg-white text-brand-800' : 'text-brand-100 hover:bg-white/10 hover:text-white'
    }`;

  return (
    <header className="sticky top-0 z-40 bg-brand-800 shadow-sm">
      <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-3 sm:px-6 lg:px-8">
    
        <Link to="/" className="flex min-w-0 items-center gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/15 text-white">
            <School className="size-5" aria-hidden="true" />
          </span>
          <span className="min-w-0">
            <span className="block truncate text-sm font-bold text-white sm:text-base" title={schoolName}>
              {schoolName}
            </span>
            <span className="block truncate text-xs text-brand-200">
              Nursery – Grade 12{academicYear ? ` · ${academicYear}` : ''}
            </span>
          </span>
        </Link>

        <nav className="ml-4 hidden items-center gap-1 lg:flex" aria-label="Main">
          {items.map(({ to, label, shortLabel, end }) => (
            <NavLink key={to} to={to} end={end} className={deskLink}>
              {/* The student's longest label is shortened on narrow desktops only. */}
              <span className={shortLabel ? 'hidden xl:inline' : undefined}>{label}</span>
              {shortLabel && <span className="xl:hidden">{shortLabel}</span>}
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          {isAuthenticated && unreadCount > 0 && (
            <div className="relative" ref={notificationRef}>
              <button
                type="button"
                className="relative grid size-9 shrink-0 place-items-center rounded-lg text-white hover:bg-white/10"
                aria-label={`${unreadCount} unread notifications`}
                title={`${unreadCount} unread notifications`}
                aria-expanded={notificationOpen}
                aria-haspopup="dialog"
                onClick={() => setNotificationOpen((open) => !open)}
              >
                <Bell className="size-5" aria-hidden="true" />
                <span className="absolute -top-0.5 -right-0.5 grid min-h-4 min-w-4 place-items-center rounded-full bg-rose-500 px-1 text-[10px] font-bold text-white">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              </button>
              <AnimatePresence>
                {notificationOpen && (
                  <motion.section
                    initial={{ opacity: 0, y: -6 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -6 }}
                    role="dialog"
                    aria-label="Notifications"
                    className="absolute right-0 z-50 mt-2 w-80 max-w-[calc(100vw-2rem)] overflow-hidden rounded-xl border border-slate-200 bg-white text-slate-900 shadow-xl sm:w-96"
                  >
                    <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
                      <div>
                        <h2 className="font-semibold">Notifications</h2>
                        <p className="text-xs text-slate-500">{unreadCount} unread</p>
                      </div>
                      <button
                        type="button"
                        onClick={markAllNotificationsRead}
                        className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-700 hover:text-brand-900"
                      >
                        <CheckCheck className="size-4" aria-hidden="true" />
                        Mark all as read
                      </button>
                    </div>
                    {notificationError && (
                      <p role="alert" className="border-b border-rose-100 bg-rose-50 px-4 py-2 text-xs text-rose-700">
                        {notificationError}
                      </p>
                    )}
                    <div className="max-h-96 overflow-y-auto">
                      {notificationBusy ? (
                        <p className="px-4 py-8 text-center text-sm text-slate-500">Loading notifications…</p>
                      ) : notifications.length ? notifications.map((notification) => (
                        <button
                          key={notification.id}
                          type="button"
                          onClick={() => markNotificationRead(notification)}
                          disabled={readingNotificationIds.has(notification.id)}
                          className={`block w-full border-b border-slate-100 px-4 py-3 text-left last:border-0 hover:bg-slate-50 disabled:opacity-60 ${notification.readAt ? 'bg-white' : 'bg-sky-50/70'}`}
                        >
                          <span className="flex items-start gap-2">
                            {!notification.readAt && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-sky-600" aria-hidden="true" />}
                            <span className="min-w-0 flex-1">
                              <span className="block text-sm font-semibold">{notification.title}</span>
                              <span className="mt-0.5 block text-xs text-slate-600">{notification.message}</span>
                              <span className="mt-1 block text-[11px] text-slate-400">{new Date(notification.createdAt).toLocaleString()}</span>
                            </span>
                          </span>
                        </button>
                      )) : (
                        <p className="px-4 py-8 text-center text-sm text-slate-500">No recent notifications.</p>
                      )}
                    </div>
                  </motion.section>
                )}
              </AnimatePresence>
            </div>
          )}
          {isAuthenticated ? (
            <>
              {/* Separator: everything to the right of it is about the account, not the site. */}
              <span className="mx-1 hidden h-8 w-px bg-white/15 md:block" aria-hidden="true" />

              <div className="hidden text-right md:block">
                <p className="truncate text-sm font-semibold leading-tight text-white">
                  {user?.fullName}
                </p>
                <IdentityLine user={user} roles={roles} />
              </div>

              {/* A student's bar carries a standalone Logout button rather than a menu, so
                  signing out is one tap with no discovery needed. */}
              {isStudent ? (
                <>
                  <span className="grid size-10 shrink-0 place-items-center rounded-full bg-white/15 text-sm font-bold text-white">
                    {initialsOf(user?.fullName)}
                  </span>
                  {/* Always visible, at every width: on a narrow screen it drops to the icon
                      rather than hiding behind the menu. */}
                  <button
                    type="button"
                    onClick={() => logout({ silent: false })}
                    className="inline-flex items-center gap-2 rounded-lg bg-rose-500/90 px-2.5 py-2 text-sm font-semibold text-white transition-colors hover:bg-rose-500 sm:px-3"
                  >
                    <LogOut className="size-4" aria-hidden="true" />
                    <span className="hidden sm:inline">Logout</span>
                    <span className="sr-only sm:hidden">Logout</span>
                  </button>
                </>
              ) : (
                <div className="relative" ref={menuRef}>
                  <button
                    type="button"
                    onClick={() => setMenuOpen((open) => !open)}
                    className="flex items-center gap-1.5 rounded-full bg-white/15 py-1 pl-1 pr-2 text-white transition-colors hover:bg-white/25"
                    aria-haspopup="menu"
                    aria-expanded={menuOpen}
                    aria-label="Account menu"
                  >
                    <span className="grid size-8 place-items-center rounded-full bg-white/20 text-xs font-bold">
                      {initialsOf(user?.fullName)}
                    </span>
                    <ChevronDown
                      className={`size-4 transition-transform ${menuOpen ? 'rotate-180' : ''}`}
                      aria-hidden="true"
                    />
                  </button>

                  <AnimatePresence>
                    {menuOpen && (
                      <motion.div
                        initial={{ opacity: 0, y: -6, scale: 0.98 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: -6, scale: 0.98 }}
                        transition={{ duration: 0.14 }}
                        role="menu"
                        className="absolute right-0 mt-2 w-64 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg"
                      >
                        <div className="border-b border-slate-100 px-4 py-3">
                          <p className="truncate font-semibold text-slate-900">{user?.fullName}</p>
                          <p className="truncate text-xs text-slate-500">{user?.email}</p>
                          <div className="mt-2 flex flex-wrap gap-1">
                            {roles.map((role) => (
                              <span
                                key={role}
                                className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700"
                              >
                                {role}
                              </span>
                            ))}
                            {user?.employeeId && (
                              <span className="rounded-full bg-slate-100 px-2 py-0.5 font-mono text-[11px] text-slate-600">
                                {user.employeeId}
                              </span>
                            )}
                          </div>
                        </div>

                        <button
                          type="button"
                          role="menuitem"
                          onClick={() => {
                            setMenuOpen(false);
                            logout({ silent: false });
                          }}
                          className="flex w-full items-center gap-2 px-4 py-3 text-sm font-semibold text-rose-700 transition-colors hover:bg-rose-50"
                        >
                          <LogOut className="size-4" aria-hidden="true" />
                          Logout
                        </button>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              )}
            </>
          ) : null}

          <button
            type="button"
            onClick={() => setMobileOpen((open) => !open)}
            className="rounded-lg p-2 text-brand-100 transition-colors hover:bg-white/10 hover:text-white lg:hidden"
            aria-label={mobileOpen ? 'Close navigation' : 'Open navigation'}
            aria-expanded={mobileOpen}
            aria-controls="mobile-navigation"
          >
            <AnimatePresence mode="wait" initial={false}>
              <motion.span
                key={mobileOpen ? 'close' : 'open'}
                initial={{ rotate: -90, opacity: 0 }}
                animate={{ rotate: 0, opacity: 1 }}
                exit={{ rotate: 90, opacity: 0 }}
                transition={{ duration: 0.15 }}
                className="block"
              >
                {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
              </motion.span>
            </AnimatePresence>
          </button>
        </div>
      </div>

      {/* Mobile sheet. Collapses in place instead of covering the page, so the content behind
          it stays visible while choosing. */}
      <AnimatePresence>
        {mobileOpen && (
          <motion.div
            id="mobile-navigation"
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2, ease: 'easeOut' }}
            className="overflow-hidden border-t border-white/10 lg:hidden"
          >
            <nav className="px-4 py-3 sm:px-6" aria-label="Mobile">
              {isAuthenticated && (
                <div className="mb-3 flex items-center gap-3 rounded-lg bg-white/5 px-3 py-2.5">
                  <span className="grid size-9 shrink-0 place-items-center rounded-full bg-white/15 text-xs font-bold text-white">
                    {initialsOf(user?.fullName)}
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-white">
                      {user?.fullName}
                    </span>
                    <IdentityLine user={user} roles={roles} />
                  </span>
                </div>
              )}

              <ul className="space-y-1">
                {items.map(({ to, label, icon: Icon, end, description }) => (
                  <li key={to}>
                    <NavLink to={to} end={end} className={sheetLink}>
                      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                      <span className="min-w-0">
                        <span className="block font-semibold">{label}</span>
                        {description && <span className="block text-xs opacity-80">{description}</span>}
                      </span>
                    </NavLink>
                  </li>
                ))}
              </ul>

              {isAuthenticated && (
                <div className="mt-3 border-t border-white/10 pt-3">
                  <button
                    type="button"
                    onClick={() => {
                      setMobileOpen(false);
                      logout({ silent: false });
                    }}
                    className="flex w-full items-center gap-3 rounded-lg bg-rose-500/15 px-3 py-2.5 text-sm font-semibold text-rose-200 transition-colors hover:bg-rose-500/25"
                  >
                    <LogOut className="size-4" aria-hidden="true" />
                    Logout
                  </button>
                </div>
              )}
            </nav>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  );
}
