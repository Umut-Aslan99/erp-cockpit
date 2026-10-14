import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { get, type Overview } from '../lib/api';
import { compactMoney, date, money, number, percent } from '../lib/format';
import { PageHead } from '../components/Layout';
import { StatTile } from '../components/StatTile';
import { ChartCard } from '../components/ChartCard';
import { TrendChart, trendLegend } from '../charts/TrendChart';
import { BacklogChart } from '../charts/BacklogChart';
import { BarListChart } from '../charts/BarListChart';

const TARGET = 0.9;
const turnoverFmt = (v: number) => `${number(v)}×`;

export function Dashboard() {
  const navigate = useNavigate();
  const { data: k, error } = useQuery({ queryKey: ['kpis'], queryFn: () => get<Overview>('/api/kpis') });

  if (error) return <p className="alert alert-error">{error.message}</p>;
  if (!k) return <p className="muted">Kennzahlen werden berechnet …</p>;

  return (
    <>
      <PageHead eyebrow={`Übersicht · Stand ${date(k.today)}`} title="Was steckt in den ERP-Daten?"
        intro="Lager, Aufträge und Lieferanten auf einen Blick – berechnet aus den Rohdaten des ERP-Exports. Jede Zahl führt zur Tabelle dahinter." />

      <div className="tiles">
        <StatTile label="Lagerwert" value={compactMoney(k.stockValue)} sub="zu Einstandspreisen" to="/lager?view=stock" />
        <StatTile label="Lagerumschlag" value={k.turnover === null ? '–' : `${number(k.turnover)}× / Jahr`} sub={`Abgänge 12 Monate: ${compactMoney(k.cogs365)}`} to="/lager" />
        <StatTile label="Ladenhüter" value={compactMoney(k.slowMoverValue)} sub={`${k.slowMoverCount} Artikel > 180 Tage ohne Abgang`} to="/lager?view=slow" />
        <StatTile label="Rückstand überfällig" value={compactMoney(k.overdueValue)} tone={k.overdueLines > 0 ? 'critical' : undefined}
          sub={`${k.overdueLines} von ${k.backlogLines} offenen Positionen (${compactMoney(k.backlogValue)})`} to="/auftraege?view=overdue" />
        <StatTile label="Liefertreue" value={percent(k.onTimeRate)} sub={`${k.openPurchaseLines} offene Bestellungen, ${k.overduePurchaseLines} überfällig`} to="/lieferanten" />
      </div>

      <div className="charts">
        <ChartCard title="Lagerabgänge und -zugänge" sub="Monatlich zu Einstandspreisen, letzte 12 Monate" legend={trendLegend}
          table={<table className="data"><thead><tr><th>Monat</th><th className="right">Abgänge</th><th className="right">Zugänge</th></tr></thead>
            <tbody>{k.trend.map((p) => <tr key={p.month}><td>{p.month}</td><td className="right num">{money(p.issues)}</td><td className="right num">{money(p.receipts)}</td></tr>)}</tbody></table>}>
          <TrendChart data={k.trend} />
        </ChartCard>

        <ChartCard title="Auftragsrückstand nach Liefertermin" sub="Offener Auftragswert je Kalenderwoche · Balken anklicken für Details"
          table={<table className="data"><thead><tr><th>Fälligkeit</th><th className="right">Wert</th><th className="right">Positionen</th></tr></thead>
            <tbody>{k.backlogByWeek.map((w) => <tr key={w.bucket}><td className={w.overdue ? 'critical' : undefined}>{w.overdue ? '⚠ ' : ''}{w.bucket}</td><td className="right num">{money(w.value)}</td><td className="right num">{w.lines}</td></tr>)}</tbody></table>}>
          <BacklogChart data={k.backlogByWeek} onSelect={(w) => navigate(w.overdue ? '/auftraege?view=overdue' : '/auftraege?sort=dueDate')} />
        </ChartCard>

        <ChartCard title="Lagerumschlag je Warengruppe" sub="Wie oft sich der Bestand pro Jahr erneuert · höher ist besser"
          table={<table className="data"><thead><tr><th>Warengruppe</th><th className="right">Lagerwert</th><th className="right">Umschlag</th><th className="right">Ladenhüter</th></tr></thead>
            <tbody>{k.groups.map((g) => <tr key={g.group}><td>{g.group}</td><td className="right num">{money(g.stockValue)}</td><td className="right num">{g.turnover === null ? '–' : turnoverFmt(g.turnover)}</td><td className="right num">{g.slowMovers}</td></tr>)}</tbody></table>}>
          <BarListChart format={turnoverFmt} onSelect={(g) => navigate(`/lager?group=${encodeURIComponent(g)}`)}
            items={k.groups.map((g) => ({ key: g.group, label: g.group, value: g.turnover ?? 0,
              detail: [{ label: 'Lagerwert', value: money(g.stockValue) }, { label: 'Ladenhüter', value: String(g.slowMovers) }] }))} />
        </ChartCard>

        <ChartCard title="Liefertreue je Lieferant" sub={`Pünktliche Wareneingänge, 12 Monate · Ziel ${percent(TARGET)}`}
          table={<table className="data"><thead><tr><th>Lieferant</th><th className="right">Liefertreue</th><th className="right">Ø Verzug</th><th className="right">Überfällig offen</th></tr></thead>
            <tbody>{k.suppliers.map((s) => <tr key={s.number}><td>{s.name}</td><td className="right num">{percent(s.onTimeRate)}</td><td className="right num">{s.avgDelayLate === null ? '–' : `${number(s.avgDelayLate)} Tage`}</td><td className="right num">{s.openOverdue}</td></tr>)}</tbody></table>}>
          <BarListChart format={(v) => percent(v)} max={1} target={TARGET} targetLabel={`Ziel ${percent(TARGET)}`}
            onSelect={(n) => navigate(`/lieferanten?supplier=${encodeURIComponent(n)}`)}
            items={k.suppliers.filter((s) => s.onTimeRate !== null).map((s) => ({
              key: s.number, label: s.name.replace(/ (GmbH|KG|AG)$/, ''), value: s.onTimeRate!, flagged: s.onTimeRate! < TARGET,
              detail: [{ label: 'Wareneingänge', value: String(s.received) }, { label: 'Ø Verzug (verspätete)', value: s.avgDelayLate === null ? '–' : `${number(s.avgDelayLate)} Tage` }],
            }))} />
        </ChartCard>
      </div>
    </>
  );
}
