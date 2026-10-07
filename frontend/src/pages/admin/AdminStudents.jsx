import { useCallback, useEffect, useMemo, useState } from 'react';
import { BookOpen, Download, Layers, Plus, RefreshCw, Users, UsersRound } from 'lucide-react';
import { bulkAdminApi, gradeLevelApi, reportPdfApi, sectionApi, studentApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Badge, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';
import ProfilePhotoField from '../../components/auth/ProfilePhotoField';
import { PAGE_SIZE, Pager, PeopleTable, RowActions, SearchBox, StatCard, totalPagesOf } from './adminShared';

/** Enrol one student directly. Skips the approval queue: an administrator is the approval. */
function NewStudentForm({ gradeLevels, sections, onCreated, onError }) {
  const [form, setForm] = useState({ fullName: '', gradeLevelId: '', sectionId: '' });
  const [photo, setPhoto] = useState(null);
  const [photoPending, setPhotoPending] = useState(false);
  const [saving, setSaving] = useState(false);

  const update = (key) => (event) => setForm((prev) => ({ ...prev, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    if (photoPending) {
      onError('Confirm or cancel the selected photo before creating the student.');
      return;
    }
    setSaving(true);
    onError(null);

    try {
      const result = await studentApi.create({
        fullName: form.fullName.trim(),
        gradeLevelId: Number(form.gradeLevelId),
        sectionId: Number(form.sectionId),
        photo,
      });
      setForm({ fullName: '', gradeLevelId: '', sectionId: '' });
      setPhoto(null);
      onCreated(result);
    } catch (err) {
      onError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} className="grid gap-3 border-t border-slate-200 p-4 sm:grid-cols-4">
      <p className="text-xs text-slate-500 sm:col-span-4">
        A student ID and school login email are generated automatically when enrolled.
      </p>
      <ProfilePhotoField
        file={photo}
        onChange={setPhoto}
        onPendingChange={setPhotoPending}
        disabled={saving}
      />
      <input
        className="input sm:col-span-1"
        placeholder="Full name"
        required
        value={form.fullName}
        onChange={update('fullName')}
        aria-label="Full name"
      />
      <select
        className="input"
        required
        value={form.gradeLevelId}
        onChange={update('gradeLevelId')}
        aria-label="Grade level"
      >
        <option value="">Grade…</option>
        {gradeLevels.map((grade) => (
          <option key={grade.id} value={grade.id}>
            {grade.name}
          </option>
        ))}
      </select>
      <div className="flex gap-2">
        <select
          className="input"
          required
          value={form.sectionId}
          onChange={update('sectionId')}
          aria-label="Section"
        >
          <option value="">Section…</option>
          {sections.map((section) => (
            <option key={section.id} value={section.id}>
              {section.name}
            </option>
          ))}
        </select>
        <button type="submit" className="btn-primary shrink-0" disabled={saving}>
          {saving ? <Spinner className="size-4" /> : <Plus className="size-4" aria-hidden="true" />}
          Add
        </button>
      </div>
    </form>
  );
}

/**
 * Students: the enrolment picture and the roster.
 *
 * Staff accounts and the approval queue live on the Accounts page, which keeps this one about
 * the student body: how full each class is, and who is in it.
 */
export default function AdminStudents() {
  const [gradeLevels, setGradeLevels] = useState([]);
  const [sections, setSections] = useState([]);
  const [summary, setSummary] = useState([]);
  const [teacherCount, setTeacherCount] = useState(null);
  const [loading, setLoading] = useState(true);
  const [referenceError, setReferenceError] = useState(null);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [showCreate, setShowCreate] = useState(false);
  const [students, setStudents] = useState({ items: [], totalCount: 0, page: 1 });
  const [filters, setFilters] = useState({ search: '', gradeLevelId: '', sectionId: '' });
  const [page, setPage] = useState(1);
  const [studentsLoading, setStudentsLoading] = useState(false);
  const [busyId, setBusyId] = useState(null);
  const [downloadingPdf, setDownloadingPdf] = useState(null);
  const [bulkIds, setBulkIds] = useState('');
  const [bulkGradeId, setBulkGradeId] = useState('');
  const [bulkSectionId, setBulkSectionId] = useState('');
  const [bulkSaving, setBulkSaving] = useState(false);

  /* The grade and section lists drive both the filters and the enrolment form, so a failure
     here is terminal for the page and offers a retry rather than a screen full of zeros. */
  const loadReference = useCallback(async () => {
    setLoading(true);
    setReferenceError(null);

    try {
      // pageSize 1 because only the staff total is wanted here.
      const [grades, sectionList, rosterSummary, staff] = await Promise.all([
        gradeLevelApi.list(),
        sectionApi.list(),
        studentApi.summary(),
        teacherApi.list({ page: 1, pageSize: 1, includeInactive: true }),
      ]);

      setGradeLevels(Array.isArray(grades) ? grades : []);
      setSections(Array.isArray(sectionList) ? sectionList : []);
      setSummary(Array.isArray(rosterSummary) ? rosterSummary : []);
      setTeacherCount(Number.isFinite(staff?.totalCount) ? staff.totalCount : 0);
    } catch (err) {
      setReferenceError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadReference();
  }, [loadReference]);

  const loadStudents = useCallback(async () => {
    setStudentsLoading(true);
    try {
      const data = await studentApi.list({
        page,
        pageSize: PAGE_SIZE,
        includeInactive: true,
        search: filters.search || undefined,
        gradeLevelId: filters.gradeLevelId || undefined,
        sectionId: filters.sectionId || undefined,
      });
      setStudents({
        items: Array.isArray(data?.items) ? data.items : [],
        page: Number.isInteger(data?.page) ? data.page : page,
        pageSize: Number.isInteger(data?.pageSize) ? data.pageSize : PAGE_SIZE,
        totalCount: Number.isFinite(data?.totalCount) ? data.totalCount : 0,
      });
      setError(null);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setStudentsLoading(false);
    }
  }, [page, filters]);

  useEffect(() => {
    loadStudents();
  }, [loadStudents]);

  const refreshMetrics = useCallback(async () => {
    try {
      setSummary(await studentApi.summary());
    } catch {
      // Metrics are informational; a failure here must not blank the roster below.
    }
  }, []);

  const totals = useMemo(() => {
    const enrolled = summary.reduce((sum, row) => sum + row.studentCount, 0);
    const subjects = gradeLevels.reduce((sum, grade) => sum + (grade.subjectCount ?? 0), 0);
    return { enrolled, subjects };
  }, [summary, gradeLevels]);

  async function toggleStudent(student) {
    setBusyId(student.id);
    setError(null);
    try {
      await studentApi.setStatus(student.id, !student.isActive);
      setNotice(
        `${student.fullName} is now ${student.isActive ? 'deactivated' : 'active'}. Marks are always retained.`,
      );
      await Promise.all([loadStudents(), refreshMetrics()]);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function resetPassword(student) {
    setBusyId(student.id);
    setError(null);
    try {
      const { temporaryPassword } = await studentApi.resetPassword(student.id);
      // Shown once: the API never returns this value again.
      setNotice(`Temporary password for ${student.fullName}: ${temporaryPassword}`);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function downloadStudentRoster(sectionSpecific) {
    setDownloadingPdf(sectionSpecific ? 'section' : 'all');
    setError(null);
    try {
      await reportPdfApi.studentsRoster(sectionSpecific
        ? { gradeLevelId: filters.gradeLevelId, sectionId: filters.sectionId }
        : {});
    } catch (err) {
      setError(err.friendlyMessage ?? err.message ?? 'The student roster PDF could not be downloaded.');
    } finally {
      setDownloadingPdf(null);
    }
  }

  async function enrollStudentsInBulk(event) {
    event.preventDefault();
    const studentIdNumbers = bulkIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean);
    if (!studentIdNumbers.length || !bulkGradeId || !bulkSectionId) {
      setError('Enter student ID numbers and choose a destination grade and section.');
      return;
    }

    setBulkSaving(true);
    setError(null);
    try {
      const result = await bulkAdminApi.enrollStudents({
        studentIdNumbers,
        gradeLevelId: Number(bulkGradeId),
        sectionId: Number(bulkSectionId),
      });
      setNotice(`${result.updated} student(s) enrolled in the selected class.`);
      setBulkIds('');
      await Promise.all([loadStudents(), refreshMetrics()]);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBulkSaving(false);
    }
  }

  if (loading) return <LoadingPanel label="Loading students…" />;

  if (referenceError) {
    return <ErrorState title="Could not load students" message={referenceError} onRetry={loadReference} />;
  }

  return (
    <div className="space-y-6">
      {error && (
        <Alert variant="error" title="Something went wrong" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      {notice && (
        <Alert variant="success" onDismiss={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          icon={Users}
          label="Active students"
          value={totals.enrolled}
          hint={`${summary.length} class group${summary.length === 1 ? '' : 's'} in use`}
        />
        <StatCard
          icon={UsersRound}
          label="Teachers"
          value={teacherCount ?? '—'}
          hint="Including deactivated accounts"
          tone="green"
        />
        <StatCard
          icon={BookOpen}
          label="Subjects"
          value={totals.subjects}
          hint="Across all grade levels"
          tone="amber"
        />
        <StatCard
          icon={Layers}
          label="Grades / sections"
          value={`${gradeLevels.length} / ${sections.length}`}
          hint="Nursery through Grade 12"
          tone="slate"
        />
      </div>

      <section className="card p-5">
        <div className="mb-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Batch enrollment</p>
          <h2 className="text-lg font-bold text-slate-900">Enroll students by ID</h2>
          <p className="mt-1 text-sm text-slate-600">Paste student ID numbers separated by commas, spaces, or new lines. All IDs are validated before any student is moved.</p>
        </div>
        <form onSubmit={enrollStudentsInBulk} className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_1fr_1fr_auto]">
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Student ID numbers</span>
            <textarea className="input min-h-20" value={bulkIds} onChange={(event) => setBulkIds(event.target.value)} maxLength={10000} placeholder="STU-001, STU-002" />
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Grade</span>
            <select className="input" value={bulkGradeId} onChange={(event) => setBulkGradeId(event.target.value)} required>
              <option value="">Choose grade</option>
              {gradeLevels.map((grade) => <option key={grade.id} value={grade.id}>{grade.name}</option>)}
            </select>
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Section</span>
            <select className="input" value={bulkSectionId} onChange={(event) => setBulkSectionId(event.target.value)} required>
              <option value="">Choose section</option>
              {sections.map((section) => <option key={section.id} value={section.id}>{section.name}</option>)}
            </select>
          </label>
          <div className="flex items-end">
            <button type="submit" className="btn-primary w-full" disabled={bulkSaving}>
              {bulkSaving ? <Spinner className="size-4" /> : null}
              {bulkSaving ? 'Enrolling…' : 'Enroll batch'}
            </button>
          </div>
        </form>
      </section>

      <section className="card">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-900">Enrolment by class</h2>
          <button type="button" onClick={refreshMetrics} className="btn-secondary" title="Refresh">
            <RefreshCw className="size-4" aria-hidden="true" />
            Refresh
          </button>
        </div>

        {summary.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={Users}
              title="No students enrolled yet"
              description="Add a student below and the class breakdown will appear here."
            />
          </div>
        ) : (
          <div className="grid gap-3 p-5 sm:grid-cols-2 lg:grid-cols-3">
            {summary.map((row) => (
              <div
                key={`${row.gradeLevelId}-${row.sectionId}`}
                className="flex items-center justify-between rounded-lg border border-slate-200 px-4 py-3"
              >
                <div>
                  <p className="text-sm font-semibold text-slate-800">{row.gradeLevelName}</p>
                  <p className="text-xs text-slate-500">{row.sectionName}</p>
                </div>
                <Badge tone="brand">{row.studentCount}</Badge>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="card">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-900">Student roster</h2>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="btn-secondary"
              onClick={() => downloadStudentRoster(false)}
              disabled={downloadingPdf !== null}
            >
              {downloadingPdf === 'all' ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}
              {downloadingPdf === 'all' ? 'Downloading PDF…' : 'Export All Students PDF'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => downloadStudentRoster(true)}
              disabled={downloadingPdf !== null || !filters.gradeLevelId || !filters.sectionId}
              title={!filters.gradeLevelId || !filters.sectionId ? 'Choose a grade and section first' : undefined}
            >
              {downloadingPdf === 'section' ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}
              {downloadingPdf === 'section' ? 'Downloading PDF…' : 'Export Section PDF'}
            </button>
            <select
              className="input w-auto"
              value={filters.gradeLevelId}
              onChange={(event) => {
                setPage(1);
                setFilters((prev) => ({ ...prev, gradeLevelId: event.target.value }));
              }}
              aria-label="Filter by grade"
            >
              <option value="">All grades</option>
              {gradeLevels.map((grade) => (
                <option key={grade.id} value={grade.id}>
                  {grade.name}
                </option>
              ))}
            </select>
            <select
              className="input w-auto"
              value={filters.sectionId}
              onChange={(event) => {
                setPage(1);
                setFilters((prev) => ({ ...prev, sectionId: event.target.value }));
              }}
              aria-label="Filter by section"
            >
              <option value="">All sections</option>
              {sections.map((section) => (
                <option key={section.id} value={section.id}>
                  {section.name}
                </option>
              ))}
            </select>
            <SearchBox
              value={filters.search}
              onSearch={(value) => {
                setPage(1);
                setFilters((prev) => ({ ...prev, search: value }));
              }}
            />
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setShowCreate((value) => !value)}
              aria-expanded={showCreate}
            >
              <Plus className="size-4" aria-hidden="true" />
              Enrol student
            </button>
          </div>
        </div>

        {showCreate && (
          <NewStudentForm
            gradeLevels={gradeLevels}
            sections={sections}
            onError={setError}
            onCreated={async (result) => {
              setNotice(
                result.temporaryPassword
                  ? `${result.student.fullName} enrolled as ${result.student.studentIdNumber} (${result.student.email}). Temporary password: ${result.temporaryPassword}`
                  : `${result.student.fullName} enrolled as ${result.student.studentIdNumber} (${result.student.email}).`,
              );
              await Promise.all([loadStudents(), refreshMetrics()]);
            }}
          />
        )}

        <div className="overflow-x-auto">
          <PeopleTable
            loading={studentsLoading}
            rows={students.items}
            emptyTitle="No students match these filters"
            columns={['Student', 'ID', 'Class', 'Status', '']}
            renderRow={(student) => (
              <tr key={student.id} className="border-t border-slate-100">
                <td className="px-5 py-3">
                  <p className="font-medium text-slate-900">{student.fullName}</p>
                  <p className="text-xs text-slate-500">{student.email ?? 'No login'}</p>
                </td>
                <td className="px-5 py-3 text-slate-600">{student.studentIdNumber}</td>
                <td className="px-5 py-3 text-slate-600">
                  {student.gradeLevelName} · {student.sectionName}
                </td>
                <td className="px-5 py-3">
                  <Badge tone={student.isActive ? 'green' : 'red'}>
                    {student.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </td>
                <td className="px-5 py-3">
                  <RowActions
                    busy={busyId === student.id}
                    isActive={student.isActive}
                    hasLogin={student.hasLogin}
                    onToggle={() => toggleStudent(student)}
                    onReset={() => resetPassword(student)}
                  />
                </td>
              </tr>
            )}
          />
        </div>

        <Pager
          page={page}
          totalPages={totalPagesOf(students)}
          totalCount={students.totalCount}
          onChange={setPage}
        />
      </section>
    </div>
  );
}
