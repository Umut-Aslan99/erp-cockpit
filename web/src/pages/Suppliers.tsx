import { useQuery } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { get, type Overview, type PurchaseRow } from '../lib/api';
import { date, number, percent } from '../lib/format';
import { useTable } from '../lib/useTable';
import { PageHead } from '../components/Layout';
import { DataTable } from '../components/DataTable';
import { Icon } from '../components/Icon';
import { Segmented } from '../components/Segmented';
import { SearchInput } from '../components/SearchInput';

const TARGET = 0.9;

const stateChip = (r: PurchaseRow) => {
  switch (r.state) {
    case 'überfällig': return <span className="chip chip-critical">⚠ überfällig {r.delayDays} T.</span>;
    case 'verspätet': return <span className="chip">verspätet {r.delayDays} T.</span>;
    case 'pünktlich': return <span className="chip chip-good">✓ pünktlich</span>;
    default: return <span className="chip">offen</span>;
  }
};

const columns: ColumnDef<PurchaseRow, unknown>[] = [
  { id: 'orderNumber', header: 'Bestellung', cell: ({ row: { original: r } }) => <span className="mono">{r.orderNumber}/{r.lineNo}</span> },
  { id: 'supplierName', header: 'Lieferant', accessorKey: 'supplierName' },
  { id: 'articleNumber', header: 'Artikel', meta: { cell: 'wrap-cell' },
    cell: ({ row: { original: r } }) => <><span className="mono">{r.articleNumber}</span><br /><span className="muted">{r.articleName}</span></> },
  { id: 'quantity', header: 'Menge', meta: { align: 'right' }, cell: ({ row: { original: r } }) => <span className="num">{number(r.quantity)}</span> },
  { id: 'orderDate', header: 'Bestellt', cell: ({ row: { original: r } }) => <span className="num">{date(r.orderDate)}</span> },
  { id: 'promisedDate', header: 'Bestätigt für', cell: ({ row: { original: r } }) => <span className="num">{date(r.promisedDate)}</span> },
  { id: 'receivedDate', header: 'Eingang', cell: ({ row: { original: r } }) => <span className="num">{date(r.receivedDate)}</span> },
  { id: 'state', header: 'Status', cell: ({ row: { original: r } }) => stateChip(r) },
];

export function Suppliers() {
  const t = useTable<PurchaseRow>('purchases', ['q', 'view', 'supplier']);
  const suppliers = useQuery({ queryKey: ['kpis'], queryFn: () => get<Overview>('/api/kpis') }).data?.suppliers ?? [];
  const selected = suppliers.find((s) => s.number === t.filters.supplier);

  return (
    <>
      <PageHead eyebrow="Einkauf" title="Lieferanten und Liefertreue"
        intro={`Liefertreue = Anteil der Wareneingänge der letzten 12 Monate, die spätestens zum bestätigten Termin kamen. Ziel: ${percent(TARGET)}.`} />
      <div className="card table-wrap" style={{ marginBottom: 18 }}>
        <table className="data">
          <thead><tr><th>Lieferant</th><th>Ort</th><th className="right">Wareneingänge</th><th className="right">Liefertreue</th><th className="right">Ø Verzug (verspätete)</th><th className="right">Offen</th><th className="right">davon überfällig</th></tr></thead>
          <tbody>
            {suppliers.map((s) => {
              const low = s.onTimeRate !== null && s.onTimeRate < TARGET;
              return (
                <tr key={s.number} className="clickable" aria-selected={s.number === t.filters.supplier}
                  style={s.number === t.filters.supplier ? { background: 'var(--glow)' } : undefined}
                  onClick={() => t.setFilter('supplier', s.number === t.filters.supplier ? '' : s.number)}>
                  <td><span className="mono">{s.number}</span> {s.name}</td>
                  <td className="muted">{s.city}</td>
                  <td className="right num">{s.received}</td>
                  <td className={`right num${low ? ' critical' : ''}`}>{low ? '⚠ ' : ''}{percent(s.onTimeRate)}</td>
                  <td className="right num">{s.avgDelayLate === null ? '–' : `${number(s.avgDelayLate)} Tage`}</td>
                  <td className="right num">{s.open}</td>
                  <td className={`right num${s.openOverdue ? ' critical' : ''}`}>{s.openOverdue}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <h2 style={{ fontSize: '1.15rem', marginBottom: 10 }}>Bestellpositionen{selected ? ` · ${selected.name}` : ''}</h2>
      <div className="toolbar">
        <SearchInput placeholder="Bestellung oder Artikel …" label="Suchen" value={t.filters.q ?? ''} onChange={(v) => t.setFilter('q', v)} />
        <Segmented label="Status" value={t.filters.view ?? ''} onChange={(v) => t.setFilter('view', v)}
          options={[['', 'Alle'], ['open', 'Offen'], ['overdue', 'Überfällig'], ['late', 'Verspätet geliefert']]} />
        {selected && <button className="btn btn-sm btn-ghost" type="button" onClick={() => t.setFilter('supplier', '')}>Alle Lieferanten</button>}
        <span className="grow" />
        <a className="btn btn-sm" href={t.exportHref}><Icon name="download" />Excel</a>
      </div>
      <DataTable columns={columns} {...t.table} />
    </>
  );
}
