import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { get, qs, type MovementRow, type Page, type StockRow } from '../lib/api';
import { date, money, number } from '../lib/format';
import { useTable } from '../lib/useTable';
import { PageHead } from '../components/Layout';
import { DataTable } from '../components/DataTable';
import { Icon } from '../components/Icon';
import { StatTile } from '../components/StatTile';

const columns: ColumnDef<MovementRow, unknown>[] = [
  { id: 'date', header: 'Datum', cell: ({ row: { original: m } }) => <span className="num">{date(m.date)}</span> },
  { id: 'type', header: 'Bewegungsart', enableSorting: false, accessorKey: 'type' },
  { id: 'quantity', header: 'Menge', meta: { align: 'right' },
    cell: ({ row: { original: m } }) => <span className="num" style={{ color: m.quantity < 0 ? 'var(--text-2)' : undefined }}>{m.quantity > 0 ? '+' : ''}{number(m.quantity)}</span> },
  { id: 'reference', header: 'Beleg', enableSorting: false, cell: ({ row: { original: m } }) => <span className="mono">{m.reference ?? '–'}</span> },
];

export function Article() {
  const { number: articleNumber = '' } = useParams();
  const t = useTable<MovementRow>('movements', ['view'], { article: articleNumber });
  const article = useQuery({
    queryKey: ['stock-one', articleNumber],
    queryFn: () => get<Page<StockRow>>(`/api/tables/stock${qs({ q: articleNumber, size: 5 })}`).then((p) => p.rows.find((r) => r.number === articleNumber)),
  }).data;


  return (
    <>
      <Link className="btn btn-sm btn-ghost" to="/lager">← Lager</Link>
      <PageHead eyebrow={`Artikel ${articleNumber}`} title={article?.name ?? articleNumber} intro={article ? `${article.group} · Hauptlieferant ${article.supplierNumber ?? '–'}` : undefined}>
        <a className="btn btn-sm" href={t.exportHref}><Icon name="download" />Bewegungen als Excel</a>
      </PageHead>
      {article && (
        <div className="tiles" style={{ marginBottom: 14 }}>
          <StatTile label="Bestand" value={`${number(article.onHand)} ${article.unit}`} sub={`Lagerwert ${money(article.value)}`} />
          <StatTile label="Einstandspreis" value={money(article.unitCost)} />
          <StatTile label="Abgang 12 Monate" value={`${number(article.issued365)} ${article.unit}`} sub={article.turnover === null ? undefined : `Umschlag ${number(article.turnover)}×`} />
          <StatTile label="Reichweite" value={article.daysOfSupply === null ? '–' : `${article.daysOfSupply} Tage`} sub="bei Verbrauch der letzten 90 Tage" />
          <StatTile label="Letzter Abgang" value={date(article.lastIssue)} tone={article.slowMover ? 'critical' : undefined} sub={article.slowMover ? '⚠ Ladenhüter' : undefined} />
        </div>
      )}
      <DataTable columns={columns} {...t.table} />
    </>
  );
}
