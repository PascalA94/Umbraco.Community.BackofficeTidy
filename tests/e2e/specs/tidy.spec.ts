import { expect, test, type Page } from '@playwright/test';
import { env, login } from '../support/backoffice';
import { consoleGuard } from '../support/console-guard';

// The demo site's BackofficeTidy rules (demo appsettings.json): Welcome for everyone, the search header app for the
// writer group, and a deliberate typo alias. The Copilot preset only applies in Production.
const WELCOME = 'Umb.Dashboard.UmbracoNews';
const SEARCH = 'Umb.HeaderApp.Search';
const TYPO = 'Umb.Dashbaord.Typo';

/** Signs in and returns the aliases the server answered with, plus the BackofficeTidy console warnings. */
async function signIn(page: Page, email: string, password: string) {
  const warnings: string[] = [];
  page.on('console', (m) => {
    if (m.type() === 'warning' && m.text().startsWith('BackofficeTidy')) warnings.push(m.text());
  });
  const hidden = page.waitForResponse((r) => r.url().includes('/backoffice-tidy/hidden'));
  await login(page, email, password);
  const response = await hidden;
  expect(response.status()).toBe(200);
  expect(response.headers()['cache-control']).toContain('no-store');
  const body = (await response.json()) as { aliases: string[] };
  // The header is rendered once the current user's profile app is; only then is "absent" meaningful.
  const headerApps = page.locator('umb-backoffice-header-apps');
  await expect(headerApps.locator('[label^="User profile"]').filter({ visible: true })).toHaveCount(1);
  return { aliases: body.aliases, warnings, headerApps };
}

test('writer: Welcome and the search header app are hidden, and the typo warns once', async ({ page }) => {
  const guard = consoleGuard(page);
  const { aliases, warnings, headerApps } = await signIn(page, 'writer@example.test', env.writerPassword);

  expect(aliases).toEqual(expect.arrayContaining([WELCOME, SEARCH, TYPO]));
  await expect(headerApps.locator('[label="search"]').filter({ visible: true })).toHaveCount(0);
  await expect(page.getByRole('tab', { name: 'Welcome' })).toHaveCount(0);
  expect(warnings.filter((w) => w.includes(TYPO))).toHaveLength(1);
  expect(warnings).toHaveLength(1);
  guard.assertNoErrors();
});

test('editor: the search header app is still there, Welcome is hidden, and the response never lists the writer rule', async ({ page }) => {
  const guard = consoleGuard(page);
  const { aliases, warnings, headerApps } = await signIn(page, 'editor@example.test', env.editorPassword);

  expect(aliases).toContain(WELCOME);
  expect(aliases).not.toContain(SEARCH);
  await expect(headerApps.locator('[label="search"]').filter({ visible: true })).toHaveCount(1);
  await expect(page.getByRole('tab', { name: 'Welcome' })).toHaveCount(0);
  expect(warnings).toHaveLength(1);
  guard.assertNoErrors();
});

// Positive control for the two absence checks above: with the rules request blocked nothing is hidden, so the same
// locators must find the Welcome tab and the search header app. Without this, "absent" could be a wrong locator.
// No console guard: the browser logs the deliberately aborted request as an error.
test('control: when the rules cannot load, Welcome and the search header app stay visible', async ({ page }) => {
  await page.route('**/backoffice-tidy/hidden', (route) => route.abort());
  await login(page, 'writer@example.test', env.writerPassword);
  const headerApps = page.locator('umb-backoffice-header-apps');
  await expect(headerApps.locator('[label^="User profile"]').filter({ visible: true })).toHaveCount(1);
  await expect(headerApps.locator('[label="search"]').filter({ visible: true })).toHaveCount(1);
  await expect(page.getByRole('tab', { name: 'Welcome' })).toHaveCount(1);
});
