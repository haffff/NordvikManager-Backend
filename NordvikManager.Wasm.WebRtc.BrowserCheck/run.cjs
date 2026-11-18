// Serves the check's bundle, opens it in headless Chrome and prints its console until RESULT.
// node run.cjs <AppBundle folder> [playwright module path]; CHROME_PATH overrides the browser.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require(process.argv[3] || 'playwright');

const root = process.argv[2];
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.dat': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.readFile(p.endsWith(path.sep) ? path.join(p, 'index.html') : p, (err, data) => {
    if (err) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { 'Content-Type': mime[path.extname(p)] || 'application/octet-stream' });
    res.end(data);
  });
});

server.listen(0, async () => {
  const port = server.address().port;
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await browser.newPage();
  let done;
  const result = new Promise((r) => (done = r));
  page.on('console', (m) => {
    console.log('[page]', m.text());
    if (m.text().startsWith('RESULT:')) done(m.text());
    // A candidate the browser refused (e.g. added before the offer was set) is a failure even if ICE recovers.
    if (m.text().includes('addIceCandidate failed')) done('RESULT: FAIL ' + m.text());
  });
  page.on('pageerror', (e) => { console.log('[pageerror]', e.message); });
  await page.goto(`http://localhost:${port}/index.html`);
  const outcome = await Promise.race([result, new Promise((r) => setTimeout(() => r('RESULT: FAIL timeout'), 60000))]);
  console.log(outcome);
  await browser.close();
  server.close();
  process.exit(outcome.startsWith('RESULT: PASS') ? 0 : 1);
});
