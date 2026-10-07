import { useCallback, useMemo } from 'react';
import { BookOpenCheck, UserCheck, UserX } from 'lucide-react';
import { PassFailChart } from '../../components/analytics/AnalyticsCharts';
import { ErrorState, LoadingPanel } from '../../components/ui/Feedback';
import { useApiResource } from '../../hooks/useApiResource';
import { analyticsApi } from '../../services/endpoints';
import { StatCard } from '../admin/adminShared';

export default function TeacherAnalytics() {
  const fetchPerformance = useCallback(() => analyticsApi.teacherPerformance(), []);
  const { data, error, loading, reload, reloading } = useApiResource(fetchPerformance);
  const passRate = useMemo(
    () => data?.studentsWithResults
      ? Math.round((data.passedStudents / data.studentsWithResults) * 100)
      : 0,
    [data],
  );

  if (loading) return <LoadingPanel label="Loading class performance…" />;
  if (error) {
    return (
      <ErrorState
        title="Could not load class performance"
        message={error}
        onRetry={reload}
        retrying={reloading}
      />
    );
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Class performance</h1>
        <p className="text-sm text-slate-500">Published results across your active subject and section assignments.</p>
      </header>

      <div className="grid gap-3 sm:grid-cols-3">
        <StatCard icon={BookOpenCheck} label="Assigned classes" value={data?.assignedClassCount ?? 0} />
        <StatCard icon={UserCheck} label="Passed students" value={data?.passedStudents ?? 0} tone="green" hint={`${passRate}% of students with results`} />
        <StatCard icon={UserX} label="Failed students" value={data?.failedStudents ?? 0} tone="amber" hint="Below the 50% pass mark" />
      </div>

      <PassFailChart
        passed={data?.passedStudents ?? 0}
        failed={data?.failedStudents ?? 0}
        title="Your assigned classes"
      />

      <p className="text-xs text-slate-500">
        Pass/fail totals include students with published results in your assigned subjects and sections.
      </p>
    </div>
  );
}
