const de = 'de-DE';
const eur = new Intl.NumberFormat(de, { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 });
const eur2 = new Intl.NumberFormat(de, { style: 'currency', currency: 'EUR', minimumFractionDigits: 2 });
const num = new Intl.NumberFormat(de, { maximumFractionDigits: 2 });
const pct = new Intl.NumberFormat(de, { style: 'percent', maximumFractionDigits: 1 });

export const money = (v: number) => eur.format(v);
export const money2 = (v: number) => eur2.format(v);
export const number = (v: number) => num.format(v);
export const percent = (v: number | null) => (v === null ? '–' : pct.format(v));

/** 1.234 → "1,2 Tsd. €", 1.234.567 → "1,2 Mio. €" – for axes and tiles. */
export function compactMoney(v: number) {
  const abs = Math.abs(v);
  if (abs >= 1_000_000) return `${num.format(Math.round(v / 100_000) / 10)} Mio. €`;
  if (abs >= 10_000) return `${num.format(Math.round(v / 1000))} Tsd. €`;
  if (abs >= 1000) return `${num.format(Math.round(v / 100) / 10)} Tsd. €`;
  return eur.format(v);
}

/** Axis ticks: 0 · 50 T · 100 T (short, no currency sign – the axis title carries it). */
export function axisMoney(v: number) {
  if (Math.abs(v) >= 1_000_000) return `${num.format(v / 1_000_000)} Mio`;
  if (Math.abs(v) >= 1000) return `${num.format(v / 1000)} T`;
  return num.format(v);
}

export const date = (iso: string | null) => (iso ? new Date(`${iso}T00:00:00`).toLocaleDateString(de) : '–');
export const dateTime = (iso: string) => new Date(iso).toLocaleString(de, { dateStyle: 'medium', timeStyle: 'short' });
