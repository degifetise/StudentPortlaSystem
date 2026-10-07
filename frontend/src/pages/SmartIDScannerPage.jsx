import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Camera, CheckCircle2, RotateCcw, ScanLine, XCircle } from 'lucide-react';
import { ROLES, useAuth } from '../context/AuthContext';
import { API_BASE_URL } from '../services/api';
import { smartIdApi } from '../services/endpoints';

const STATUS_STYLE = {
  VALID: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  EXPIRED: 'border-amber-200 bg-amber-50 text-amber-900',
  SUSPENDED: 'border-rose-200 bg-rose-50 text-rose-900',
  LOST: 'border-rose-200 bg-rose-50 text-rose-900',
};

function profilePhotoUrl(photoUrl) {
  if (!photoUrl) return null;
  try {
    return new URL(photoUrl, API_BASE_URL).toString();
  } catch {
    return null;
  }
}

export default function SmartIDScannerPage() {
  const { roles } = useAuth();
  const videoRef = useRef(null);
  const controlsRef = useRef(null);
  const inFlightRef = useRef(false);
  const [scanning, setScanning] = useState(true);
  const [scanLocation, setScanLocation] = useState('Main Gate');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [code, setCode] = useState('');
  const [verifying, setVerifying] = useState(false);

  const submitCode = useCallback(async (rawCode) => {
    if (!rawCode.trim() || inFlightRef.current) return;
    inFlightRef.current = true;
    setVerifying(true);
    setError('');
    setResult(null);
    setCode(rawCode);
    setScanning(false);
    controlsRef.current?.stop();
    try {
      setResult(await smartIdApi.verifyScan(rawCode, scanLocation));
    } catch (verifyError) {
      setError(verifyError.friendlyMessage ?? 'The scan could not be verified.');
    } finally {
      inFlightRef.current = false;
      setVerifying(false);
    }
  }, [scanLocation]);

  useEffect(() => {
    if (!scanning) return undefined;
    if (!navigator.mediaDevices?.getUserMedia) {
      setError('Camera access is unavailable. Open this page over HTTPS or use the manual code field.');
      setScanning(false);
      return undefined;
    }

    let cancelled = false;
    import('@zxing/browser')
      .then(({ BrowserMultiFormatReader }) => {
        if (cancelled) return undefined;
        const reader = new BrowserMultiFormatReader();
        return reader.decodeFromVideoDevice(undefined, videoRef.current, (decodeResult, _decodeError, controls) => {
          if (cancelled) {
            controls.stop();
            return;
          }
          controlsRef.current = controls;
          if (decodeResult) submitCode(decodeResult.getText());
        });
      })
      .then((controls) => {
        if (!controls) return;
        if (cancelled) controls.stop();
        else controlsRef.current = controls;
      })
      .catch((cameraError) => {
        if (!cancelled) {
          setError(cameraError.message ?? 'Camera could not be started. Check browser permission or enter a code manually.');
          setScanning(false);
        }
      });

    return () => {
      cancelled = true;
      controlsRef.current?.stop();
      controlsRef.current = null;
    };
  }, [scanning, submitCode]);

  function scanAgain() {
    setError('');
    setResult(null);
    setCode('');
    inFlightRef.current = false;
    setScanning(true);
  }

  const card = result?.card;
  const photo = profilePhotoUrl(card?.photoUrl);
  const statusClass = STATUS_STYLE[result?.status] ?? 'border-slate-200 bg-slate-50 text-slate-800';

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <header>
        <h1 className="text-xl font-bold text-slate-900">Smart ID verification</h1>
        <p className="mt-1 text-sm text-slate-600">Scan the QR code or barcode to check card status and record the gate event.</p>
      </header>

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="card space-y-4 p-5">
          <label className="block text-sm font-medium text-slate-700">
            Scan location
            <input className="input mt-1" maxLength={200} value={scanLocation} onChange={(event) => setScanLocation(event.target.value)} />
          </label>
          <div className="overflow-hidden rounded-xl bg-slate-950">
            <video ref={videoRef} className="aspect-video w-full object-cover" muted playsInline aria-label="Live ID scanner camera" />
          </div>
          <p className="flex items-center gap-2 text-xs text-slate-500">
            <Camera className="size-4" aria-hidden="true" />
            Camera access requires HTTPS, except on localhost. Scanned card data is logged for verification.
          </p>
          {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
          <form
            className="flex gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              submitCode(code);
            }}
          >
            <input
              className="input font-mono"
              aria-label="Card UID or QR token"
              placeholder="Enter or paste card UID"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              maxLength={4096}
            />
            <button className="btn-primary shrink-0" type="submit" disabled={verifying || !code.trim()}>
              <ScanLine className="size-4" aria-hidden="true" /> Verify
            </button>
          </form>
          <button type="button" className="btn-secondary w-full" disabled={verifying} onClick={scanAgain}>
            <RotateCcw className="size-4" aria-hidden="true" /> Start camera scan
          </button>
        </section>

        <section className="card min-h-64 p-5" aria-live="polite">
          {verifying && <p className="text-sm text-slate-500">Verifying scanned card…</p>}
          {!result && !verifying && (
            <div className="grid min-h-52 place-items-center text-center text-sm text-slate-500">
              <p>Scan a Smart ID or enter its UID to see verification details.</p>
            </div>
          )}
          {result && (
            <div className="space-y-4">
              <div className={`flex items-center gap-3 rounded-xl border p-4 ${statusClass}`}>
                {result.isValid ? <CheckCircle2 className="size-6 shrink-0" /> : <XCircle className="size-6 shrink-0" />}
                <div>
                  <p className="font-bold">{result.status}</p>
                  <p className="text-sm">{result.message}</p>
                </div>
              </div>
              {card && (
                <>
                  <div className="flex items-center gap-4">
                    {photo ? (
                      <img src={photo} alt="" className="size-16 rounded-xl object-cover" />
                    ) : (
                      <span className="grid size-16 place-items-center rounded-xl bg-slate-100 text-xs text-slate-500">No photo</span>
                    )}
                    <div className="min-w-0">
                      <h2 className="truncate text-lg font-bold text-slate-900">{card.fullName}</h2>
                      <p className="text-sm text-slate-600">{card.role} · {card.identifier}</p>
                      <p className="text-xs text-slate-500">{[card.gradeLevel, card.section].filter(Boolean).join(' · ') || 'Staff profile'}</p>
                    </div>
                  </div>
                  <dl className="grid grid-cols-2 gap-3 rounded-lg bg-slate-50 p-3 text-sm">
                    <div><dt className="text-xs text-slate-500">Card UID</dt><dd className="font-mono">{card.cardUID}</dd></div>
                    <div><dt className="text-xs text-slate-500">Expires</dt><dd>{new Date(card.expirationDate).toLocaleDateString()}</dd></div>
                  </dl>
                  {result.status === 'VALID' && (
                    <div className="space-y-2">
                      <p className="rounded-lg border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-800">
                        Gate entry scan recorded for {scanLocation || 'this location'}.
                      </p>
                      {(roles.includes(ROLES.admin) || roles.includes(ROLES.teacher)) && (
                        <Link className="btn-secondary w-full" to={roles.includes(ROLES.admin) ? '/admin/attendance' : '/teacher/attendance'}>
                          Check attendance
                        </Link>
                      )}
                    </div>
                  )}
                </>
              )}
              <button type="button" className="btn-secondary w-full" onClick={scanAgain}>Scan next card</button>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
