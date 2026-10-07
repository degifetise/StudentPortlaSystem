import { useEffect, useState } from 'react';
import { gradeLevelApi, sectionApi } from '../../services/endpoints';
import {
  Calendar,
  CheckSquare,
  Clock,
  MapPin,
  Users,
  X,
} from 'lucide-react';

const audienceOptions = [
  'All',
  'Students',
  'Teachers',
];

const allGradesKey = 'All Grades';
const categoryOptions = ['Academic', 'Sports', 'Arts', 'Community', 'General'];

const initialForm = {
  title: '',
  description: '',
  category: 'General',
  startDate: '',
  startTime: '',
  endDate: '',
  endTime: '',
  venue: '',
  capacity: '',
  isActive: true,
  audience: audienceOptions[0],
  gradeScope: { [allGradesKey]: true },
  allSections: true,
  targetSectionIds: [],
};

function toDateInput(value) {
  return value ? new Date(value).toISOString().slice(0, 10) : '';
}

function toTimeInput(value) {
  return value ? new Date(value).toISOString().slice(11, 16) : '';
}

function formFromEvent(event, grades = []) {
  if (!event) return initialForm;
  const selectedGradeIds = event.targetGradeIds?.length
    ? event.targetGradeIds
    : event.gradeLevelId ? [event.gradeLevelId] : [];
  return {
    ...initialForm,
    title: event.title ?? '',
    description: event.description ?? '',
    category: event.category ?? 'General',
    startDate: toDateInput(event.startDate),
    startTime: toTimeInput(event.startDate),
    endDate: toDateInput(event.endDate),
    endTime: toTimeInput(event.endDate),
    venue: event.location ?? '',
    capacity: event.maxAttendees ?? '',
    isActive: event.isActive,
    audience: event.targetStakeholders ?? 'All',
    gradeScope: {
      [allGradesKey]: selectedGradeIds.length === 0,
      ...Object.fromEntries(grades.map((grade) => [
        String(grade.id),
        selectedGradeIds.includes(grade.id),
      ])),
    },
    allSections: !event.targetSectionIds?.length,
    targetSectionIds: event.targetSectionIds ?? [],
  };
}

function getGradeScopeList(gradeScope, grades) {
  if (gradeScope[allGradesKey]) return [allGradesKey];
  return grades
    .filter((grade) => gradeScope[String(grade.id)])
    .map((grade) => grade.name);
}

function getGradeIds(gradeScope, grades) {
  if (gradeScope[allGradesKey]) return [];
  return grades
    .filter((grade) => gradeScope[String(grade.id)])
    .map((grade) => grade.id);
}

export default function EventCreationForm({ event = null, isOpen = true, onClose = () => {}, onSubmit = async () => {} }) {
  const isEditing = Boolean(event);
  const [form, setForm] = useState(() => formFromEvent(event));
  const [errors, setErrors] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');
  const [grades, setGrades] = useState([]);
  const [sections, setSections] = useState([]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([gradeLevelApi.list(), sectionApi.list()])
      .then(([gradeList, sectionList]) => {
        if (cancelled) return;
        setGrades(Array.isArray(gradeList) ? gradeList : []);
        setSections(Array.isArray(sectionList) ? sectionList : []);
      })
      .catch(() => {
        if (!cancelled) {
          setGrades([]);
          setSections([]);
        }
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    setForm(formFromEvent(event));
    setErrors({});
    setSuccessMessage('');
  }, [event]);

  useEffect(() => {
    if (!event || grades.length === 0) return;
    setForm((current) => ({
      ...current,
      gradeScope: formFromEvent(event, grades).gradeScope,
    }));
  }, [event, grades]);

  if (!isOpen) return null;

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined, gradeScope: undefined }));
  };

  const toggleGrade = (gradeKey) => {
    setForm((current) => {
      const next = { ...current.gradeScope };

      if (gradeKey === allGradesKey) {
        const active = !next[allGradesKey];
        return {
          ...current,
          gradeScope: {
            [allGradesKey]: active,
            ...Object.fromEntries(grades.map((grade) => [String(grade.id), false])),
          },
          allSections: true,
          targetSectionIds: [],
        };
      }

      const nextValue = !next[gradeKey];
      next[gradeKey] = nextValue;

      if (nextValue) {
        next[allGradesKey] = false;
      } else if (!grades.some((grade) => next[String(grade.id)])) {
        next[allGradesKey] = true;
      }

      return { ...current, gradeScope: next };
    });

    setErrors((current) => ({ ...current, gradeScope: undefined }));
  };

  const validateForm = () => {
    const nextErrors = {};

    if (!form.title.trim()) {
      nextErrors.title = 'Event title is required.';
    } else if (form.title.trim().length > 150) {
      nextErrors.title = 'Event title cannot exceed 150 characters.';
    }

    if (!form.description.trim()) {
      nextErrors.description = 'Please add a brief description or agenda.';
    }

    if (!form.startDate || !form.startTime) {
      nextErrors.startDate = 'Start date and time are required.';
    }

    if (!form.endDate || !form.endTime) {
      nextErrors.endDate = 'End date and time are required.';
    }

    if (form.startDate && form.startTime && form.endDate && form.endTime) {
      const startDateTime = new Date(`${form.startDate}T${form.startTime}`);
      const endDateTime = new Date(`${form.endDate}T${form.endTime}`);

      if (Number.isNaN(startDateTime.getTime()) || Number.isNaN(endDateTime.getTime())) {
        nextErrors.endDate = 'Please choose valid date and time values.';
      } else if (endDateTime <= startDateTime) {
        nextErrors.endDate = 'End time must be after the start time.';
      }
    }

    if (!form.venue.trim()) {
      nextErrors.venue = 'Venue is required.';
    }

    if (form.capacity !== '' && Number(form.capacity) <= 0) {
      nextErrors.capacity = 'Capacity must be greater than zero when set.';
    }

    const selectedGrades = getGradeScopeList(form.gradeScope, grades);
    if (selectedGrades.length === 0) {
      nextErrors.gradeScope = 'Select at least one grade scope.';
    }

    if (selectedGrades.some((grade) => grade !== allGradesKey) && !form.allSections && form.targetSectionIds.length === 0) {
      nextErrors.sections = 'Select at least one section or choose all sections.';
    }

    if (!form.audience) {
      nextErrors.audience = 'Please choose a target audience.';
    }

    return nextErrors;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const nextErrors = validateForm();
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);
    setSuccessMessage('');

    const payload = {
      ...form,
      title: form.title.trim(),
      description: form.description.trim(),
      capacity: form.capacity === '' ? null : Number(form.capacity),
      gradeScope: getGradeScopeList(form.gradeScope, grades),
      targetStakeholders: form.audience,
      targetGradeIds: getGradeIds(form.gradeScope, grades),
      targetSectionIds: form.allSections || form.gradeScope[allGradesKey] ? [] : form.targetSectionIds,
    };

    try {
      await onSubmit(payload);
      setSuccessMessage(isEditing ? 'Event updated successfully.' : 'Event published successfully.');
      setForm(initialForm);
      setErrors({});
    } catch (err) {
      console.error('Event creation error:', err);
      setErrors((current) => ({
        ...current,
        submit: err?.response?.data?.message ??
          err?.response?.data?.detail ??
          err?.friendlyMessage ??
          err?.message ??
          'Failed to create event.',
      }));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-4 backdrop-blur-sm">
      <div className="w-full max-w-4xl overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl ring-1 ring-black/5">
        <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-5 py-4 sm:px-6">
          <div>
            <p className="text-xs font-semibold tracking-[0.2em] text-brand-700 uppercase">School events</p>
            <h2 className="mt-1 text-2xl font-bold text-slate-900">{isEditing ? 'Edit Event' : 'Create Event'}</h2>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="inline-flex size-10 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-600 transition-colors hover:border-slate-300 hover:text-slate-900"
            aria-label="Close event form"
          >
            <X className="size-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="max-h-[82vh] overflow-y-auto p-5 sm:p-6">
          <div className="space-y-8">
            {errors.submit && (
              <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
                {errors.submit}
              </div>
            )}
            <section className="space-y-5 rounded-2xl border border-slate-200 bg-slate-50/80 p-5">
              <div className="flex items-center gap-3">
                <div className="grid size-10 place-items-center rounded-xl bg-brand-100 text-brand-700">
                  <Calendar className="size-5" />
                </div>
                <h3 className="text-lg font-semibold text-slate-900">Core event metadata</h3>
              </div>

              <div className="grid gap-5 md:grid-cols-2">
                <div className="md:col-span-2">
                  <label htmlFor="event-title" className="label">
                    Event title
                  </label>
                  <input
                    id="event-title"
                    type="text"
                    value={form.title}
                    onChange={(event) => updateField('title', event.target.value)}
                    maxLength={150}
                    placeholder="Annual Science Fair 2026"
                    className={errors.title ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                  <div className="mt-2 flex items-center justify-between gap-3 text-xs">
                    <span className="text-rose-600">{errors.title || null}</span>
                    <span className="text-slate-500">{form.title.length}/150</span>
                  </div>
                </div>

                <div className="md:col-span-2">
                  <label htmlFor="event-description" className="label">
                    Description / agenda
                  </label>
                  <textarea
                    id="event-description"
                    rows={5}
                    value={form.description}
                    onChange={(event) => updateField('description', event.target.value)}
                    placeholder="Share the purpose, agenda, highlights, and expected outcomes."
                    className={errors.description ? 'input min-h-[120px] resize-y border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input min-h-[120px] resize-y'}
                  />
                  <div className="mt-2 flex items-center justify-between gap-3 text-xs">
                    <span className="text-rose-600">{errors.description || null}</span>
                    <span className="text-slate-500">{form.description.length} chars</span>
                  </div>
                </div>

                <div className="md:col-span-2">
                  <label htmlFor="event-category" className="label">
                    Category
                  </label>
                  <select
                    id="event-category"
                    value={form.category}
                    onChange={(event) => updateField('category', event.target.value)}
                    className="input"
                  >
                    {categoryOptions.map((category) => (
                      <option key={category} value={category}>
                        {category}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="start-date" className="label">
                    Start date
                  </label>
                  <input
                    id="start-date"
                    type="date"
                    value={form.startDate}
                    onChange={(event) => updateField('startDate', event.target.value)}
                    className={errors.startDate ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                </div>

                <div>
                  <label htmlFor="start-time" className="label">
                    Start time
                  </label>
                  <input
                    id="start-time"
                    type="time"
                    value={form.startTime}
                    onChange={(event) => updateField('startTime', event.target.value)}
                    className={errors.startDate ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                </div>

                <div>
                  <label htmlFor="end-date" className="label">
                    End date
                  </label>
                  <input
                    id="end-date"
                    type="date"
                    value={form.endDate}
                    onChange={(event) => updateField('endDate', event.target.value)}
                    className={errors.endDate ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                </div>

                <div>
                  <label htmlFor="end-time" className="label">
                    End time
                  </label>
                  <input
                    id="end-time"
                    type="time"
                    value={form.endTime}
                    onChange={(event) => updateField('endTime', event.target.value)}
                    className={errors.endDate ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                </div>

                {errors.startDate || errors.endDate ? (
                  <div className="md:col-span-2 text-xs text-rose-600">
                    {errors.startDate || errors.endDate}
                  </div>
                ) : null}

                <div className="md:col-span-2">
                  <label htmlFor="event-venue" className="label">
                    Venue / location
                  </label>
                  <div className="relative">
                    <MapPin className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
                    <input
                      id="event-venue"
                      type="text"
                      value={form.venue}
                      onChange={(event) => updateField('venue', event.target.value)}
                      placeholder="Main Auditorium, Science Lab 2, Sports Field"
                      className={errors.venue ? 'input pl-9 border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input pl-9'}
                    />
                  </div>
                  {errors.venue ? <p className="mt-2 text-xs text-rose-600">{errors.venue}</p> : null}
                </div>
              </div>
            </section>

            <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5">
              <div className="flex items-center gap-3">
                <div className="grid size-10 place-items-center rounded-xl bg-emerald-100 text-emerald-700">
                  <Users className="size-5" />
                </div>
                <h3 className="text-lg font-semibold text-slate-900">Capacity & participation</h3>
              </div>

              <div className="grid gap-5 md:grid-cols-2">
                <div>
                  <label htmlFor="event-capacity" className="label">
                    Maximum capacity
                  </label>
                  <input
                    id="event-capacity"
                    type="number"
                    min="1"
                    placeholder="Unlimited"
                    value={form.capacity}
                    onChange={(event) => updateField('capacity', event.target.value)}
                    className={errors.capacity ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  />
                  {errors.capacity ? <p className="mt-2 text-xs text-rose-600">{errors.capacity}</p> : null}
                </div>

                <div className="flex items-end">
                  <div className="w-full rounded-xl border border-slate-200 bg-slate-50 p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="text-sm font-semibold text-slate-900">Published / active</p>
                        <p className="text-xs text-slate-500">Keep this event visible and open for registration</p>
                      </div>

                      <button
                        type="button"
                        onClick={() => updateField('isActive', !form.isActive)}
                        className={`relative inline-flex h-7 w-12 items-center rounded-full transition-colors ${
                          form.isActive ? 'bg-brand-600' : 'bg-slate-300'
                        }`}
                        aria-label="Toggle RSVP"
                        role="switch"
                        aria-checked={form.isActive}
                      >
                        <span
                          className={`inline-block size-5 rounded-full bg-white shadow-sm transition-transform ${
                            form.isActive ? 'translate-x-6' : 'translate-x-1'
                          }`}
                        />
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <section className="space-y-5 rounded-2xl border border-slate-200 bg-slate-50/80 p-5">
              <div className="flex items-center gap-3">
                <div className="grid size-10 place-items-center rounded-xl bg-violet-100 text-violet-700">
                  <CheckSquare className="size-5" />
                </div>
                <h3 className="text-lg font-semibold text-slate-900">Audience & grade eligibility</h3>
              </div>

              <div className="grid gap-5 md:grid-cols-2">
                <div>
                  <label htmlFor="audience" className="label">
                    Target stakeholders
                  </label>
                  <select
                    id="audience"
                    value={form.audience}
                    onChange={(event) => updateField('audience', event.target.value)}
                    className={errors.audience ? 'input border-rose-300 focus:border-rose-500 focus:ring-rose-200' : 'input'}
                  >
                    {audienceOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                  {errors.audience ? <p className="mt-2 text-xs text-rose-600">{errors.audience}</p> : null}
                </div>

                <div className="md:col-span-2">
                  <label className="label">Target grade scope</label>
                  <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                    {[{ id: allGradesKey, name: allGradesKey }, ...grades.map((grade) => ({
                      id: String(grade.id),
                      name: grade.name,
                    }))].map((grade) => {
                      const isSelected = form.gradeScope[grade.id];

                      return (
                        <label
                          key={grade.id}
                          className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 transition-colors ${
                            isSelected
                              ? 'border-brand-200 bg-brand-50 text-brand-800'
                              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
                          }`}
                        >
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => toggleGrade(grade.id)}
                            className="size-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                          />
                          <span className="text-sm font-medium">{grade.name}</span>
                        </label>
                      );
                    })}
                  </div>
                  {errors.gradeScope ? <p className="mt-2 text-xs text-rose-600">{errors.gradeScope}</p> : null}
                </div>

                {grades.some((grade) => form.gradeScope[String(grade.id)]) && (
                  <div className="md:col-span-2">
                    <p className="label">Target section scope</p>
                    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                      <label className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 ${form.allSections ? 'border-brand-200 bg-brand-50 text-brand-800' : 'border-slate-200 bg-white text-slate-700'}`}>
                        <input
                          type="checkbox"
                          checked={form.allSections}
                          onChange={() => setForm((current) => ({ ...current, allSections: !current.allSections, targetSectionIds: [] }))}
                          className="size-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                        />
                        <span className="text-sm font-medium">All Sections</span>
                      </label>
                      {sections.map((section) => {
                        const checked = form.targetSectionIds.includes(section.id);
                        return (
                          <label key={section.id} className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 ${checked ? 'border-brand-200 bg-brand-50 text-brand-800' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'}`}>
                            <input
                              type="checkbox"
                              checked={checked}
                              disabled={form.allSections}
                              onChange={() => setForm((current) => ({
                                ...current,
                                allSections: false,
                                targetSectionIds: checked
                                  ? current.targetSectionIds.filter((id) => id !== section.id)
                                  : [...current.targetSectionIds, section.id],
                              }))}
                              className="size-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                            />
                            <span className="text-sm font-medium">Section {section.name}</span>
                          </label>
                        );
                      })}
                    </div>
                    {errors.sections ? <p className="mt-2 text-xs text-rose-600">{errors.sections}</p> : null}
                  </div>
                )}
              </div>
            </section>

            {successMessage ? (
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800">
                {successMessage}
              </div>
            ) : null}
          </div>

          <div className="mt-8 flex flex-col-reverse justify-end gap-3 border-t border-slate-200 pt-5 sm:flex-row">
            <button type="button" onClick={onClose} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" disabled={isSubmitting} className="btn-primary min-w-[170px]">
              {isSubmitting ? (isEditing ? 'Saving...' : 'Publishing...') : isEditing ? 'Save Changes' : 'Publish Event'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
