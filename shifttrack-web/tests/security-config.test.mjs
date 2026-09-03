import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

test('Static Web Apps config sends browser security headers', async () => {
  const raw = await readFile(new URL('../staticwebapp.config.json', import.meta.url), 'utf8');
  const config = JSON.parse(raw);

  assert.equal(config.globalHeaders['X-Frame-Options'], 'DENY');
  assert.match(config.globalHeaders['Content-Security-Policy'], /frame-ancestors 'none'/);
  assert.match(config.globalHeaders['Content-Security-Policy'], /default-src 'self'/);
});

test('production frontend sources do not hardcode localhost API base', async () => {
  const constants = await readFile(new URL('../src/lib/constants.ts', import.meta.url), 'utf8');
  const api = await readFile(new URL('../src/lib/api.ts', import.meta.url), 'utf8');

  assert.doesNotMatch(constants, /http:\/\/localhost:5243/);
  assert.doesNotMatch(api, /http:\/\/localhost:5243/);
  assert.match(constants, /import\.meta\.env\.DEV/);
});

test('Entra callback does not reflect raw Microsoft error details', async () => {
  const callback = await readFile(new URL('../src/pages/EntraCallbackPage.tsx', import.meta.url), 'utf8');

  assert.match(callback, /Microsoft login failed\. Please try again\./);
  assert.doesNotMatch(callback, /Microsoft login failed: \$\{error\}/);
});

test('login flows do not consume JWT from auth response bodies', async () => {
  const login = await readFile(new URL('../src/pages/LoginPage.tsx', import.meta.url), 'utf8');
  const callback = await readFile(new URL('../src/pages/EntraCallbackPage.tsx', import.meta.url), 'utf8');
  const app = await readFile(new URL('../src/App.tsx', import.meta.url), 'utf8');

  assert.doesNotMatch(login, /data\.token/);
  assert.doesNotMatch(callback, /data\.token/);
  assert.doesNotMatch(app, /data\.token/);
});
