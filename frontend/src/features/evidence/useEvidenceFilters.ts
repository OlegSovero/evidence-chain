import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { EvidenceIntegrityStatus } from '../../api/types';

export interface EvidenceFilters {
  q: string;
  custodianId?: number;
  status?: EvidenceIntegrityStatus;
  sort: 'asc' | 'desc';
}

const DEBOUNCE_MS = 400;

// Todo el estado de los filtros vive en la URL (useSearchParams): recargar la
// página o compartir el enlace conserva exactamente la misma bandeja.
export function useEvidenceFilters() {
  const [searchParams, setSearchParams] = useSearchParams();

  const q = searchParams.get('q') ?? '';
  const custodianIdRaw = searchParams.get('custodianId');
  const custodianId = custodianIdRaw ? Number(custodianIdRaw) : undefined;
  const status = (searchParams.get('status') as EvidenceIntegrityStatus | null) ?? undefined;
  const sort: 'asc' | 'desc' = searchParams.get('sort') === 'asc' ? 'asc' : 'desc';

  const [qInput, setQInput] = useState(q);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    setQInput(q);
  }, [q]);

  useEffect(
    () => () => {
      clearTimeout(debounceRef.current);
    },
    [],
  );

  function applyFilters(patch: Record<string, string | undefined>) {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(patch)) {
          if (value === undefined || value === '') {
            next.delete(key);
          } else {
            next.set(key, value);
          }
        }
        // Cambiar cualquier filtro invalida la posición de la paginación keyset.
        next.delete('cursor');
        return next;
      },
      { replace: true },
    );
  }

  function setQ(value: string) {
    setQInput(value);
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => applyFilters({ q: value }), DEBOUNCE_MS);
  }

  const setCustodianId = (value: number | undefined) => applyFilters({ custodianId: value?.toString() });
  const setStatus = (value: EvidenceIntegrityStatus | undefined) => applyFilters({ status: value });
  const setSort = (value: 'asc' | 'desc') => applyFilters({ sort: value });

  const filters: EvidenceFilters = { q, custodianId, status, sort };

  return { filters, qInput, setQ, setCustodianId, setStatus, setSort };
}
