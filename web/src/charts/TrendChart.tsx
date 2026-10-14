import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { MonthPoint } from '../lib/api';
import { compactMoney, money } from '../lib/format';
import { ChartTooltip, axisLine, axisTick, gridProps, moneyAxis } from './common';

const series = [
  { key: 'issues', label: 'Abgänge', color: 'var(--series-1)' },
  { key: 'receipts', label: 'Zugänge', color: 'var(--series-2)' },
] as const;

export const trendLegend = (
  <div className="legend">{series.map((s) => <span key={s.key}><i style={{ background: s.color }} />{s.label}</span>)}</div>
);

/** Two series over 12 months: legend above, direct label at the line ends, crosshair tooltip. */
export function TrendChart({ data }: { data: MonthPoint[] }) {
  const last = data.length - 1;
  return (
    <ResponsiveContainer width="100%" height={260}>
      <LineChart data={data} margin={{ top: 12, right: 76, left: 0, bottom: 0 }}>
        <CartesianGrid {...gridProps} />
        <XAxis dataKey="month" tick={axisTick} axisLine={axisLine} tickLine={false} interval="preserveStartEnd" minTickGap={18} />
        <YAxis {...moneyAxis} />
        <Tooltip
          cursor={{ stroke: 'var(--axis)', strokeWidth: 1 }}
          content={({ active, payload, label }) => active && payload?.length ? (
            <ChartTooltip title={String(label)} rows={series.map((s) => ({
              key: s.key, label: s.label, color: s.color,
              value: money(Number(payload.find((p) => p.dataKey === s.key)?.value ?? 0)),
            }))} />
          ) : null}
        />
        {series.map((s) => (
          <Line key={s.key} type="monotone" dataKey={s.key} name={s.label} stroke={s.color} strokeWidth={2} dot={false}
            activeDot={{ r: 4, fill: s.color, stroke: 'var(--chart-surface)', strokeWidth: 2 }} isAnimationActive={false}
            label={({ x, y, index, value }: { x?: unknown; y?: unknown; index?: number; value?: unknown }) =>
              index === last ? (
                <text x={Number(x) + 8} y={Number(y)} dy={4} fontSize={12} fill="var(--text-2)">{compactMoney(Number(value))}</text>
              ) : <g />}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );
}
