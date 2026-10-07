import SubjectManagement from '../../components/teacher/SubjectManagement';

export default function SubjectManagementPage() {
  return (
    <div className="space-y-6">
      <section className="card p-5">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Academic setup</p>
        <h1 className="text-2xl font-bold text-slate-900">Subjects</h1>
      </section>
      <SubjectManagement />
    </div>
  );
}
