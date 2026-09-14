import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { listEvidence } from '../../api/evidence';
import { listDemoUsers } from '../../api/auth';
import { LoadingState } from '../../components/LoadingState';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { EvidenceTable } from './EvidenceTable';
import { useEvidenceFilters } from './useEvidenceFilters';
import type { EvidenceIntegrityStatus } from '../../api/types';

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: EvidenceIntegrityStatus; label: string }[] = [
  { value: 'NoVerificada', label: 'No verificada' },
  { value: 'Integra', label: 'Íntegra' },
  { value: 'Comprometida', label: 'Comprometida' },
];

export function InboxPage() {
  const { filters, qInput, setQ, setCustodianId, setStatus, setSort } = useEvidenceFilters();
  const [searchParams, setSearchParams] = useSearchParams();
  const cursor = searchParams.get('cursor') ?? undefined;

  // La API solo expone nextCursor (paginación keyset hacia adelante); guardamos
  // los cursores ya visitados en el cliente para poder ofrecer "Anterior".
  const [history, setHistory] = useState<string[]>([]);
  const filterKey = `${filters.q}|${filters.custodianId ?? ''}|${filters.status ?? ''}|${filters.sort}`;
  useEffect(() => {
    setHistory([]);
  }, [filterKey]);

  const { data: custodians } = useQuery({
    queryKey: ['auth', 'demo-users'],
    queryFn: ({ signal }) => listDemoUsers(signal),
    staleTime: Infinity,
    select: (response) => response.items.filter((u) => u.role === 'Custodio'),
  });

  const { data, isLoading, isFetching, isError, error, refetch } = useQuery({
    queryKey: ['evidence', 'list', filters, cursor, PAGE_SIZE],
    queryFn: ({ signal }) =>
      listEvidence(
        {
          q: filters.q || undefined,
          custodianId: filters.custodianId,
          status: filters.status,
          sort: filters.sort,
          cursor,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
  });

  function goToCursor(next: string | undefined) {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev);
        if (next) {
          params.set('cursor', next);
        } else {
          params.delete('cursor');
        }
        return params;
      },
      { replace: true },
    );
  }

  function goNext() {
    if (!data?.nextCursor) return;
    setHistory((h) => [...h, cursor ?? '']);
    goToCursor(data.nextCursor);
  }

  function goPrev() {
    setHistory((h) => {
      const copy = [...h];
      const previous = copy.pop();
      goToCursor(previous || undefined);
      return copy;
    });
  }

  return (
    <section>
      <h1>Evidencias</h1>
      <form className="filters" onSubmit={(e) => e.preventDefault()}>
        <div className="filters__field">
          <label htmlFor="q">Buscar</label>
          <input
            id="q"
            type="search"
            placeholder="Código o descripción"
            value={qInput}
            onChange={(e) => setQ(e.target.value)}
          />
        </div>
        <div className="filters__field">
          <label htmlFor="custodianId">Custodio</label>
          <select
            id="custodianId"
            value={filters.custodianId ?? ''}
            onChange={(e) => setCustodianId(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">Todos</option>
            {custodians?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.displayName}
              </option>
            ))}
          </select>
        </div>
        <div className="filters__field">
          <label htmlFor="status">Estado de integridad</label>
          <select
            id="status"
            value={filters.status ?? ''}
            onChange={(e) => setStatus((e.target.value || undefined) as EvidenceIntegrityStatus | undefined)}
          >
            <option value="">Todos</option>
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
        <div className="filters__field">
          <label htmlFor="sort">Orden por fecha</label>
          <select id="sort" value={filters.sort} onChange={(e) => setSort(e.target.value as 'asc' | 'desc')}>
            <option value="desc">Más reciente primero</option>
            <option value="asc">Más antiguo primero</option>
          </select>
        </div>
      </form>

      {isLoading && <LoadingState label="Cargando evidencias…" />}
      {isError && <ErrorState error={error} onRetry={() => void refetch()} />}
      {!isLoading && !isError && data && data.items.length === 0 && (
        <EmptyState message="No hay evidencias que coincidan con estos filtros." />
      )}
      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          <EvidenceTable items={data.items} />
          <nav className="pagination" aria-label="Paginación de evidencias">
            <button type="button" onClick={goPrev} disabled={history.length === 0 || isFetching}>
              Anterior
            </button>
            <button type="button" onClick={goNext} disabled={!data.nextCursor || isFetching}>
              Siguiente
            </button>
            {isFetching && <span aria-hidden="true"> actualizando…</span>}
          </nav>
        </>
      )}
    </section>
  );
}
