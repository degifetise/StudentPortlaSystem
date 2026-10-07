import AttendanceTracker from '../../components/teacher/AttendanceTracker';

export default function AttendancePage() {
  return (
    <div className="space-y-6">
      <section className="card p-5">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Attendance</p>
        <h1 className="text-2xl font-bold text-slate-900">Class attendance tracker</h1>
      </section>
      <AttendanceTracker />
    </div>
  );
}
