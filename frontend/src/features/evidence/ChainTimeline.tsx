import type { ChainEvent, CustodyEventType } from '../../api/types';

const dateFormatter = new Intl.DateTimeFormat('es', { dateStyle: 'medium', timeStyle: 'medium' });

const EVENT_LABEL: Record<CustodyEventType, string> = {
  Registrada: 'Registrada',
  TransferenciaSolicitada: 'Transferencia solicitada',
  TransferenciaAceptada: 'Transferencia aceptada',
  TransferenciaRechazada: 'Transferencia rechazada',
};

function truncateHash(hash: string): string {
  return `${hash.slice(0, 12)}…`;
}

export function ChainTimeline({
  events,
  highlightSequence,
}: {
  events: ChainEvent[];
  highlightSequence?: number;
}) {
  return (
    <ol className="timeline">
      {events.map((event) => (
        <li
          key={event.id}
          className={event.sequence === highlightSequence ? 'timeline__item timeline__item--broken' : 'timeline__item'}
        >
          <div className="timeline__header">
            <span className="timeline__sequence">#{event.sequence}</span>
            <span>{EVENT_LABEL[event.eventType]}</span>
            <time dateTime={event.occurredAtUtc}>{dateFormatter.format(new Date(event.occurredAtUtc))}</time>
          </div>
          <div className="timeline__detail">
            {event.fromCustodian && event.toCustodian ? (
              <span>
                {event.fromCustodian.displayName} → {event.toCustodian.displayName}
              </span>
            ) : (
              <span>Actor: {event.actor.displayName}</span>
            )}
          </div>
          {event.notes && <p className="timeline__notes">{event.notes}</p>}
          <code className="timeline__hash" title={event.hash}>
            hash {truncateHash(event.hash)}
          </code>
          {event.sequence === highlightSequence && (
            <p className="timeline__flag" role="alert">
              La verificación marcó este evento como el primer punto de ruptura de la cadena.
            </p>
          )}
        </li>
      ))}
    </ol>
  );
}
