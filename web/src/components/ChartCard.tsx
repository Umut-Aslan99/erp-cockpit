import { useState } from 'react';
import { Icon } from './Icon';

/**
 * Chart container with its accessible twin: every chart can be switched to a plain table
 * showing the same numbers (tooltips enhance, they never gate).
 */
export function ChartCard({ title, sub, legend, table, children, action, tour }: {
  title: string; sub?: string; legend?: React.ReactNode; table: React.ReactNode; children: React.ReactNode; action?: React.ReactNode;
  /** Anchor for the guided tour (data-tour). */
  tour?: string;
}) {
  const [asTable, setAsTable] = useState(false);
  return (
    <figure className="card chart-card" style={{ margin: 0 }} data-tour={tour}>
      <header>
        <div>
          <h2>{title}</h2>
          {sub && <p className="sub">{sub}</p>}
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          {action}
          <button className="btn btn-sm btn-ghost" type="button" onClick={() => setAsTable(!asTable)} aria-pressed={asTable}
            aria-label={asTable ? 'Als Diagramm anzeigen' : 'Als Tabelle anzeigen'}>
            <Icon name={asTable ? 'chart' : 'table'} />{asTable ? 'Diagramm' : 'Tabelle'}
          </button>
        </div>
      </header>
      {!asTable && legend}
      {asTable ? <div className="table-wrap">{table}</div> : children}
    </figure>
  );
}
