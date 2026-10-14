import { expect, test } from '@playwright/test';

test('no third-party requests, cookie is HttpOnly and strict', async ({ page, context, baseURL }) => {
  const foreign: string[] = [];
  page.on('request', (r) => { if (!r.url().startsWith(baseURL!) && !r.url().startsWith('data:')) foreign.push(r.url()); });
  await page.goto('/', { waitUntil: 'networkidle' });
  expect(foreign).toEqual([]);
  const cookies = await context.cookies();
  expect(cookies.map((c) => c.name)).toEqual(['erp_ws']);
  expect(cookies[0]!.httpOnly).toBe(true);
  expect(cookies[0]!.sameSite).toBe('Strict');
});

test('no horizontal scrolling on any page', async ({ page }) => {
  for (const path of ['/', '/lager', '/auftraege', '/lieferanten', '/import']) {
    await page.goto(path, { waitUntil: 'networkidle' });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth);
    expect(overflow, path).toBeLessThanOrEqual(0);
  }
});

test('theme toggle switches and survives a reload', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/');
  await page.getByRole('button', { name: 'Helles Design' }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});
