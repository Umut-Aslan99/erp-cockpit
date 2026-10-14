import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';

test('dashboard shows key figures and drills down into the overdue orders', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Was steckt in den ERP-Daten?');
  for (const label of ['Lagerwert', 'Lagerumschlag', 'Ladenhüter', 'Rückstand überfällig', 'Liefertreue']) {
    await expect(page.locator('.tile').filter({ hasText: label })).toBeVisible();
  }
  await expect(page.locator('.chart-card')).toHaveCount(4);
  await expect(page.locator('.chart-card svg.recharts-surface')).toHaveCount(4);

  await page.locator('.tile').filter({ hasText: 'Rückstand überfällig' }).click();
  await expect(page).toHaveURL(/auftraege\?view=overdue/);
  const rows = page.locator('table.data tbody tr');
  await expect(rows.first()).toContainText('überfällig');
  for (const status of await page.locator('table.data tbody td:last-child').allInnerTexts()) expect(status).toContain('überfällig');
});

test('every chart has a table view with the same numbers', async ({ page }) => {
  await page.goto('/');
  const card = page.locator('.chart-card').filter({ hasText: 'Liefertreue je Lieferant' });
  await card.getByRole('button', { name: 'Als Tabelle anzeigen' }).click();
  await expect(card.locator('table')).toContainText('Sensorik Arndt GmbH');
});

test('importing an old ERP export: mapping, error report, commit and rollback', async ({ page }) => {
  await page.goto('/import');
  await page.getByTestId('try-Lagerbewegungen_ERP-Export.csv').click();

  // Step 2: columns recognised automatically.
  await expect(page.getByRole('heading', { name: '2 · Welche Spalte gehört wohin?' })).toBeVisible();
  await expect(page.getByText('Windows-1252')).toBeVisible();
  await expect(page.getByLabel('Spalte für Artikelnummer')).toHaveValue('1');
  await expect(page.getByLabel('Spalte für Buchungsdatum')).toHaveValue('0');
  await page.getByRole('button', { name: 'Zeilen prüfen' }).click();

  // Step 3: four broken rows are explained, 36 can be imported.
  await expect(page.locator('.tile').filter({ hasText: 'Fehlerhaft' })).toContainText('4');
  const errors = page.getByTestId('errors');
  await expect(errors).toContainText('A-99999');
  await expect(errors).toContainText('zwölf');
  await expect(errors).toContainText('Umlagerung');
  await page.getByRole('button', { name: '36 gültige Zeilen übernehmen' }).click();

  // Step 4 + history with rollback.
  await expect(page.getByRole('status')).toContainText('36 neu');
  const history = page.getByTestId('history');
  await expect(history).toContainText('Lagerbewegungen_ERP-Export.csv');
  page.once('dialog', (d) => d.accept());
  await history.getByRole('button', { name: 'Zurücknehmen' }).click();
  await expect(history).toContainText('zurückgenommen');
});

test('a visitor\'s import is invisible to other visitors', async ({ browser }) => {
  const alice = await (await browser.newContext()).newPage();
  const bob = await (await browser.newContext()).newPage();
  await alice.goto('/import');
  await alice.getByTestId('try-Artikelstamm_Neuanlage.xlsx').click();
  await alice.getByRole('button', { name: 'Zeilen prüfen' }).click();
  await alice.getByRole('button', { name: /gültige Zeilen übernehmen/ }).click();
  await expect(alice.getByRole('status')).toContainText('5 neu');

  const groupOptions = async (p: typeof alice) => { await p.goto('/lager'); await p.getByLabel('Warengruppe').locator('option').nth(1).waitFor({ state: 'attached' }); return p.getByLabel('Warengruppe').locator('option').allInnerTexts(); };
  expect(await groupOptions(alice)).toContain('Werkzeuge');
  expect(await groupOptions(bob)).not.toContain('Werkzeuge');
});

test('stock table: filter, open an article, export to Excel', async ({ page }) => {
  await page.goto('/lager?view=slow');
  await expect(page.locator('table.data tbody tr').first()).toContainText('Ladenhüter');

  const download = page.waitForEvent('download');
  await page.getByRole('link', { name: 'Excel' }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^Lagerbestand_.*\.xlsx$/);
  expect(readFileSync(await file.path()).subarray(0, 2).toString()).toBe('PK'); // xlsx = zip

  await page.locator('table.data tbody tr').first().click();
  await expect(page).toHaveURL(/\/lager\/A-/);
  await expect(page.locator('.tile').filter({ hasText: 'Letzter Abgang' })).toContainText('Ladenhüter');
  await expect(page.locator('table.data tbody tr').first()).toBeVisible();
});

test('supplier drill-down from the chart table to its purchase lines', async ({ page }) => {
  await page.goto('/lieferanten?supplier=L-170');
  await expect(page.getByRole('heading', { name: /Bestellpositionen · Sensorik Arndt GmbH/ })).toBeVisible();
  for (const supplier of await page.locator('table.data').last().locator('tbody td:nth-child(2)').allInnerTexts()) {
    expect(supplier).toBe('Sensorik Arndt GmbH');
  }
});
