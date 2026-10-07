import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CalendarDays, CheckCheck, Clock3, LoaderCircle, Save, UserRoundX } from 'lucide-react';
import { attendanceApi, sectionApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { ErrorState, Spinner } from '../ui/Feedback';

const STATUS_OPTIONS = [
  { value: 'Present', label: 'Present' },
  { value: 'Absent', label: 'Absent' },
  { value: 'Late', label: 'Late' },
  { value: 'Excused', label: 'Excused' },
];

const TEACHER_LINK_ERROR =
  'Your account is logged in, but not yet linked to an active Teacher profile or class assignment. Please contact your system administrator.';

function teacherAccessMessage(error) {
  const status = error?.status ?? error?.response?.status;
  if (status === 503) {
    return 'The database is temporarily unavailable. Please check the SQL Server service or contact your system administrator.';
  }

  return status === 403 || status === 404
    ? TEACHER_LINK_ERROR
    : error?.friendlyMessage ?? extractErrorMessage(error) ?? 'We could not load attendance. Please try again.';
}

function statusTone(status) {
  if (status === 'Present') return 'bg-emerald-100 text-emerald-700';
  if (status === 'Late') return 'bg-amber-100 text-amber-700';
  if (status === 'Excused') return 'bg-sky-100 text-sky-700';
  return 'bg-rose-100 text-rose-700';
}

export default function AttendanceTracker() {
  const today = new Date().toISOString().slice(0, 10);
  const [sections, setSections] = useState([]);
  const [classes, setClasses] = useState([]);
  const [selectedSection, setSelectedSection] = useState('');
  const [selectedClass, setSelectedClass] = useState('');
  const [attendanceDate, setAttendanceDate] = useState(today);
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [rosterLoading, setRosterLoading] = useState(false);
  const [attendanceLogs, setAttendanceLogs] = useState([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [logsError, setLogsError] = useState('');
  const [logsRefresh, setLogsRefresh] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [referenceRetry, setReferenceRetry] = useState(0);
  const rosterRequestInFlight = useRef(false);
  const selectedClassRef = useRef(selectedClass);
  const fetchClassRosterRef = useRef(null);
  selectedClassRef.current = selectedClass;

  useEffect(() => {
    let cancelled = false;
    async function loadReference() {
      try {
        const [sectionList, myClasses] = await Promise.all([
          sectionApi.list(),
          teacherApi.myClasses(),
        ]);
        if (cancelled) return;

        const availableSections = Array.isArray(sectionList) ? sectionList : [];
        const assignments = Array.isArray(myClasses) ? myClasses : [];
        setSections(availableSections);
        setClasses(assignments);
        if (assignments.length > 0) {
          const firstClass = assignments[0];
          setSelectedClass(String(firstClass.id));
          setSelectedSection(String(firstClass.sectionId));
        } else {
          setSelectedClass('');
          setSelectedSection('');
        }
      } catch (err) {
        if (!cancelled) setError(teacherAccessMessage(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadReference();
    return () => { cancelled = true; };
  }, [referenceRetry]);

  const fetchClassRoster = useCallback(async () => {
    if (!selectedClass) {
      setEntries([]);
      setRosterLoading(false);
      return;
    }

    if (rosterRequestInFlight.current) return;
    rosterRequestInFlight.current = true;
    const requestedClass = selectedClass;
    setRosterLoading(true);
    setEntries([]);
    setError('');
    try {
      const roster = await teacherApi.classRoster(requestedClass);
      if (selectedClassRef.current === requestedClass) {
        const students = Array.isArray(roster)
          ? roster
          : Array.isArray(roster?.students)
            ? roster.students
            : [];
        setEntries(students.map((student) => ({
          studentId: student.studentId,
          studentName: student.fullName,
          studentIdNumber: student.studentIdNumber,
          status: 'Present',
          remark: '',
        })));
      }
    } catch (err) {
      const status = err?.status ?? err?.response?.status;
      if (selectedClassRef.current === requestedClass) {
        setEntries([]);
        setError(status === 404 ? '' : teacherAccessMessage(err));
      }
    } finally {
      rosterRequestInFlight.current = false;
      if (selectedClassRef.current === requestedClass) {
        setRosterLoading(false);
      } else if (selectedClassRef.current) {
        fetchClassRosterRef.current?.();
      }
    }
  }, [selectedClass]);
  fetchClassRosterRef.current = fetchClassRoster;

  useEffect(() => {
    fetchClassRoster();
    const interval = setInterval(fetchClassRoster, 300000);
    return () => clearInterval(interval);
  }, [fetchClassRoster]);

  const selectedAssignment = useMemo(
    () => classes.find((item) => String(item.id) === String(selectedClass)) ?? null,
    [classes, selectedClass],
  );
  const selectedSectionId = selectedAssignment?.sectionId;

  useEffect(() => {
    if (!selectedSectionId) {
      setAttendanceLogs([]);
      setLogsLoading(false);
      return undefined;
    }

    let cancelled = false;
    setLogsLoading(true);
    setLogsError('');
    attendanceApi.list({
      sectionId: selectedSectionId,
      from: attendanceDate,
      to: attendanceDate,
    })
      .then((data) => {
        if (!cancelled) setAttendanceLogs(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        if (!cancelled) {
          setAttendanceLogs([]);
          setLogsError(err.friendlyMessage ?? extractErrorMessage(err));
        }
      })
      .finally(() => {
        if (!cancelled) setLogsLoading(false);
      });

    return () => { cancelled = true; };
  }, [selectedSectionId, attendanceDate, logsRefresh]);

  function updateEntry(studentId, field, value) {
    setEntries((prev) =>
      prev.map((entry) => (entry.studentId === studentId ? { ...entry, [field]: value } : entry)),
    );
  }

  async function saveAttendance() {
    if (!selectedAssignment || entries.length === 0) return;

    setSaving(true);
    setError('');
    try {
      await attendanceApi.bulkMark({
        sectionId: Number(selectedAssignment.sectionId),
        subjectId: Number(selectedAssignment.subjectId),
        attendanceDate: attendanceDate,
        entries: entries.map((entry) => ({
          studentId: entry.studentId,
          status: entry.status,
          remark: entry.remark || null,
        })),
      });
      setError('Attendance saved successfully.');
      setLogsRefresh((value) => value + 1);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="card p-6"><div className="flex items-center gap-3 text-sm text-slate-600"><LoaderCircle className="size-4 animate-spin" aria-hidden="true" /> Loading attendance…</div></div>;

  if (error && classes.length === 0 && !selectedClass) {
    return (
      <ErrorState
        title="Could not load your attendance classes"
        message={error}
        onRetry={() => {
          setError('');
          setReferenceRetry((value) => value + 1);
        }}
      />
    );
  }

  return (
    <div className="space-y-6">
      <section className="card p-5">
        <div className="grid gap-3 md:grid-cols-3">
          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Class</span>
            <select className="input" value={selectedClass} onChange={(event) => setSelectedClass(event.target.value)}>
              <option value="">Select a class</option>
              {classes.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.subjectName} · {item.gradeLevelName} {item.sectionName}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Section</span>
            <select className="input" value={selectedSection} onChange={(event) => setSelectedSection(event.target.value)}>
              <option value="">Select section</option>
              {sections.map((section) => (
                <option key={section.id} value={section.id}>{section.name}</option>
              ))}
            </select>
          </label>

          <label className="space-y-1 text-sm font-medium text-slate-700">
            <span>Date</span>
            <input type="date" className="input" value={attendanceDate} onChange={(event) => setAttendanceDate(event.target.value)} />
          </label>
        </div>
      </section>

      {error && (
        <div className={`rounded-lg border px-3 py-2 text-sm ${error.includes('successfully') ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-rose-200 bg-rose-50 text-rose-700'}`}>
          {error}
        </div>
      )}

      <section className="card overflow-hidden">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Attendance register</p>
            <h2 className="text-lg font-bold text-slate-900">{selectedAssignment ? `${selectedAssignment.subjectName} · ${selectedAssignment.gradeLevelName} ${selectedAssignment.sectionName}` : 'Choose a class'}</h2>
          </div>
          <button type="button" className="btn-primary" onClick={saveAttendance} disabled={saving || !selectedAssignment || entries.length === 0}>
            {saving ? <Spinner className="size-4" /> : <Save className="size-4" aria-hidden="true" />}
            Save attendance
          </button>
        </div>

        <div className="overflow-x-auto">
          {error && selectedClass && entries.length === 0 ? (
            <div className="p-5">
              <ErrorState
                title="Could not load this class list"
                message={error}
                onRetry={() => {
                  setError('');
                  fetchClassRoster();
                }}
              />
            </div>
          ) : (
          <table className="w-full min-w-3xl text-left text-sm">
            <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-5 py-3">Student</th>
                <th className="px-5 py-3">ID</th>
                <th className="px-5 py-3">Status</th>
                <th className="px-5 py-3">Remark</th>
              </tr>
            </thead>
            <tbody>
              {entries.length === 0 ? (
                <tr>
                  <td colSpan="4" className="px-5 py-10 text-center text-slate-500">
                    {rosterLoading
                      ? 'Loading students…'
                      : selectedClass
                        ? 'No students found in this section'
                        : 'Select a class to begin marking attendance.'}
                  </td>
                </tr>
              ) : (
                entries.map((entry) => (
                  <tr key={entry.studentId} className="border-t border-slate-100">
                    <td className="px-5 py-3">
                      <p className="font-medium text-slate-900">{entry.studentName}</p>
                    </td>
                    <td className="px-5 py-3 font-mono text-xs text-slate-600">{entry.studentIdNumber}</td>
                    <td className="px-5 py-3">
                      <select
                        className="input min-w-32"
                        value={entry.status}
                        onChange={(event) => updateEntry(entry.studentId, 'status', event.target.value)}
                      >
                        {STATUS_OPTIONS.map((status) => (
                          <option key={status.value} value={status.value}>{status.label}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-5 py-3">
                      <input
                        className="input min-w-44"
                        value={entry.remark}
                        placeholder="Optional remark"
                        onChange={(event) => updateEntry(entry.studentId, 'remark', event.target.value)}
                      />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          )}
        </div>
      </section>

      <section className="card overflow-hidden">
        <div className="border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-900">Attendance recorded for this date</h2>
          <p className="text-sm text-slate-500">Class-scoped records for {attendanceDate}</p>
        </div>
        {logsError ? (
          <div className="p-5 text-sm text-rose-700">{logsError}</div>
        ) : logsLoading ? (
          <div className="p-5 text-sm text-slate-500">Loading attendance records…</div>
        ) : attendanceLogs.length === 0 ? (
          <div className="p-5 text-sm text-slate-500">No attendance records for this date.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-3xl text-left text-sm">
              <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-5 py-3">Student</th>
                  <th className="px-5 py-3">ID</th>
                  <th className="px-5 py-3">Status</th>
                  <th className="px-5 py-3">Remark</th>
                </tr>
              </thead>
              <tbody>
                {attendanceLogs.map((record) => (
                  <tr key={record.id} className="border-t border-slate-100">
                    <td className="px-5 py-3 font-medium text-slate-900">{record.studentName}</td>
                    <td className="px-5 py-3 font-mono text-xs text-slate-600">{record.studentIdNumber}</td>
                    <td className="px-5 py-3">
                      <span className={`rounded-full px-2 py-1 text-xs font-semibold ${statusTone(record.status)}`}>
                        {record.status}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-slate-600">{record.remark || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
