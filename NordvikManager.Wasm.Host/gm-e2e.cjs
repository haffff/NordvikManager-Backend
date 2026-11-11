// Drives the browser host's GM page against a real Central: log in, create a game, start its
// session, stop it. node gm-e2e.cjs <AppBundle> <playwright> <port> <centralUrl> <user> <password>
const http = require('http');
const fs = require('fs');
const path = require('path');
const [root, playwrightPath, port, central, user, password] = process.argv.slice(2);
const { chromium } = require(playwrightPath);

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.dat': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.readFile(p.endsWith(path.sep) ? path.join(p, 'index.html') : p, (err, data) => {
    if (err) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { 'Content-Type': mime[path.extname(p)] || 'application/octet-stream' });
    res.end(data);
  });
});

const step = (name, ok, detail) => { console.log(`STEP ${name}: ${ok ? 'PASS' : 'FAIL'} ${detail ?? ''}`); if (!ok) throw new Error(name); };

server.listen(Number(port), async () => {
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await browser.newPage();
  page.on('console', (m) => console.log('[page]', m.text()));
  page.on('pageerror', (e) => console.log('[pageerror]', e.message));
  let failed = false;
  try {
    await page.goto(`http://localhost:${port}/index.html?central=${encodeURIComponent(central)}`);
    await page.waitForSelector('#login-submit', { timeout: 60000 });
    step('page ready', true, await page.textContent('#central'));

    await page.fill('#username', user);
    await page.fill('#password', password);
    await page.click('#login-submit');
    await page.waitForFunction(() => /Logged in|Wrong/.test(document.querySelector('#status').textContent), null, { timeout: 30000 });
    const login = await page.textContent('#status');
    step('login', login.startsWith('Logged in'), login);

    await page.waitForSelector('#create-submit', { timeout: 30000 });
    const basics = await page.$eval('#create input[name="addon"][value="basics"]', (box) => box.checked).catch(() => null);
    step('Basics offered, ticked, for the new game', basics === true, String(basics));
    await page.fill('#game-name', 'E2E tab game');
    await page.click('#create-submit');
    await page.waitForSelector('li[data-game] .start', { timeout: 30000 });
    step('game created and listed', true, await page.textContent('#status'));

    await page.click('li[data-game] .start');
    await page.waitForFunction(() => /is running|Could not start/.test(document.querySelector('#status').textContent), null, { timeout: 60000 });
    const started = await page.textContent('#status');
    step('session started (signaling authenticated)', started.includes('is running'), started);

    await page.click('li[data-game] .stop');
    await page.waitForFunction(() => /stopped|Could not stop/.test(document.querySelector('#status').textContent), null, { timeout: 30000 });
    const stopped = await page.textContent('#status');
    step('session stopped', stopped.includes('stopped'), stopped);
  } catch (e) {
    failed = true;
    console.log('RESULT: FAIL ' + e.message);
  }
  if (!failed) console.log('RESULT: PASS');
  await browser.close();
  server.close();
  process.exit(failed ? 1 : 0);
});
