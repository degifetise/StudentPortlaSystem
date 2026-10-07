import { useEffect, useMemo, useState } from 'react';
import { BookOpen, CalendarCheck, Download, GraduationCap, UsersRound } from 'lucide-react';
import { guardianApi, reportPdfApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

function Metric({ label, value, tone = 'slate' }) {
  const tones = {
    slate: 'bg-slate-50 text-slate-900',
    green: 'bg-emerald-50 text-emerald-800',
    red: 'bg-rose-50 text-rose-800',
    amber: 'bg-amber-50 text-amber-800',
  };
  return (
    <div className={`rounded-xl p-4 ${tones[tone] ?? tones.slate}`}>
      <p className="text-xs font-semibold uppercase tracking-wide opacity-70">{label}</p>
      <p className="mt-1 text-2xl font-bold">{value}</p>
    </div>
  );
}

export default function ParentDashboard() {
  const [students, setStudents] = useState([]);
  const [selectedId, setSelectedId] = useState('');
  const [attendance, setAttendance] = useState([]);
  const [results, setResults] = useState(null);
  const [loadingStudents, setLoadingStudents] = useState(true);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [error, setError] = useState('');
  const [detailsError, setDetailsError] = useState('');
  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    guardianApi.myStudents()
      .then((rows) => {
        if (cancelled) return;
        const linked = Array.isArray(rows) ? rows : [];
        setStudents(linked);
        setSelectedId((current) => current || String(linked[0]?.studentId ?? ''));
      })
      .catch((loadError) => {
        if (!cancelled) setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
      })
      .finally(() => {
        if (!cancelled) setLoadingStudents(false);
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!selectedId) {
      setAttendance([]);
      setResults(null);
      return undefined;
    }

    let cancelled = false;
    setLoadingDetails(true);
    setDetailsError('');
    Promise.allSettled([
      guardianApi.studentAttendance(selectedId),
      guardianApi.studentResults(selectedId),
    ])
      .then(([attendanceResponse, resultsResponse]) => {
        if (cancelled) return;
        if (attendanceResponse.status === 'fulfilled') {
          setAttendance(Array.isArray(attendanceResponse.value) ? attendanceResponse.value : []);
        } else {
          setAttendance([]);
        }
        if (resultsResponse.status === 'fulfilled') {
          setResults(resultsResponse.value);
        } else {
          setResults(null);
        }

        const errors = [attendanceResponse, resultsResponse]
          .filter((response) => response.status === 'rejected')
          .map((response) => response.reason?.friendlyMessage ?? extractErrorMessage(response.reason));
        setDetailsError(errors.join(' '));
      })
      .finally(() => {
        if (!cancelled) setLoadingDetails(false);
      });
    return () => { cancelled = true; };
  }, [selectedId]);

  const selectedStudent = students.find((student) => String(student.studentId) === selectedId);
  const attendanceCounts = useMemo(() => ({
    present: attendance.filter((row) => row.status === 'Present').length,
    absent: attendance.filter((row) => row.status === 'Absent').length,
    late: attendance.filter((row) => row.status === 'Late').length,
  }), [attendance]);

  async function downloadReport() {
    setDownloading(true);
    setDetailsError('');
    try {
      await reportPdfApi.parentStudentReportCard(selectedId);
    } catch (downloadError) {
      setDetailsError(downloadError.friendlyMessage ?? extractErrorMessage(downloadError));
    } finally {
      setDownloading(false);
    }
  }

  if (loadingStudents) return <LoadingPanel label="Loading linked students…" />;
  if (error) return <ErrorState title="Could not load your students" message={error} />;
  if (students.length === 0) {
    return (
      <EmptyState
        icon={UsersRound}
        title="No students linked yet"
        description="Ask the school administrator to connect your guardian account to a student."
      />
    );
  }

  return (
    <div className="space-y-6">
      {detailsError && <Alert variant="error" title="Student information unavailable" onDismiss={() => setDetailsError('')}>{detailsError}</Alert>}
      <section className="card flex flex-wrap items-center justify-between gap-4 p-5">
        <div className="flex items-center gap-3">
          <span className="grid size-11 place-items-center rounded-xl bg-brand-100 text-brand-700">
            <UsersRound className="size-6" aria-hidden="true" />
          </span>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Guardian portal</p>
            <h1 className="text-2xl font-bold text-slate-900">My children</h1>
          </div>
        </div>
        {students.length > 1 && (
          <label className="w-full max-w-sm space-y-1 text-sm font-medium text-slate-700">
            Select student
            <select className="input" value={selectedId} onChange={(event) => setSelectedId(event.target.value)}>
              {students.map((student) => (
                <option key={student.studentId} value={student.studentId}>{student.fullName} · {student.studentIdNumber}</option>
              ))}
            </select>
          </label>
        )}
      </section>

      {selectedStudent && (
        <>
          <section className="card p-5">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Student overview</p>
                <h2 className="mt-1 text-xl font-bold text-slate-900">{selectedStudent.fullName}</h2>
                <p className="mt-1 text-sm text-slate-600">{selectedStudent.gradeLevelName} · {selectedStudent.sectionName} · {selectedStudent.relationship}</p>
              </div>
              <span className="rounded-full bg-brand-50 px-3 py-1 font-mono text-sm font-semibold text-brand-800">{selectedStudent.studentIdNumber}</span>
            </div>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <div className="rounded-lg bg-slate-50 p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">School email</p>
                <p className="mt-1 break-all text-sm font-medium text-slate-800">{selectedStudent.email || 'Not available'}</p>
              </div>
              <div className="rounded-lg bg-slate-50 p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Class</p>
                <p className="mt-1 text-sm font-medium text-slate-800">{selectedStudent.gradeLevelName} · {selectedStudent.sectionName}</p>
              </div>
            </div>
          </section>

          {loadingDetails ? <LoadingPanel label="Loading attendance and results…" /> : (
            <>
              <section className="space-y-3">
                <div className="flex items-center gap-2">
                  <CalendarCheck className="size-5 text-brand-700" aria-hidden="true" />
                  <h2 className="text-lg font-bold text-slate-900">Attendance</h2>
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <Metric label="Present" value={attendanceCounts.present} tone="green" />
                  <Metric label="Absent" value={attendanceCounts.absent} tone="red" />
                  <Metric label="Late" value={attendanceCounts.late} tone="amber" />
                </div>
                {attendance.length ? (
                  <div className="card overflow-hidden">
                    <div className="overflow-x-auto">
                      <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                          <tr><th className="px-4 py-3">Date</th><th className="px-4 py-3">Status</th><th className="px-4 py-3">Note</th></tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                          {attendance.map((row) => (
                            <tr key={row.id}>
                              <td className="px-4 py-3">{row.attendanceDate}</td>
                              <td className="px-4 py-3 font-medium">{row.status}</td>
                              <td className="px-4 py-3 text-slate-600">{row.remark || '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : <p className="card p-4 text-sm text-slate-500">No attendance records have been recorded.</p>}
              </section>

              <section className="space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <BookOpen className="size-5 text-brand-700" aria-hidden="true" />
                    <h2 className="text-lg font-bold text-slate-900">Published academic results</h2>
                  </div>
                  <button type="button" className="btn-primary" onClick={downloadReport} disabled={downloading}>
                    {downloading ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}
                    Download report card (PDF)
                  </button>
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <Metric label="Academic year" value={results?.academicYear || '—'} />
                  <Metric label="Weighted average" value={results?.summary?.weightedAverage == null ? '—' : `${results.summary.weightedAverage}%`} />
                  <Metric label="Subjects passed" value={results?.summary?.subjectsPassed ?? 0} />
                </div>
                {results?.subjects?.length ? (
                  <div className="card overflow-hidden">
                    <div className="overflow-x-auto">
                      <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                          <tr><th className="px-4 py-3">Subject</th><th className="px-4 py-3">Weighted total</th><th className="px-4 py-3">Grade status</th></tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                          {results.subjects.map((subject) => (
                            <tr key={subject.subjectId}>
                              <td className="px-4 py-3">
                                <span className="font-medium text-slate-800">{subject.subjectName}</span>
                                <span className="ml-2 text-xs text-slate-500">{subject.subjectCode}</span>
                              </td>
                              <td className="px-4 py-3">{subject.totalScore}%</td>
                              <td className="px-4 py-3">{subject.status}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : (
                  <EmptyState icon={GraduationCap} title="No published grades yet" description="Results will appear here after teachers publish marks." />
                )}
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
}
