import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  analyze, get, post, type Analyzed, type Batch, type Checked, type Committed, type Dataset, type Sample, type SchemaDto,
} from '../lib/api';
import { dateTime, number } from '../lib/format';
import { PageHead } from '../components/Layout';
import { Icon } from '../components/Icon';

type Step = 'file' | 'map' | 'check' | 'done';
const STEPS: [Step, string][] = [['file', 'Datei'], ['map', 'Spalten zuordnen'], ['check', 'Prüfen'], ['done', 'Übernommen']];

export function Import() {
  const client = useQueryClient();
  const schemas = useQuery({ queryKey: ['schemas'], queryFn: () => get<SchemaDto[]>('/api/imports/schemas') }).data ?? [];
  const samples = useQuery({ queryKey: ['samples'], queryFn: () => get<Sample[]>('/api/samples') }).data ?? [];

  const [step, setStep] = useState<Step>('file');
  const [dataset, setDataset] = useState<Dataset>('StockMovements');
  const [file, setFile] = useState<Analyzed | null>(null);
  const [mapping, setMapping] = useState<Record<string, number>>({});
  const [checked, setChecked] = useState<Checked | null>(null);
  const [result, setResult] = useState<Committed | null>(null);
  const [over, setOver] = useState(false);
  const schema = schemas.find((s) => s.dataset === dataset);

  const upload = useMutation({
    mutationFn: ({ f, ds }: { f: File; ds: Dataset }) => analyze(f, ds),
    onSuccess: (a) => {
      setFile(a);
      setMapping(Object.fromEntries(a.suggestions.map((s) => [s.fieldKey, s.column ?? -1])));
      setStep('map');
    },
  });
  const request = () => ({ uploadId: file!.uploadId, dataset, mapping });
  const check = useMutation({ mutationFn: () => post<Checked>('/api/imports/validate', request()), onSuccess: (c) => { setChecked(c); setStep('check'); } });
  const commit = useMutation({
    mutationFn: () => post<Committed>('/api/imports/commit', request()),
    onSuccess: (r) => { setResult(r); setStep('done'); client.invalidateQueries(); },
  });

  const start = (f: File, ds: Dataset) => { setDataset(ds); upload.mutate({ f, ds }); };
  const trySample = async (s: Sample) => {
    const blob = await fetch(`/api/samples/${encodeURIComponent(s.file)}`).then((r) => r.blob());
    start(new File([blob], s.file), s.dataset);
  };
  const restart = () => { setStep('file'); setFile(null); setChecked(null); setResult(null); upload.reset(); check.reset(); commit.reset(); };
  const error = upload.error ?? check.error ?? commit.error;
  const stepIndex = STEPS.findIndex(([s]) => s === step);

  return (
    <>
      <PageHead eyebrow="Import" title="ERP-Export einlesen"
        intro="CSV oder Excel aus dem ERP hochladen. Spalten werden automatisch erkannt, jede Zeile wird geprüft – übernommen werden nur gültige Zeilen, und jeder Import lässt sich zurücknehmen." />

      <ol className="steps" aria-label="Fortschritt">
        {STEPS.map(([s, label], i) => (
          <li key={s} aria-current={s === step ? 'step' : undefined} className={i < stepIndex ? 'done' : undefined}>{label}</li>
        ))}
      </ol>

      {error && <p className="alert alert-error" role="alert" style={{ marginBottom: 14 }}>{error.message}</p>}

      {step === 'file' && (
        <div className="stack">
          <section className="card panel" aria-labelledby="ds-title" style={{ padding: 18 }}>
            <h2 id="ds-title">1 · Was soll importiert werden?</h2>
            <div className="datasets">
              {schemas.map((s) => (
                <button key={s.dataset} type="button" className="dataset" aria-pressed={dataset === s.dataset} onClick={() => setDataset(s.dataset)}>
                  <strong>{s.label}</strong><span>{s.description}</span>
                </button>
              ))}
            </div>
            <div className={`drop${over ? ' over' : ''}`}
              onDragOver={(e) => { e.preventDefault(); setOver(true); }} onDragLeave={() => setOver(false)}
              onDrop={(e) => { e.preventDefault(); setOver(false); const f = e.dataTransfer.files[0]; if (f) start(f, dataset); }}>
              <Icon name="upload" className="" />
              <span>{upload.isPending ? 'Datei wird gelesen …' : <>Datei hierher ziehen oder</>}</span>
              <label className="btn btn-primary">
                Datei auswählen
                <input type="file" accept=".csv,.txt,.xlsx" className="sr-only" data-testid="file-input"
                  onChange={(e) => { const f = e.target.files?.[0]; if (f) start(f, dataset); e.target.value = ''; }} />
              </label>
              <span className="label">.csv oder .xlsx · bis 5 MB · bis 20.000 Zeilen</span>
            </div>
          </section>

          <section aria-labelledby="samples-title">
            <h2 id="samples-title" style={{ fontSize: '1.05rem' }}>Keine Datei zur Hand? Beispieldateien aus typischen Quellen</h2>
            <div className="samples">
              {samples.map((s) => (
                <article key={s.file} className="card sample">
                  <span className="chip">{s.datasetLabel}</span>
                  <strong>{s.title}</strong>
                  <p>{s.description}</p>
                  <div className="actions">
                    <button type="button" className="btn btn-sm btn-primary" disabled={upload.isPending} onClick={() => trySample(s)} data-testid={`try-${s.file}`}>
                      <Icon name="play" />Ausprobieren
                    </button>
                    <a className="btn btn-sm" href={`/api/samples/${encodeURIComponent(s.file)}`} download><Icon name="download" />Herunterladen</a>
                  </div>
                </article>
              ))}
            </div>
          </section>
        </div>
      )}

      {step === 'map' && file && schema && (
        <div className="stack">
          <section className="card panel">
            <h2>2 · Welche Spalte gehört wohin?</h2>
            <p className="muted" style={{ marginBottom: 12 }}>
              <span className="mono">{file.fileName}</span> · {file.format} · {number(file.rowCount)} Zeilen.
              Vorschläge sind vorausgefüllt – bitte prüfen und bei Bedarf ändern. Nicht zugeordnete Spalten werden ignoriert.
            </p>
            <div className="table-wrap">
              <table className="mapping">
                <thead><tr><th>Feld im ERP-Cockpit</th><th>Spalte in der Datei</th><th>Beispielwert</th><th /></tr></thead>
                <tbody>
                  {schema.fields.map((f) => {
                    const col = mapping[f.key] ?? -1;
                    const suggestion = file.suggestions.find((s) => s.fieldKey === f.key);
                    const auto = suggestion?.column === col && col >= 0;
                    return (
                      <tr key={f.key}>
                        <td><strong>{f.label}</strong>{f.required && <span className="chip" style={{ marginLeft: 8 }}>Pflicht</span>}</td>
                        <td>
                          <select className="select" aria-label={`Spalte für ${f.label}`} value={col}
                            onChange={(e) => setMapping({ ...mapping, [f.key]: Number(e.target.value) })}>
                            <option value={-1}>— nicht zuordnen —</option>
                            {file.headers.map((h, i) => <option key={i} value={i}>{h || `Spalte ${i + 1}`}</option>)}
                          </select>
                        </td>
                        <td className="example">{col >= 0 ? file.sample[0]?.[col] : ''}</td>
                        <td>{auto && <span className="chip chip-accent" title={`Ähnlichkeit ${Math.round(suggestion!.confidence * 100)} %`}>automatisch erkannt</span>}
                          {f.required && col < 0 && <span className="chip chip-critical">fehlt</span>}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>

          <section className="card panel">
            <h2>Vorschau der Datei</h2>
            <div className="table-wrap">
              <table className="data">
                <thead><tr>{file.headers.map((h, i) => (
                  <th key={i} style={Object.values(mapping).includes(i) ? { color: 'var(--accent)' } : undefined}>{h}</th>
                ))}</tr></thead>
                <tbody>{file.sample.map((r, i) => <tr key={i}>{r.map((c, j) => <td key={j}>{c}</td>)}</tr>)}</tbody>
              </table>
            </div>
          </section>

          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn" type="button" onClick={restart}>Andere Datei</button>
            <button className="btn btn-primary" type="button" disabled={check.isPending} onClick={() => check.mutate()}>
              {check.isPending ? 'Wird geprüft …' : 'Zeilen prüfen'}<Icon name="arrow" />
            </button>
          </div>
        </div>
      )}

      {step === 'check' && checked && schema && (
        <div className="stack">
          <div className="tiles" style={{ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' }}>
            <div className="card tile"><span className="label">Zeilen in der Datei</span><span className="value">{number(checked.totalRows)}</span></div>
            <div className="card tile"><span className="label">Gültig</span><span className="value">{number(checked.validRows)}</span><span className="sub">werden übernommen</span></div>
            <div className="card tile"><span className="label">Fehlerhaft</span>
              <span className={`value${checked.invalidRows ? ' critical' : ''}`}>{number(checked.invalidRows)}</span><span className="sub">werden übersprungen</span></div>
          </div>

          {checked.missingFields.length > 0 && (
            <p className="alert alert-error" role="alert">Pflichtfelder ohne Spalte: {checked.missingFields.join(', ')}. Bitte in der Zuordnung ergänzen.</p>
          )}
          {Object.keys(checked.numberStyles).length > 0 && (
            <p className="muted" style={{ fontSize: '0.88rem' }}>
              Erkanntes Zahlenformat: {Object.entries(checked.numberStyles).map(([f, s]) => <span key={f} className="chip" style={{ marginLeft: 6 }}>{f}: {s}</span>)}
            </p>
          )}

          {checked.errors.length > 0 && (
            <div className="grid-2">
              <section className="card panel">
                <h2>Fehler nach Art</h2>
                <ul style={{ margin: 0, paddingLeft: 18, display: 'grid', gap: 6 }}>
                  {checked.errorSummary.map((e) => <li key={e.message}><span className="num">{e.count}×</span> {e.message}</li>)}
                </ul>
              </section>
              <section className="card panel">
                <h2>Fehlerhafte Zeilen</h2>
                <div className="table-wrap" style={{ maxHeight: 320, overflowY: 'auto' }}>
                  <table className="data" data-testid="errors">
                    <thead><tr><th>Zeile</th><th>Feld</th><th>Wert</th><th>Problem</th></tr></thead>
                    <tbody>{checked.errors.map((e, i) => (
                      <tr key={i}><td className="num">{e.row}</td><td>{e.field}</td><td className="mono">{e.value || '–'}</td><td className="wrap-cell">{e.message}</td></tr>
                    ))}</tbody>
                  </table>
                </div>
              </section>
            </div>
          )}

          {checked.preview.length > 0 && (
            <section className="card panel">
              <h2>So werden die gültigen Zeilen übernommen</h2>
              <div className="table-wrap">
                <table className="data">
                  <thead><tr>{schema.fields.map((f) => <th key={f.key}>{f.label}</th>)}</tr></thead>
                  <tbody>{checked.preview.map((r, i) => <tr key={i}>{schema.fields.map((f) => <td key={f.key}>{r[f.key]}</td>)}</tr>)}</tbody>
                </table>
              </div>
            </section>
          )}

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button className="btn" type="button" onClick={() => setStep('map')}>Zuordnung ändern</button>
            <button className="btn btn-primary" type="button" disabled={commit.isPending || checked.validRows === 0 || checked.missingFields.length > 0}
              onClick={() => commit.mutate()}>
              {commit.isPending ? 'Wird übernommen …' : `${number(checked.validRows)} gültige Zeilen übernehmen`}<Icon name="check" />
            </button>
          </div>
        </div>
      )}

      {step === 'done' && result && (
        <div className="stack">
          <p className="alert alert-ok" role="status">
            Import abgeschlossen: <strong>{result.inserted} neu</strong>{result.updated ? <>, <strong>{result.updated} aktualisiert</strong></> : null}
            {result.rejected ? <>, {result.rejected} fehlerhafte Zeilen übersprungen</> : null}. Alle Kennzahlen sind neu berechnet.
          </p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <Link className="btn btn-primary" to="/">Zur Übersicht<Icon name="arrow" /></Link>
            <button className="btn" type="button" onClick={restart}>Weitere Datei importieren</button>
          </div>
        </div>
      )}

      <History />
    </>
  );
}

function History() {
  const client = useQueryClient();
  const batches = useQuery({ queryKey: ['imports'], queryFn: () => get<Batch[]>('/api/imports') }).data ?? [];
  const rollback = useMutation({ mutationFn: (id: string) => post(`/api/imports/${id}/rollback`), onSuccess: () => client.invalidateQueries() });
  if (batches.length === 0) return null;

  return (
    <section style={{ marginTop: 36 }} aria-labelledby="history-title">
      <p className="eyebrow">Verlauf</p>
      <h2 id="history-title" style={{ fontSize: '1.3rem', margin: '10px 0 12px' }}>Ihre Importe</h2>
      {rollback.error && <p className="alert alert-error" role="alert" style={{ marginBottom: 10 }}>{rollback.error.message}</p>}
      <div className="card table-wrap">
        <table className="data" data-testid="history">
          <thead><tr><th>Zeitpunkt</th><th>Daten</th><th>Datei</th><th className="right">Neu</th><th className="right">Aktualisiert</th><th className="right">Übersprungen</th><th>Status</th><th /></tr></thead>
          <tbody>
            {batches.map((b) => (
              <tr key={b.id}>
                <td className="num">{dateTime(b.createdAt)}</td>
                <td>{b.datasetLabel}</td>
                <td className="mono">{b.fileName}</td>
                <td className="right num">{b.inserted}</td>
                <td className="right num">{b.updated}</td>
                <td className="right num">{b.rejected}</td>
                <td><span className={`chip${b.status === 'aktiv' ? ' chip-good' : ''}`}>{b.status}</span></td>
                <td>{b.canRollback && (
                  <button className="btn btn-sm" type="button" disabled={rollback.isPending}
                    onClick={() => { if (confirm(`Import „${b.fileName}“ zurücknehmen?`)) rollback.mutate(b.id); }}>
                    <Icon name="undo" />Zurücknehmen
                  </button>
                )}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="muted" style={{ fontSize: '0.84rem', marginTop: 8 }}>
        Zurückgenommen werden kann jeweils der letzte Import – so bleiben alle Verweise (z. B. Bewegungen auf neue Artikel) stimmig.
      </p>
    </section>
  );
}
