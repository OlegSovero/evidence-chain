import { apiFetch } from './client';
import type { ListTransfersResponse, TransferResponse, TransferStatus } from './types';

export interface RequestTransferInput {
  evidenceId: number;
  toCustodianId: number;
  reason: string;
  idempotencyKey: string;
}

export function requestTransfer(input: RequestTransferInput, signal?: AbortSignal) {
  return apiFetch<TransferResponse>('/api/v1/custody-transfers', {
    method: 'POST',
    body: {
      evidenceId: input.evidenceId,
      toCustodianId: input.toCustodianId,
      reason: input.reason,
    },
    headers: { 'Idempotency-Key': input.idempotencyKey },
    signal,
  }).then((r) => r.data);
}

export interface ListTransfersParams {
  status?: 'pending' | 'accepted' | 'rejected';
  mine?: boolean;
}

export function listMyPendingTransfers(params: ListTransfersParams = {}, signal?: AbortSignal) {
  return apiFetch<ListTransfersResponse>('/api/v1/custody-transfers', { query: { ...params }, signal }).then(
    (r) => r.data,
  );
}

export function getTransfer(id: string, signal?: AbortSignal) {
  return apiFetch<TransferResponse>(`/api/v1/custody-transfers/${id}`, { signal }).then((r) => r.data);
}

export type TransferAction = 'accept' | 'reject';

export interface RespondTransferInput {
  id: string;
  action: TransferAction;
  version: string;
  note?: string;
}

export function respondTransfer(input: RespondTransferInput, signal?: AbortSignal) {
  return apiFetch<TransferResponse>(`/api/v1/custody-transfers/${input.id}/${input.action}`, {
    method: 'POST',
    body: { note: input.note?.trim() || null },
    headers: { 'If-Match': input.version },
    signal,
  }).then((r) => r.data);
}

export function isTerminal(status: TransferStatus): boolean {
  return status !== 'Pendiente';
}
