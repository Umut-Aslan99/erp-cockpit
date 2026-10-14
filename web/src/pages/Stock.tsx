import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { get, type Overview, type StockRow } from '../lib/api';
import { date, money, number } from '../lib/format';
import { useTable } from '../lib/useTable';
import { PageHead } from '../components/Layout';
import { DataTable } from '../components/DataTable';
import { Icon } from '../components/Icon';
import { Segmented } from '../components/Segmented';
import { SearchInput } from '../components/SearchInput';

const columns: ColumnDef<StockRow, unknown>[] = [
  { id: 'number', header: 'Artikel', accessorKey: 'number', meta: { cell: 'wrap-cell' },
    cell: ({ row: { original: r } }) => <><span className="mono">{r.number}</span><br /><span className="muted">{r.name}</span></> },
  { id: 'group', header: 'Warengruppe', accessorKey: 'group' },
  { id: 'onHand', header: 'Bestand', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{number(r.onHand)} {r.unit}</span> },
  { id: 'value', header: 'Lagerwert', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{money(r.value)}</span> },
  { id: 'turnover', header: 'Umschlag', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{r.turnover === null ? '–' : `${number(r.turnover)}×`}</span> },
  { id: 'daysOfSupply', header: 'Reichweite', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{r.daysOfSupply === null ? '–' : `${r.daysOfSupply} Tage`}</span> },
  { id: 'lastIssue', header: 'Letzter Abgang', cell: ({ row: { original: r } }) => <span className="num">{date(r.lastIssue)}</span> },
  { id: 'slowMover', header: '', enableSorting: false, cell: ({ row: { original: r } }) => r.slowMover ? <span className="chip chip-critical">Ladenhüter</span> : null },
];

export function Stock() {
  const navigate = useNavigate();
  const t = useTable<StockRow>('stock', ['q', 'view', 'group']);
  const groups = useQuery({ queryKey: ['kpis'], queryFn: () => get<Overview>('/api/kpis') }).data?.groups.map((g) => g.group).sort() ?? [];

  return (
    <>
      <PageHead eyebrow="Lager" title="Bestand, Umschlag, Ladenhüter"
        intro="Bestand aus allen Lagerbewegungen berechnet. Ladenhüter: Bestand vorhanden, aber seit über 180 Tagen kein Abgang. Zeile anklicken für die Bewegungen des Artikels." />
      <div className="toolbar">
        <SearchInput placeholder="Artikel suchen …" label="Artikel suchen" value={t.filters.q ?? ''} onChange={(v) => t.setFilter('q', v)} />
        <select className="select" aria-label="Warengruppe" value={t.filters.group} onChange={(e) => t.setFilter('group', e.target.value)}>
          <option value="">Alle Warengruppen</option>
          {groups.map((g) => <option key={g}>{g}</option>)}
        </select>
        <Segmented label="Ansicht" value={t.filters.view ?? ''} onChange={(v) => t.setFilter('view', v)}
          options={[['', 'Alle'], ['stock', 'Mit Bestand'], ['slow', 'Ladenhüter']]} />
        <span className="grow" />
        <a className="btn btn-sm" href={t.exportHref}><Icon name="download" />Excel</a>
      </div>
      <DataTable columns={columns} {...t.table} onRowClick={(r) => navigate(`/lager/${encodeURIComponent(r.number)}`)} />
    </>
  );
}
