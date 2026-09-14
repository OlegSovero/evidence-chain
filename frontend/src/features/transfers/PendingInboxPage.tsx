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

export function PendingInboxPage() {
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: PENDING_KEY,
    queryFn: ({ signal }) => listMyPendingTransfers({ status: 'pending', mine: true }, signal),
  });

  const mutation = useMutation({
    mutationFn: (input: { id: string; action: TransferAction; version: string; note?: string }) =>
      respondTransfer(input),
    onMutate: async (input) => {
      setRowErrors((prev) => ({ ...prev, [input.id]: '' }));
      await queryClient.cancelQueries({ queryKey: PENDING_KEY });
      const previous = queryClient.getQueryData<ListTransfersResponse>(PENDING_KEY);
      if (previous) {
        queryClient.setQueryData<ListTransfersResponse>(PENDING_KEY, {
          items: previous.items.filter((t) => t.id !== input.id),
        });
      }
      return { previous };
    },
    onError: (err, input, context) => {
      if (context?.previous) {
        queryClient.setQueryData(PENDING_KEY, context.previous);
      }
      setRowErrors((prev) => ({ ...prev, [input.id]: describeError(err) }));
    },
    onSettled: (response) => {
      void queryClient.invalidateQueries({ queryKey: PENDING_KEY });
      if (response) {
        void queryClient.invalidateQueries({ queryKey: ['evidence', 'detail', response.evidence.id] });
      }
    },
  });

  if (isLoading) {
    return <LoadingState label="Cargando transferencias pendientes…" />;
  }
  if (isError) {
    return <ErrorState error={error} onRetry={() => void refetch()} />;
  }
  if (!data || data.items.length === 0) {
    return <EmptyState message="No tienes transferencias pendientes." />;
  }

  return (
    <section>
      <h1>Transferencias pendientes</h1>
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

            {rowErrors[transfer.id] && (
              <p role="alert" className="form-error">
                {rowErrors[transfer.id]}
              </p>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
