import { Link } from 'react-router';

/** Stat tile contract: label · value · optional context line. Links to the table behind the number. */
export function StatTile({ label, value, sub, to, tone }: { label: string; value: string; sub?: React.ReactNode; to?: string; tone?: 'critical' }) {
  const body = (
    <>
      <span className="label">{label}</span>
      <span className={`value${tone === 'critical' ? ' critical' : ''}`}>{value}</span>
      {sub && <span className="sub">{sub}</span>}
    </>
  );
  return to ? <Link className="card tile" to={to}>{body}</Link> : <div className="card tile">{body}</div>;
}
