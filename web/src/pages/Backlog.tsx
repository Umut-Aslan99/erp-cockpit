import type { ColumnDef } from '@tanstack/react-table';
import type { BacklogRow } from '../lib/api';
import { date, money, number } from '../lib/format';
import { useTable } from '../lib/useTable';
import { PageHead } from '../components/Layout';
import { DataTable } from '../components/DataTable';
import { Icon } from '../components/Icon';
import { Segmented } from '../components/Segmented';
import { SearchInput } from '../components/SearchInput';

const columns: ColumnDef<BacklogRow, unknown>[] = [
  { id: 'orderNumber', header: 'Auftrag', cell: ({ row: { original: r } }) => <span className="mono">{r.orderNumber}/{r.lineNo}</span> },
  { id: 'customer', header: 'Kunde', accessorKey: 'customer' },
  { id: 'articleNumber', header: 'Artikel', meta: { cell: 'wrap-cell' },
    cell: ({ row: { original: r } }) => <><span className="mono">{r.articleNumber}</span><br /><span className="muted">{r.articleName}</span></> },
  { id: 'quantity', header: 'Menge', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{number(r.quantity)}</span> },
  { id: 'value', header: 'Wert', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{money(r.value)}</span> },
  { id: 'orderDate', header: 'Auftragsdatum', cell: ({ row: { original: r } }) => <span className="num">{date(r.orderDate)}</span> },
  { id: 'dueDate', header: 'Liefertermin', cell: ({ row: { original: r } }) => <span className="num">{date(r.dueDate)}</span> },
  { id: 'daysOverdue', header: 'Status', cell: ({ row: { original: r } }) => r.daysOverdue > 0
    ? <span className="chip chip-critical">⚠ {r.daysOverdue} Tage überfällig</span> : <span className="chip">offen</span> },
];

export function Backlog() {
  const t = useTable<BacklogRow>('backlog', ['q', 'view']);
  return (
    <>
      <PageHead eyebrow="Kundenaufträge" title="Auftragsrückstand"
        intro="Alle Auftragspositionen ohne Lieferdatum. Überfällig heißt: Liefertermin liegt in der Vergangenheit." />
      <div className="toolbar">
        <SearchInput placeholder="Auftrag, Kunde oder Artikel …" label="Suchen" value={t.filters.q ?? ''} onChange={(v) => t.setFilter('q', v)} />
        <Segmented label="Ansicht" value={t.filters.view ?? ''} onChange={(v) => t.setFilter('view', v)} options={[['', 'Alle offenen'], ['overdue', 'Überfällig']]} />
        <span className="grow" />
        <a className="btn btn-sm" href={t.exportHref}><Icon name="download" />Excel</a>
      </div>
      <DataTable columns={columns} {...t.table} />
    </>
  );
}
