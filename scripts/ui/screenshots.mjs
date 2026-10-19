// Drives the running system in a real browser (Playwright, Chromium) through the reviewer's scenarios
// and saves screenshots + observations to docs/screenshots. Used by .github/workflows/ui-check.yml.
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { execSync } from 'node:child_process';

const BASE = 'http://localhost:8080';
const OUT = process.argv[2] ?? 'docs/screenshots';
mkdirSync(OUT, { recursive: true });
const notes = [];
const note = (text) => { notes.push(text); console.log(text); };

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'he-IL' });
const page = await context.newPage();
page.on('pageerror', (e) => note(`PAGE ERROR: ${e.message}`));
page.on('console', (m) => m.type() === 'error' && note(`CONSOLE ERROR: ${m.text()}`));

const shot = (name, p = page) => p.screenshot({ path: `${OUT}/${name}.png` });
const rows = (p = page) => p.locator('tr.mat-mdc-row.row');
async function step(name, fn) {
  try { await fn(); } catch (e) { note(`STEP FAILED – ${name}: ${e.message.split('\n')[0]}`); await shot(`failed-${name}`).catch(() => {}); }
}

await step('list', async () => {
  await page.goto(BASE);
  await rows().first().waitFor({ timeout: 30000 });
  note(`List: ${await rows().count()} rows; paginator: ${await page.locator('.mat-mdc-paginator-range-label').innerText()}`);
  await shot('01-list');
});

await step('search-debounce', async () => {
  const listCalls = [];
  const cancelled = [];
  page.on('request', (r) => r.url().includes('/api/requests?') && listCalls.push(r.url()));
  page.on('requestfailed', (r) => r.url().includes('/api/requests') && cancelled.push(r.failure()?.errorText));
  const box = page.locator('app-request-filters input[type="search"]');
  await box.focus();
  await page.keyboard.type('היתר', { delay: 80 });
  await page.waitForTimeout(1500);
  note(`Typing "היתר" (4 keys, 80ms apart) sent ${listCalls.length} list request(s): ${listCalls.map((u) => decodeURIComponent(u.split('?')[1])).join(' | ')}`);
  note(`URL after search: ${decodeURIComponent(page.url())}`);
  // Rapid filter changes: the older request must be cancelled by switchMap.
  listCalls.length = 0;
  await page.goto(`${BASE}/?search=${encodeURIComponent('ב')}`); // slow-ish query
  await page.goto(`${BASE}/?status=Waiting`);
  await rows().first().waitFor();
  await shot('02-search');
});

await step('summary-filter-and-reload', async () => {
  await page.goto(BASE);
  await rows().first().waitFor();
  await page.locator('.summary .tile', { hasText: 'ממתינה' }).click();
  await page.waitForURL(/status=Waiting/);
  await rows().first().waitFor();
  await page.getByRole('button', { name: /next page|העמוד הבא/ }).click();
  await page.waitForURL(/page=2/);
  await rows().first().waitFor();
  const before = page.url();
  await page.reload();
  await rows().first().waitFor();
  note(`Reload keeps the view: ${before === page.url()} (${decodeURIComponent(page.url())}); range: ${await page.locator('.mat-mdc-paginator-range-label').innerText()}`);
  await shot('03-filter-page2-after-reload');
});

let conflictUrl;
await step('details', async () => {
  await page.goto(`${BASE}/?status=InProgress`);
  await rows().first().waitFor();
  await rows().first().click();
  await page.locator('app-request-details h3').waitFor();
  conflictUrl = page.url();
  note(`Details URL: ${decodeURIComponent(conflictUrl)}`);
  await shot('04-details');
});

await step('conflict-409', async () => {
  const other = await context.newPage();
  await other.goto(conflictUrl);
  await other.locator('app-request-details h3').waitFor();
  // first tab: Waiting
  await page.locator('app-request-details .buttons button', { hasText: 'ממתינה' }).click();
  await page.locator('app-request-details .entry').first().waitFor();
  await page.waitForTimeout(800);
  // second tab, still holding the old version: Completed
  await other.locator('app-request-details .buttons button', { hasText: 'הושלמה' }).click();
  await other.locator('mat-dialog-container').waitFor({ timeout: 10000 });
  note(`409 dialog: ${(await other.locator('mat-dialog-container').innerText()).replace(/\s+/g, ' ')}`);
  await other.waitForTimeout(600); // dialog open animation
  await shot('05-conflict-dialog', other);
  await other.close();
});

await step('bulk', async () => {
  await page.goto(`${BASE}/?status=InProgress`);
  await rows().first().waitFor();
  const checkboxes = page.locator('tr.row mat-checkbox input');
  for (let i = 0; i < 3; i++) { await checkboxes.nth(i).check(); }
  // Another user changes the second selected request in the meantime.
  const id = (await rows().nth(1).locator('td').nth(1).innerText()).trim();
  const etag = execSync(`curl -si ${BASE}/api/requests/${id} | grep -i '^etag:' | cut -d' ' -f2 | tr -d '\\r'`).toString().trim();
  execSync(`curl -s -o /dev/null -X PATCH ${BASE}/api/requests/${id}/status -H 'If-Match: ${etag}' -H 'X-User-Name: Other' -H 'Content-Type: application/json' -d '{"newStatus":"Waiting"}'`);
  await page.locator('app-bulk-actions-bar .mat-mdc-select-trigger').click({ force: true });
  await page.locator('mat-option', { hasText: 'הושלמה' }).click();
  await page.locator('app-bulk-actions-bar button', { hasText: 'עדכון כל הנבחרות' }).click();
  await page.locator('mat-dialog-container').waitFor({ timeout: 10000 });
  await page.waitForTimeout(600);
  const layout = await page.evaluate(() => {
    const scrolled = [...document.querySelectorAll('*')].filter((e) => e.scrollLeft !== 0)
      .map((e) => `${e.tagName.toLowerCase()}.${[...e.classList].join('.')} scrollLeft=${e.scrollLeft} scrollWidth=${e.scrollWidth} clientWidth=${e.clientWidth}`);
    const wide = [...document.querySelectorAll('app-requests-page *')].filter((e) => {
      const r = e.getBoundingClientRect(); return r.width > 0 && (r.left < -2 || r.right > window.innerWidth + 2);
    }).slice(0, 8).map((e) => { const r = e.getBoundingClientRect(); return `${e.tagName.toLowerCase()}.${[...e.classList].slice(0, 3).join('.')} [${Math.round(r.left)}..${Math.round(r.right)}]`; });
    return { scrolled, wide, docScroll: document.scrollingElement.scrollLeft };
  });
  note(`Layout check: ${JSON.stringify(layout)}`);
  note(`Bulk result: ${(await page.locator('mat-dialog-container').innerText()).replace(/\s+/g, ' ')}`);
  await shot('06-bulk-result');
  await page.keyboard.press('Escape');
});

await step('offline-banner', async () => {
  execSync('docker compose stop api', { stdio: 'ignore' });
  await page.goto(`${BASE}/?priority=High`);
  await page.locator('app-connection-banner .banner').waitFor({ timeout: 15000 });
  note(`Offline banner: ${(await page.locator('app-connection-banner').innerText()).replace(/\s+/g, ' ')}`);
  await shot('07-offline-banner');
  execSync('docker compose start api', { stdio: 'ignore' });
});

await step('empty-state', async () => {
  for (let i = 0; i < 30; i++) {
    const ok = (() => { try { execSync(`curl -fsS ${BASE}/health`, { stdio: 'ignore' }); return true; } catch { return false; } })();
    if (ok) break;
    await page.waitForTimeout(2000);
  }
  await page.goto(`${BASE}/?search=${encodeURIComponent('אין-כזה-דבר')}`);
  await page.locator('td.message', { hasText: 'לא נמצאו' }).waitFor();
  note(`Empty state: ${(await page.locator('td.message').innerText()).trim()}`);
  await shot('08-empty');
});

writeFileSync(`${OUT}/observations.md`, `# UI check (generated by scripts/ui/screenshots.mjs)\n\n${notes.map((n) => `* ${n}`).join('\n')}\n`);
await browser.close();
