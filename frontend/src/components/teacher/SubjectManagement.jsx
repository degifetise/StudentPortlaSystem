import { useEffect, useMemo, useState } from 'react';
import { BookMarked, Pencil, Plus, Save, Trash2 } from 'lucide-react';
import { gradeLevelApi, subjectApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Spinner } from '../ui/Feedback';

export default function SubjectManagement() {
  const [gradeLevels, setGradeLevels] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [form, setForm] = useState({ gradeLevelId: '', subjectName: '', code: '', description: '', creditHours: 3 });
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const loadData = async () => {
    try {
      const [grades, subjectList] = await Promise.all([gradeLevelApi.list(), subjectApi.list()]);
      setGradeLevels(grades);
      setSubjects(subjectList);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const grouped = useMemo(() => {
    return gradeLevels.map((grade) => ({
      ...grade,
      items: subjects.filter((subject) => Number(subject.gradeLevelId) === Number(grade.id)),
    }));
  }, [gradeLevels, subjects]);

  function resetForm() {
    setForm({ gradeLevelId: '', subjectName: '', code: '', description: '', creditHours: 3 });
    setEditingId(null);
  }

  async function submit(event) {
    event.preventDefault();
    if (!form.gradeLevelId || !form.subjectName.trim() || !form.code.trim()) {
      setError('Choose a grade, a subject name, and a subject code.');
      return;
    }

    setSaving(true);
    setError('');
    setNotice('');

    try {
      if (editingId) {
        await subjectApi.update(editingId, {
          subjectName: form.subjectName.trim(),
          code: form.code.trim(),
          gradeLevelId: Number(form.gradeLevelId),
          description: form.description.trim() || null,
          creditHours: Number(form.creditHours),
          isActive: true,
        });
        setNotice('Subject updated successfully.');
      } else {
        await subjectApi.create({
          gradeLevelId: Number(form.gradeLevelId),
          subjectName: form.subjectName.trim(),
          code: form.code.trim(),
          description: form.description.trim() || null,
          creditHours: Number(form.creditHours),
        });
        setNotice('New subject created successfully.');
      }

      resetForm();
      await loadData();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  function startEdit(subject) {
    setEditingId(subject.id);
    setForm({
      gradeLevelId: String(subject.gradeLevelId),
      subjectName: subject.subjectName,
      code: subject.code,
      description: subject.description ?? '',
      creditHours: subject.creditHours ?? 3,
    });
  }

  async function deleteSubject(subject) {
    if (!window.confirm(`Delete "${subject.subjectName}"? Subjects with teaching or academic records cannot be deleted.`)) {
      return;
    }

    setDeletingId(subject.id);
    setError('');
    setNotice('');
    try {
      await subjectApi.remove(subject.id);
      setNotice(`${subject.subjectName} was deleted.`);
      if (editingId === subject.id) resetForm();
      await loadData();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div className="space-y-6">
      <section className="card p-5">
        <div className="flex items-center gap-3">
          <span className="grid size-10 place-items-center rounded-lg bg-brand-100 text-brand-700">
            <BookMarked className="size-5" aria-hidden="true" />
          </span>
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Academic catalog</p>
            <h2 className="text-xl font-bold text-slate-900">Subject management</h2>
          </div>
        </div>

        {error && <div className="mt-4 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">{error}</div>}
        {notice && <div className="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{notice}</div>}

        <form onSubmit={submit} className="mt-5 grid gap-3 md:grid-cols-5">
          <select className="input" value={form.gradeLevelId} onChange={(event) => setForm((prev) => ({ ...prev, gradeLevelId: event.target.value }))}>
            <option value="">Grade level</option>
            {gradeLevels.map((grade) => <option key={grade.id} value={grade.id}>{grade.name}</option>)}
          </select>
          <input className="input" value={form.subjectName} onChange={(event) => setForm((prev) => ({ ...prev, subjectName: event.target.value }))} placeholder="Subject name" />
          <input className="input" value={form.code} onChange={(event) => setForm((prev) => ({ ...prev, code: event.target.value }))} placeholder="Code" />
          <input className="input" type="number" min="1" value={form.creditHours} onChange={(event) => setForm((prev) => ({ ...prev, creditHours: Number(event.target.value) || 1 }))} placeholder="Credit hours" />
          <button type="submit" className="btn-primary" disabled={saving}>
            {saving ? <Spinner className="size-4" /> : editingId ? <Save className="size-4" aria-hidden="true" /> : <Plus className="size-4" aria-hidden="true" />}
            {editingId ? 'Save' : 'Add subject'}
          </button>
          <textarea className="input md:col-span-5" rows="2" value={form.description} onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))} placeholder="Description (optional)" />
        </form>
      </section>

      <section className="space-y-4">
        {grouped.map((grade) => (
          <div key={grade.id} className="card overflow-hidden">
            <div className="border-b border-slate-200 bg-slate-50 px-5 py-3">
              <h3 className="font-semibold text-slate-900">{grade.name}</h3>
            </div>

            {grade.items.length === 0 ? (
              <p className="px-5 py-4 text-sm text-slate-500">No subjects added for this grade level yet.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th className="px-5 py-3">Subject</th>
                      <th className="px-5 py-3">Code</th>
                      <th className="px-5 py-3">Credit hours</th>
                      <th className="px-5 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {grade.items.map((subject) => (
                      <tr key={subject.id} className="border-t border-slate-100">
                        <td className="px-5 py-3">
                          <p className="font-medium text-slate-900">{subject.subjectName}</p>
                          <p className="text-xs text-slate-500">{subject.description || 'No description'}</p>
                        </td>
                        <td className="px-5 py-3 font-mono text-xs text-slate-600">{subject.code}</td>
                        <td className="px-5 py-3 text-slate-600">{subject.creditHours}</td>
                        <td className="px-5 py-3 text-right">
                          <div className="flex justify-end gap-2">
                            <button type="button" className="btn-secondary" onClick={() => startEdit(subject)}>
                              <Pencil className="size-4" aria-hidden="true" />
                              Edit
                            </button>
                            <button
                              type="button"
                              className="btn-danger"
                              onClick={() => deleteSubject(subject)}
                              disabled={deletingId === subject.id}
                              aria-label={`Delete ${subject.subjectName}`}
                            >
                              {deletingId === subject.id ? <Spinner className="size-4" /> : <Trash2 className="size-4" aria-hidden="true" />}
                              Delete
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ))}
      </section>
    </div>
  );
}
