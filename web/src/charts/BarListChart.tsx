import { Bar, BarChart, Cell, LabelList, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ChartTooltip, axisTick } from './common';

export interface BarItem { key: string; label: string; value: number; flagged?: boolean; detail?: { label: string; value: string }[] }

/**
 * Horizontal bars for one measure across named items (product groups, suppliers): one hue,
 * value at the bar tip, optional target line; flagged items (below target) use the status color plus ⚠.
 */
export function BarListChart({ items, format, max, target, targetLabel, onSelect }: {
  items: BarItem[]; format: (v: number) => string; max?: number; target?: number; targetLabel?: string; onSelect?: (key: string) => void;
}) {
  const height = Math.max(160, items.length * 30 + 30);
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={items} layout="vertical" margin={{ top: 4, right: 64, left: 4, bottom: 4 }} barCategoryGap="28%">
        <XAxis type="number" hide domain={[0, max ?? 'auto']} />
        <YAxis type="category" dataKey="label" width={172} tickLine={false} axisLine={false} interval={0}
          tick={({ x, y, payload, index }: { x?: number | string; y?: number | string; payload?: { value: string }; index?: number }) => {
            const flagged = items[index ?? 0]?.flagged;
            return (
              <text x={Number(x) - 6} y={Number(y)} dy={4} textAnchor="end" fontSize={12} fill={flagged ? 'var(--critical)' : axisTick.fill} fontWeight={flagged ? 600 : 400}>
                {flagged ? '⚠ ' : ''}{payload?.value}
              </text>
            );
          }} />
        {target !== undefined && (
          <ReferenceLine x={target} stroke="var(--text-2)" strokeDasharray="4 4"
            label={{ value: targetLabel, position: 'top', fill: 'var(--text-2)', fontSize: 11 }} />
        )}
        <Tooltip
          cursor={{ fill: 'var(--glow)' }}
          content={({ active, payload }) => {
            const it = payload?.[0]?.payload as BarItem | undefined;
            return active && it ? <ChartTooltip title={it.label} rows={[{ key: 'v', label: 'Wert', value: format(it.value) }, ...(it.detail ?? []).map((d) => ({ key: d.label, ...d }))]} /> : null;
          }}
        />
        <Bar dataKey="value" radius={[0, 4, 4, 0]} maxBarSize={18} isAnimationActive={false}
          onClick={(d) => onSelect?.((d.payload as BarItem).key)} style={{ cursor: onSelect ? 'pointer' : undefined }}>
          {items.map((it) => <Cell key={it.key} fill={it.flagged ? 'var(--critical)' : 'var(--series-1)'} />)}
          <LabelList dataKey="value" position="right" fontSize={11.5} fill="var(--text-2)" formatter={(v: unknown) => format(Number(v))} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
