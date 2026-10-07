import { useCallback, useEffect, useState } from 'react';
import { MessageSquareText, Send } from 'lucide-react';
import { feedbackApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

const categories = ['Subject', 'Teacher', 'Marks'];

export default function StudentFeedback() {
  const [items, setItems] = useState([]);
  const [category, setCategory] = useState(categories[0]);
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const loadFeedback = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setItems(await feedbackApi.mine());
    } catch (loadError) {
      console.error('Could not load student feedback', {
        url: '/api/feedback',
        status: loadError?.response?.status ?? null,
      });
      setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadFeedback(); }, [loadFeedback]);

  async function submit(event) {
    event.preventDefault();
    if (message.trim().length < 5) {
      setError('Please provide at least five characters of feedback.');
      return;
    }
    setSaving(true);
    setError('');
    setNotice('');
    try {
      const created = await feedbackApi.submit({ category, message: message.trim() });
      setItems((current) => [created, ...current]);
      setMessage('');
      setNotice('Your feedback was submitted for review.');
    } catch (submitError) {
      console.error('Could not submit student feedback', {
        url: '/api/feedback',
        status: submitError?.response?.status ?? null,
      });
      setError(submitError.friendlyMessage ?? extractErrorMessage(submitError));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingPanel label="Loading your feedback…" />;
  if (error && items.length === 0) return <ErrorState title="Could not load feedback" message={error} onRetry={loadFeedback} />;

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      {error && <Alert variant="error">{error}</Alert>}
      {notice && <Alert variant="success" onDismiss={() => setNotice('')}>{notice}</Alert>}
      <section className="card p-6">
        <div className="mb-5 flex items-center gap-3">
          <span className="grid size-11 place-items-center rounded-xl bg-brand-100 text-brand-700"><MessageSquareText className="size-5" /></span>
          <div><p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Student voice</p><h1 className="text-2xl font-bold text-slate-900">Feedback &amp; ideas</h1></div>
        </div>
        <p className="mb-5 text-sm text-slate-600">Share thoughts about a subject, teacher, or academic marks. Your message is visible to school administrators.</p>
        <form onSubmit={submit} className="space-y-4">
          <label className="block space-y-1 text-sm font-medium text-slate-700">
            <span>Category</span>
            <select className="input" value={category} onChange={(event) => setCategory(event.target.value)}>
              {categories.map((item) => <option key={item}>{item}</option>)}
            </select>
          </label>
          <label className="block space-y-1 text-sm font-medium text-slate-700">
            <span>Your feedback</span>
            <textarea className="input min-h-32" value={message} onChange={(event) => setMessage(event.target.value)} maxLength={4000} required placeholder="Describe your suggestion or concern…" />
          </label>
          <div className="flex justify-end">
            <button className="btn-primary" type="submit" disabled={saving || message.trim().length < 5}>
              {saving ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />}
              {saving ? 'Submitting…' : 'Submit feedback'}
            </button>
          </div>
        </form>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-slate-900">Your submissions</h2>
        {items.length === 0 ? <div className="card p-5 text-sm text-slate-500">You have not submitted feedback yet.</div> : items.map((item) => (
          <article key={item.id} className="card p-5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="font-semibold text-slate-900">{item.category}</p>
              <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${item.status === 'Resolved' ? 'bg-emerald-100 text-emerald-800' : item.status === 'Acknowledged' ? 'bg-sky-100 text-sky-800' : 'bg-amber-100 text-amber-800'}`}>{item.status}</span>
            </div>
            <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">{item.message}</p>
            <p className="mt-3 text-xs text-slate-400">Submitted {new Date(item.submittedAt).toLocaleString()}</p>
          </article>
        ))}
      </section>
    </div>
  );
}
