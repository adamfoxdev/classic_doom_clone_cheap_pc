// The browser version's checks (CI runs them on every push): the page loads and draws the game, the keys drive it,
// and a scripted two-player session comes out the same as on the desktop (so the two can play online together).
//   node tools/web/test_web.cjs <url of the page> <the desktop's --sync-probe output file> [screenshot dir]
const { chromium } = require('playwright');
const fs = require('fs');

(async () => {
  const [url, probeFile, shots] = process.argv.slice(2);
  let failed = 0;
  const check = (ok, what) => { console.log(`  ${ok ? 'ok  ' : 'FAIL'} ${what}`); if (!ok) failed++; };
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 836 } });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

  await page.goto(url);
  await page.waitForFunction(() => document.getElementById('overlay').hidden && window.hexen, null, { timeout: 120000 });
  await page.waitForTimeout(1500);
  const picture = () => page.evaluate(() => {
    const c = document.getElementById('screen'), d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    const colours = new Set();
    for (let i = 0; i < d.length; i += 4 * 7) colours.add((d[i] << 16) | (d[i + 1] << 8) | d[i + 2]);
    return colours.size;
  });
  const title = await picture();
  check(title > 50, `the title screen draws (${title} colours)`);
  if (shots) await page.screenshot({ path: `${shots}/web_title.png` });

  // how fast it runs here: frames drawn in two seconds
  const fps = await page.evaluate(() => new Promise(done => {
    let n = 0; const start = performance.now();
    const tick = () => { n++; if (performance.now() - start < 2000) requestAnimationFrame(tick); else done(n / 2); };
    requestAnimationFrame(tick);
  }));
  console.log(`  (${fps} frames a second)`);

  // the keyboard drives the menus: down to Options and in, then back out
  const before = await page.screenshot({ clip: { x: 0, y: 0, width: 1280, height: 800 } });
  await page.keyboard.press('ArrowDown'); await page.waitForTimeout(150);
  await page.keyboard.press('ArrowDown'); await page.waitForTimeout(150);
  const after = await page.screenshot({ clip: { x: 0, y: 0, width: 1280, height: 800 } });
  check(!before.equals(after), 'the arrow keys move through the title menu');
  // New game, then the class screen: the game starts
  await page.keyboard.press('ArrowUp'); await page.keyboard.press('ArrowUp'); await page.waitForTimeout(100);
  await page.keyboard.press('Enter'); await page.waitForTimeout(400);
  await page.keyboard.press('Enter'); await page.waitForTimeout(400); // (past the style picker, if there is one)
  await page.keyboard.press('Digit1'); await page.waitForTimeout(2500); // the class screen: the Marine
  // a few steps and a look around
  await page.keyboard.down('KeyW'); await page.waitForTimeout(600); await page.keyboard.up('KeyW');
  const playing = await picture();
  check(playing > 50, `a new game starts and draws (${playing} colours)`);
  if (shots) await page.screenshot({ path: `${shots}/web_playing.png` });
  // saved between visits: a setting changed in the console reaches the browser's storage, and survives a reload
  await page.keyboard.press('Backquote'); await page.waitForTimeout(200);
  await page.keyboard.type('name webtester'); await page.keyboard.press('Enter'); await page.waitForTimeout(200);
  await page.keyboard.press('Backquote'); await page.waitForTimeout(200);
  await page.evaluate(() => window.hexen.Flush());
  const settings = () => page.evaluate(() => localStorage.getItem('hexen:/hexen/settings.cfg') ?? '');
  check((await settings()).includes('name WEBTESTER'), "the game's settings are kept in the browser");
  await page.reload();
  await page.waitForFunction(() => document.getElementById('overlay').hidden && window.hexen, null, { timeout: 120000 });
  await page.waitForTimeout(500);
  await page.evaluate(() => window.hexen.Flush());
  check((await settings()).includes('name WEBTESTER'), 'and still there after a reload');

  if (probeFile) {
    const web = (await page.evaluate(() => window.hexen.SyncProbe())).trim();
    const desktop = fs.readFileSync(probeFile, 'utf8').trim();
    check(web === desktop && !web.includes('OUT-OF-STEP'), 'a scripted two-player session comes out the same as on the desktop (cross-play stays in step)');
    if (web !== desktop) { console.log('  web:\n' + web); console.log('  desktop:\n' + desktop); }
  }
  check(errors.length === 0, `no errors on the page${errors.length ? ': ' + errors.join(' | ') : ''}`);
  await browser.close();
  console.log(failed ? `${failed} check(s) failed.` : 'all web checks passed.');
  process.exit(failed ? 1 : 0);
})();
