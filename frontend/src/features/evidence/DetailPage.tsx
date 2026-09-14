import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getChain, getEvidence } from '../../api/evidence';
import { LoadingState } from '../../components/LoadingState';
import { ErrorState } from '../../components/ErrorState';
import { IntegrityBadge, SeverityBadge } from '../../components/Badges';
import { useAuth } from '../../auth/useAuth';
import { ChainTimeline } from './ChainTimeline';
import { VerifyChainButton } from './VerifyChainButton';
import { TransferDialog } from '../transfers/TransferDialog';
import type { VerifyChainResponse } from '../../api/types';

const dateFormatter = new Intl.DateTimeFormat('es', { dateStyle: 'medium', timeStyle: 'short' });

export function DetailPage() {
  const { id } = useParams<{ id: string }>();
  const evidenceId = Number(id);
  const { user } = useAuth();
  const [highlightSequence, setHighlightSequence] = useState<number | undefined>(undefined);
  const [dialogOpen, setDialogOpen] = useState(false);
  const requestButtonRef = useRef<HTMLButtonElement>(null);

  const detailQuery = useQuery({
    queryKey: ['evidence', 'detail', evidenceId],
    queryFn: ({ signal }) => getEvidence(evidenceId, signal),
    enabled: Number.isFinite(evidenceId),
  });

  const chainQuery = useQuery({
    queryKey: ['evidence', 'chain', evidenceId],
    queryFn: ({ signal }) => getChain(evidenceId, signal),
    enabled: Number.isFinite(evidenceId),
  });

  function handleVerifyResult(result: VerifyChainResponse) {
    setHighlightSequence(result.firstInvalidEvent?.sequence);
  }

  if (detailQuery.isLoading) {
    return <LoadingState label="Cargando evidencia…" />;
  }
  if (detailQuery.isError) {
    return <ErrorState error={detailQuery.error} onRetry={() => void detailQuery.refetch()} />;
  }

  const evidence = detailQuery.data!;
  const canRequestTransfer = user?.role === 'Investigador' || user?.role === 'Supervisor';
  const anomaly = evidence.pendingTransfer?.anomaly;

  return (
    <section>
      <header className="detail-header">
        <h1>{evidence.code}</h1>
        <p>{evidence.description}</p>
        <dl>
          <div>
            <dt>Custodio actual</dt>
            <dd>{evidence.currentCustodian.displayName}</dd>
          </div>
          <div>
            <dt>Último evento</dt>
            <dd>{dateFormatter.format(new Date(evidence.lastEventAtUtc))}</dd>
          </div>
          <div>
            <dt>Integridad</dt>
            <dd>
              <IntegrityBadge status={evidence.integrityStatus} />
            </dd>
          </div>
        </dl>
        <VerifyChainButton evidenceId={evidence.id} onResult={handleVerifyResult} />
      </header>

      {anomaly && (
        <div className="anomaly-banner" role="alert">
          <SeverityBadge severity={anomaly.severity} />
          <p>{anomaly.message}</p>
        </div>
      )}

      {canRequestTransfer && (
        <button
          ref={requestButtonRef}
          type="button"
          onClick={() => setDialogOpen(true)}
          disabled={Boolean(evidence.pendingTransfer)}
          title={evidence.pendingTransfer ? 'Ya hay una transferencia pendiente para esta evidencia.' : undefined}
        >
          Solicitar transferencia
        </button>
      )}

      <TransferDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        evidenceId={evidence.id}
        currentCustodianId={evidence.currentCustodian.id}
      />

      <h2>Línea de tiempo</h2>
      {chainQuery.isLoading && <LoadingState label="Cargando eventos…" />}
      {chainQuery.isError && <ErrorState error={chainQuery.error} onRetry={() => void chainQuery.refetch()} />}
      {chainQuery.data && <ChainTimeline events={chainQuery.data.events} highlightSequence={highlightSequence} />}
    </section>
  );
}
