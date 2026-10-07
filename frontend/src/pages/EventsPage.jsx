import { useCallback, useEffect, useState } from 'react';
import { CalendarDays, MapPin, Pencil, Plus, RefreshCw, Search, Trash2, Users, X } from 'lucide-react';
import { motion } from 'framer-motion';
import { eventApi } from '../services/endpoints';
import { useAuth } from '../context/AuthContext';
import { useApiResource } from '../hooks/useApiResource';
import { Alert, EmptyState, ErrorState, Skeleton, Spinner } from '../components/ui/Feedback';
import EventCreationForm from '../components/events/EventCreationForm';
import EventComments from '../components/events/EventComments';

const dateTimeFormat = { dateStyle: 'medium', timeStyle: 'short' };
const categories = ['All', 'Academic', 'Sports', 'Arts', 'Community', 'General'];

function formatDate(value) {
  return new Intl.DateTimeFormat(undefined, dateTimeFormat).format(new Date(value));
}

function statusClass(status) {
  return {
    Upcoming: 'bg-emerald-100 text-emerald-800',
    Ongoing: 'bg-sky-100 text-sky-800',
    Completed: 'bg-slate-100 text-slate-700',
    Canceled: 'bg-rose-100 text-rose-800',
  }[status] ?? 'bg-slate-100 text-slate-700';
}

function EventCard({ event, canComment, isAdmin, userRole, onEdit, onDelete, onStatusChange }) {
  const capacity = event.maxAttendees;
  const count = event.currentRegistrationsCount ?? 0;
  const percentage = capacity ? Math.min(100, Math.round((count / capacity) * 100)) : 0;

  return (
    <motion.article initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="card overflow-hidden">
      {event.bannerUrl ? <img src={event.bannerUrl} alt="" className="h-44 w-full object-cover" /> : <div className="h-3 bg-brand-600" />}
      <div className="space-y-4 p-5">
        <div className="flex items-start justify-between gap-3"><div><p className="text-xs font-semibold tracking-wide text-brand-700 uppercase">{event.category}</p><h2 className={`mt-1 text-xl font-bold text-slate-900 ${event.status === 'Canceled' ? 'line-through' : ''}`}>{event.title}</h2></div><span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${statusClass(event.status)}`}>{event.status === 'Ongoing' && <span className="size-1.5 animate-pulse rounded-full bg-current" aria-hidden="true" />}{event.status}</span></div>
        {event.description && <p className="line-clamp-3 text-sm leading-6 text-slate-600">{event.description}</p>}
        <div className="space-y-2 text-sm text-slate-600"><p className="flex items-center gap-2"><CalendarDays className="size-4 text-brand-600" />{formatDate(event.startDate)}</p>{event.location && <p className="flex items-center gap-2"><MapPin className="size-4 text-brand-600" />{event.location}</p>}{event.organizer && <p className="flex items-center gap-2"><Users className="size-4 text-brand-600" />{event.organizer}</p>}</div>
        {capacity && <div><div className="mb-1 flex justify-between text-xs text-slate-500"><span>{count} registered</span><span>{Math.max(0, capacity - count)} seats left</span></div><div className="h-2 overflow-hidden rounded-full bg-slate-200"><div className="h-full rounded-full bg-brand-600 transition-all" style={{ width: `${percentage}%` }} /></div></div>}
        {canComment && Number.isInteger(Number(event.id)) && Number(event.id) > 0 && (
          <EventComments eventId={event.id} canComment isAdmin={isAdmin} userRole={userRole} />
        )}
        {isAdmin && <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4"><button type="button" onClick={() => onEdit(event)} className="btn-secondary flex-1"><Pencil className="size-4" />Edit</button>{event.status !== 'Completed' && event.status !== 'Canceled' && <button type="button" onClick={() => onStatusChange(event, 'Completed')} className="btn-secondary flex-1">Mark completed</button>}{event.status !== 'Canceled' && <button type="button" onClick={() => onStatusChange(event, 'Canceled')} className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg border border-rose-200 px-4 py-2 text-sm font-semibold text-rose-700 transition-colors hover:bg-rose-50">Cancel event</button>}<button type="button" onClick={() => onDelete(event)} className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg border border-rose-200 px-4 py-2 text-sm font-semibold text-rose-700 transition-colors hover:bg-rose-50"><Trash2 className="size-4" />Delete</button></div>}
      </div>
    </motion.article>
  );
}

export default function EventsPage() {
  const { hasRole } = useAuth();
  const [filters, setFilters] = useState({ page: 1, pageSize: 12, search: '', category: '', status: '' });
  const [editingEvent, setEditingEvent] = useState(null);
  const [deletingEvent, setDeletingEvent] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [actionNotice, setActionNotice] = useState(null);
  const [events, setEvents] = useState([]);
  const [hiddenEventIds, setHiddenEventIds] = useState(() => new Set());
  const fetchEvents = useCallback(() => eventApi.list(filters), [filters]);
  const { data, error, loading, reload, reloading } = useApiResource(fetchEvents, [filters]);
  const isAdmin = hasRole('Admin');
  const isTeacher = hasRole('Teacher');
  const isStudent = hasRole('Student');
  const canComment = isStudent || isTeacher || isAdmin;
  const userRole = isAdmin ? 'Admin' : isTeacher ? 'Teacher' : 'Student';

  useEffect(() => {
    if (data?.items) {
      setEvents(data.items.filter((event) => !hiddenEventIds.has(event.id)));
    }
  }, [data, hiddenEventIds]);

  const updateFilters = (updates) => {
    setFilters((current) => ({ ...current, ...updates, page: 1 }));
  };

  const handleCreate = async (payload) => {
    try {
      await eventApi.create({ title: payload.title, description: payload.description, category: payload.category, location: payload.venue, startDate: new Date(`${payload.startDate}T${payload.startTime}`).toISOString(), endDate: new Date(`${payload.endDate}T${payload.endTime}`).toISOString(), maxAttendees: payload.capacity, targetStakeholders: payload.targetStakeholders, targetGradeIds: payload.targetGradeIds, targetSectionIds: payload.targetSectionIds });
      setIsCreateOpen(false);
      setActionNotice({ variant: 'success', message: 'Event created successfully.' });
      await reload();
    } catch (createError) {
      setActionNotice({ variant: 'error', message: createError.friendlyMessage ?? 'Event could not be created.' });
      throw createError;
    }
  };

  const handleUpdate = async (payload) => {
    try {
      await eventApi.update(editingEvent.id, { title: payload.title, description: payload.description, category: payload.category, location: payload.venue, startDate: new Date(`${payload.startDate}T${payload.startTime}`).toISOString(), endDate: new Date(`${payload.endDate}T${payload.endTime}`).toISOString(), maxAttendees: payload.capacity, isActive: payload.isActive, targetStakeholders: payload.targetStakeholders, targetGradeIds: payload.targetGradeIds, targetSectionIds: payload.targetSectionIds });
      setEditingEvent(null);
      setActionNotice({ variant: 'success', message: 'Event updated successfully.' });
      await reload();
    } catch (updateError) {
      setActionNotice({ variant: 'error', message: updateError.friendlyMessage ?? 'Event could not be updated.' });
      throw updateError;
    }
  };

  const handleStatusChange = async (event, status) => {
    const action = status === 'Canceled' ? 'cancel' : 'mark as completed';
    if (!window.confirm(`Are you sure you want to ${action} "${event.title}"?`)) return;

    try {
      const updated = await eventApi.updateStatus(event.id, status);
      setEvents((currentEvents) => currentEvents.map((currentEvent) => currentEvent.id === event.id ? updated : currentEvent));
      setActionNotice({ variant: 'success', message: status === 'Canceled' ? 'Event canceled successfully.' : 'Event marked as completed.' });
      await reload();
    } catch (statusError) {
      setActionNotice({ variant: 'error', message: statusError.friendlyMessage ?? 'Event status could not be updated.' });
    }
  };

  const handleDelete = async () => {
    if (!deletingEvent) return;

    setIsDeleting(true);
    try {
      const deletedId = deletingEvent.id;
      await eventApi.remove(deletedId);
      setEvents((currentEvents) => currentEvents.filter((event) => event.id !== deletedId));
      setHiddenEventIds((currentIds) => new Set(currentIds).add(deletedId));
      setDeletingEvent(null);
      setActionNotice({ variant: 'success', message: 'Event deleted successfully.' });
      await reload();
    } catch (deleteError) {
      setActionNotice({ variant: 'error', message: deleteError.friendlyMessage ?? 'Event could not be deleted.' });
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      <header className="mb-8 flex flex-wrap items-end justify-between gap-4"><div><p className="text-sm font-semibold tracking-wide text-brand-700 uppercase">School events</p><h1 className="mt-2 text-3xl font-bold text-slate-900">Explore events</h1><p className="mt-2 text-slate-600">Browse school activities, event details, and student questions.</p></div><div className="flex gap-3">{isAdmin && <button type="button" onClick={() => setIsCreateOpen(true)} className="btn-primary"><Plus className="size-4" />Create event</button>}<button type="button" onClick={reload} className="btn-secondary" disabled={loading || reloading}>{reloading ? <Spinner className="size-4" /> : <RefreshCw className="size-4" />}Refresh</button></div></header>
      {actionNotice && <Alert variant={actionNotice.variant} onDismiss={() => setActionNotice(null)} className="mb-6">{actionNotice.message}</Alert>}
      <div className="mb-7 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between"><label className="relative block max-w-md flex-1"><Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" /><input className="input pl-9" placeholder="Search events" value={filters.search} onChange={(e) => updateFilters({ search: e.target.value })} /></label><div className="flex flex-wrap gap-2">{categories.map((category) => <button type="button" key={category} onClick={() => updateFilters({ category: category === 'All' ? '' : category })} className={`rounded-full px-3 py-1.5 text-sm font-semibold ${filters.category === (category === 'All' ? '' : category) ? 'bg-brand-600 text-white' : 'bg-white text-slate-600 ring-1 ring-slate-200'}`}>{category}</button>)}<select className="input w-auto" value={filters.status} onChange={(e) => updateFilters({ status: e.target.value })}><option value="">All statuses</option><option>Upcoming</option><option>Ongoing</option><option>Completed</option><option>Canceled</option></select></div></div>
      {error ? <ErrorState title="Could not load events" message={error} onRetry={reload} retrying={reloading} /> : loading ? <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">{[1, 2, 3].map((item) => <div className="card space-y-4 p-5" key={item}><Skeleton className="h-3 w-full" /><Skeleton className="h-6 w-2/3" /><Skeleton className="h-20 w-full" /></div>)}</div> : events.length === 0 ? <EmptyState icon={CalendarDays} title="No events found" description="Try another search or category." /> : <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">{events.map((event) => <EventCard key={event.id} event={event} canComment={canComment} isAdmin={isAdmin} userRole={userRole} onEdit={setEditingEvent} onDelete={setDeletingEvent} onStatusChange={handleStatusChange} />)}</div>}
      {isCreateOpen && <EventCreationForm isOpen onClose={() => setIsCreateOpen(false)} onSubmit={handleCreate} />}
      {editingEvent && <EventCreationForm event={editingEvent} isOpen onClose={() => setEditingEvent(null)} onSubmit={handleUpdate} />}
      {deletingEvent && <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/50 p-4"><div className="card w-full max-w-md p-6"><div className="flex items-start justify-between"><div><p className="text-xs font-semibold text-rose-700 uppercase">Delete event</p><h2 className="mt-1 text-xl font-bold text-slate-900">{deletingEvent.title}</h2></div><button type="button" className="rounded-full p-2 text-slate-500 hover:bg-slate-100" onClick={() => setDeletingEvent(null)} aria-label="Close"><X className="size-5" /></button></div><p className="mt-4 text-sm text-slate-600">Delete this event? It will no longer be published or available for registration.</p><div className="mt-6 flex justify-end gap-3"><button type="button" className="btn-secondary" onClick={() => setDeletingEvent(null)} disabled={isDeleting}>Cancel</button><button type="button" className="inline-flex items-center justify-center gap-2 rounded-lg bg-rose-600 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-700 disabled:opacity-60" onClick={handleDelete} disabled={isDeleting}>{isDeleting ? <Spinner className="size-4" /> : <Trash2 className="size-4" />}Delete event</button></div></div></div>}
    </div>
  );
}