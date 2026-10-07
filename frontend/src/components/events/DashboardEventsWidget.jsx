import { useCallback } from 'react';
import { CalendarDays, TrendingUp, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import { eventApi } from '../../services/endpoints';
import { useApiResource } from '../../hooks/useApiResource';
import { Skeleton } from '../ui/Feedback';

function shortDate(value) {
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(value));
}

export default function DashboardEventsWidget() {
  const fetchDashboardStats = useCallback(() => eventApi.dashboardStats(4), []);
  const { data, loading } = useApiResource(fetchDashboardStats);

  if (loading) return <div className="card mb-6 grid gap-4 p-5 md:grid-cols-3"><Skeleton className="h-16" /><Skeleton className="h-16" /><Skeleton className="h-16" /></div>;
  if (!data) return null;

  return (
    <section className="card mb-6 p-5">
      <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-xs font-semibold tracking-wide text-brand-700 uppercase">Event pulse</p><h2 className="mt-1 text-lg font-bold text-slate-900">What is happening next</h2></div><Link to="/events" className="text-sm font-semibold text-brand-700 hover:text-brand-900">View all</Link></div>
      <div className="mt-5 grid gap-4 md:grid-cols-3"><div className="rounded-lg bg-brand-50 p-4"><Users className="size-5 text-brand-600" /><p className="mt-3 text-2xl font-bold text-slate-900">{data.totalRegistrations}</p><p className="text-sm text-slate-600">Confirmed registrations</p></div><div className="md:col-span-2"><div className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-700"><CalendarDays className="size-4 text-brand-600" />Upcoming events</div><div className="grid gap-2 sm:grid-cols-2">{(data.upcomingEvents ?? []).slice(0, 4).map((event) => <Link key={event.id} to="/events" className="rounded-lg border border-slate-200 p-3 transition-colors hover:border-brand-300 hover:bg-brand-50"><p className="truncate text-sm font-semibold text-slate-900">{event.title}</p><p className="mt-1 text-xs text-slate-500">{shortDate(event.startDate)} · {event.currentRegistrationsCount} registered</p></Link>)}</div></div></div>
      {(data.popularEvents ?? []).length > 0 && <div className="mt-5 border-t border-slate-200 pt-4"><div className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-700"><TrendingUp className="size-4 text-brand-600" />Popular events</div><div className="flex flex-wrap gap-x-5 gap-y-2">{data.popularEvents.slice(0, 4).map((event) => <span key={event.id} className="text-sm text-slate-600">{event.title} <strong className="text-slate-900">{event.currentRegistrationsCount}</strong></span>)}</div></div>}
    </section>
  );
}