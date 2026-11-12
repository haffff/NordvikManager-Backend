// The browser host's database on OPFS survives being cut off mid-commit: a writer
// (?selfcheck=crash-write) commits in a loop and is stopped while it does, then a checker
// (?selfcheck=crash-check) finds the file intact (integrity_check ok, every row whole). Twice:
// the worker terminated outright (nothing of it runs after), and the whole browser closed.
// node storage-e2e.cjs <AppBundle> <playwright> <profile folder (made, then deleted)>
const http = require('http');
const fs = require('fs');
const path = require('path');
const [root, playwrightPath, profile] = process.argv.slice(2);
const { chromium } = require(playwrightPath);

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

const step = (name, ok, detail) => { console.log(`STEP ${name}: ${ok ? 'PASS' : 'FAIL'} ${detail ?? ''}`); if (!ok) throw new Error(name); };
const browserOptions = { executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true };

// Opens the page, resolves with the first console line matching `until` (or null after `ms`).
async function open(context, url, until, ms) {
  const page = await context.newPage();
  const seen = [];
  const line = new Promise((resolve) => page.on('console', (m) => {
    seen.push(m.text());
    if (until.test(m.text())) resolve(m.text());
  }));
  await page.goto(url);
  return { page, seen, line: await Promise.race([line, new Promise((r) => setTimeout(() => r(null), ms))]) };
}

// The writer prints "WRITING n commits" after its nth commit; the checker "... N rows ...".
const committed = (lines) => Math.max(0, ...lines.map((l) => Number(/^WRITING (\d+) /.exec(l)?.[1] ?? 0)));
const rows = (line) => Number(/ (\d+) rows/.exec(line ?? '')?.[1] ?? -1);

server.listen(0, async () => {
  const url = `http://localhost:${server.address().port}/index.html`;
  fs.rmSync(profile, { recursive: true, force: true });
  let failed = false;
  try {
    const browser = await chromium.launchPersistentContext(profile, browserOptions);
    const first = await open(browser, `${url}?selfcheck=crash-write`, /^WRITING /, 120000);
    step('the writer commits in a loop', !!first.line, first.line);
    await new Promise((r) => setTimeout(r, 1500));
    await first.page.evaluate(() => globalThis.nordvikHostWorker.terminate());
    await first.page.close();
    const afterTerminate = await open(browser, `${url}?selfcheck=crash-check`, /^INTEGRITY /, 120000);
    step('intact after the worker was terminated mid-commit', /^INTEGRITY ok /.test(afterTerminate.line ?? '') && !/HOT MAGIC/.test(afterTerminate.line), afterTerminate.line ?? afterTerminate.seen.slice(-5).join(' | '));
    step('no commit the writer reported was lost', rows(afterTerminate.line) >= committed(first.seen), `${rows(afterTerminate.line)} rows, ${committed(first.seen)} reported`);
    await afterTerminate.page.close();
    await browser.close();

    const writer = await chromium.launchPersistentContext(profile, browserOptions);
    const { line: writing, seen: writerSeen } = await open(writer, `${url}?selfcheck=crash-write`, /^WRITING /, 120000);
    step('the writer commits in a loop again', !!writing, writing);
    await new Promise((r) => setTimeout(r, 1500)); // well into the loop
    const reported = committed(writerSeen); // before the close: whatever it prints after can't count
    await writer.close(); // the tab, its worker and any commit in flight: gone
    step('the browser was closed mid-write', true);

    const checker = await chromium.launchPersistentContext(profile, browserOptions);
    const { line: verdict, seen } = await open(checker, `${url}?selfcheck=crash-check`, /^INTEGRITY /, 120000);
    step('intact after the browser was closed mid-commit', /^INTEGRITY ok /.test(verdict ?? '') && !/HOT MAGIC/.test(verdict), verdict ?? seen.slice(-5).join(' | '));
    step('no commit the writer reported was lost (browser closed)', rows(verdict) >= reported, `${rows(verdict)} rows, ${reported} reported`);
    await checker.close();
  } catch (e) {
    failed = true;
    console.log('RESULT: FAIL ' + e.message);
  }
  if (!failed) console.log('RESULT: PASS');
  fs.rmSync(profile, { recursive: true, force: true });
  server.close();
  process.exit(failed ? 1 : 0);
});
