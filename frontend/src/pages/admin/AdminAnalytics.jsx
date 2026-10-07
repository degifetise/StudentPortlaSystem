import { useCallback, useMemo } from 'react';
import { BarChart3, GraduationCap, UserCheck, UserX, Users } from 'lucide-react';
import { StatCard } from './adminShared';
import { GradeEnrollmentChart, PassFailChart } from '../../components/analytics/AnalyticsCharts';
import { ErrorState, LoadingPanel } from '../../components/ui/Feedback';
import { useApiResource } from '../../hooks/useApiResource';
import { analyticsApi } from '../../services/endpoints';

export default function AdminAnalytics() {
  const fetchOverview = useCallback(() => analyticsApi.adminOverview(), []);
  const { data, error, loading, reload, reloading } = useApiResource(fetchOverview);
  const grades = useMemo(() => data?.gradeDistribution ?? [], [data]);
  const passRate = useMemo(
    () => data?.studentsWithResults
      ? Math.round((data.passedStudents / data.studentsWithResults) * 100)
      : 0,
    [data],
  );

  if (loading) return <LoadingPanel label="Loading school analytics…" />;
  if (error) {
    return (
      <ErrorState
        title="Could not load analytics"
        message={error}
        onRetry={reload}
        retrying={reloading}
      />
    );
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">School analytics</h1>
        <p className="text-sm text-slate-500">Active enrolment and performance from published results.</p>
      </header>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={Users} label="Total students" value={data?.totalStudents ?? 0} />
        <StatCard icon={GraduationCap} label="Total teachers" value={data?.totalTeachers ?? 0} tone="brand" />
        <StatCard icon={UserCheck} label="Passed students" value={data?.passedStudents ?? 0} tone="green" hint={`${passRate}% of students with results`} />
        <StatCard icon={UserX} label="Failed students" value={data?.failedStudents ?? 0} tone="amber" hint="Below the 50% pass mark" />
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <GradeEnrollmentChart grades={grades} />
        <PassFailChart
          passed={data?.passedStudents ?? 0}
          failed={data?.failedStudents ?? 0}
          title="Overall student performance"
        />
      </div>

      <p className="flex items-center gap-2 text-xs text-slate-500">
        <BarChart3 className="size-4" aria-hidden="true" />
        Pass/fail totals include active students with at least one published subject result.
      </p>
    </div>
  );
}
