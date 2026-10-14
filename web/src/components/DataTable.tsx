import { flexRender, getCoreRowModel, useReactTable, type ColumnDef, type SortingState } from '@tanstack/react-table';

/** Column meta: `align: 'right'` for numbers, `cell: 'wrap-cell'` for long text. */
type Meta = { align?: string; cell?: string } | undefined;
const cellClass = (meta: unknown) => [(meta as Meta)?.align, (meta as Meta)?.cell].filter(Boolean).join(' ') || undefined;

export interface ServerTable<T> {
  rows: T[];
  total: number;
  page: number;
  size: number;
  sorting: SortingState;
  onSortingChange: (s: SortingState) => void;
  onPageChange: (p: number) => void;
  loading?: boolean;
  onRowClick?: (row: T) => void;
  rowClass?: (row: T) => string | undefined;
}

/** TanStack Table in "manual" mode: sorting and paging happen on the server, the table only renders. */
export function DataTable<T>({ columns, ...t }: { columns: ColumnDef<T, unknown>[] } & ServerTable<T>) {
  const table = useReactTable({
    data: t.rows,
    columns,
    getCoreRowModel: getCoreRowModel(),
    manualSorting: true,
    manualPagination: true,
    enableSortingRemoval: false,
    state: { sorting: t.sorting },
    onSortingChange: (updater) => t.onSortingChange(typeof updater === 'function' ? updater(t.sorting) : updater),
    pageCount: Math.max(1, Math.ceil(t.total / t.size)),
  });
  const pages = Math.max(1, Math.ceil(t.total / t.size));

  return (
    <div className={`card${t.loading ? ' loading' : ''}`}>
      <div className="table-wrap">
        <table className="data">
          <thead>
            {table.getHeaderGroups().map((hg) => (
              <tr key={hg.id}>
                {hg.headers.map((h) => {
                  const sorted = h.column.getIsSorted();
                  const align = (h.column.columnDef.meta as Meta)?.align;
                  return (
                    <th key={h.id} className={align} aria-sort={sorted ? (sorted === 'asc' ? 'ascending' : 'descending') : undefined}>
                      {h.column.getCanSort() ? (
                        <button type="button" onClick={h.column.getToggleSortingHandler()}>
                          {flexRender(h.column.columnDef.header, h.getContext())}
                          <span aria-hidden="true">{sorted === 'asc' ? '▲' : sorted === 'desc' ? '▼' : ''}</span>
                        </button>
                      ) : flexRender(h.column.columnDef.header, h.getContext())}
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((r) => (
              <tr key={r.id} className={[t.onRowClick ? 'clickable' : '', t.rowClass?.(r.original) ?? ''].join(' ').trim() || undefined}
                onClick={t.onRowClick ? () => t.onRowClick!(r.original) : undefined}>
                {r.getVisibleCells().map((c) => (
                  <td key={c.id} className={cellClass(c.column.columnDef.meta)}>
                    {flexRender(c.column.columnDef.cell, c.getContext())}
                  </td>
                ))}
              </tr>
            ))}
            {t.rows.length === 0 && (
              <tr><td colSpan={columns.length} className="muted" style={{ padding: 24, textAlign: 'center' }}>Keine Einträge für diese Auswahl.</td></tr>
            )}
          </tbody>
        </table>
      </div>
      <div className="table-foot">
        <span className="num">{t.total.toLocaleString('de-DE')} Einträge</span>
        <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <button className="btn btn-sm" type="button" disabled={t.page <= 1} onClick={() => t.onPageChange(t.page - 1)}>Zurück</button>
          <span className="num">Seite {t.page} / {pages}</span>
          <button className="btn btn-sm" type="button" disabled={t.page >= pages} onClick={() => t.onPageChange(t.page + 1)}>Weiter</button>
        </span>
      </div>
    </div>
  );
}
