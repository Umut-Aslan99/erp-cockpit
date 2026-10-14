import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChartCard } from './ChartCard';

describe('ChartCard', () => {
  it('switches between the chart and its accessible table view', async () => {
    render(
      <ChartCard title="Liefertreue" table={<table><tbody><tr><td>L-170</td><td>64,6 %</td></tr></tbody></table>}>
        <div data-testid="chart">chart</div>
      </ChartCard>,
    );
    expect(screen.getByTestId('chart')).toBeTruthy();
    expect(screen.queryByText('L-170')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Als Tabelle anzeigen' }));
    expect(screen.getByText('L-170')).toBeTruthy();
    expect(screen.queryByTestId('chart')).toBeNull();
    expect(screen.getByRole('button', { name: 'Als Diagramm anzeigen' }).getAttribute('aria-pressed')).toBe('true');
  });
});
