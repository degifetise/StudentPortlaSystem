import { useCallback, useMemo, useState } from 'react';
import { CalendarDays, ClipboardList } from 'lucide-react';
import { attendanceApi } from '../../services/endpoints';
import { useApiResource } from '../../hooks/useApiResource';
import { EmptyState, ErrorState, LoadingPanel } from '../../components/ui/Feedback';

function formatDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

const today = formatDate(new Date());
const monthStart = formatDate(new Date(new Date().getFullYear(), new Date().getMonth(), 1));

function Metric({ label, value }) {
  return (
    <div className="card p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-bold text-slate-900">{value}</p>
    </div>
  );
}

export default function AdminAttendance() {
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [range, setRange] = useState({ from: monthStart, to: today });
  const [dateError, setDateError] = useState('');
  const { from: rangeFrom, to: rangeTo } = range;

  const fetchSummary = useCallback(
    () => attendanceApi.summary({ from: rangeFrom, to: rangeTo }),
    [rangeFrom, rangeTo],
  );
  const { data, error, loading, reload, reloading } = useApiResource(fetchSummary);
  const records = useMemo(() => (Array.isArray(data) ? data : []), [data]);
  const totals = useMemo(() => records.reduce((summary, record) => ({
    present: summary.present + (record.presentCount ?? 0),
    absent: summary.absent + (record.absentCount ?? 0),
    late: summary.late + (record.lateCount ?? 0),
    excused: summary.excused + (record.excusedCount ?? 0),
    total: summary.total + (record.totalCount ?? 0),
  }), { present: 0, absent: 0, late: 0, excused: 0, total: 0 }), [records]);

  function applyRange(event) {
    event.preventDefault();
    if (!from || !to || from > to) {
      setDateError('Choose a valid date range. The start date must be on or before the end date.');
      return;
    }
    setDateError('');
    setRange({ from, to });
  }

  if (loading) return <LoadingPanel label="Loading attendance summaries…" />;
  if (error) {
    return <ErrorState title="Could not load attendance" message={error} onRetry={reload} retrying={reloading} />;
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Attendance</h1>
          <p className="text-sm text-slate-500">System-wide attendance by student for the selected date range.</p>
        </div>
        <form onSubmit={applyRange} className="flex flex-wrap items-end gap-2">
          <label className="text-sm font-medium text-slate-700">
            <span className="mb-1 block">From</span>
            <input type="date" className="input" value={from} onChange={(event) => setFrom(event.target.value)} />
          </label>
          <label className="text-sm font-medium text-slate-700">
            <span className="mb-1 block">To</span>
            <input type="date" className="input" value={to} onChange={(event) => setTo(event.target.value)} />
          </label>
          <button type="submit" className="btn-primary">
            <CalendarDays className="size-4" aria-hidden="true" />
            Apply
          </button>
        </form>
      </header>

      {dateError && <p role="alert" className="text-sm text-rose-700">{dateError}</p>}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Metric label="Records" value={totals.total} />
        <Metric label="Present" value={totals.present} />
        <Metric label="Absent" value={totals.absent} />
        <Metric label="Late" value={totals.late} />
        <Metric label="Excused" value={totals.excused} />
      </div>

      <section className="card overflow-hidden">
        <div className="border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-900">By student</h2>
          <p className="text-sm text-slate-500">{range.from} to {range.to}</p>
        </div>
        {records.length === 0 ? (
          <div className="p-5">
            <EmptyState icon={ClipboardList} title="No attendance records in this period" description="Records will appear here after attendance has been recorded." />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-3xl text-left text-sm">
              <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-5 py-3">Student</th>
                  <th className="px-5 py-3">ID</th>
                  <th className="px-5 py-3">Present</th>
                  <th className="px-5 py-3">Absent</th>
                  <th className="px-5 py-3">Late</th>
                  <th className="px-5 py-3">Excused</th>
                  <th className="px-5 py-3">Total</th>
                </tr>
              </thead>
              <tbody>
                {records.map((record) => (
                  <tr key={record.studentId} className="border-t border-slate-100">
                    <td className="px-5 py-3 font-medium text-slate-900">{record.studentName}</td>
                    <td className="px-5 py-3 font-mono text-xs text-slate-600">{record.studentIdNumber}</td>
                    <td className="px-5 py-3">{record.presentCount}</td>
                    <td className="px-5 py-3">{record.absentCount}</td>
                    <td className="px-5 py-3">{record.lateCount}</td>
                    <td className="px-5 py-3">{record.excusedCount}</td>
                    <td className="px-5 py-3 font-semibold">{record.totalCount}</td>
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