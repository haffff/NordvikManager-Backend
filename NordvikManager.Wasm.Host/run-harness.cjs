const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({
    executablePath: 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    headless: true,
    args: ['--disable-background-networking', '--no-sandbox'],
  });
  const page = await browser.newPage();
  page.on('console', (msg) => console.log('[page]', msg.text()));
  page.on('pageerror', (err) => console.error('[pageerror]', err));

  const donePromise = new Promise((resolve) => {
    page.on('request', (req) => {
      if (req.url().includes('/__done')) {
        const url = new URL(req.url());
        resolve({ ok: url.searchParams.get('ok'), message: url.searchParams.get('message') });
      }
    });
  });

  await page.goto('http://localhost:8098/index.html');

  const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error('timeout after 105s')), 105000));

  try {
    const result = await Promise.race([donePromise, timeout]);
    console.log('RESULT:', JSON.stringify(result));
  } catch (e) {
    console.error('FAILED:', e.message);
  } finally {
    await browser.close();
  }
})();
