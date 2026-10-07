import { useEffect, useRef, useState } from 'react';
import JsBarcode from 'jsbarcode';
import { Download, PenLine, Printer, Upload, UserRound } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import { useSchoolInfo } from '../../context/SchoolInfoContext';
import { ROLES, useAuth } from '../../context/AuthContext';
import { API_BASE_URL } from '../../services/api';

const CARD_WIDTH_MM = 85.6;
const CARD_HEIGHT_MM = 53.98;

function photoSource(photoUrl) {
  if (!photoUrl) return null;
  try {
    return new URL(photoUrl, API_BASE_URL).toString();
  } catch {
    return null;
  }
}

function displayDate(date) {
  return date ? new Date(date).toLocaleDateString() : '—';
}

function normalizeOklchColors(value, colorContext) {
  return value.replace(/oklch\([^)]*\)/gi, (color) => {
    if (!colorContext) return '#ffffff';

    colorContext.fillStyle = '#010203';
    const sentinel = colorContext.fillStyle;
    colorContext.fillStyle = color;
    const normalized = colorContext.fillStyle;
    return normalized === sentinel ? '#ffffff' : normalized;
  });
}

export function SmartCardFace({ card, side = 'front', faceRef }) {
  const { schoolName, contactEmail, academicYear } = useSchoolInfo();
  const barcodeRef = useRef(null);
  const photo = photoSource(card.photoUrl);

  useEffect(() => {
    if (side !== 'back' || !barcodeRef.current || !card.cardUID) return;
    JsBarcode(barcodeRef.current, card.cardUID, {
      format: 'CODE128',
      displayValue: false,
      height: 20,
      margin: 0,
      width: 1.5,
    });
  }, [card.cardUID, side]);

  if (side === 'back') {
    return (
      <article ref={faceRef} className="smart-id-card-face smart-id-card-back" aria-label={`${card.fullName} ID card back`}>
        <div className="flex items-center justify-between border-b border-white/20 pb-2">
          <span className="text-[10px] font-bold uppercase tracking-[0.16em]">Verification & important information</span>
          <span className="rounded-full border border-white/40 px-2 py-1 text-[8px] font-semibold">OFFICIAL</span>
        </div>
        <div className="mt-2 flex min-h-0 flex-1 gap-2">
          <div className="min-w-0 flex-1 space-y-1.5 text-[9px] leading-tight">
            <p><strong>Emergency contact</strong><br />{card.emergencyContact || 'Not provided'}</p>
            <p><strong>Blood group</strong><br />{card.bloodGroup || 'Not provided'}</p>
            <p><strong>Return to</strong><br />{schoolName}{contactEmail ? ` · ${contactEmail}` : ''}</p>
          </div>
          <div className="flex shrink-0 flex-col items-center gap-0.5 rounded bg-white p-1">
            <QRCodeSVG value={card.cardUID} size={54} level="H" marginSize={0} />
            <span className="max-w-12 truncate font-mono text-[6px] text-slate-800">{card.cardUID}</span>
          </div>
          <div className="flex h-12 w-16 shrink-0 items-end justify-center rounded bg-white/95 p-1">
            {card.digitalSignatureUrl ? (
              <img
                src={photoSource(card.digitalSignatureUrl)}
                alt="Digital signature"
                className="max-h-full max-w-full object-contain"
                crossOrigin="anonymous"
              />
            ) : (
              <span className="text-[8px] text-slate-400">Authorized signature</span>
            )}
          </div>
        </div>
        <div className="mt-1 flex items-end justify-between gap-2 border-t border-white/20 pt-1">
          <svg ref={barcodeRef} className="h-5 max-w-28 bg-white px-1" aria-label={`Barcode ${card.cardUID}`} />
          <p className="max-w-[45%] text-right text-[7px] leading-tight text-white/80">
            This card remains the property of {schoolName}. If found, return it to the school office.
            Possession and access are subject to verification.
          </p>
        </div>
      </article>
    );
  }

  return (
    <article ref={faceRef} className="smart-id-card-face smart-id-card-front" aria-label={`${card.fullName} ID card front`}>
      <header className="flex items-center gap-2 border-b border-white/20 pb-1.5">
        <span className="grid size-7 shrink-0 place-items-center rounded-full bg-white/15 text-[10px] font-bold" aria-hidden="true">
          {schoolName.slice(0, 1).toUpperCase()}
        </span>
        <span className="min-w-0 flex-1 truncate text-[10px] font-bold uppercase tracking-wide">{schoolName}</span>
        <span className="rounded bg-white/15 px-1.5 py-1 text-[8px] font-semibold uppercase">{card.role}</span>
      </header>
      <div className="flex min-h-0 flex-1 gap-2.5 pt-2">
        <div className="grid h-[76px] w-[58px] shrink-0 place-items-center overflow-hidden rounded-md border border-white/40 bg-white/15 text-[8px]">
          {photo ? (
            <img src={photo} alt={`${card.fullName}`} className="h-full w-full object-cover" crossOrigin="anonymous" />
          ) : (
            <UserRound className="size-8 text-white/75" aria-label="Default profile avatar" />
          )}
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-[13px] font-extrabold leading-tight">{card.fullName}</h2>
          <p className="mt-0.5 truncate text-[9px] font-semibold text-white/80">{card.identifier}</p>
          <dl className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-[8px] leading-tight">
            <div><dt className="text-white/65">Grade / Dept.</dt><dd className="truncate font-semibold">{card.gradeLevel || '—'}</dd></div>
            <div><dt className="text-white/65">Section</dt><dd className="truncate font-semibold">{card.section || '—'}</dd></div>
            <div><dt className="text-white/65">Academic year</dt><dd className="truncate font-semibold">{card.academicYear || academicYear || '—'}</dd></div>
            <div><dt className="text-white/65">Issued</dt><dd className="truncate font-semibold">{displayDate(card.issuedDate)}</dd></div>
          </dl>
        </div>
      </div>
      <footer className="flex h-7 items-end justify-between border-t border-white/20 pt-1">
        <span className="font-mono text-[8px] font-semibold">{card.cardUID}</span>
        <span className="pb-0.5 text-[7px] uppercase tracking-wider text-white/70">Valid until {displayDate(card.expirationDate)}</span>
      </footer>
    </article>
  );
}

export default function SmartCardView({
  card,
  onPhotoUploaded,
  onPhotoSaved,
  onSignatureUploaded,
  onSignatureSaved,
}) {
  const { roles } = useAuth();
  const isAdmin = roles.includes(ROLES.admin);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const [previewPhotoUrl, setPreviewPhotoUrl] = useState(null);
  const [previewSignatureUrl, setPreviewSignatureUrl] = useState(null);
  const frontRef = useRef(null);
  const backRef = useRef(null);
  const cardPreview = {
    ...card,
    ...(previewPhotoUrl ? { photoUrl: previewPhotoUrl } : {}),
    ...(previewSignatureUrl ? { digitalSignatureUrl: previewSignatureUrl } : {}),
  };

  async function captureCardFace(element) {
    if (!element) throw new Error('Card face is not available for export.');

    const images = [...element.querySelectorAll('img')];
    await Promise.all(images.map(async (image) => {
      if (image.complete && image.naturalWidth > 0) return;
      if (typeof image.decode === 'function') {
        await image.decode();
        return;
      }
      await new Promise((resolve, reject) => {
        image.addEventListener('load', resolve, { once: true });
        image.addEventListener('error', () => reject(new Error('A card image could not be loaded.')), { once: true });
      });
    }));

    const { default: html2canvas } = await import('html2canvas');
    return html2canvas(element, {
      scale: 3,
      useCORS: true,
      allowTaint: false,
      backgroundColor: '#fff',
      onclone: (clonedDoc) => {
        const colorContext = clonedDoc.createElement('canvas').getContext('2d');
        const elements = clonedDoc.querySelectorAll('.smart-id-card-face, .smart-id-card-face *');

        elements.forEach((clonedElement) => {
          const computedStyle = clonedDoc.defaultView.getComputedStyle(clonedElement);
          for (let index = 0; index < computedStyle.length; index += 1) {
            const property = computedStyle[index];
            const value = computedStyle.getPropertyValue(property);
            if (value.toLowerCase().includes('oklch(')) {
              clonedElement.style.setProperty(
                property,
                normalizeOklchColors(value, colorContext),
                'important',
              );
            }
          }
        });
      },
    });
  }

  function downloadBlob(blob, fileName) {
    const objectUrl = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }

  function safeFilePart(value) {
    return (value || 'Unknown').normalize('NFKD').replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-zA-Z0-9_-]+/g, '_').replace(/^_+|_+$/g, '') || 'Unknown';
  }

  async function exportPng() {
    if (!isAdmin) return;
    setError('');
    try {
      const canvas = await captureCardFace(frontRef.current);
      const blob = await new Promise((resolve, reject) => {
        canvas.toBlob((result) => result ? resolve(result) : reject(new Error('PNG encoding failed.')), 'image/png');
      });
      downloadBlob(blob, `SmartID_${safeFilePart(card.identifier)}_${safeFilePart(card.fullName)}_Front.png`);
    } catch (exportError) {
      setError(exportError.friendlyMessage ?? exportError.message ?? 'The front image could not be generated.');
    }
  }

  async function exportPdf() {
    if (!isAdmin) return;
    setError('');
    try {
      const [{ jsPDF }, frontCanvas, backCanvas] = await Promise.all([
        import('jspdf'),
        captureCardFace(frontRef.current),
        captureCardFace(backRef.current),
      ]);
      const pdf = new jsPDF({ orientation: 'landscape', unit: 'mm', format: [CARD_WIDTH_MM, CARD_HEIGHT_MM] });
      pdf.addImage(frontCanvas.toDataURL('image/png'), 'PNG', 0, 0, CARD_WIDTH_MM, CARD_HEIGHT_MM);
      pdf.addPage([CARD_WIDTH_MM, CARD_HEIGHT_MM], 'landscape');
      pdf.addImage(backCanvas.toDataURL('image/png'), 'PNG', 0, 0, CARD_WIDTH_MM, CARD_HEIGHT_MM);
      downloadBlob(
        pdf.output('blob'),
        `SmartID_${safeFilePart(card.identifier)}_${safeFilePart(card.fullName)}.pdf`,
      );
    } catch (exportError) {
      setError(exportError.friendlyMessage ?? exportError.message ?? 'The two-sided PDF could not be generated.');
    }
  }

  async function uploadCardImage(event, imageType) {
    if (!isAdmin) return;
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!['image/jpeg', 'image/png'].includes(file.type) || file.size > 2 * 1024 * 1024) {
      setError('Choose a JPG or PNG image no larger than 2 MB.');
      return;
    }

    const upload = imageType === 'photo' ? onPhotoUploaded : onSignatureUploaded;
    if (!upload) return;

    setUploading(true);
    setError('');
    const previewUrl = URL.createObjectURL(file);
    const setPreviewUrl = imageType === 'photo' ? setPreviewPhotoUrl : setPreviewSignatureUrl;
    setPreviewUrl(previewUrl);
    try {
      const result = await upload(card.userId, file);
      if (imageType === 'photo') {
        onPhotoSaved?.(card.userId, result.photoUrl);
      } else {
        onSignatureSaved?.(card.userId, result.digitalSignatureUrl);
      }
    } catch (uploadError) {
      setError(uploadError.friendlyMessage ?? `The ${imageType} could not be uploaded.`);
    } finally {
      URL.revokeObjectURL(previewUrl);
      setPreviewUrl(null);
      setUploading(false);
    }
  }

  function printSingleCard() {
    if (!isAdmin) return;
    document.body.classList.add('smart-id-print-single');
    window.addEventListener('afterprint', () => document.body.classList.remove('smart-id-print-single'), { once: true });
    window.print();
  }

  return (
    <div className="space-y-3">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Front side</p>
          <SmartCardFace card={cardPreview} side="front" faceRef={frontRef} />
        </div>
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Back side</p>
          <SmartCardFace card={cardPreview} side="back" faceRef={backRef} />
        </div>
      </div>
      <div className="smart-card-single-print-target" aria-hidden="true">
        <SmartCardFace card={cardPreview} side="front" />
        <SmartCardFace card={cardPreview} side="back" />
      </div>
      <div className="flex flex-wrap gap-2">
        {isAdmin && <>
          <button type="button" className="btn-secondary" onClick={exportPdf}>
            <Download className="size-4" aria-hidden="true" /> Download PDF
          </button>
          <button type="button" className="btn-secondary" onClick={exportPng}>
            <Download className="size-4" aria-hidden="true" /> Download front PNG
          </button>
          <button type="button" className="btn-primary" onClick={printSingleCard}>
            <Printer className="size-4" aria-hidden="true" /> Print ID card
          </button>
        </>}
        {isAdmin && onPhotoUploaded && (
          <label className="btn-secondary cursor-pointer">
            <Upload className="size-4" aria-hidden="true" /> {uploading ? 'Uploading…' : 'Upload photo'}
            <input
              type="file"
              accept=".jpg,.jpeg,.png,image/jpeg,image/png"
              className="sr-only"
              disabled={uploading}
              onChange={(event) => uploadCardImage(event, 'photo')}
              aria-label="Upload Smart ID profile photo"
            />
          </label>
        )}
        {isAdmin && onSignatureUploaded && (
          <label className="btn-secondary cursor-pointer">
            <PenLine className="size-4" aria-hidden="true" /> {uploading ? 'Uploading…' : 'Upload signature'}
            <input
              type="file"
              accept=".jpg,.jpeg,.png,image/jpeg,image/png"
              className="sr-only"
              disabled={uploading}
              onChange={(event) => uploadCardImage(event, 'signature')}
              aria-label="Upload Smart ID digital signature"
            />
          </label>
        )}
      </div>
      {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
    </div>
  );
}
