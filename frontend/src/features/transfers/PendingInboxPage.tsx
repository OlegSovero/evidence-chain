import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listMyPendingTransfers, respondTransfer, type TransferAction } from '../../api/transfers';
import { describeError } from '../../api/describeError';
import { LoadingState } from '../../components/LoadingState';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import type { ListTransfersResponse } from '../../api/types';

const PENDING_KEY = ['transfers', 'pending'] as const;

const dateFormatter = new Intl.DateTimeFormat('es', { dateStyle: 'medium', timeStyle: 'short' });

interface Failure {
  id: string;
  evidenceCode: string;
  message: string;
}

export function PendingInboxPage() {
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState<Record<string, string>>({});
  // Independiente de `data.items`: un 409 aquí significa que la transferencia
  // ya salió de "Pendiente" (la aceptó/rechazó alguien más), así que la
  // reconciliación (onSettled) SIEMPRE la va a quitar de la lista. Si el
  // aviso viviera dentro del <li> de esa transferencia, desaparecería con
  // ella antes de que el usuario alcance a leerlo — justo lo que no debe
  // pasar ante un 409 (enunciado: "debe reconciliarse... de manera clara").
  const [failures, setFailures] = useState<Failure[]>([]);

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: PENDING_KEY,
    queryFn: ({ signal }) => listMyPendingTransfers({ status: 'pending', mine: true }, signal),
  });

  const mutation = useMutation({
    mutationFn: (input: { id: string; action: TransferAction; version: string; note?: string }) =>
      respondTransfer(input),
    onMutate: async (input) => {
      setFailures((prev) => prev.filter((f) => f.id !== input.id));
      await queryClient.cancelQueries({ queryKey: PENDING_KEY });
      const previous = queryClient.getQueryData<ListTransfersResponse>(PENDING_KEY);
      const evidenceCode = previous?.items.find((t) => t.id === input.id)?.evidence.code ?? '';
      if (previous) {
        queryClient.setQueryData<ListTransfersResponse>(PENDING_KEY, {
          items: previous.items.filter((t) => t.id !== input.id),
        });
      }
      return { previous, evidenceCode };
    },
    onError: (err, input, context) => {
      if (context?.previous) {
        queryClient.setQueryData(PENDING_KEY, context.previous);
      }
      setFailures((prev) => [
        ...prev.filter((f) => f.id !== input.id),
        { id: input.id, evidenceCode: context?.evidenceCode ?? '', message: describeError(err) },
      ]);
    },
    onSettled: (response) => {
      void queryClient.invalidateQueries({ queryKey: PENDING_KEY });
      if (response) {
        void queryClient.invalidateQueries({ queryKey: ['evidence', 'detail', response.evidence.id] });
      }
    },
  });

  function dismissFailure(id: string) {
    setFailures((prev) => prev.filter((f) => f.id !== id));
  }

  if (isLoading) {
    return <LoadingState label="Cargando transferencias pendientes…" />;
  }
  if (isError) {
    return <ErrorState error={error} onRetry={() => void refetch()} />;
  }
  const failureBanner = failures.length > 0 && (
    <ul className="transfer-failures">
      {failures.map((failure) => (
        <li key={failure.id} role="alert" className="form-error">
          <span>
            {failure.evidenceCode && <strong>{failure.evidenceCode}: </strong>}
            {failure.message}
          </span>
          <button type="button" onClick={() => dismissFailure(failure.id)} aria-label="Descartar aviso">
            ×
          </button>
        </li>
      ))}
    </ul>
  );

  if (!data || data.items.length === 0) {
    return (
      <section>
        <h1>Transferencias pendientes</h1>
        {failureBanner}
        <EmptyState message="No tienes transferencias pendientes." />
      </section>
    );
  }

  return (
    <section>
      <h1>Transferencias pendientes</h1>

      {failureBanner}

      <ul className="transfer-list">
        {data.items.map((transfer) => (
          <li key={transfer.id} className="transfer-list__item">
            <div>
              <strong>{transfer.evidence.code}</strong> — {transfer.evidence.description}
            </div>
            <div>
              De {transfer.fromCustodian.displayName} · Solicitado por {transfer.requestedBy.displayName} el{' '}
              {dateFormatter.format(new Date(transfer.requestedAtUtc))}
            </div>
            <p>{transfer.reason}</p>

            <div className="field">
              <label htmlFor={`note-${transfer.id}`}>Nota de respuesta (opcional)</label>
              <input
                id={`note-${transfer.id}`}
                type="text"
                value={notes[transfer.id] ?? ''}
                onChange={(e) => setNotes((prev) => ({ ...prev, [transfer.id]: e.target.value }))}
              />
            </div>

            <div className="transfer-list__actions">
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate({
                    id: transfer.id,
                    action: 'accept',
                    version: transfer.version,
                    note: notes[transfer.id],
                  })
                }
              >
                Aceptar
              </button>
              <button
                type="button"
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate({
                    id: transfer.id,
                    action: 'reject',
                    version: transfer.version,
                    note: notes[transfer.id],
                  })
                }
              >
                Rechazar
              </button>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
