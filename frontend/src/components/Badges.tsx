import type { AnomalySeverity, EvidenceIntegrityStatus, TransferStatus } from '../api/types';

const INTEGRITY_LABEL: Record<EvidenceIntegrityStatus, string> = {
  NoVerificada: 'No verificada',
  Integra: 'Íntegra',
  Comprometida: 'Comprometida',
};

const INTEGRITY_TONE: Record<EvidenceIntegrityStatus, string> = {
  NoVerificada: 'neutral',
  Integra: 'success',
  Comprometida: 'danger',
};

export function IntegrityBadge({ status }: { status: EvidenceIntegrityStatus }) {
  return <span className={`badge badge--${INTEGRITY_TONE[status]}`}>{INTEGRITY_LABEL[status]}</span>;
}

const TRANSFER_TONE: Record<TransferStatus, string> = {
  Pendiente: 'warning',
  Aceptada: 'success',
  Rechazada: 'neutral',
};

export function TransferStatusBadge({ status }: { status: TransferStatus }) {
  return <span className={`badge badge--${TRANSFER_TONE[status]}`}>{status}</span>;
}

const SEVERITY_TONE: Record<AnomalySeverity, string> = {
  Media: 'warning',
  Alta: 'danger',
};

export function SeverityBadge({ severity }: { severity: AnomalySeverity }) {
  return <span className={`badge badge--${SEVERITY_TONE[severity]}`}>Severidad {severity}</span>;
}
