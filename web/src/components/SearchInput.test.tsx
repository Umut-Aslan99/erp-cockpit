import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { SearchInput } from './SearchInput';

describe('SearchInput', () => {
  it('reports the value once, 300 ms after the last keystroke', () => {
    vi.useFakeTimers();
    const onChange = vi.fn();
    render(<SearchInput value="" onChange={onChange} placeholder="Suchen" label="Suchen" />);
    const input = screen.getByLabelText('Suchen');

    for (const text of ['D', 'Di', 'Dic', 'Dichtung']) fireEvent.change(input, { target: { value: text } });
    act(() => { vi.advanceTimersByTime(299); });
    expect(onChange).not.toHaveBeenCalled();
    act(() => { vi.advanceTimersByTime(1); });
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('Dichtung');
    vi.useRealTimers();
  });
});
