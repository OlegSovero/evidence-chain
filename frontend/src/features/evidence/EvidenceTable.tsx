import { Link } from 'react-router-dom';
import { IntegrityBadge } from '../../components/Badges';
import type { EvidenceListItem } from '../../api/types';

const dateFormatter = new Intl.DateTimeFormat('es', { dateStyle: 'medium', timeStyle: 'short' });

export function EvidenceTable({ items }: { items: EvidenceListItem[] }) {
  return (
    <table className="table">
      <thead>
        <tr>
          <th scope="col">Código</th>
          <th scope="col">Descripción</th>
          <th scope="col">Custodio actual</th>
          <th scope="col">Último evento</th>
          <th scope="col">Integridad</th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.id}>
            <th scope="row">
              <Link to={`/evidencias/${item.id}`}>{item.code}</Link>
            </th>
            <td>{item.description}</td>
            <td>{item.currentCustodian.displayName}</td>
            <td>{dateFormatter.format(new Date(item.lastEventAtUtc))}</td>
            <td>
              <IntegrityBadge status={item.integrityStatus} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
