import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link2, Pencil, Plus, Save, UserPlus, UsersRound, Trash2, X } from 'lucide-react';
import { guardianApi, studentApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

function RegisterParentModal({ onClose, onCreated, onError }) {
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', phoneNumber: '' });
  const [saving, setSaving] = useState(false);

  const update = (key) => (event) => setForm((current) => ({ ...current, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    setSaving(true);
    onError(null);
    try {
      const created = await guardianApi.registerParent({
        ...form,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim() || null,
      });
      onCreated(created);
      onClose();
    } catch (error) {
      onError(error.friendlyMessage ?? extractErrorMessage(error));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/50 p-4" role="presentation">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="register-parent-title"
        className="card w-full max-w-xl p-6 shadow-xl"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Admin only</p>
            <h2 id="register-parent-title" className="mt-1 text-xl font-bold text-slate-900">Register parent</h2>
            <p className="mt-1 text-sm text-slate-600">A temporary password is generated and shown once after creation.</p>
          </div>
          <button type="button" className="btn" onClick={onClose}>Close</button>
        </div>
        <form onSubmit={submit} className="mt-5 grid gap-4 sm:grid-cols-2">
          <label className="space-y-1 text-sm font-medium text-slate-700">
            First name
            <input className="input" required maxLength={75} value={form.firstName} onChange={update('firstName')} />
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700">
            Last name
            <input className="input" required maxLength={75} value={form.lastName} onChange={update('lastName')} />
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700 sm:col-span-2">
            Email
            <input className="input" type="email" required maxLength={256} autoComplete="email" value={form.email} onChange={update('email')} />
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700 sm:col-span-2">
            Phone number
            <input className="input" type="tel" maxLength={30} autoComplete="tel" value={form.phoneNumber} onChange={update('phoneNumber')} />
          </label>
          <div className="flex justify-end gap-2 sm:col-span-2">
            <button type="button" className="btn" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn-primary" disabled={saving}>
              {saving ? <Spinner className="size-4" /> : <UserPlus className="size-4" aria-hidden="true" />}
              Register parent
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

export default function AdminGuardians() {
  const [guardians, setGuardians] = useState([]);
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [studentsLoading, setStudentsLoading] = useState(false);
  const hasInitializedStudentSearch = useRef(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [showRegister, setShowRegister] = useState(false);
  const [credentials, setCredentials] = useState(null);
  const [guardianSearch, setGuardianSearch] = useState('');
  const [studentSearch, setStudentSearch] = useState('');
  const [selectedGuardianId, setSelectedGuardianId] = useState('');
  const [selectedStudentId, setSelectedStudentId] = useState('');
  const [relationship, setRelationship] = useState('');
  const [linking, setLinking] = useState(false);
  const [unlinkingPair, setUnlinkingPair] = useState(null);
  const [unlinking, setUnlinking] = useState(false);
  const [editingLink, setEditingLink] = useState(null);
  const [savingLink, setSavingLink] = useState(false);

  const loadGuardians = useCallback(async () => {
    const result = await guardianApi.list();
    setGuardians(Array.isArray(result) ? result : []);
  }, []);

  const loadStudents = useCallback(async (search = '') => {
    setStudentsLoading(true);
    try {
      const result = await studentApi.list({ page: 1, pageSize: 100, search: search.trim() || undefined });
      setStudents(result.items ?? []);
    } catch (loadError) {
      setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
    } finally {
      setStudentsLoading(false);
    }
  }, []);

  const reload = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      await Promise.all([loadGuardians(), loadStudents()]);
    } catch (loadError) {
      setError(loadError.friendlyMessage ?? extractErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [loadGuardians, loadStudents]);

  useEffect(() => { reload(); }, [reload]);

  useEffect(() => {
    if (!hasInitializedStudentSearch.current) {
      hasInitializedStudentSearch.current = true;
      return undefined;
    }
    const timer = window.setTimeout(() => loadStudents(studentSearch), 250);
    return () => window.clearTimeout(timer);
  }, [studentSearch, loadStudents]);

  const visibleGuardians = useMemo(() => {
    const term = guardianSearch.trim().toLowerCase();
    if (!term) return guardians;
    return guardians.filter((guardian) =>
      `${guardian.fullName} ${guardian.email} ${guardian.phoneNumber ?? ''}`.toLowerCase().includes(term),
    );
  }, [guardianSearch, guardians]);

  async function linkStudent(event) {
    event.preventDefault();
    setLinking(true);
    setError('');
    setNotice('');
    try {
      await guardianApi.linkStudent({
        guardianId: Number(selectedGuardianId),
        studentId: Number(selectedStudentId),
        relationship: relationship.trim(),
      });
      setSelectedStudentId('');
      setRelationship('');
    } catch (linkError) {
      setError(linkError.friendlyMessage ?? extractErrorMessage(linkError));
      setLinking(false);
      return;
    }

    setNotice('Student linked to parent.');
    setLinking(false);
    try {
      await loadGuardians();
    } catch (loadError) {
      setError(`The link was saved, but the parent list could not be refreshed: ${loadError.friendlyMessage ?? extractErrorMessage(loadError)}`);
    }
  }

  async function confirmUnlink(guardianId, studentId, studentName) {
    setUnlinkingPair({ guardianId, studentId, studentName });
  }

  async function handleUnlink() {
    if (!unlinkingPair) return;
    setUnlinking(true);
    setError('');
    try {
      await guardianApi.unlinkStudent(unlinkingPair.guardianId, unlinkingPair.studentId);
      setUnlinkingPair(null);
      setNotice(`${unlinkingPair.studentName} has been unlinked from this parent.`);
    } catch (unlinkError) {
      setError(unlinkError.friendlyMessage ?? extractErrorMessage(unlinkError));
    } finally {
      setUnlinking(false);
      try {
        await loadGuardians();
      } catch (loadError) {
        setError(`The unlink was saved, but the parent list could not be refreshed: ${loadError.friendlyMessage ?? extractErrorMessage(loadError)}`);
      }
    }
  }

  async function saveRelationship(event) {
    event.preventDefault();
    const relationshipValue = editingLink?.relationship.trim();
    if (!editingLink || !relationshipValue) {
      setError('Enter a relationship before saving.');
      return;
    }

    setSavingLink(true);
    setError('');
    setNotice('');
    try {
      await guardianApi.updateLink({
        guardianId: editingLink.guardianId,
        studentId: editingLink.studentId,
        relationship: relationshipValue,
      });
      setEditingLink(null);
      setNotice('Student relationship updated.');
      await loadGuardians();
    } catch (updateError) {
      setError(updateError.friendlyMessage ?? extractErrorMessage(updateError));
    } finally {
      setSavingLink(false);
    }
  }

  async function handleParentCreated(result) {
    setCredentials(result);
    setNotice('Parent account created. Copy the temporary password now; it will not be shown again.');
    try {
      await loadGuardians();
    } catch (loadError) {
      setError(`The account was created, but the parent list could not be refreshed: ${loadError.friendlyMessage ?? extractErrorMessage(loadError)}`);
    }
  }

  if (loading) return <LoadingPanel label="Loading parent accounts…" />;
  if (error && guardians.length === 0) {
    return <ErrorState title="Could not load parent accounts" message={error} onRetry={reload} retrying={loading} />;
  }

  return (
    <div className="space-y-6">
      {error && <Alert variant="error" title="Parent management error" onDismiss={() => setError('')}>{error}</Alert>}
      {notice && <Alert variant="success" onDismiss={() => setNotice('')}>{notice}</Alert>}
      {credentials?.temporaryPassword && (
        <Alert variant="warning" title="Save this temporary password">
          <p>Account: <strong>{credentials.fullName}</strong> ({credentials.email})</p>
          <p className="mt-1 font-mono">{credentials.temporaryPassword}</p>
        </Alert>
      )}

      <section className="card flex flex-wrap items-center justify-between gap-4 p-5">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Family access</p>
          <h1 className="mt-1 text-2xl font-bold text-slate-900">Parents & guardians</h1>
          <p className="mt-1 text-sm text-slate-600">Create guardian accounts and give read-only access to linked students.</p>
        </div>
        <button type="button" className="btn-primary" onClick={() => setShowRegister(true)}>
          <Plus className="size-4" aria-hidden="true" />
          Register parent
        </button>
      </section>

      <section className="card p-5">
        <div className="mb-4 flex items-center gap-2">
          <Link2 className="size-5 text-brand-700" aria-hidden="true" />
          <div>
            <h2 className="font-bold text-slate-900">Link student to parent</h2>
            <p className="text-sm text-slate-500">A parent can be linked to multiple students and each student to multiple guardians.</p>
          </div>
        </div>
        <form onSubmit={linkStudent} className="grid gap-3 md:grid-cols-2">
          <label className="space-y-1 text-sm font-medium text-slate-700">
            Find parent
            <input className="input" type="search" placeholder="Search name or email" value={guardianSearch} onChange={(event) => setGuardianSearch(event.target.value)} />
            <select className="input" required value={selectedGuardianId} onChange={(event) => setSelectedGuardianId(event.target.value)}>
              <option value="">Select parent…</option>
              {visibleGuardians.map((guardian) => (
                <option key={guardian.guardianId} value={guardian.guardianId}>{guardian.fullName} · {guardian.email}</option>
              ))}
            </select>
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700">
            Find student
            <input className="input" type="search" placeholder="Search name, ID or email" value={studentSearch} onChange={(event) => setStudentSearch(event.target.value)} />
            <select className="input" required value={selectedStudentId} onChange={(event) => setSelectedStudentId(event.target.value)} disabled={studentsLoading}>
              <option value="">{studentsLoading ? 'Searching…' : 'Select student…'}</option>
              {students.map((student) => (
                <option key={student.id} value={student.id}>{student.fullName} · {student.studentIdNumber} · {student.gradeLevelName} {student.sectionName}</option>
              ))}
            </select>
          </label>
          <label className="space-y-1 text-sm font-medium text-slate-700 md:col-span-2">
            Relationship
            <input className="input" required maxLength={50} placeholder="e.g. Mother, Father, Legal Guardian" value={relationship} onChange={(event) => setRelationship(event.target.value)} />
          </label>
          <div className="md:col-span-2">
            <button type="submit" className="btn-primary" disabled={linking || studentsLoading || !selectedGuardianId || !selectedStudentId}>
              {linking ? <Spinner className="size-4" /> : <Link2 className="size-4" aria-hidden="true" />}
              Link student
            </button>
          </div>
        </form>
      </section>

      <section className="card overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 p-5">
          <div className="flex items-center gap-2">
            <UsersRound className="size-5 text-brand-700" aria-hidden="true" />
            <h2 className="font-bold text-slate-900">Registered parents</h2>
          </div>
          <input className="input max-w-sm" type="search" placeholder="Filter parents…" value={guardianSearch} onChange={(event) => setGuardianSearch(event.target.value)} />
        </div>
        {visibleGuardians.length === 0 ? (
          <EmptyState icon={UsersRound} title="No parent accounts found" description="Register a parent account to begin linking students." />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr><th className="px-5 py-3">Parent</th><th className="px-5 py-3">Contact</th><th className="px-5 py-3">Linked children</th></tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {visibleGuardians.map((guardian) => (
                  <tr key={guardian.guardianId} className="align-top">
                    <td className="px-5 py-4 font-semibold text-slate-900">{guardian.fullName}</td>
                    <td className="px-5 py-4 text-slate-600">{guardian.email}<br />{guardian.phoneNumber || 'No phone on file'}</td>
                    <td className="px-5 py-4">
                      {guardian.linkedStudents?.length ? (
                        <ul className="space-y-2">
                          {guardian.linkedStudents.map((student) => (
                            <li key={student.studentId} className="flex items-start justify-between gap-2">
                              <div className="flex-1">
                                <p className="font-medium text-slate-800">{student.fullName} <span className="font-mono text-xs text-slate-500">{student.studentIdNumber}</span></p>
                                <p className="text-xs text-slate-500">{student.email} · {student.gradeLevelName} {student.sectionName}</p>
                                {editingLink?.guardianId === guardian.guardianId && editingLink.studentId === student.studentId ? (
                                  <form onSubmit={saveRelationship} className="mt-2 flex flex-wrap items-center gap-2">
                                    <input
                                      className="input max-w-xs"
                                      aria-label={`Relationship to ${student.fullName}`}
                                      required
                                      maxLength={50}
                                      value={editingLink.relationship}
                                      onChange={(event) => setEditingLink((current) => ({ ...current, relationship: event.target.value }))}
                                    />
                                    <button type="submit" className="btn-primary" disabled={savingLink}>
                                      {savingLink ? <Spinner className="size-4" /> : <Save className="size-4" aria-hidden="true" />}
                                      Save
                                    </button>
                                    <button type="button" className="btn" onClick={() => setEditingLink(null)} disabled={savingLink} aria-label="Cancel relationship edit">
                                      <X className="size-4" aria-hidden="true" />
                                      Cancel
                                    </button>
                                  </form>
                                ) : (
                                  <p className="mt-1 text-xs text-slate-500">Relationship: {student.relationship}</p>
                                )}
                              </div>
                              {!(editingLink?.guardianId === guardian.guardianId && editingLink.studentId === student.studentId) && (
                                <button
                                  type="button"
                                  onClick={() => setEditingLink({
                                    guardianId: guardian.guardianId,
                                    studentId: student.studentId,
                                    relationship: student.relationship,
                                  })}
                                  className="flex-shrink-0 rounded bg-slate-100 p-1 text-slate-600 hover:bg-slate-200"
                                  title="Edit relationship"
                                  aria-label={`Edit relationship for ${student.fullName}`}
                                >
                                  <Pencil className="size-4" aria-hidden="true" />
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={() => confirmUnlink(guardian.guardianId, student.studentId, student.fullName)}
                                className="flex-shrink-0 rounded bg-red-50 p-1 text-red-600 hover:bg-red-100 hover:text-red-700 transition-colors"
                                title="Unlink student"
                                aria-label={`Unlink ${student.fullName}`}
                              >
                                <Trash2 className="size-4" aria-hidden="true" />
                              </button>
                            </li>
                          ))}
                        </ul>
                      ) : <span className="text-slate-500">No students linked</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {showRegister && (
        <RegisterParentModal
          onClose={() => setShowRegister(false)}
          onCreated={handleParentCreated}
          onError={setError}
        />
      )}

      {unlinkingPair && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/50 p-4" role="presentation">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-unlink-title"
            className="card w-full max-w-sm p-6 shadow-xl"
          >
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-red-700">Confirm unlink</p>
              <h2 id="confirm-unlink-title" className="mt-1 text-lg font-bold text-slate-900">
                Remove access for {unlinkingPair.studentName}?
              </h2>
              <p className="mt-2 text-sm text-slate-600">
                The parent will immediately lose access to this student's attendance, academic results, and report cards.
              </p>
            </div>
            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                className="btn"
                onClick={() => setUnlinkingPair(null)}
                disabled={unlinking}
              >
                Keep linked
              </button>
              <button
                type="button"
                className="btn-danger"
                onClick={handleUnlink}
                disabled={unlinking}
              >
                {unlinking ? <Spinner className="size-4" /> : <Trash2 className="size-4" aria-hidden="true" />}
                Unlink
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
