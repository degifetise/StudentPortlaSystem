import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import SmartCardView from '../components/smart-id/SmartCardView';
import { useAuth } from '../context/AuthContext';
import { smartIdApi } from '../services/endpoints';

export default function MySmartIDPage() {
  const { user } = useAuth();
  const [card, setCard] = useState(null);
  const [cardNotGenerated, setCardNotGenerated] = useState(false);
  const [dynamicToken, setDynamicToken] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const refreshToken = useCallback(async () => {
    if (!user?.id) return;
    try {
      const token = await smartIdApi.generateQrToken(user.id);
      setDynamicToken(token);
      setError('');
    } catch (tokenError) {
      setError(tokenError.friendlyMessage ?? 'A live QR token could not be generated.');
      setDynamicToken(null);
    }
  }, [user?.id]);

  useEffect(() => {
    let cancelled = false;
    async function loadCard() {
      if (!user?.id) {
        setError('Your account ID is not available. Sign in again and retry.');
        setLoading(false);
        return;
      }

      try {
        const result = await smartIdApi.cardDetails(user.id);
        if (!cancelled) {
          setCard(result);
          setCardNotGenerated(false);
          await refreshToken();
        }
      } catch (loadError) {
        if (!cancelled && loadError.response?.status === 404) {
          setCardNotGenerated(true);
          setError('');
        } else if (!cancelled) {
          setError(loadError.friendlyMessage ?? 'Your Smart ID card could not be loaded.');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadCard();
    return () => { cancelled = true; };
  }, [refreshToken, user?.id]);

  useEffect(() => {
    if (!card) return undefined;
    const interval = window.setInterval(refreshToken, 45000);
    return () => window.clearInterval(interval);
  }, [card, refreshToken]);

  function updatePhoto(userId, photoUrl) {
    if (userId === card?.userId) setCard((current) => ({ ...current, photoUrl }));
  }

  function updateSignature(userId, digitalSignatureUrl) {
    if (userId === card?.userId) setCard((current) => ({ ...current, digitalSignatureUrl }));
  }

  if (loading) return <p className="text-sm text-slate-500">Loading your Smart ID…</p>;

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-xl font-bold text-slate-900">My Smart ID</h1>
        <p className="mt-1 text-sm text-slate-600">Your digital card and live QR verification token.</p>
      </header>
      {error && <p role="alert" className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
        <AlertCircle className="size-4 shrink-0" />{error}
      </p>}
      {!card && !loading && cardNotGenerated && (
        <p role="status" className="rounded-lg border border-sky-200 bg-sky-50 p-4 text-sm font-medium text-sky-900">
          Your Digital ID has not been generated yet. Please contact the administrator to generate your ID.
        </p>
      )}
      {card && (
        <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_auto]">
          <section className="card p-5">
            <SmartCardView
              card={card}
              onPhotoUploaded={smartIdApi.uploadPhoto}
              onPhotoSaved={updatePhoto}
              onSignatureUploaded={smartIdApi.uploadSignature}
              onSignatureSaved={updateSignature}
            />
          </section>
          <section className="card flex flex-col items-center p-5 text-center">
            <h2 className="font-semibold text-slate-900">Live verification QR</h2>
            <p className="mt-1 max-w-xs text-xs text-slate-500">Refreshes every 45 seconds. A screenshot or expired token will not verify.</p>
            <div className="mt-4 rounded-xl border border-slate-200 bg-white p-3">
              {dynamicToken
                ? <QRCodeSVG value={dynamicToken.token} size={210} level="H" />
                : <span className="grid size-[210px] place-items-center text-sm text-slate-500">QR not available</span>}
            </div>
            <p className="mt-3 text-xs text-slate-500">
              {dynamicToken ? `Valid until ${new Date(dynamicToken.expiresAt).toLocaleTimeString()}` : 'No active token'}
            </p>
            <button type="button" className="btn-secondary mt-3" onClick={refreshToken}>
              <RefreshCw className="size-4" aria-hidden="true" /> Refresh QR
            </button>
          </section>
        </div>
      )}
    </div>
  );
}
