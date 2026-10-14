import { useEffect, useState } from 'react';

/** Search box that reports its value 300 ms after the last keystroke instead of on every key. */
export function SearchInput({ value, onChange, placeholder, label }: { value: string; onChange: (v: string) => void; placeholder: string; label: string }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  useEffect(() => {
    if (text === value) return;
    const t = setTimeout(() => onChange(text), 300);
    return () => clearTimeout(t);
  }, [text, value, onChange]);
  return <input className="input" type="search" placeholder={placeholder} aria-label={label} value={text} onChange={(e) => setText(e.target.value)} />;
}
