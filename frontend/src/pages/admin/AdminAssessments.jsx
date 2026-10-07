import { useEffect, useState } from 'react';
import { BookOpenCheck } from 'lucide-react';
import { assessmentApi, sectionApi, subjectApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { EmptyState, ErrorState, LoadingPanel } from '../../components/ui/Feedback';
import AssessmentForm from '../../components/assessments/AssessmentForm';

export default function AdminAssessments() {
  const [subjects, setSubjects] = useState([]);
  const [sections, setSections] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let cancelled = false;
    async function loadReferences() {
      try {
        const [subjectList, sectionList] = await Promise.all([
          subjectApi.list({ pageSize: 100 }),
          sectionApi.list(),
        ]);
        if (!cancelled) {
          setSubjects(Array.isArray(subjectList) ? subjectList : subjectList?.items ?? []);
          setSections(Array.isArray(sectionList) ? sectionList : []);
        }
      } catch (loadError) {
        if (!cancelled) setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    loadReferences();
    return () => { cancelled = true; };
  }, []);

  if (loading) return <LoadingPanel label="Loading assessment options…" />;
  if (error) return <ErrorState title="Could not load assessment options" message={error} />;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Assessments</h1>
        <p className="text-sm text-slate-500">Create a subject assessment and set its maximum marks.</p>
      </header>
      {notice && <p role="status" className="text-sm text-emerald-700">{notice}</p>}
      {subjects.length === 0 ? (
        <EmptyState icon={BookOpenCheck} title="No active subjects" description="Create or activate a subject before adding assessments." />
      ) : (
        <section className="card overflow-hidden">
          <AssessmentForm
            subjects={subjects}
            sections={sections}
            allowOther
            forceSubjectSelector
            onSubmit={async (payload) => {
              const created = await assessmentApi.create(payload);
              setNotice(`${created.title} was created for ${created.subjectName}.`);
            }}
          />
        </section>
      )}
    </div>
  );
}