// Browser test for the HTML map editor (tools/editor/index.html), driven by Playwright in headless Chromium.
//
//   dotnet build -c Release
//   NODE_PATH=$(npm root -g) node tools/editor/test_editor.cjs [screenshot-dir]
//
// It checks that every built-in map loads and saves back byte-for-byte, that the editor's checks say exactly
// what the game's editor says (via `HexenSharp --check-map`), and that painting, stairs, fill and undo work.

const { chromium } = require("playwright");
const { execFileSync } = require("child_process");
const fs = require("fs");
const os = require("os");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const dll = path.join(root, "bin", "Release", "net8.0", "HexenSharp.dll");
const shots = process.argv[2];
let failures = 0;
const check = (ok, what) => { console.log((ok ? "  ok   " : "  FAIL ") + what); if (!ok) failures++; };

/** The game's own verdict on a map, one issue per line ([] when it's fine). */
function gameChecks(text) {
  const file = path.join(os.tmpdir(), `hexen_check_${process.pid}.hxm`);
  fs.writeFileSync(file, text);
  let out;
  try { out = execFileSync("dotnet", [dll, "--check-map", file], { encoding: "utf8" }); }
  catch (e) { out = e.stdout; }  // exit code 1 when the map can't be played
  fs.unlinkSync(file);
  const lines = out.trim().split("\n").map(l => l.trim()).filter(Boolean);
  return lines.length === 1 && lines[0] === "ok" ? [] : lines;
}

(async () => {
  if (!fs.existsSync(dll)) { console.log("build the game first: dotnet build -c Release"); process.exit(2); }
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 860 } });
  const errors = [];
  page.on("pageerror", e => errors.push(e.message));
  await page.goto("file://" + path.join(__dirname, "index.html"));
  await page.evaluate(() => localStorage.clear());
  await page.reload();
  const E = (fn, ...args) => page.evaluate(fn, ...args);

  console.log("Built-in maps:");
  const builtins = await E(() => window.HEXEN_BUILTIN_MAPS);
  check(builtins.length >= 5, `the built-in map menu lists the hub's maps (${builtins.length})`);
  for (const m of builtins) {
    const back = await E(t => hexenEditor.serialize(hexenEditor.parse(t)), m.text);
    check(back === m.text, `${m.name}: loads and saves back byte-for-byte`);
    const web = await E(t => hexenEditor.validateCore(hexenEditor.parse(t)), m.text);
    const game = gameChecks(m.text);
    check(JSON.stringify(web) === JSON.stringify(game), `${m.name}: same checks as the game (${JSON.stringify(game)})`);
  }

  console.log("Painting a map with the mouse:");
  await page.click("#bNew");
  await page.click('#dNew [data-size="20x16"]');
  const cellPoint = async (x, y) => {
    const box = await page.locator("#map").boundingBox();
    const s = await E(() => hexenEditor.ZOOMS[hexenEditor.state.zoom]);
    return { x: box.x + (x + 0.5) * s, y: box.y + (y + 0.5) * s };
  };
  const scrollTo = async () => page.locator("#map").scrollIntoViewIfNeeded();
  const clickCell = async (x, y, opts = {}) => { const p = await cellPoint(x, y); await page.mouse.click(p.x, p.y, opts); };
  const drag = async (x0, y0, x1, y1, opts = {}) => {
    const a = await cellPoint(x0, y0), b = await cellPoint(x1, y1);
    if (opts.shift) await page.keyboard.down("Shift");
    await page.mouse.move(a.x, a.y); await page.mouse.down({ button: opts.button || "left" });
    await page.mouse.move(b.x, b.y, { steps: 8 }); await page.mouse.up({ button: opts.button || "left" });
    if (opts.shift) await page.keyboard.up("Shift");
  };
  const brush = async g => page.click(`#palette button[data-glyph="${g.replace(/"/g, '\\"')}"]`);
  const cell = (x, y, layer = "cells") => E(([x, y, layer]) => { const d = hexenEditor.state.doc; return d[layer][y * d.w + x]; }, [x, y, layer]);
  const text = () => E(() => hexenEditor.serialize(hexenEditor.state.doc));
  // the checks list refreshes on the next animation frame, so let it catch up before reading it
  const issues = () => E(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() =>
    r([...document.querySelectorAll("#issues li")].map(li => li.textContent))))));
  await scrollTo();

  check((await E(() => [hexenEditor.state.doc.w, hexenEditor.state.doc.h])).join() === "20,16", "New → 20 × 16 makes an empty walled map");
  check((await issues())[0] === "Place a player start (@) first.", "the checks ask for a player start first");
  await brush("@"); await clickCell(2, 2);
  await brush("E"); await clickCell(16, 12);
  check(await cell(2, 2) === "@" && await cell(16, 12) === "E", "clicking places the start and the exit");
  check((await issues())[0] === "Ready to play.", "with a start and an exit the map is ready");
  await brush("#"); await drag(9, 1, 9, 14);
  const wall = await E(() => { const d = hexenEditor.state.doc; return [1, 5, 10, 14].every(y => d.cells[y * d.w + 9] === "#"); });
  check(wall, "dragging paints a wall line with no gaps");
  check((await issues()).includes("The exit can't be reached from the start."), "walling off the exit is caught");
  check(JSON.stringify(await E(() => hexenEditor.validateCore(hexenEditor.state.doc))) === JSON.stringify(gameChecks(await text())),
        "and the game agrees");
  await brush("D"); await clickCell(9, 7);
  check((await issues())[0] === "Ready to play.", "a door in the wall opens the way again");
  await brush("e"); await clickCell(14, 4);
  await brush("#"); await page.click('#tools [data-tool="rect"]'); await drag(12, 2, 16, 6, { shift: true });
  check(await cell(12, 2) === "#" && await cell(16, 6) === "#" && await cell(14, 4) === "e", "Shift + rectangle tool draws a hollow room around the monster");
  await page.click('#tools [data-tool="brush"]');
  check((await issues()).some(t => t.startsWith("1 monster(s)/item(s) can't be reached")), "a sealed-in monster is counted as unreachable");
  check(JSON.stringify(await E(() => hexenEditor.validateCore(hexenEditor.state.doc))) === JSON.stringify(gameChecks(await text())),
        "the game counts it the same way");
  await page.keyboard.press("Control+z");
  check(await cell(12, 2) === "." && await cell(14, 4) === "e", "Ctrl+Z undoes the room");
  await page.keyboard.press("Control+y");
  check(await cell(12, 2) === "#", "Ctrl+Y redoes it");
  await drag(14, 2, 14, 2, { button: "right" });
  check(await cell(14, 2) === ".", "right-click erases");

  console.log("Heights:");
  await page.keyboard.press("3");
  await page.keyboard.press("k");
  await brush("2");
  await drag(3, 8, 6, 8);
  const stairs = await E(() => { const d = hexenEditor.state.doc; return [3, 4, 5, 6].map(x => d.floors[8 * d.w + x]).join(""); });
  check(stairs === "2345", `the stair brush climbs a step per cell (${stairs})`);
  await page.keyboard.press("k");
  await page.keyboard.press("2");
  await page.click('#tools [data-tool="fill"]');
  await brush("6");
  await clickCell(4, 4);
  const filled = await E(() => { const d = hexenEditor.state.doc; return [d.heights[4 * d.w + 4], d.heights[4 * d.w + 8], d.heights[4 * d.w + 10]]; });
  check(filled[0] === "6" && filled[1] === "6" && filled[2] === "6", "the fill tool raises the ceiling across the room");
  const saved = await text();
  check(saved.split("---").length === 4, "a map with floors saves heights and floors sections");
  const reparsed = await E(t => hexenEditor.serialize(hexenEditor.parse(t)), saved);
  check(reparsed === saved, "and loads back identically");
  check(gameChecks(saved).length === 0, "the game reads the map and finds nothing wrong");

  console.log("Files and settings:");
  await page.fill("#fName", "Test Grotto");
  check(await E(() => hexenEditor.fileName(hexenEditor.state.doc.name)) === "test_grotto.hxm", "file names follow the game's rule");
  check((await page.textContent("#playCmd")).includes("--play path/to/test_grotto.hxm"), "the play-test command names the file");
  await page.fill("#fW", "24"); await page.fill("#fH", "18"); await page.click("#bResize");
  check((await E(() => [hexenEditor.state.doc.w, hexenEditor.state.doc.h])).join() === "24,18" && await cell(2, 2) === "@", "resizing keeps the map in the top-left corner");
  // mini-bosses, weapon mods, a hazard and elites: saved, read back, and read the same by the game
  await page.selectOption("#fHazard", "flood");
  await page.selectOption("#fElites", "0.3");
  await page.click("#map", { position: { x: 1, y: 1 }, button: "middle" }).catch(() => {});
  await page.keyboard.press("1"); // back to the tiles layer, with the brush
  await page.click('#tools [data-tool="brush"]');
  await brush("G");
  await clickCell(5, 5);
  await brush("m");
  await clickCell(6, 5);
  const featured = await text();
  check(featured.includes("hazard: flood\nelites: 0.3\n") && featured.split("---")[1].includes("Gm"), "a map can have mini-bosses, weapon mods, a hazard and elites");
  check(await E(t => hexenEditor.serialize(hexenEditor.parse(t)), featured) === featured, "and they load back identically");
  const cs = execFileSync("dotnet", [dll, "--check-map", (() => { const f = path.join(os.tmpdir(), `hexen_feat_${process.pid}.hxm`); fs.writeFileSync(f, featured); return f; })()], { encoding: "utf8" }).trim();
  check(!cs.startsWith("!") && JSON.stringify(await E(t => hexenEditor.validateCore(hexenEditor.parse(t)), featured)) === JSON.stringify(gameChecks(featured)), "the game reads them and agrees with the editor's checks");
  check((await page.textContent("#stats")).includes("Mini-bosses"), "the contents count the mini-bosses");
  await page.selectOption("#builtin", builtins[0].file);
  check(await E(() => hexenEditor.state.doc.name) === builtins[0].name, "the built-in map menu opens a template");
  check(await E(() => [hexenEditor.state.doc.hazard, hexenEditor.state.doc.elites].join()) === "none,0", "which starts with no hazard or elites");
  await page.reload();
  check(await E(() => hexenEditor.state.doc.name) === builtins[0].name, "the draft survives a reload");
  check(errors.length === 0, `no script errors (${errors.join("; ")})`);

  if (shots) {
    fs.mkdirSync(shots, { recursive: true });
    await page.selectOption("#builtin", builtins.find(m => m.name === "Windspire").file);
    await page.keyboard.press("3");
    await page.screenshot({ path: path.join(shots, "editor_floors.png") });
    await page.keyboard.press("1");
    await page.click("#bReach");
    await page.screenshot({ path: path.join(shots, "editor_tiles.png") });
    console.log("wrote screenshots to " + shots);
  }

  await browser.close();
  console.log(failures === 0 ? "All editor checks passed." : `${failures} editor check(s) failed.`);
  process.exit(failures === 0 ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
