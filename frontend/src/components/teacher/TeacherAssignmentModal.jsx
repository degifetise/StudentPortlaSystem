import { useEffect, useMemo, useState } from 'react';
import { BookOpen, LoaderCircle, Plus, X } from 'lucide-react';
import { gradeLevelApi, sectionApi, subjectApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Spinner } from '../ui/Feedback';

export default function TeacherAssignmentModal({ teacher, open, onClose, onSaved }) {
  const [gradeLevels, setGradeLevels] = useState([]);
  const [sections, setSections] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({ gradeLevelId: '', subjectId: '', sectionId: '' });

  useEffect(() => {
    if (!open) return;

    let cancelled = false;
    async function load() {
      setLoading(true);
      setError('');

      try {
        const [grades, sectionList, subjectList] = await Promise.all([
          gradeLevelApi.list(),
          sectionApi.list(),
          subjectApi.list(),
        ]);

        if (cancelled) return;

        setGradeLevels(grades);
        setSections(sectionList);
        setSubjects(subjectList);
      } catch (err) {
        if (!cancelled) setError(err.friendlyMessage ?? extractErrorMessage(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => { cancelled = true; };
  }, [open]);

  const gradeOptions = useMemo(() => {
    if (!form.gradeLevelId) return subjects;
    return subjects.filter((subject) => String(subject.gradeLevelId) === String(form.gradeLevelId));
  }, [form.gradeLevelId, subjects]);

  useEffect(() => {
    if (!form.gradeLevelId) {
      setForm((prev) => ({ ...prev, subjectId: '' }));
      return;
    }

    const matches = subjects.filter((subject) => String(subject.gradeLevelId) === String(form.gradeLevelId));
    if (matches.length && !matches.some((subject) => String(subject.id) === String(form.subjectId))) {
      setForm((prev) => ({ ...prev, subjectId: String(matches[0].id) }));
    }
  }, [form.gradeLevelId, form.subjectId, subjects]);

  if (!open || !teacher) return null;

  async function submit(event) {
    event.preventDefault();
    if (!form.subjectId || !form.sectionId) {
      setError('Choose a subject and section before saving.');
      return;
    }

    setSaving(true);
    setError('');

    try {
      const saved = await teacherApi.assign(teacher.id, {
        subjectId: Number(form.subjectId),
        sectionId: Number(form.sectionId),
      });
      onSaved?.(saved);
      onClose();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 bg-slate-950/45 p-4 backdrop-blur-sm">
      <div className="mx-auto mt-16 max-w-xl rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-brand-600">Teacher assignment</p>
            <h2 className="text-xl font-bold text-slate-900">{teacher.fullName}</h2>
          </div>
          <button type="button" className="rounded-lg p-2 text-slate-500 hover:bg-slate-100" onClick={onClose}>
            <X className="size-5" aria-hidden="true" />
            <span className="sr-only">Close</span>
          </button>
        </div>

        <form onSubmit={submit} className="space-y-4 p-5">
          {error && (
            <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">{error}</div>
          )}

          {loading ? (
            <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
              Loading subjects and class list…
            </div>
          ) : (
            <>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 text-sm font-medium text-slate-700">
                  <span>Grade level</span>
                  <select
                    className="input"
                    value={form.gradeLevelId}
                    onChange={(event) => setForm((prev) => ({ ...prev, gradeLevelId: event.target.value }))}
                  >
                    <option value="">Select grade</option>
                    {gradeLevels.map((grade) => (
                      <option key={grade.id} value={grade.id}>{grade.name}</option>
                    ))}
                  </select>
                </label>

                <label className="space-y-1 text-sm font-medium text-slate-700">
                  <span>Section</span>
                  <select
                    className="input"
                    value={form.sectionId}
                    onChange={(event) => setForm((prev) => ({ ...prev, sectionId: event.target.value }))}
                  >
                    <option value="">Select section</option>
                    {sections.map((section) => (
                      <option key={section.id} value={section.id}>{section.name}</option>
                    ))}
                  </select>
                </label>
              </div>

              <label className="block space-y-1 text-sm font-medium text-slate-700">
                <span>Subject</span>
                <select
                  className="input"
                  value={form.subjectId}
                  onChange={(event) => setForm((prev) => ({ ...prev, subjectId: event.target.value }))}
                  disabled={!form.gradeLevelId}
                >
                  <option value="">{form.gradeLevelId ? 'Select subject' : 'Choose a grade first'}</option>
                  {gradeOptions.map((subject) => (
                    <option key={subject.id} value={subject.id}>{subject.subjectName}</option>
                  ))}
                </select>
              </label>
            </>
          )}

          <div className="flex items-center justify-end gap-2 border-t border-slate-200 pt-4">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={saving}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={saving || loading || !form.subjectId || !form.sectionId}>
              {saving ? <Spinner className="size-4" /> : <Plus className="size-4" aria-hidden="true" />}
              Save assignment
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
