import { apiFetch } from './client';
import type {
  ChainResponse,
  EvidenceDetail,
  EvidenceIntegrityStatus,
  EvidencePage,
  VerifyChainResponse,
} from './types';

export interface EvidenceListParams {
  q?: string;
  custodianId?: number;
  status?: EvidenceIntegrityStatus;
  sort?: 'asc' | 'desc';
  cursor?: string;
  pageSize?: number;
}

export function listEvidence(params: EvidenceListParams, signal?: AbortSignal) {
  return apiFetch<EvidencePage>('/api/v1/evidence', { query: { ...params }, signal }).then((r) => r.data);
}

export function getEvidence(id: number, signal?: AbortSignal) {
  return apiFetch<EvidenceDetail>(`/api/v1/evidence/${id}`, { signal }).then((r) => r.data);
}

export function getChain(id: number, signal?: AbortSignal) {
  return apiFetch<ChainResponse>(`/api/v1/evidence/${id}/chain`, { signal }).then((r) => r.data);
}

export function verifyChain(id: number, signal?: AbortSignal) {
  return apiFetch<VerifyChainResponse>(`/api/v1/evidence/${id}/chain/verify`, {
    method: 'GET',
    signal,
  }).then((r) => r.data);
}
