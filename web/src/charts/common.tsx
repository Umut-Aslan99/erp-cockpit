import { axisMoney } from '../lib/format';

// Shared chart chrome: recessive axes and hairline grid, values in text tokens, never in series colors.
export const axisTick = { fill: 'var(--axis-text)', fontSize: 12 } as const;
export const axisLine = { stroke: 'var(--axis)' } as const;
export const gridProps = { stroke: 'var(--grid)', strokeDasharray: undefined, vertical: false } as const;
export const moneyAxis = { tickFormatter: (v: number) => axisMoney(v), width: 56, tick: axisTick, axisLine: false, tickLine: false } as const;

export interface TooltipRow { key: string; label: string; value: string; color?: string }

/** Values lead, labels follow; series keyed by a short line in their color. */
export function ChartTooltip({ title, rows }: { title: string; rows: TooltipRow[] }) {
  return (
    <div className="tooltip" role="status">
      <div className="t-title">{title}</div>
      {rows.map((r) => (
        <div className="t-row" key={r.key}>
          {r.color && <span className="t-key" style={{ background: r.color }} />}
          <span>{r.label}</span>
          <b>{r.value}</b>
        </div>
      ))}
    </div>
  );
}
