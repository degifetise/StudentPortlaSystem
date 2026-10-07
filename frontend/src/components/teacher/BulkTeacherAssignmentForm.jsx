import { useEffect, useMemo, useState } from 'react';
import { bulkAdminApi, gradeLevelApi, sectionApi, subjectApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Spinner } from '../ui/Feedback';

export default function BulkTeacherAssignmentForm({ teachers, onSaved }) {
  const [grades, setGrades] = useState([]);
  const [sections, setSections] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [teacherId, setTeacherId] = useState('');
  const [gradeLevelId, setGradeLevelId] = useState('');
  const [sectionId, setSectionId] = useState('');
  const [subjectIds, setSubjectIds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let cancelled = false;
    Promise.all([gradeLevelApi.list(), sectionApi.list(), subjectApi.list()])
      .then(([gradeList, sectionList, subjectList]) => {
        if (cancelled) return;
        setGrades(gradeList);
        setSections(sectionList);
        setSubjects(subjectList);
      })
      .catch((loadError) => {
        if (!cancelled) setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const gradeSubjects = useMemo(
    () => gradeLevelId === 'all'
      ? subjects
      : subjects.filter((subject) => String(subject.gradeLevelId) === String(gradeLevelId)),
    [gradeLevelId, subjects],
  );
  const selectedSections = sectionId === 'all'
    ? sections
    : sections.filter((section) => String(section.id) === String(sectionId));

  useEffect(() => {
    setSubjectIds((current) => current.filter((id) => gradeSubjects.some((subject) => subject.id === id)));
  }, [gradeSubjects]);

  async function submit(event) {
    event.preventDefault();
    if (!teacherId || !gradeLevelId || subjectIds.length === 0 || selectedSections.length === 0) {
      setError('Choose a teacher, grade scope, at least one subject, and section scope.');
      return;
    }

    setSaving(true);
    setError('');
    setNotice('');
    try {
      const result = await bulkAdminApi.assignTeacher({
        teacherId: Number(teacherId),
        subjectIds,
        sectionIds: selectedSections.map((section) => section.id),
      });
      setNotice(`Created ${result.created}, reactivated ${result.reactivated}, already active ${result.alreadyActive} assignment(s).`);
      await onSaved?.();
    } catch (saveError) {
      setError(saveError.friendlyMessage ?? extractErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="card space-y-4 p-5">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Bulk setup</p>
        <h2 className="text-lg font-bold text-slate-900">Assign teacher across classes</h2>
        <p className="mt-1 text-sm text-slate-600">Select one or more subjects and apply them to one section or all sections.</p>
      </div>
      {error && <Alert variant="error">{error}</Alert>}
      {notice && <Alert variant="success">{notice}</Alert>}
      {loading ? <p className="text-sm text-slate-500">Loading grades, subjects, and sections…</p> : (
        <form onSubmit={submit} className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className="space-y-1 text-sm font-medium text-slate-700">
              <span>Teacher</span>
              <select className="input" value={teacherId} onChange={(event) => setTeacherId(event.target.value)} required>
                <option value="">Select teacher</option>
                {teachers.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
              </select>
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              <span>Grade</span>
              <select className="input" value={gradeLevelId} onChange={(event) => setGradeLevelId(event.target.value)} required>
                <option value="">Select grade</option>
                <option value="all">All Grades</option>
                {grades.map((grade) => <option key={grade.id} value={grade.id}>{grade.name}</option>)}
              </select>
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              <span>Sections</span>
              <select className="input" value={sectionId} onChange={(event) => setSectionId(event.target.value)} required>
                <option value="">Select section</option>
                <option value="all">All Sections</option>
                {sections.map((section) => <option key={section.id} value={section.id}>{section.name}</option>)}
              </select>
            </label>
            <div className="flex items-end">
              <button type="submit" className="btn-primary w-full" disabled={saving || loading}>
                {saving ? <Spinner className="size-4" /> : null}
                {saving ? 'Saving assignments…' : 'Assign selected'}
              </button>
            </div>
          </div>

          {gradeLevelId && (
            <fieldset className="rounded-lg border border-slate-200 p-3">
              <legend className="px-1 text-sm font-semibold text-slate-700">Subjects</legend>
              {gradeSubjects.length ? (
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                  {gradeSubjects.map((subject) => (
                    <label key={subject.id} className="flex items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={subjectIds.includes(subject.id)}
                        onChange={(event) => setSubjectIds((current) => event.target.checked
                          ? [...current, subject.id]
                          : current.filter((id) => id !== subject.id))}
                      />
                      {subject.subjectName}
                    </label>
                  ))}
                </div>
              ) : <p className="text-sm text-slate-500">No active subjects are available for this grade.</p>}
            </fieldset>
          )}
        </form>
      )}
    </section>
  );
}
