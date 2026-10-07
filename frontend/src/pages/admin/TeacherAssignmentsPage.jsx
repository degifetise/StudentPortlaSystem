import { useCallback, useEffect, useState } from 'react';
import { ArrowRightLeft, BriefcaseBusiness, Plus, Save, UserRoundCog, X } from 'lucide-react';
import TeacherAssignmentModal from '../../components/teacher/TeacherAssignmentModal';
import BulkTeacherAssignmentForm from '../../components/teacher/BulkTeacherAssignmentForm';
import { teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

export default function TeacherAssignmentsPage() {
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [selectedTeacher, setSelectedTeacher] = useState(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [reassigning, setReassigning] = useState(null);
  const [reassignTeacherId, setReassignTeacherId] = useState('');
  const [savingReassignment, setSavingReassignment] = useState(false);

  const loadTeachers = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await teacherApi.list({ page: 1, pageSize: 100, includeInactive: true });
      setTeachers(data.items ?? []);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTeachers();
  }, [loadTeachers]);

  async function removeAssignment(teacherId, assignmentId) {
    try {
      await teacherApi.removeAssignment(teacherId, assignmentId);
      setNotice('Assignment removed.');
      await loadTeachers();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    }
  }

  async function saveReassignment(event) {
    event.preventDefault();
    if (!reassigning || !reassignTeacherId) return;

    setSavingReassignment(true);
    setError('');
    try {
      await teacherApi.reassignAssignment(reassigning.assignmentId, Number(reassignTeacherId));
      setNotice('Teaching assignment reassigned.');
      setReassigning(null);
      setReassignTeacherId('');
      await loadTeachers();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSavingReassignment(false);
    }
  }

  if (loading) return <LoadingPanel label="Loading teachers…" />;
  if (error) return <ErrorState title="Could not load teachers" message={error} onRetry={loadTeachers} />;

  return (
    <div className="space-y-6">
      {notice && <Alert variant="success" onDismiss={() => setNotice(null)}>{notice}</Alert>}
      {error && <Alert variant="error" title="Something went wrong" onDismiss={() => setError(null)}>{error}</Alert>}

      <section className="card p-5">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Staff map</p>
            <h1 className="text-2xl font-bold text-slate-900">Teacher assignments</h1>
          </div>
          <div className="rounded-lg bg-brand-100 px-3 py-2 text-sm font-medium text-brand-700">
            {teachers.length} teachers
          </div>
        </div>
      </section>

      {teachers.length === 0 ? (
        <EmptyState icon={BriefcaseBusiness} title="No teachers found" description="Create teacher profiles first to assign classes." />
      ) : (
        <>
        <BulkTeacherAssignmentForm teachers={teachers} onSaved={loadTeachers} />
        <section className="space-y-4">
          {teachers.map((teacher) => (
            <div key={teacher.id} className="card p-5">
              <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-4">
                <div>
                  <h2 className="text-lg font-bold text-slate-900">{teacher.fullName}</h2>
                  <p className="text-sm text-slate-500">{teacher.specialization || 'No specialization'} · {teacher.employeeId}</p>
                </div>
                <button className="btn-primary" onClick={() => { setSelectedTeacher(teacher); setModalOpen(true); }}>
                  <Plus className="size-4" aria-hidden="true" />
                  Assign class
                </button>
              </div>

              {teacher.assignments?.length ? (
                <div className="mt-4 space-y-3">
                  {teacher.assignments.map((assignment) => (
                    <div key={assignment.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
                      <div>
                        <p className="font-semibold text-slate-900">{assignment.subjectName}</p>
                        <p className="text-sm text-slate-500">{assignment.gradeLevelName} · {assignment.sectionName} · {assignment.academicYear}</p>
                      </div>
                      <button type="button" className="btn-secondary" onClick={() => removeAssignment(teacher.id, assignment.id)}>
                        Remove
                      </button>
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={() => {
                          setReassigning({
                            assignmentId: assignment.id,
                            teacherId: teacher.id,
                            subjectName: assignment.subjectName,
                            sectionName: `${assignment.gradeLevelName} · ${assignment.sectionName}`,
                          });
                          setReassignTeacherId('');
                        }}
                      >
                        <ArrowRightLeft className="size-4" aria-hidden="true" />
                        Change teacher
                      </button>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="mt-4 text-sm text-slate-500">No active assignments yet.</p>
              )}
            </div>
          ))}
        </section>
        </>
      )}

      <TeacherAssignmentModal
        teacher={selectedTeacher}
        open={modalOpen}
        onClose={() => {
          setModalOpen(false);
          setSelectedTeacher(null);
        }}
        onSaved={async () => {
          setModalOpen(false);
          setSelectedTeacher(null);
          await loadTeachers();
        }}
      />

      {reassigning && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/50 p-4" role="presentation">
          <section role="dialog" aria-modal="true" aria-labelledby="reassign-title" className="card w-full max-w-lg p-6 shadow-xl">
            <h2 id="reassign-title" className="text-xl font-bold text-slate-900">Change assigned teacher</h2>
            <p className="mt-1 text-sm text-slate-600">
              {reassigning.subjectName} · {reassigning.sectionName}
            </p>
            <form onSubmit={saveReassignment} className="mt-5 space-y-4">
              <label className="block space-y-1 text-sm font-medium text-slate-700">
                New teacher
                <select
                  className="input"
                  required
                  value={reassignTeacherId}
                  onChange={(event) => setReassignTeacherId(event.target.value)}
                >
                  <option value="">Select an active teacher…</option>
                  {teachers
                    .filter((teacher) => teacher.isActive && teacher.id !== reassigning.teacherId)
                    .map((teacher) => (
                      <option key={teacher.id} value={teacher.id}>{teacher.fullName} · {teacher.employeeId}</option>
                    ))}
                </select>
              </label>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  className="btn"
                  onClick={() => setReassigning(null)}
                  disabled={savingReassignment}
                >
                  <X className="size-4" aria-hidden="true" />
                  Cancel
                </button>
                <button type="submit" className="btn-primary" disabled={savingReassignment || !reassignTeacherId}>
                  {savingReassignment ? <Spinner className="size-4" /> : <Save className="size-4" aria-hidden="true" />}
                  Reassign
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  );
}
