import { useState } from 'react';
import { UserPlus } from 'lucide-react';
import { authApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Spinner } from '../ui/Feedback';
import ProfilePhotoField from './ProfilePhotoField';

/**
 * Public application for a place at the school.
 *
 * No password is collected: the school issues the sign-in address and a temporary password when
 * an administrator approves the application, and emails them to the address given here. Nothing
 * is signed in on submit, so this form reports back instead of redirecting.
 *
 * @param gradeLevels grades from /api/public/overview.
 * @param sections    sections from the same payload.
 * @param onSubmitted called with the API's receipt message once the queue accepts it.
 */
export default function StudentRegistrationForm({ gradeLevels = [], sections = [], onSubmitted }) {
  const [form, setForm] = useState({
    fullName: '',
    email: '',
    requestedRole: 'Student',
    gradeLevelId: '',
    sectionId: '',
  });
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [photo, setPhoto] = useState(null);
  const [photoPending, setPhotoPending] = useState(false);

  const update = (key) => (event) => setForm((prev) => ({ ...prev, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    setError(null);
    if (photoPending) {
      setError('Confirm or cancel the selected photo before submitting your registration.');
      return;
    }
    setSubmitting(true);

    try {
      const receipt = await authApi.registerStudent({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        requestedRole: form.requestedRole,
        gradeLevelId: form.requestedRole === 'Student' ? Number(form.gradeLevelId) : null,
        sectionId: form.requestedRole === 'Student' ? Number(form.sectionId) : null,
        photo,
      });

      onSubmitted(
        receipt?.message ??
          'Your registration has been received and is waiting for the school to review it.',
      );
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {error && (
        <Alert variant="error" title="Registration failed" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      <div>
        <label className="label" htmlFor="register-name">
          Full name
        </label>
        <input
          id="register-name"
          className="input"
          required
          maxLength={150}
          autoComplete="name"
          value={form.fullName}
          onChange={update('fullName')}
        />
      </div>

      <div>
        <label className="label" htmlFor="register-email">
          Your email address
        </label>
        <input
          id="register-email"
          type="email"
          className="input"
          required
          autoComplete="email"
          value={form.email}
          onChange={update('email')}
          placeholder="you@example.com"
        />
        <p className="mt-1 text-xs text-slate-500">
          Where the school will send your school sign-in address and temporary password once your
          registration is approved.
        </p>
      </div>

      <div>
        <label className="label" htmlFor="register-role">
          Applying as
        </label>
        <select
          id="register-role"
          className="input"
          required
          value={form.requestedRole}
          onChange={update('requestedRole')}
        >
          <option value="Student">Student</option>
          <option value="Teacher">Teacher</option>
        </select>
      </div>

      {form.requestedRole === 'Student' && <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="label" htmlFor="register-grade">
            Grade
          </label>
          <select
            id="register-grade"
            className="input"
            required
            value={form.gradeLevelId}
            onChange={update('gradeLevelId')}
          >
            <option value="">Choose…</option>
            {gradeLevels.map((grade) => (
              <option key={grade.id} value={grade.id}>
                {grade.name}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="label" htmlFor="register-section">
            Section
          </label>
          <select
            id="register-section"
            className="input"
            required
            value={form.sectionId}
            onChange={update('sectionId')}
          >
            <option value="">Choose…</option>
            {sections.map((section) => (
              <option key={section.id} value={section.id}>
                {section.name}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-slate-500">The school may move you to another section.</p>
        </div>
      </div>}

      <ProfilePhotoField
        file={photo}
        onChange={setPhoto}
        onPendingChange={setPhotoPending}
        disabled={submitting}
      />

      <p className="rounded-lg border border-brand-100 bg-brand-50 px-3 py-2 text-sm text-brand-800">
        A unique Smart ID is generated automatically when your registration is approved. You can
        include your optional photo now; it will be attached to your account and Smart ID if approved.
      </p>

      <button type="submit" className="btn-primary w-full" disabled={submitting}>
        {submitting ? <Spinner className="size-4" /> : <UserPlus className="size-4" aria-hidden="true" />}
        {submitting ? 'Submitting…' : 'Submit registration'}
      </button>
    </form>
  );
}
