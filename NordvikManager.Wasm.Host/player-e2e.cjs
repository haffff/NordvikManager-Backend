// A real player client joins a game hosted in a browser tab, through a real Central.
// node player-e2e.cjs <hostBundle> <playerBuild> <playwright> <central> <hostPort> <playerPort>
const http = require('http');
const fs = require('fs');
const path = require('path');
const [hostRoot, playerRoot, playwrightPath, central, hostPort, playerPort] = process.argv.slice(2);
const { chromium } = require(playwrightPath);
const GM = { user: 'gmtest', password: 'gm-test-pass-1' };
const PLAYER = { user: 'playertest', password: 'player-test-pass-1' };

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.dat': 'application/octet-stream' };
function serve(root, port) {
  return new Promise((resolve) => {
    const s = http.createServer((req, res) => {
      let p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
      if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
      if (p.endsWith(path.sep)) p = path.join(p, 'index.html');
      fs.readFile(p, (err, data) => {
        if (err) { res.writeHead(404); return res.end(); }
        res.writeHead(200, { 'Content-Type': mime[path.extname(p)] || 'application/octet-stream' });
        res.end(data);
      });
    });
    s.listen(Number(port), () => resolve(s));
  });
}

const step = (name, ok, detail) => { console.log(`STEP ${name}: ${ok ? 'PASS' : 'FAIL'} ${detail ?? ''}`); if (!ok) throw new Error(name); };
const ua = { 'User-Agent': 'Mozilla/5.0', 'Content-Type': 'application/json' };

async function ensurePlayerAccount() {
  const login = await (await fetch(`${central}/api/user/login`, { method: 'POST', headers: ua, body: JSON.stringify({ UserName: GM.user, password: GM.password }) })).json();
  const invite = await (await fetch(`${central}/api/user/GenerateInvite?hours=1`, { headers: { ...ua, Authorization: `Bearer ${login.accessToken}` } })).json();
  const reg = await fetch(`${central}/api/user/register`, { method: 'POST', headers: ua,
    body: JSON.stringify({ UserName: PLAYER.user, password: PLAYER.password, confirmPassword: PLAYER.password, email: 'player@example.test', inviteCode: invite.key }) });
  return reg.status;
}

(async () => {
  const servers = [await serve(hostRoot, hostPort), await serve(playerRoot, playerPort)];
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true });
  const hostLog = [], playerLog = [];
  let failed = false;
  try {
    step('player account', [201, 200, 409, 400].includes(await ensurePlayerAccount()));

    const gm = await (await browser.newContext()).newPage();
    gm.on('console', (m) => { hostLog.push(m.text()); console.log('[host]', m.text()); });
    gm.on('pageerror', (e) => console.log('[host pageerror]', e.message));
    await gm.goto(`http://localhost:${hostPort}/index.html?central=${encodeURIComponent(central)}`);
    await gm.waitForSelector('#login-submit', { timeout: 60000 });
    await gm.fill('#username', GM.user);
    await gm.fill('#password', GM.password);
    await gm.click('#login-submit');
    await gm.waitForSelector('#create-submit', { timeout: 30000 });
    await gm.fill('#game-name', 'Player join test');
    await gm.click('#create-submit');
    await gm.waitForSelector('li[data-game] .start', { timeout: 30000 });
    await gm.click('li[data-game] .start');
    await gm.waitForFunction(() => /is running|Could not start/.test(document.querySelector('#status').textContent), null, { timeout: 60000 });
    const started = await gm.textContent('#status');
    step('GM hosts the game in a tab', started.includes('is running'), started);
    const sessionId = /Central session ([0-9a-f-]{36})/.exec(started)[1];

    const player = await (await browser.newContext()).newPage();
    player.on('console', (m) => { playerLog.push(m.text()); console.log('[player]', m.text()); });
    player.on('pageerror', (e) => console.log('[player pageerror]', e.message));
    await player.goto(`http://localhost:${playerPort}/?game=${sessionId}`);
    await player.waitForSelector('input[placeholder="Username"]', { timeout: 60000 });
    await player.fill('input[placeholder="Username"]', PLAYER.user);
    await player.fill('input[placeholder="Enter password"]', PLAYER.password);
    await player.click('button:has-text("Log")');
    step('player logs in to the player client', true);

    // The player's data channel to the tab: the host logs it when it opens.
    const opened = await waitFor(() => hostLog.some((l) => /Data channel open player=/.test(l)), 60000);
    step('player connects to the tab-hosted game (data channel open)', opened, hostLog.find((l) => /Data channel open/.test(l)));

    // Let the client load the game, then see what the host answered.
    await player.waitForTimeout(15000);
    await player.screenshot({ path: path.join(path.dirname(playerRoot), 'player-joined.png') });
    // Chat: player → data channel → the tab's GameLobby/ChatHandler/database → broadcast back.
    const close = player.locator('button[aria-label="Close"], button:has-text("×")').first();
    if (await close.count()) await close.click().catch(() => {});
    const message = 'hello from a real player client ' + Date.now();
    const input = player.locator('textarea[placeholder^="Type a message"]').first();
    await input.click();
    await input.fill(message);
    await input.press('Enter');
    await player.waitForSelector(`text=${message}`, { timeout: 20000 });
    step('chat message sent and shown back from the tab-hosted game', true, message);

    // Reload: the history comes from the host's database, over a new data channel.
    await player.reload();
    const back = await player.waitForSelector(`text=${message}`, { timeout: 60000 }).then(() => true, () => false);
    step('after a reload the player reconnects and the chat history comes back', back);
    await player.screenshot({ path: path.join(path.dirname(playerRoot), 'player-chat.png') });

    // The GM reloads the host page: the game and its chat come back from this browser's storage.
    await gm.waitForTimeout(3000); // past the periodic save
    await gm.reload();
    await gm.waitForSelector('#login-submit', { timeout: 60000 });
    await gm.fill('#username', GM.user);
    await gm.fill('#password', GM.password);
    await gm.click('#login-submit');
    await gm.waitForSelector('li[data-game] .start', { timeout: 30000 });
    const listed = await gm.$$eval('li[data-game] .name', (names) => names.map((n) => n.textContent));
    step('after the GM reloads, the game is still there', listed.includes('Player join test'), listed.join(', '));
    await gm.click('li[data-game] .start');
    await gm.waitForFunction(() => /is running|Could not start/.test(document.querySelector('#status').textContent), null, { timeout: 60000 });
    const restarted = await gm.textContent('#status');
    step('the game starts again on the same Central session', restarted.includes(sessionId), restarted);

    await player.reload();
    const kept = await player.waitForSelector(`text=${message}`, { timeout: 90000 }).then(() => true, () => false);
    step('the player rejoins and the chat from before the GM reload is there', kept);

    const notAnswered = hostLog.filter((l) => /Not available in this host|only available on a GM server|→ (4|5)\d\d|returning (4|5)\d\d/.test(l));
    console.log('HOST NON-2xx ANSWERS:\n' + (notAnswered.join('\n') || '(none)'));
    const errors = playerLog.filter((l) => /error|failed|501|500/i.test(l));
    console.log('PLAYER CONSOLE ERRORS:\n' + (errors.slice(0, 40).join('\n') || '(none)'));
  } catch (e) {
    failed = true;
    console.log('RESULT: FAIL ' + e.message);
  }
  if (!failed) console.log('RESULT: PASS');
  await browser.close();
  servers.forEach((s) => s.close());
  process.exit(failed ? 1 : 0);
})();

async function waitFor(check, ms) {
  const until = Date.now() + ms;
  while (Date.now() < until) {
    if (check()) return true;
    await new Promise((r) => setTimeout(r, 250));
  }
  return false;
}
