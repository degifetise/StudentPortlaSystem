import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ClipboardList, Download, Search, Users } from 'lucide-react';
import { reportPdfApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Badge, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

const gradeTone = (letter) => {
  if (!letter) return 'slate';
  if (letter.startsWith('A')) return 'green';
  if (letter.startsWith('B')) return 'brand';
  if (letter.startsWith('C')) return 'amber';
  return 'red';
};

const TEACHER_LINK_ERROR =
  'Your account is logged in, but not yet linked to an active Teacher profile or class assignment. Please contact your system administrator.';

function teacherAccessMessage(error) {
  const status = error?.status ?? error?.response?.status;
  if (status === 503) {
    return 'The database is temporarily unavailable. Please check the SQL Server service or contact your system administrator.';
  }

  return status === 403 || status === 404
    ? TEACHER_LINK_ERROR
    : error?.friendlyMessage ?? extractErrorMessage(error) ?? 'We could not load this class list. Please try again.';
}

function ClassSummary({ roster }) {
  const cards = [
    { label: 'Students', value: roster.students.length },
    { label: 'With marks', value: `${roster.markedCount}/${roster.students.length}` },
    {
      label: 'Class average',
      value: roster.classAverage === null ? '—' : `${roster.classAverage.toFixed(1)}%`,
    },
    {
      label: `Passing (≥ ${roster.passMarkPercentage}%)`,
      value: roster.markedCount === 0 ? '—' : `${roster.passCount}/${roster.markedCount}`,
    },
  ];

  return (
    <div className="grid gap-3 sm:grid-cols-4">
      {cards.map(({ label, value }) => (
        <div key={label} className="rounded-lg border border-slate-200 px-4 py-3">
          <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</p>
          <p className="text-xl font-bold text-slate-900">{value}</p>
        </div>
      ))}
    </div>
  );
}

/**
 * A teacher's class lists and how each student is standing in that teacher's subject.
 *
 * Totals come from the same view the report cards use, so a figure here is the figure the
 * student sees - published marks only. Unpublished work is why a student can show as not yet
 * marked while scores exist in the mark entry screen.
 */
export default function TeacherStudents() {
  const [classes, setClasses] = useState([]);
  const [assignmentId, setAssignmentId] = useState('');
  const [roster, setRoster] = useState(null);
  const [search, setSearch] = useState('');
  const [downloadingPdf, setDownloadingPdf] = useState(false);
  const [downloadError, setDownloadError] = useState(null);

  const [loading, setLoading] = useState(true);
  const [classesError, setClassesError] = useState(null);
  const [rosterLoading, setRosterLoading] = useState(false);
  const [rosterError, setRosterError] = useState(null);
  const rosterRequestInFlight = useRef(false);
  const assignmentIdRef = useRef(assignmentId);
  const loadRosterRef = useRef(null);
  assignmentIdRef.current = assignmentId;

  const loadClasses = useCallback(async () => {
    setLoading(true);
    setClassesError(null);

    try {
      const data = await teacherApi.myClasses();
      const assignments = Array.isArray(data) ? data : [];
      setClasses(assignments);
      if (assignments.length > 0) setAssignmentId(String(assignments[0].id));
      else setAssignmentId('');
    } catch (err) {
      setClassesError(teacherAccessMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadClasses();
  }, [loadClasses]);

  const loadRoster = useCallback(async () => {
    if (!assignmentId) {
      setRoster(null);
      setRosterLoading(false);
      return;
    }

    if (rosterRequestInFlight.current) return;
    rosterRequestInFlight.current = true;
    const requestedAssignmentId = assignmentId;
    setRosterLoading(true);
    setRosterError(null);
    setRoster(null);

    try {
      const result = await teacherApi.classRoster(requestedAssignmentId);
      if (assignmentIdRef.current === requestedAssignmentId) {
        setRoster(Array.isArray(result) ? {
          assignmentId: Number(requestedAssignmentId),
          students: result,
          markedCount: 0,
          passCount: 0,
          classAverage: null,
          passMarkPercentage: 50,
        } : {
          ...result,
          students: Array.isArray(result?.students) ? result.students : [],
        });
      }
    } catch (err) {
      const status = err?.status ?? err?.response?.status;
      if (assignmentIdRef.current !== requestedAssignmentId) return;
      if (status === 404) {
        setRoster({
          assignmentId: Number(requestedAssignmentId),
          students: [],
          markedCount: 0,
          passCount: 0,
          classAverage: null,
          passMarkPercentage: 50,
        });
        setRosterError(null);
        return;
      }

      setRoster(null);
      setRosterError(teacherAccessMessage(err));
    } finally {
      rosterRequestInFlight.current = false;
      if (assignmentIdRef.current === requestedAssignmentId) {
        setRosterLoading(false);
      } else if (assignmentIdRef.current) {
        loadRosterRef.current?.();
      }
    }
  }, [assignmentId]);
  loadRosterRef.current = loadRoster;

  useEffect(() => {
    loadRoster();
    const interval = setInterval(loadRoster, 300000);
    return () => clearInterval(interval);
  }, [loadRoster]);

  const visible = useMemo(() => {
    if (!roster) return [];
    const term = search.trim().toLowerCase();
    if (!term) return roster.students;

    return roster.students.filter(
      (student) =>
        student.fullName.toLowerCase().includes(term) ||
        student.studentIdNumber.toLowerCase().includes(term),
    );
  }, [roster, search]);

  async function downloadSectionRoster() {
    if (!roster?.sectionId) return;
    setDownloadingPdf(true);
    setDownloadError(null);
    try {
      await reportPdfApi.teacherSectionRoster(roster.sectionId);
    } catch (err) {
      setDownloadError(err.friendlyMessage ?? err.message ?? 'The section roster PDF could not be downloaded.');
    } finally {
      setDownloadingPdf(false);
    }
  }

  if (loading) return <LoadingPanel label="Loading your classes…" />;

  if (classesError) {
    return <ErrorState title="Could not load your classes" message={classesError} onRetry={loadClasses} />;
  }

  if (classes.length === 0) {
    return (
      <EmptyState
        icon={Users}
        title="No classes assigned yet"
        description="An administrator assigns the subjects and sections you teach. Once that is done, your class lists appear here."
      />
    );
  }

  return (
    <div className="space-y-6">
      {downloadError && (
        <Alert variant="error" title="PDF download failed" onDismiss={() => setDownloadError(null)}>
          {downloadError}
        </Alert>
      )}
      <section className="card p-5">
        <label htmlFor="class-picker" className="label">
          Class
        </label>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <select
            id="class-picker"
            className="input w-auto min-w-72"
            value={assignmentId}
            onChange={(event) => setAssignmentId(event.target.value)}
          >
            {classes.map((item) => (
              <option key={item.id} value={item.id}>
                {item.subjectName} · {item.gradeLevelName} {item.sectionName}
              </option>
            ))}
          </select>

          {roster && (
            <>
              {/* Mark entry is not in the navigation bar, so this is the way in - and it carries
                  the class along so the teacher does not choose it twice. */}
              <Link to={`/teacher/marks?assignment=${roster.assignmentId}`} className="btn-primary">
                <ClipboardList className="size-4" aria-hidden="true" />
                Enter marks for this class
              </Link>
              <button
                type="button"
                className="btn-secondary"
                onClick={downloadSectionRoster}
                disabled={downloadingPdf}
              >
                {downloadingPdf ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}
                {downloadingPdf ? 'Downloading PDF…' : 'Download Section Roster PDF'}
              </button>
            </>
          )}
        </div>
      </section>

      {rosterError && (
        <ErrorState title="Could not load this class list" message={rosterError} onRetry={loadRoster} />
      )}

      {rosterLoading && <LoadingPanel label="Loading class list…" />}

      {roster && !rosterLoading && !rosterError && (
        <>
          <ClassSummary roster={roster} />

          <section className="card">
            <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
              <div>
                <h2 className="font-semibold text-slate-900">
                  {roster.subjectName}{' '}
                  <span className="font-normal text-slate-500">
                    · {roster.gradeLevelName} {roster.sectionName} · {roster.academicYear}
                  </span>
                </h2>
                <p className="text-sm text-slate-500">
                  Weighted totals from published marks only.
                </p>
              </div>

              <div className="relative ml-auto">
                <Search
                  className="pointer-events-none absolute inset-y-0 left-3 my-auto size-4 text-slate-400"
                  aria-hidden="true"
                />
                <input
                  className="input w-56 pl-9"
                  placeholder="Search this class"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  aria-label="Search this class"
                />
              </div>
            </div>

            {visible.length === 0 ? (
              <div className="p-5">
                <EmptyState
                  icon={Users}
                  title={search ? 'Nobody in this class matches that' : 'No students found in this section'}
                  description={
                    search
                      ? undefined
                      : 'Students appear here once an administrator enrols them into this section.'
                  }
                />
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-3xl text-left text-sm">
                  <thead className="bg-slate-50 text-xs tracking-wide text-slate-500 uppercase">
                    <tr>
                      <th scope="col" className="px-5 py-3 font-semibold">
                        Student
                      </th>
                      <th scope="col" className="px-5 py-3 font-semibold">
                        ID
                      </th>
                      <th scope="col" className="px-5 py-3 font-semibold">
                        Components marked
                      </th>
                      <th scope="col" className="px-5 py-3 font-semibold">
                        Weighted total
                      </th>
                      <th scope="col" className="px-5 py-3 text-right font-semibold">
                        Grade
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {visible.map((student) => (
                      <motion.tr
                        key={student.studentId}
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        className="border-t border-slate-100"
                      >
                        <td className="px-5 py-3">
                          <p className="font-medium text-slate-900">{student.fullName}</p>
                          <p className="text-xs text-slate-500">{student.email ?? 'No login'}</p>
                        </td>
                        <td className="px-5 py-3 font-mono text-xs text-slate-600">
                          {student.studentIdNumber}
                        </td>
                        <td className="px-5 py-3 text-slate-600">{student.componentsMarked} of 5</td>
                        <td className="px-5 py-3">
                          {student.totalScore === null ? (
                            <span className="text-slate-400">Not marked yet</span>
                          ) : (
                            <span className="font-semibold text-slate-900">
                              {student.totalScore.toFixed(1)}%
                            </span>
                          )}
                        </td>
                        <td className="px-5 py-3 text-right">
                          {student.letterGrade ? (
                            <Badge tone={gradeTone(student.letterGrade)}>{student.letterGrade}</Badge>
                          ) : (
                            <span className="text-slate-400">—</span>
                          )}
                        </td>
                      </motion.tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}
