const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, 'bin', 'Release', 'net10.0', 'browser-wasm', 'AppBundle');
const resultFile = path.join(__dirname, 'harness-result.json');

const mime = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.mjs': 'text/javascript',
  '.wasm': 'application/wasm',
  '.json': 'application/json',
  '.symbols': 'application/octet-stream',
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');

  if (url.pathname === '/__done') {
    const ok = url.searchParams.get('ok');
    const message = url.searchParams.get('message');
    fs.writeFileSync(resultFile, JSON.stringify({ ok: ok === 'true', message }, null, 2));
    console.log('[serve] harness report received:', ok, message);
    res.writeHead(200);
    res.end('ok');
    return;
  }

  let filePath = path.join(root, url.pathname === '/' ? 'index.html' : url.pathname);
  if (!filePath.startsWith(root)) {
    res.writeHead(403);
    res.end();
    return;
  }

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end('not found: ' + url.pathname);
      return;
    }
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': mime[ext] || 'application/octet-stream' });
    res.end(data);
  });
});

const port = process.argv[2] || 8098;
server.listen(port, () => console.log(`[serve] listening on http://localhost:${port}`));
