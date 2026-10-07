import { useCallback, useEffect, useState } from 'react';
import { feedbackApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, ErrorState, LoadingPanel } from '../../components/ui/Feedback';

const statuses = ['Pending Review', 'Acknowledged', 'Resolved'];

export default function AdminFeedback() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const loadItems = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setItems(await feedbackApi.list());
    } catch (loadError) {
      console.error('Could not load administrator feedback', {
        url: '/api/admin/feedback',
        status: loadError?.response?.status ?? null,
      });
      setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadItems(); }, [loadItems]);

  async function changeStatus(item, status) {
    try {
      const updated = await feedbackApi.updateStatus(item.id, status);
      setItems((current) => current.map((entry) => entry.id === item.id ? updated : entry));
    } catch (updateError) {
      setError(updateError.friendlyMessage ?? extractErrorMessage(updateError));
    }
  }

  if (loading) return <LoadingPanel label="Loading student feedback…" />;
  if (error && items.length === 0) return <ErrorState title="Could not load feedback" message={error} onRetry={loadItems} />;

  return (
    <div className="space-y-5">
      <header className="card p-5">
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Student support</p>
        <h1 className="mt-1 text-2xl font-bold text-slate-900">Feedback management</h1>
        <p className="mt-1 text-sm text-slate-600">Review student comments about subjects, teachers, and marks, then update their status.</p>
      </header>
      {error && <Alert variant="error">{error}</Alert>}
      {items.length === 0 ? <div className="card p-8 text-center text-sm text-slate-500">There is no student feedback to review.</div> : items.map((item) => (
        <article key={item.id} className="card p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="font-semibold text-slate-900">{item.studentName}</h2>
                <span className="text-xs text-slate-500">{item.studentIdNumber}</span>
                <span className="rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700">{item.category}</span>
              </div>
              <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-slate-700">{item.message}</p>
              <p className="mt-3 text-xs text-slate-400">Submitted {new Date(item.submittedAt).toLocaleString()}</p>
            </div>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              <span>Status</span>
              <select className="input min-w-40" value={item.status} onChange={(event) => changeStatus(item, event.target.value)}>
                {statuses.map((status) => <option key={status}>{status}</option>)}
              </select>
            </label>
          </div>
        </article>
      ))}
    </div>
  );
}
