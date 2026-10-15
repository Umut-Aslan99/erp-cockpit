import { useEffect, useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { get, post, type WorkspaceInfo } from '../lib/api';
import { Icon } from './Icon';

const nav = [
  ['/', 'Übersicht'],
  ['/lager', 'Lager'],
  ['/auftraege', 'Aufträge'],
  ['/lieferanten', 'Lieferanten'],
  ['/import', 'Import'],
] as const;

function useTheme() {
  const resolve = () =>
    document.documentElement.dataset.theme ?? (matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
  const [theme, setTheme] = useState(resolve);
  useEffect(() => {
    const mq = matchMedia('(prefers-color-scheme: light)');
    const sync = () => setTheme(resolve());
    mq.addEventListener('change', sync);
    return () => mq.removeEventListener('change', sync);
  }, []);
  const toggle = () => {
    const next = theme === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem('theme', next); } catch { /* private mode */ }
    setTheme(next);
  };
  return { theme, toggle };
}

export function Layout() {
  const { theme, toggle } = useTheme();
  const client = useQueryClient();
  const ws = useQuery({ queryKey: ['workspace'], queryFn: () => get<WorkspaceInfo>('/api/workspace') });
  const reset = useMutation({
    mutationFn: () => post('/api/workspace/reset'),
    onSuccess: () => client.invalidateQueries(),
  });

  return (
    <>
      <a className="sr-only" href="#main">Zum Inhalt</a>
      <header className="band">
        <div className="band-inner">
          <span className="brand">
            <a className="who" href="https://umut-aslan.dev">Umut Aslan</a>
            <span className="sep">/</span>
            <NavLink className="what" to="/">ERP-Cockpit</NavLink>
          </span>
          <nav className="nav" aria-label="Bereiche">
            {nav.map(([to, label]) => (
              <NavLink key={to} to={to} end={to === '/'}>{label}</NavLink>
            ))}
          </nav>
          <div className="tools">
            <button className="tool" type="button" onClick={toggle} aria-label={theme === 'dark' ? 'Helles Design' : 'Dunkles Design'}>
              <Icon name={theme === 'dark' ? 'sun' : 'moon'} />
            </button>
          </div>
        </div>
        <div className="bar" aria-hidden="true" />
      </header>

      <main className="page" id="main">
        <div className="workspace-note" role="note" data-tour="workspace">
          <span><strong>Ihr persönlicher Demo-Arbeitsbereich.</strong> Importe und Änderungen sehen nur Sie; er wird nach {ws.data?.expiresAfterIdleHours ?? 24} Stunden ohne Nutzung gelöscht. Alle Firmen und Daten sind frei erfunden.</span>
          <button className="btn btn-sm btn-ghost" type="button" disabled={reset.isPending}
            onClick={() => { if (confirm('Arbeitsbereich auf die Demo-Daten zurücksetzen? Ihre Importe gehen verloren.')) reset.mutate(); }}>
            <Icon name="reset" />{reset.isPending ? 'Wird zurückgesetzt …' : 'Zurücksetzen'}
          </button>
        </div>
        <Outlet />
      </main>

      <footer className="foot">
        <div className="foot-inner">
          <span>Ein Demo-Projekt von <a href="https://umut-aslan.dev">Umut Aslan</a> · React · ASP.NET Core · PostgreSQL</span>
          <span><a href="https://umut-aslan.dev/impressum/">Impressum</a> · <a href="https://umut-aslan.dev/datenschutz/">Datenschutz</a></span>
        </div>
      </footer>
    </>
  );
}

export function PageHead({ eyebrow, title, intro, children }: { eyebrow: string; title: string; intro?: string; children?: React.ReactNode }) {
  return (
    <div className="page-head">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        {intro && <p className="muted">{intro}</p>}
      </div>
      {children}
    </div>
  );
}
