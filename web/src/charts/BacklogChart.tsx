import { Bar, BarChart, CartesianGrid, Cell, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { WeekPoint } from '../lib/api';
import { compactMoney, money } from '../lib/format';
import { ChartTooltip, axisLine, gridProps, moneyAxis } from './common';

/**
 * Open order value by due week. One series (slot 1); the overdue bucket wears the reserved
 * status "critical" because it *means* bad – with a warning sign and its value as label, never color alone.
 */
export function BacklogChart({ data, onSelect }: { data: WeekPoint[]; onSelect?: (w: WeekPoint) => void }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <BarChart data={data} margin={{ top: 22, right: 8, left: 0, bottom: 0 }} barCategoryGap="22%">
        <CartesianGrid {...gridProps} />
        <XAxis dataKey="bucket" tickLine={false} axisLine={axisLine} interval={0}
          tick={({ x, y, payload }: { x?: number | string; y?: number | string; payload?: { value: string } }) => {
            const overdue = payload?.value === 'Überfällig';
            return (
              <text x={Number(x)} y={Number(y) + 14} textAnchor="middle" fontSize={11.5} fill={overdue ? 'var(--critical)' : 'var(--axis-text)'} fontWeight={overdue ? 600 : 400}>
                {overdue ? '⚠ Überfällig' : payload?.value}
              </text>
            );
          }} />
        <YAxis {...moneyAxis} />
        <Tooltip
          cursor={{ fill: 'var(--glow)' }}
          content={({ active, payload }) => {
            const w = payload?.[0]?.payload as WeekPoint | undefined;
            return active && w ? <ChartTooltip title={w.bucket} rows={[
              { key: 'v', label: 'Auftragswert', value: money(w.value) },
              { key: 'n', label: 'Positionen', value: String(w.lines) },
            ]} /> : null;
          }}
        />
        <Bar dataKey="value" radius={[4, 4, 0, 0]} maxBarSize={24} isAnimationActive={false}
          onClick={(d) => onSelect?.(d.payload as WeekPoint)} style={{ cursor: onSelect ? 'pointer' : undefined }}>
          {data.map((w) => <Cell key={w.bucket} fill={w.overdue ? 'var(--critical)' : 'var(--series-1)'} />)}
          <LabelList dataKey="value" position="top" fontSize={11.5} fill="var(--text-2)"
            content={({ x, y, width, value, index }) => index === 0 && Number(value) > 0 ? (
              <text x={Number(x) + Number(width) / 2} y={Number(y) - 6} textAnchor="middle" fontSize={11.5} fill="var(--text-2)">{compactMoney(Number(value))}</text>
            ) : null} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
