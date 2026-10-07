const GRADE_COLORS = {
  9: 'bg-sky-500',
  10: 'bg-violet-500',
  11: 'bg-amber-500',
  12: 'bg-emerald-500',
};

export function GradeEnrollmentChart({ grades = [] }) {
  const maxCount = Math.max(1, ...grades.map((grade) => grade.studentCount ?? 0));

  return (
    <section className="card p-5" aria-labelledby="grade-chart-title">
      <h2 id="grade-chart-title" className="font-semibold text-slate-900">Enrolment by grade</h2>
      <p className="mt-1 text-sm text-slate-500">Active students in Nursery through Grade 12</p>
      <div className="mt-6 space-y-4" role="img" aria-label="Bar chart of student enrolment by grade">
        {[9, 10, 11, 12].map((level) => {
          const count = grades.find((grade) => grade.gradeLevel === level)?.studentCount ?? 0;
          const width = (count / maxCount) * 100;

          return (
            <div key={level} className="grid grid-cols-[4.5rem_1fr_3rem] items-center gap-3">
              <span className="text-sm font-medium text-slate-700">Grade {level}</span>
              <div className="h-3 overflow-hidden rounded-full bg-slate-100">
                <div
                  className={`h-full rounded-full ${GRADE_COLORS[level]}`}
                  style={{ width: `${width}%` }}
                />
              </div>
              <span className="text-right text-sm font-semibold tabular-nums text-slate-700">{count}</span>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export function PassFailChart({ passed = 0, failed = 0, title = 'Pass and fail' }) {
  const total = passed + failed;
  const circumference = 2 * Math.PI * 42;
  const passedLength = total ? (passed / total) * circumference : 0;
  const failedLength = total ? (failed / total) * circumference : 0;

  return (
    <section className="card p-5" aria-labelledby="pass-fail-title">
      <h2 id="pass-fail-title" className="font-semibold text-slate-900">{title}</h2>
      <p className="mt-1 text-sm text-slate-500">Students with published results · pass mark 50%</p>
      <div className="mt-5 flex flex-wrap items-center justify-center gap-8">
        <div className="relative size-36" role="img" aria-label={`${passed} passed, ${failed} failed`}>
          <svg viewBox="0 0 100 100" className="size-full -rotate-90" aria-hidden="true">
            <circle cx="50" cy="50" r="42" fill="none" stroke="#e2e8f0" strokeWidth="12" />
            {total > 0 && (
              <>
                <circle
                  cx="50"
                  cy="50"
                  r="42"
                  fill="none"
                  stroke="#16a34a"
                  strokeWidth="12"
                  strokeDasharray={`${passedLength} ${circumference - passedLength}`}
                />
                <circle
                  cx="50"
                  cy="50"
                  r="42"
                  fill="none"
                  stroke="#e11d48"
                  strokeWidth="12"
                  strokeDasharray={`${failedLength} ${circumference - failedLength}`}
                  strokeDashoffset={-passedLength}
                />
              </>
            )}
          </svg>
          <div className="absolute inset-0 grid place-content-center text-center">
            <span className="text-2xl font-bold tabular-nums text-slate-900">{total}</span>
            <span className="text-xs text-slate-500">students</span>
          </div>
        </div>
        <div className="space-y-3">
          <div className="flex items-center gap-2 text-sm">
            <span className="size-3 rounded-full bg-green-600" aria-hidden="true" />
            <span className="text-slate-600">Passed</span>
            <strong className="ml-4 tabular-nums text-slate-900">{passed}</strong>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span className="size-3 rounded-full bg-rose-600" aria-hidden="true" />
            <span className="text-slate-600">Failed</span>
            <strong className="ml-4 tabular-nums text-slate-900">{failed}</strong>
          </div>
        </div>
      </div>
    </section>
  );
}
