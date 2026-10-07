import { useState } from 'react';
import { Spinner } from '../ui/Feedback';

const OFFICIAL_TYPES = ['Quiz', 'Test', 'MidExam', 'FinalExam'];
const ADMIN_TYPES = ['Assignment', 'Other'];

export default function AssessmentForm({
  subjects,
  sections,
  allowOther = false,
  forceSubjectSelector = false,
  defaultSubjectId = '',
  defaultSectionId = '',
  onSubmit,
}) {
  const [assessmentType, setAssessmentType] = useState('Quiz');
  const [assessmentName, setAssessmentName] = useState('');
  const [maxMarks, setMaxMarks] = useState('');
  const [subjectId, setSubjectId] = useState(String(defaultSubjectId));
  const [sectionId, setSectionId] = useState(String(defaultSectionId));
  const [dueDate, setDueDate] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function submit(event) {
    event.preventDefault();
    setSaving(true);
    setError('');
    setNotice('');

    try {
      await onSubmit({
        title: assessmentName.trim(),
        assessmentType,
        customTypeTitle: assessmentType === 'Other' ? assessmentName.trim() : null,
        maxScore: Number(maxMarks),
        subjectId: Number(subjectId),
        sectionId: sectionId ? Number(sectionId) : null,
        dueDate: dueDate || null,
      });
      setAssessmentName('');
      setMaxMarks('');
      setDueDate('');
      setNotice('Assessment created.');
    } catch (submitError) {
      setError(submitError.friendlyMessage ?? submitError.message ?? 'Assessment could not be created.');
    } finally {
      setSaving(false);
    }
  }

  const availableTypes = allowOther ? [...OFFICIAL_TYPES, ...ADMIN_TYPES] : OFFICIAL_TYPES;

  return (
    <form onSubmit={submit} className="space-y-4 border-t border-slate-200 p-5">
      <div className="grid gap-4 md:grid-cols-2">
        {subjects.length > 1 || forceSubjectSelector ? (
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Subject</span>
            <select className="input" value={subjectId} onChange={(event) => setSubjectId(event.target.value)} required>
              <option value="">Select subject</option>
              {subjects.map((subject) => (
                <option key={subject.id} value={subject.id}>{subject.code} · {subject.subjectName}</option>
              ))}
            </select>
          </label>
        ) : null}

        <label className="space-y-1 text-sm font-medium text-slate-700">
          <span>Assessment type</span>
          <select className="input" value={assessmentType} onChange={(event) => setAssessmentType(event.target.value)}>
            {availableTypes.map((type) => <option key={type} value={type}>{type}</option>)}
          </select>
        </label>

        <label className="space-y-1 text-sm font-medium text-slate-700">
          <span>{assessmentType === 'Other' ? 'Specify Assessment Name' : 'Assessment title'}</span>
          <input className="input" value={assessmentName} onChange={(event) => setAssessmentName(event.target.value)} maxLength={200} required placeholder={assessmentType === 'Other' ? 'e.g., Lab Report' : 'e.g., Quiz 1'} />
        </label>

        <label className="space-y-1 text-sm font-medium text-slate-700">
          <span>Max Marks / Out of</span>
          <input type="number" className="input" min="0.01" max="1000" step="0.01" value={maxMarks} onChange={(event) => setMaxMarks(event.target.value)} required placeholder="e.g., 10, 20, 50, 100" />
        </label>

        {sections.length > 0 && (
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Section</span>
            <select className="input" value={sectionId} onChange={(event) => setSectionId(event.target.value)}>
              <option value="">All sections</option>
              {sections.map((section) => <option key={section.id} value={section.id}>{section.name}</option>)}
            </select>
          </label>
        )}

        <label className="space-y-1 text-sm font-medium text-slate-700">
          <span>Due date</span>
          <input type="date" className="input" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
        </label>
      </div>

      {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
      {notice && <p role="status" className="text-sm text-emerald-700">{notice}</p>}
      <button type="submit" className="btn-primary" disabled={saving || !subjectId}>
        {saving ? <Spinner className="size-4" /> : null}
        Create assessment
      </button>
    </form>
  );
}
