import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Modal } from '../../components/Modal';
import { listDemoUsers } from '../../api/auth';
import { requestTransfer } from '../../api/transfers';
import { describeError } from '../../api/describeError';
import { useAuth } from '../../auth/useAuth';
import type { EvidenceDetail, TransferResponse } from '../../api/types';

const REASON_MAX_LENGTH = 500;

interface TransferDialogProps {
  open: boolean;
  onClose: () => void;
  evidenceId: number;
  currentCustodianId: number;
}

export function TransferDialog({ open, onClose, evidenceId, currentCustodianId }: TransferDialogProps) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [toCustodianId, setToCustodianId] = useState('');
  const [reason, setReason] = useState('');

  // Una Idempotency-Key por INTENTO DE ENVÍO: se genera una sola vez y se
  // reutiliza mientras el usuario reintente el mismo formulario; una clave
  // nueva en cada clic volvería inútil la idempotencia.
  const idempotencyKeyRef = useRef<string | null>(null);

  useEffect(() => {
    if (open) {
      setToCustodianId('');
      setReason('');
      idempotencyKeyRef.current = null;
    }
  }, [open]);

  const { data: custodians } = useQuery({
    queryKey: ['auth', 'demo-users'],
    queryFn: ({ signal }) => listDemoUsers(signal),
    staleTime: Infinity,
    select: (response) => response.items.filter((u) => u.role === 'Custodio' && u.id !== currentCustodianId),
  });

  const mutation = useMutation({
    mutationFn: (input: { toCustodianId: number; reason: string }) => {
      if (!idempotencyKeyRef.current) {
        idempotencyKeyRef.current = crypto.randomUUID();
      }
      return requestTransfer({
        evidenceId,
        toCustodianId: input.toCustodianId,
        reason: input.reason,
        idempotencyKey: idempotencyKeyRef.current,
      });
    },
    onMutate: async (input) => {
      const detailKey = ['evidence', 'detail', evidenceId] as const;
      await queryClient.cancelQueries({ queryKey: detailKey });
      const previous = queryClient.getQueryData<EvidenceDetail>(detailKey);
      const destination = custodians?.find((c) => c.id === input.toCustodianId);
      if (previous && destination && user) {
        const optimistic: TransferResponse = {
          id: crypto.randomUUID(),
          evidence: { id: previous.id, code: previous.code, description: previous.description },
          status: 'Pendiente',
          reason: input.reason,
          requestedAtUtc: new Date().toISOString(),
          respondedAtUtc: null,
          responseNote: null,
          fromCustodian: previous.currentCustodian,
          toCustodian: destination,
          requestedBy: { id: user.id, userName: user.userName, displayName: user.displayName },
          respondedBy: null,
          version: 'optimistic',
          anomaly: null,
          isOptimistic: true,
        };
        queryClient.setQueryData<EvidenceDetail>(detailKey, { ...previous, pendingTransfer: optimistic });
      }
      return { previous, detailKey };
    },
    onError: (_error, _input, context) => {
      if (context?.previous) {
        queryClient.setQueryData(context.detailKey, context.previous);
      }
    },
    onSuccess: () => {
      idempotencyKeyRef.current = null;
      onClose();
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['evidence', 'detail', evidenceId] });
    },
  });

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    const trimmedReason = reason.trim();
    if (!toCustodianId || !trimmedReason) {
      return;
    }
    mutation.mutate({ toCustodianId: Number(toCustodianId), reason: trimmedReason });
  }

  return (
    <Modal open={open} onClose={onClose} labelledBy="transfer-dialog-title">
      <form onSubmit={handleSubmit}>
        <h2 id="transfer-dialog-title">Solicitar transferencia de custodia</h2>

        <div className="field">
          <label htmlFor="toCustodianId">Custodio destino</label>
          <select
            id="toCustodianId"
            required
            autoFocus
            value={toCustodianId}
            onChange={(e) => setToCustodianId(e.target.value)}
          >
            <option value="" disabled>
              Elige un custodio
            </option>
            {custodians?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.displayName}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="reason">Motivo</label>
          <textarea
            id="reason"
            required
            maxLength={REASON_MAX_LENGTH}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={4}
          />
          <span className="field__hint">
            {reason.length}/{REASON_MAX_LENGTH}
          </span>
        </div>

        {mutation.isError && (
          <p role="alert" className="form-error">
            {describeError(mutation.error)}
          </p>
        )}

        <div className="modal__actions">
          <button type="button" onClick={onClose} disabled={mutation.isPending}>
            Cancelar
          </button>
          <button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? 'Enviando…' : 'Solicitar transferencia'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
