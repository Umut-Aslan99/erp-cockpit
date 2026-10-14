import { describe, expect, it } from 'vitest';
import { axisMoney, compactMoney, date, percent } from './format';

const nbsp = (s: string) => s.replace(/ /g, ' ');

describe('German number formats', () => {
  it('compacts money for tiles', () => {
    expect(nbsp(compactMoney(950))).toBe('950 €');
    expect(nbsp(compactMoney(1234))).toBe('1,2 Tsd. €');
    expect(nbsp(compactMoney(193_929))).toBe('194 Tsd. €');
    expect(nbsp(compactMoney(1_487_830))).toBe('1,5 Mio. €');
  });

  it('keeps axis ticks short', () => {
    expect(axisMoney(0)).toBe('0');
    expect(axisMoney(50_000)).toBe('50 T');
    expect(axisMoney(1_500_000)).toBe('1,5 Mio');
  });

  it('formats rates and dates', () => {
    expect(nbsp(percent(0.902))).toBe('90,2 %');
    expect(percent(null)).toBe('–');
    expect(date('2026-10-05')).toBe('5.10.2026');
    expect(date(null)).toBe('–');
  });
});
