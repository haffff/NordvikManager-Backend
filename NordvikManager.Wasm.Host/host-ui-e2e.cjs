// The frontend's host build (REACT_APP_MODE=host: the GM app with this browser host in its tab)
// against a real Central, with a real player client joining:
//   the GM logs in, the lobby has no server-only settings, the host drawer lists what to expect,
//   creates a game, enters it (the GM app's own data channel to the host in the same tab), chats;
//   a player joins from the player client and the two chat; leaving the tab while the game runs
//   asks first; after the reload the GM logs in again and the game is there.
// node host-ui-e2e.cjs <host build> <player build> <playwright> <central> <hostPort> <playerPort>
//   host build: `pnpm buildci_host` with NORDVIK_WASM_BUNDLE (this bundle) and REACT_APP_CENTRAL_URL,
//   served here at /client/host/ (as Central serves it); Central's CORS_ORIGIN lists both ports.
const http = require('http');
const fs = require('fs');
const path = require('path');
const [hostRoot, playerRoot, playwrightPath, central, hostPort, playerPort] = process.argv.slice(2);
const { chromium } = require(playwrightPath);
const GM = { user: 'gmtest', password: 'gm-test-pass-1' };
const PLAYER = { user: 'playertest', password: 'player-test-pass-1' };

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.dat': 'application/octet-stream' };
function serve(root, port, prefix = '/') {
  return new Promise((resolve) => {
    const s = http.createServer((req, res) => {
      const url = decodeURIComponent(new URL(req.url, 'http://x').pathname);
      if (!url.startsWith(prefix)) { res.writeHead(404); return res.end(); }
      let p = path.join(root, url.slice(prefix.length));
      if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
      if (fs.existsSync(p) && fs.statSync(p).isDirectory()) p = path.join(p, 'index.html');
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

async function chat(page, message) {
  const input = page.locator('textarea[placeholder^="Type a message"]').first();
  await input.click();
  await input.fill(message);
  await input.press('Enter');
}

async function gmLogin(gm, { lobby = true } = {}) {
  await gm.waitForSelector('input[placeholder="Login"]', { timeout: 120000 });
  await gm.fill('input[placeholder="Login"]', GM.user);
  await gm.fill('input[type="password"]', GM.password);
  await gm.click('button:has-text("Login")');
  if (lobby) await gm.waitForSelector(`text=Hello, ${GM.user}`, { timeout: 30000 });
}

(async () => {
  const servers = [await serve(hostRoot, hostPort, '/client/host/'), await serve(playerRoot, playerPort)];
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', headless: true });
  const shots = process.env.E2E_SHOTS;
  const hostLog = [], playerLog = [];
  let player;
  let failed = false;
  try {
    step('player account', [201, 200, 409, 400].includes(await ensurePlayerAccount()));

    const gm = await (await browser.newContext({ viewport: { width: 1400, height: 900 } })).newPage();
    gm.on('console', (m) => hostLog.push(m.text()));
    gm.on('pageerror', (e) => console.log('[gm pageerror]', e.message));
    await gm.goto(`http://localhost:${hostPort}/client/host/`);
    await gmLogin(gm);
    step('the GM logs in to the host build (the host in this tab holds the Central login)', true);

    // Once the host says what it is (after login): the startup dialog, until "Don't remind me again".
    const intro = await gm.waitForSelector("text=You're hosting from your browser", { timeout: 15000 }).then(() => true, () => false);
    const cards = await gm.$$('[data-testid="host-intro-card"]');
    const gmServerList = !!(await gm.$('text=Only on the local server'));
    step('the startup dialog: a warning, six cards, the rest below, three choices', intro && cards.length === 6 && gmServerList
      && !!(await gm.$('button:has-text("I will download local server")')) && !!(await gm.$('button:has-text("Don\'t remind me again")')), `${cards.length} cards`);
    if (shots) await gm.screenshot({ path: path.join(shots, 'host-intro.png') });
    await gm.click('button:has-text("Continue with browser version")');
    await gm.waitForSelector("text=You're hosting from your browser", { state: 'detached', timeout: 10000 });

    step('the lobby has no server-only settings (backups, server settings)', !(await gm.$('button:has-text("Application Settings")')));
    await gm.click('button[aria-label^="Game host"]');
    const drawer = await gm.waitForSelector('text=/Storage: .* used of/', { timeout: 10000 }).then(() => true, () => false);
    step('the host button (lobby toolbar) opens the drawer: what runs, the storage used', drawer);
    if (shots) await gm.screenshot({ path: path.join(shots, 'host-drawer.png') });
    await gm.click('[role="tab"]:has-text("Logs")');
    const logText = await gm.waitForSelector('text=GM host ready', { timeout: 10000 }).then(() => gm.textContent('[role="tabpanel"]:visible'), () => '');
    step('the drawer shows the host log, without the SQL', /GM host ready/.test(logText) && !/SELECT "/.test(logText), `${logText.length} chars`);
    if (shots) await gm.screenshot({ path: path.join(shots, 'host-logs.png') });
    await gm.keyboard.press('Escape');
    await gm.waitForTimeout(500);

    // Headless Chrome grants no persistent storage: the GM is told the games may be deleted.
    const banner = await gm.textContent('[role="alert"]').catch(() => '');
    const iconLabel = await gm.getAttribute('button[aria-label^="Game host"]', 'aria-label');
    step('without persistent storage: a warning banner, and a warning on the host icon', /may delete your games/.test(banner) && /storage isn't persistent/.test(iconLabel), iconLabel);
    if (shots) await gm.screenshot({ path: path.join(shots, 'host-storage-warning.png') });
    await gm.click('[role="alert"] button:has-text("Dismiss")');

    const name = 'Host build game ' + Date.now().toString(36);
    await gm.click('button:has-text("Create new game")');
    await gm.fill('input[placeholder="My epic adventure"]', name);
    await gm.click('button:has-text("Create game")');
    await gm.waitForSelector(`text=${name}`, { timeout: 30000 });
    step('a game is created from the GM lobby', true, name);

    // Addons: the host's registry is its Central (browse, readme, and the package Central downloads).
    const addons = await gm.evaluate(async (gameName) => {
      const api = async (path, init) => {
        const r = await fetch(`/__nordvik-host__/api/${path}`, init);
        return { status: r.status, body: await r.json().catch(() => null) };
      };
      const game = (await api('gamelist/getgames')).body.find((g) => g.name === gameName);
      const q = `gameid=${game.id}`;
      const browse = await api(`addon/repository?${q}`);
      const readme = await api(`addon/readme?key=poll&${q}`);
      const install = await api(`addon/install?${q}`, { method: 'POST', body: JSON.stringify({ key: 'poll' }) });
      let installed = [];
      for (let i = 0; i < 40 && !installed.includes('poll'); i++) {
        await new Promise((r) => setTimeout(r, 500));
        installed = ((await api(`addon/installed?${q}`)).body ?? []).map((a) => a.key);
      }
      return { browse: browse.status === 200 ? browse.body.map((a) => a.key) : browse, readme: readme.status, install: install.status, installed };
    }, name);
    step('the addons panel browses Central\'s registry (with readmes)', Array.isArray(addons.browse) && addons.browse.includes('poll') && addons.readme === 200, JSON.stringify(addons.browse));
    step('a registry addon installs, downloaded through Central', addons.install === 200 && addons.installed.includes('poll'), addons.installed.join(', '));

    await gm.click(`text=${name}`);
    await gm.waitForSelector('textarea[placeholder^="Type a message"]', { timeout: 60000 });
    const ownChannel = await waitFor(() => hostLog.some((l) => /Data channel open player=/.test(l)), 60000);
    step("the GM enters it: the GM app's data channel to the host in its own tab", ownChannel, hostLog.find((l) => /Data channel open/.test(l)));
    const inToolbar = await gm.$('.nm_toolbar button[aria-label^="Game host"]');
    step('in game the host button sits in the main toolbar', !!inToolbar);
    const sessionId = (await gm.textContent('text=/Session ID: /')).replace(/.*Session ID:\s*/, '').trim();
    const gmMessage = 'hello from the host tab ' + Date.now();
    await chat(gm, gmMessage);
    await gm.waitForSelector(`text=${gmMessage}`, { timeout: 20000 });
    step('the GM chats in the game', true, `Central session ${sessionId}`);

    // The GM app's Central token comes from its backend, here the host, which holds the Central
    // login. Central rotates the refresh token on each use: a second refresh works only if the
    // host kept the new one.
    const refreshes = await gm.evaluate(async () => {
      const once = async () => {
        const r = await fetch('/__nordvik-host__/api/user/refresh-central', { method: 'POST' });
        return { status: r.status, token: !!(await r.json().catch(() => null))?.accessToken };
      };
      return [await once(), await once()];
    });
    step('the Central token refreshes through the host, twice (it keeps the rotated one)', refreshes.every((r) => r.status === 200 && r.token), JSON.stringify(refreshes));
    if (shots) await gm.screenshot({ path: path.join(shots, 'host-ingame.png') });

    player = await (await browser.newContext({ viewport: { width: 1280, height: 800 } })).newPage();
    player.on('console', (m) => playerLog.push(m.text()));
    await player.goto(`http://localhost:${playerPort}/?game=${sessionId}`);
    await player.waitForSelector('input[placeholder="Username"]', { timeout: 60000 });
    await player.fill('input[placeholder="Username"]', PLAYER.user);
    await player.fill('input[placeholder="Enter password"]', PLAYER.password);
    await player.click('button:has-text("Log")');
    const seen = await player.waitForSelector(`text=${gmMessage}`, { timeout: 90000 }).then(() => true, () => false);
    step("a player joins from the player client and sees the GM's message", seen);
    const playerMessage = 'hello from the player ' + Date.now();
    await chat(player, playerMessage);
    const heard = await gm.waitForSelector(`text=${playerMessage}`, { timeout: 30000 }).then(() => true, () => false);
    step("the GM sees the player's message", heard, playerMessage);

    // The tab is the game's server: leaving it while the game runs asks first.
    let asked = false;
    gm.once('dialog', async (d) => { asked = d.type() === 'beforeunload'; await d.accept(); });
    await gm.reload({ waitUntil: 'commit' }).catch(() => {});
    await waitFor(() => asked, 5000);
    step('leaving the tab while the game runs asks first', asked);

    // As the native GM app: the address still names the game, so logging in again re-enters it.
    await gmLogin(gm, { lobby: false });
    const back = await gm.waitForSelector(`text=Session ID: ${sessionId}`, { timeout: 90000 }).then(() => true, () => false);
    step('after the reload the GM logs in again and is back in the game, on the same Central session', back);
    const history = await gm.waitForSelector(`text=${playerMessage}`, { timeout: 30000 }).then(() => true, () => false);
    step('the chat from before the reload is there (OPFS)', history);
    const rejoined = await player.waitForSelector(`text=${gmMessage}`, { timeout: 90000 }).then(() => true, () => false);
    step('the player is still in the game after the GM reload', rejoined);
  } catch (e) {
    failed = true;
    console.log('RESULT: FAIL ' + e.message);
    console.log('HOST:\n' + hostLog.filter((l) => /error|fail|Data channel|state →/i.test(l)).slice(-20).join('\n'));
    console.log('PLAYER:\n' + playerLog.filter((l) => /error|fail|WebRTC|channel|signal/i.test(l)).slice(-25).join('\n'));
    if (shots && player) await player.screenshot({ path: path.join(shots, 'player-fail.png') }).catch(() => {});
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
