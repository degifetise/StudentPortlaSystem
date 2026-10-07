import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle, IdCard, Printer, RefreshCw, Search, Trash2, Upload } from 'lucide-react';
import SmartCardView, { SmartCardFace } from '../../components/smart-id/SmartCardView';
import { API_BASE_URL } from '../../services/api';
import { smartIdApi } from '../../services/endpoints';

export default function SmartIDPrintPage() {
  const [lookupText, setLookupText] = useState('');
  const [lookup, setLookup] = useState(null);
  const [searchText, setSearchText] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [users, setUsers] = useState([]);
  const [selectedIds, setSelectedIds] = useState([]);
  const [previewUserId, setPreviewUserId] = useState('');
  const [fullName, setFullName] = useState('');
  const [status, setStatus] = useState('ACTIVE');
  const [rotateQrToken, setRotateQrToken] = useState(false);
  const [busy, setBusy] = useState(false);
  const [photoBusy, setPhotoBusy] = useState(false);
  const [listBusy, setListBusy] = useState(false);
  const [error, setError] = useState('');

  const selectedCards = useMemo(
    () => users
      .filter((user) => selectedIds.includes(user.userId) && user.card)
      .map((user) => user.card),
    [users, selectedIds],
  );
  const preview = users.find((user) => user.userId === previewUserId)?.card
    ?? (lookup?.userId === previewUserId ? lookup.card : null);
  const printSheets = [];
  for (let index = 0; index < selectedCards.length; index += 10) {
    printSheets.push(selectedCards.slice(index, index + 10));
  }

  useEffect(() => {
    setFullName(preview?.fullName ?? '');
    setStatus(preview?.cardStatus ?? 'ACTIVE');
    setRotateQrToken(false);
  }, [preview?.userId, preview?.fullName, preview?.cardStatus]);

  const loadUsers = useCallback(async (search, role) => {
    setListBusy(true);
    setError('');
    try {
      const result = await smartIdApi.adminCards({
        ...(search.trim() ? { search: search.trim() } : {}),
        ...(role ? { role } : {}),
      });
      setUsers(result);
      setSelectedIds((current) => current.filter((id) => result.some((user) => user.userId === id)));
    } catch (loadError) {
      setError(loadError.friendlyMessage ?? 'Smart ID accounts could not be loaded.');
    } finally {
      setListBusy(false);
    }
  }, []);

  useEffect(() => {
    loadUsers('', '');
  }, [loadUsers]);

  async function findUser(event) {
    event.preventDefault();
    if (!lookupText.trim()) return;

    setBusy(true);
    setError('');
    setLookup(null);
    try {
      const result = await smartIdApi.adminLookup(lookupText.trim());
      setLookup(result);
      setFullName(result.fullName);
      if (result.card) {
        setStatus(result.card.cardStatus);
        setPreviewUserId(result.userId);
      } else {
        setPreviewUserId('');
      }
    } catch (lookupError) {
      setError(lookupError.friendlyMessage ?? 'No user matched that Student or Staff ID.');
    } finally {
      setBusy(false);
    }
  }

  async function generateCard(user) {
    setBusy(true);
    setError('');
    try {
      const card = await smartIdApi.generateForUser(user.userId);
      const updated = { ...user, cardGenerated: true, card };
      setLookup(updated);
      setPreviewUserId(user.userId);
      await loadUsers(searchText, roleFilter);
    } catch (generateError) {
      setError(generateError.friendlyMessage ?? 'The Smart ID could not be generated.');
    } finally {
      setBusy(false);
    }
  }

  async function saveCardChanges() {
    if (!preview) return;
    setBusy(true);
    setError('');
    try {
      const updatedCard = await smartIdApi.updateAdminCard(preview.userId, {
        fullName,
        status,
        rotateQrToken,
      });
      setLookup((current) => current?.userId === preview.userId
        ? { ...current, fullName: updatedCard.fullName, card: updatedCard, cardGenerated: true }
        : current);
      await loadUsers(searchText, roleFilter);
      setRotateQrToken(false);
    } catch (updateError) {
      setError(updateError.friendlyMessage ?? 'The Smart ID could not be updated.');
    } finally {
      setBusy(false);
    }
  }

  async function deleteCard(user) {
    if (!window.confirm(`Revoke the Smart ID for ${user.fullName}? The user will no longer be able to view or use it.`)) return;

    setBusy(true);
    setError('');
    try {
      await smartIdApi.deleteAdminCard(user.userId);
      setUsers((current) => current.map((item) => item.userId === user.userId
        ? { ...item, cardGenerated: false, card: null }
        : item));
      setSelectedIds((current) => current.filter((id) => id !== user.userId));
      if (lookup?.userId === user.userId) {
        setLookup((current) => ({ ...current, cardGenerated: false, card: null }));
      }
      setPreviewUserId('');
    } catch (deleteError) {
      setError(deleteError.friendlyMessage ?? 'The Smart ID could not be revoked.');
    } finally {
      setBusy(false);
    }
  }

  async function uploadPhoto(userId, file) {
    setPhotoBusy(true);
    setError('');
    try {
      await smartIdApi.uploadPhoto(userId, file);
      const [updatedLookup] = await Promise.all([
        smartIdApi.adminLookup(lookup?.userId === userId ? lookup.identifier : userId),
        loadUsers(searchText, roleFilter),
      ]);
      if (updatedLookup) setLookup(updatedLookup);
    } catch (photoError) {
      setError(photoError.friendlyMessage ?? 'The profile photo could not be updated.');
    } finally {
      setPhotoBusy(false);
    }
  }

  function toggleSelected(userId) {
    setSelectedIds((current) => current.includes(userId)
      ? current.filter((id) => id !== userId)
      : [...current, userId]);
    setPreviewUserId(userId);
  }

  function updatePhoto(userId, photoUrl) {
    setUsers((current) => current.map((user) => user.userId === userId
      ? { ...user, photoUrl, card: user.card ? { ...user.card, photoUrl } : null }
      : user));
    setLookup((current) => current?.userId === userId
      ? { ...current, photoUrl, card: current.card ? { ...current.card, photoUrl } : null }
      : current);
  }

  function updateSignature(userId, digitalSignatureUrl) {
    setUsers((current) => current.map((user) => user.userId === userId && user.card
      ? { ...user, card: { ...user.card, digitalSignatureUrl } }
      : user));
  }

  return (
    <div className="space-y-6">
      <section className="card space-y-4 p-5">
        <div>
          <h1 className="text-xl font-bold text-slate-900">Smart ID management</h1>
          <p className="mt-1 text-sm text-slate-600">
            Look up a Student ID, Employee ID, or staff account email to review registration details, update photos, issue or revoke a card, and print.
          </p>
        </div>
        <form className="flex flex-col gap-3 sm:flex-row" onSubmit={findUser}>
          <label className="sr-only" htmlFor="smart-id-lookup">Student ID or Staff / Employee ID</label>
          <input
            id="smart-id-lookup"
            className="input flex-1"
            value={lookupText}
            onChange={(event) => setLookupText(event.target.value)}
            placeholder="Student ID, Employee ID, or staff email"
          />
          <button type="submit" className="btn-primary" disabled={busy || !lookupText.trim()}>
            <Search className="size-4" aria-hidden="true" /> {busy ? 'Searching…' : 'Find user'}
          </button>
        </form>

        {lookup && (
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
              {lookup.photoUrl
                ? <img src={new URL(lookup.photoUrl, API_BASE_URL).toString()} alt="" crossOrigin="anonymous" className="size-20 rounded-lg object-cover" />
                : <div className="grid size-20 place-items-center rounded-lg bg-slate-200 text-xs text-slate-500">No photo</div>}
              <div className="min-w-0 flex-1">
                <h2 className="font-semibold text-slate-900">{lookup.fullName}</h2>
                <p className="text-sm text-slate-600">{lookup.role} · {lookup.identifier}</p>
                <p className="text-sm text-slate-500">{[lookup.departmentOrGrade, lookup.section].filter(Boolean).join(' · ') || 'No department or class recorded'}</p>
                <p className="mt-1 text-xs font-semibold text-slate-600">
                  {lookup.cardGenerated ? `Smart ID ${lookup.card?.cardUID ?? 'issued'} · ${lookup.card?.cardStatus}` : 'Smart ID not generated'}
                </p>
              </div>
              <label className={`btn-secondary cursor-pointer ${photoBusy ? 'pointer-events-none opacity-60' : ''}`}>
                <Upload className="size-4" aria-hidden="true" /> {photoBusy ? 'Uploading…' : lookup.photoUrl ? 'Replace photo' : 'Upload photo'}
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,image/jpeg,image/png"
                  className="sr-only"
                  disabled={photoBusy}
                  onChange={(event) => {
                    const file = event.target.files?.[0];
                    event.target.value = '';
                    if (file) uploadPhoto(lookup.userId, file);
                  }}
                  aria-label="Upload or replace the Smart ID profile photo"
                />
              </label>
              {!lookup.cardGenerated && (
                <button type="button" className="btn-primary" disabled={busy} onClick={() => generateCard(lookup)}>
                  <IdCard className="size-4" aria-hidden="true" /> {busy ? 'Generating…' : 'Generate Smart ID'}
                </button>
              )}
            </div>
          </div>
        )}
      </section>

      {error && <p role="alert" className="flex items-center gap-2 text-sm text-rose-700"><AlertCircle className="size-4" />{error}</p>}

      <section className="card overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 md:flex-row md:items-end">
          <label className="flex-1 text-sm font-medium text-slate-700">
            Search issued and pending IDs
            <input className="input mt-1" value={searchText} onChange={(event) => setSearchText(event.target.value)} placeholder="Name, Student ID, Employee ID, or email" />
          </label>
          <label className="text-sm font-medium text-slate-700">
            Role
            <select className="input mt-1" value={roleFilter} onChange={(event) => setRoleFilter(event.target.value)}>
              <option value="">All</option>
              <option value="student">Students</option>
              <option value="teacher">Teachers</option>
              <option value="staff">Staff</option>
            </select>
          </label>
          <button type="button" className="btn-secondary" disabled={listBusy} onClick={() => loadUsers(searchText, roleFilter)}>
            <Search className="size-4" aria-hidden="true" /> {listBusy ? 'Loading…' : 'Search'}
          </button>
          <button type="button" className="btn-primary" disabled={!selectedCards.length} onClick={() => window.print()}>
            <Printer className="size-4" aria-hidden="true" /> Print selected ({selectedCards.length})
          </button>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[650px] text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr><th className="px-4 py-3">Select</th><th className="px-4 py-3">User</th><th className="px-4 py-3">Role / ID</th><th className="px-4 py-3">Card status</th><th className="px-4 py-3">Actions</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {users.map((user) => (
                <tr key={user.userId} className="hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      aria-label={`Select card for ${user.fullName}`}
                      checked={selectedIds.includes(user.userId)}
                      disabled={!user.card || user.card.cardStatus !== 'ACTIVE'}
                      onChange={() => toggleSelected(user.userId)}
                      className="size-4 rounded border-slate-300 text-brand-600"
                    />
                  </td>
                  <td className="px-4 py-3 font-semibold text-slate-900">{user.fullName}</td>
                  <td className="px-4 py-3 text-slate-600">{user.role} · {user.identifier}</td>
                  <td className="px-4 py-3">
                    {user.card
                      ? <span className="font-mono text-xs">{user.card.cardUID} · {user.card.cardStatus}</span>
                      : <span className="text-xs text-amber-700">Pending generation</span>}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2">
                      {user.card
                        ? <button type="button" className="btn-secondary !px-2 !py-1 text-xs" onClick={() => setPreviewUserId(user.userId)}>Manage</button>
                        : <button type="button" className="btn-primary !px-2 !py-1 text-xs" disabled={busy} onClick={() => generateCard(user)}>Generate</button>}
                      {user.card && <button type="button" className="btn-secondary !px-2 !py-1 text-xs text-rose-700" disabled={busy} onClick={() => deleteCard(user)} aria-label={`Revoke ID for ${user.fullName}`}>
                        <Trash2 className="size-4" aria-hidden="true" />
                      </button>}
                    </div>
                  </td>
                </tr>
              ))}
              {!listBusy && users.length === 0 && <tr><td colSpan="5" className="px-4 py-8 text-center text-slate-500">No matching students or staff accounts.</td></tr>}
            </tbody>
          </table>
        </div>
      </section>

      {preview && (
        <section className="card space-y-4 p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-slate-900">Manage Smart ID</h2>
              <p className="text-xs text-slate-500">{preview.identifier} · {preview.cardUID}</p>
            </div>
            <button type="button" className="btn-secondary" disabled={busy} onClick={() => setRotateQrToken(true)}>
              <RefreshCw className="size-4" aria-hidden="true" /> Rotate QR secret
            </button>
          </div>
          <div className="grid gap-3 sm:grid-cols-[1fr_auto_auto] sm:items-end">
            <label className="text-sm font-medium text-slate-700">
              Cardholder name
              <input className="input mt-1" value={fullName} onChange={(event) => setFullName(event.target.value)} maxLength={150} />
            </label>
            <label className="text-sm font-medium text-slate-700">
              Card status
              <select className="input mt-1" value={status} onChange={(event) => setStatus(event.target.value)}>
                {['ACTIVE', 'SUSPENDED', 'LOST', 'EXPIRED'].map((value) => <option key={value}>{value}</option>)}
              </select>
            </label>
            <button type="button" className="btn-primary" disabled={busy} onClick={saveCardChanges}>Save changes</button>
          </div>
          {rotateQrToken && <p className="text-xs text-amber-800">Saving rotates the QR signing secret and invalidates existing dynamic QR tokens.</p>}
          <SmartCardView
            card={preview}
            onPhotoUploaded={smartIdApi.uploadPhoto}
            onPhotoSaved={updatePhoto}
            onSignatureUploaded={smartIdApi.uploadSignature}
            onSignatureSaved={updateSignature}
          />
        </section>
      )}

      <section className="smart-id-print-area" aria-hidden="true">
        {printSheets.map((sheet, index) => (
          <div className="smart-id-print-page" key={`front-${index}`}>
            {sheet.map((card) => <SmartCardFace key={card.userId} card={card} side="front" />)}
          </div>
        ))}
        {printSheets.map((sheet, index) => (
          <div className="smart-id-print-page smart-id-print-back-page" key={`back-${index}`}>
            {sheet.map((card) => <SmartCardFace key={card.userId} card={card} side="back" />)}
          </div>
        ))}
      </section>
    </div>
  );
}
