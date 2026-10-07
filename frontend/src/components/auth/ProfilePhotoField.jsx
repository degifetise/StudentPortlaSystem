import { useEffect, useRef, useState } from 'react';
import { ImagePlus, UserRound, X } from 'lucide-react';

const MAX_FILE_SIZE = 2 * 1024 * 1024;

export default function ProfilePhotoField({
  file,
  onChange,
  onPendingChange,
  disabled = false,
}) {
  const inputRef = useRef(null);
  const [error, setError] = useState('');
  const [candidate, setCandidate] = useState(null);
  const [previewUrl, setPreviewUrl] = useState('');
  const [previewOpen, setPreviewOpen] = useState(false);

  useEffect(() => {
    const image = candidate ?? file;
    if (!image) {
      setPreviewUrl('');
      return undefined;
    }

    const url = URL.createObjectURL(image);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [candidate, file]);

  function selectPhoto(event) {
    const selected = event.target.files?.[0] ?? null;
    event.target.value = '';
    if (!selected) return;

    if (!['image/jpeg', 'image/png'].includes(selected.type) || selected.size > MAX_FILE_SIZE) {
      setError('Choose a JPG or PNG photo no larger than 2 MB.');
      return;
    }

    setError('');
    setCandidate(selected);
    setPreviewOpen(false);
    onPendingChange?.(true);
  }

  function confirmPhoto() {
    if (!candidate) return;
    onChange(candidate);
    setCandidate(null);
    setPreviewOpen(false);
    onPendingChange?.(false);
  }

  function cancelSelection() {
    setCandidate(null);
    setPreviewOpen(false);
    setError('');
    onPendingChange?.(false);
  }

  function clearPhoto() {
    cancelSelection();
    onChange(null);
  }

  return (
    <div className="sm:col-span-full">
      <span className="label">Profile photo (optional)</span>
      <div className="flex flex-wrap items-center gap-3">
        <div className="grid size-16 shrink-0 place-items-center overflow-hidden rounded-full border border-slate-200 bg-slate-50 text-slate-400">
          {previewUrl ? (
            <img src={previewUrl} alt="Selected profile photo preview" className="size-full object-cover" />
          ) : (
            <UserRound className="size-8" aria-hidden="true" />
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <input
            ref={inputRef}
            className="sr-only"
            type="file"
            accept="image/jpeg,image/png,.jpg,.jpeg,.png"
            onChange={selectPhoto}
            disabled={disabled}
            tabIndex={-1}
            aria-label="Choose a JPG or PNG profile photo"
          />
          <button
            type="button"
            className="btn-secondary"
            onClick={() => inputRef.current?.click()}
            disabled={disabled}
          >
            <ImagePlus className="size-4" aria-hidden="true" />
            {file || candidate ? 'Change photo' : 'Choose photo'}
          </button>
          {candidate && (
            <>
              <button type="button" className="btn-secondary" onClick={() => setPreviewOpen((open) => !open)}>
                Preview
              </button>
              <button type="button" className="btn-secondary" onClick={confirmPhoto} disabled={disabled}>
                Confirm Upload
              </button>
              <button type="button" className="btn-secondary" onClick={cancelSelection} disabled={disabled}>
                Cancel
              </button>
            </>
          )}
          {file && !candidate && (
            <button type="button" className="btn-secondary" onClick={clearPhoto} disabled={disabled}>
              <X className="size-4" aria-hidden="true" />
              Clear
            </button>
          )}
        </div>
      </div>
      {candidate && (
        <p className="mt-1 text-xs text-amber-700">
          Photo is selected. Confirm it before submitting this form.
        </p>
      )}
      {previewOpen && previewUrl && (
        <div className="fixed inset-0 z-[80] grid place-items-center bg-slate-950/70 p-4" role="dialog" aria-modal="true" aria-label="Profile photo preview">
          <button
            type="button"
            className="absolute inset-0 cursor-default"
            aria-label="Close profile photo preview"
            onClick={() => setPreviewOpen(false)}
          />
          <div className="relative z-10 rounded-xl bg-white p-4 shadow-2xl">
            <img src={previewUrl} alt="Full-size profile photo preview" className="max-h-[75vh] max-w-[min(80vw,32rem)] rounded-lg object-contain" />
            <button type="button" className="btn-secondary mt-3 w-full" onClick={() => setPreviewOpen(false)}>
              Close preview
            </button>
          </div>
        </div>
      )}
      <p className="mt-1 text-xs text-slate-500">JPG or PNG, up to 2 MB.</p>
      {error && <p role="alert" className="mt-1 text-xs font-medium text-red-700">{error}</p>}
    </div>
  );
}
