// The browser version's page side (web/WebHost.cs is the other half): it loads the .NET runtime and the game, then
// each animation frame gathers the keys, mouse and first gamepad, runs a frame of the game and draws its picture.
// Sound goes through Web Audio, and the game's files (settings, profile, save) live in localStorage.
import { dotnet } from './_framework/dotnet.js';

const W = 320, H = 200;
const canvas = document.getElementById('screen');
const screen2d = canvas.getContext('2d', { alpha: false });
const image = screen2d.createImageData(W, H);
const pixels = new Uint8Array(image.data.buffer); // (the picture's bytes, as the game's copy wants them)
const overlay = document.getElementById('overlay');
const overlayText = document.getElementById('overlay-text');
const hint = document.getElementById('hint');

function say(text) { overlayText.textContent = text; overlay.hidden = false; }

// ------------------------------------------------------------------ storage: the game's files, between visits

const PREFIX = 'hexen:';
function storedFiles() {
  const files = {};
  try {
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key.startsWith(PREFIX)) files[key.slice(PREFIX.length)] = localStorage.getItem(key);
    }
  } catch { /* storage off (a private window, say): nothing is kept */ }
  return JSON.stringify(files);
}

// ------------------------------------------------------------------ sound

let audio = null, master = null, musicAt = 0;
const sounds = [[], []]; // by art style, then by sound

function audioContext() {
  if (!audio) {
    audio = new AudioContext();
    master = audio.createGain();
    master.connect(audio.destination);
  }
  return audio;
}
// a page may only start sound after you've clicked or pressed something
function wakeAudio() { if (audioContext().state === 'suspended') audio.resume(); }

function pcmBuffer(rate, view) {
  const bytes = view.slice(); // (a copy: the view is only good during the call)
  const pcm = new Int16Array(bytes.buffer, 0, bytes.length >> 1);
  const buffer = audioContext().createBuffer(1, Math.max(1, pcm.length), rate);
  const out = buffer.getChannelData(0);
  for (let i = 0; i < pcm.length; i++) out[i] = pcm[i] / 32768;
  return buffer;
}

// ------------------------------------------------------------------ what the game calls

const host = {
  present(view) { view.copyTo(pixels); screen2d.putImageData(image, 0, 0); },
  storeFile(path, text) { try { localStorage.setItem(PREFIX + path, text); } catch (e) { console.warn('not saved:', path, e); } },
  removeFile(path) { try { localStorage.removeItem(PREFIX + path); } catch { } },
  copyText(text) { navigator.clipboard?.writeText(text).catch(() => { }); },
  download(name, view) {
    const url = URL.createObjectURL(new Blob([view.slice()], { type: 'image/png' }));
    const a = Object.assign(document.createElement('a'), { href: url, download: name });
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
  loadSound(style, id, rate, view) { sounds[style][id] = pcmBuffer(rate, view); },
  playSound(style, id, volume, rate) {
    if (!audio || audio.state !== 'running' || !sounds[style][id]) return;
    const src = audio.createBufferSource();
    src.buffer = sounds[style][id];
    src.playbackRate.value = rate;
    const gain = audio.createGain();
    gain.gain.value = volume;
    src.connect(gain).connect(master);
    src.start();
  },
  musicAhead() { return audio && audio.state === 'running' ? musicAt - audio.currentTime : 99; },
  pushMusic(rate, view) {
    const buffer = pcmBuffer(rate, view);
    const src = audio.createBufferSource();
    src.buffer = buffer;
    src.connect(master);
    const at = Math.max(musicAt, audio.currentTime + 0.05);
    src.start(at);
    musicAt = at + buffer.duration;
  },
};

// ------------------------------------------------------------------ keys, mouse and pad

// the game's key codes are GLFW's (as the desktop's Raylib uses); mouse buttons are 1001 up, the wheel 1010 and 1011
const KEYS = {
  Space: 32, Quote: 39, Comma: 44, Minus: 45, Period: 46, Slash: 47, Semicolon: 59, Equal: 61,
  BracketLeft: 91, Backslash: 92, BracketRight: 93, Backquote: 96,
  Escape: 256, Enter: 257, Tab: 258, Backspace: 259, Insert: 260, Delete: 261,
  ArrowRight: 262, ArrowLeft: 263, ArrowDown: 264, ArrowUp: 265, PageUp: 266, PageDown: 267, Home: 268, End: 269,
  CapsLock: 280, NumpadDecimal: 330, NumpadDivide: 331, NumpadMultiply: 332, NumpadSubtract: 333, NumpadAdd: 334,
  NumpadEnter: 335, ShiftLeft: 340, ControlLeft: 341, AltLeft: 342, ShiftRight: 344, ControlRight: 345, AltRight: 346,
};
for (let c = 65; c <= 90; c++) KEYS['Key' + String.fromCharCode(c)] = c;
for (let d = 0; d <= 9; d++) { KEYS['Digit' + d] = 48 + d; KEYS['Numpad' + d] = 320 + d; }
for (let f = 1; f <= 12; f++) KEYS['F' + f] = 289 + f;
const MOUSE = [1001, 1003, 1002, 1004, 1005]; // the page's left, middle, right, back, forward
const WHEEL_UP = 1010, WHEEL_DOWN = 1011, ESCAPE = 256;
// keys the page would otherwise act on (scrolling, focus, find): the game has them
const MINE = new Set(['Tab', 'Space', 'Backspace', 'Quote', 'Slash', 'Backquote', 'Enter', 'Escape', 'PageUp', 'PageDown',
  'Home', 'End', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'F1', 'F2', 'F3', 'F4', 'F6', 'F7', 'F8', 'F9', 'F10']);

const down = new Uint8Array(1024 + 16), pressed = new Uint8Array(1024 + 16);
let typed = '', mouseX = 0, mouseY = 0, wantsMouse = false, releasing = false;

addEventListener('keydown', e => {
  wakeAudio();
  const code = KEYS[e.code];
  if (code) {
    if (!e.repeat || code === KEYS.Backspace) pressed[code] = 1; // (Backspace repeats, for deleting)
    down[code] = 1;
  }
  if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) typed += e.key;
  if (MINE.has(e.code) && !e.ctrlKey && !e.metaKey) e.preventDefault();
});
addEventListener('keyup', e => { const code = KEYS[e.code]; if (code) down[code] = 0; });
addEventListener('blur', () => down.fill(0)); // (keys let go while you were away mustn't stay held)
addEventListener('paste', e => { typed += e.clipboardData?.getData('text') ?? ''; e.preventDefault(); });

canvas.addEventListener('mousedown', e => {
  wakeAudio();
  if (wantsMouse && !document.pointerLockElement) { lockMouse(); return; } // (that click is for taking the mouse)
  const code = MOUSE[e.button];
  if (code) { down[code] = 1; pressed[code] = 1; }
});
addEventListener('mouseup', e => { const code = MOUSE[e.button]; if (code) down[code] = 0; });
addEventListener('mousemove', e => { if (document.pointerLockElement === canvas) { mouseX += e.movementX; mouseY += e.movementY; } });
canvas.addEventListener('wheel', e => { pressed[e.deltaY < 0 ? WHEEL_UP : WHEEL_DOWN] = 1; e.preventDefault(); }, { passive: false });
canvas.addEventListener('contextmenu', e => e.preventDefault());

function lockMouse() {
  const p = canvas.requestPointerLock?.();
  p?.catch?.(() => { }); // (too soon after Esc let it go: the next click tries again)
}
document.addEventListener('pointerlockchange', () => {
  // Esc takes the mouse back before the game hears it, so the game's told: that's the pause menu
  if (!document.pointerLockElement && wantsMouse && !releasing) pressed[ESCAPE] = 1;
  releasing = false;
});

// the Xbox's Edge drives a pointer with the pad unless a page asks for the pad itself
if ('gamepadInputEmulation' in navigator) navigator.gamepadInputEmulation = 'gamepad';
const PAD_BITS = { 0: 1, 1: 2, 2: 4, 3: 8, 4: 16, 5: 32, 8: 64, 9: 128, 10: 4096, 11: 8192, 12: 256, 13: 512, 14: 1024, 15: 2048 };
let padUsed = false;
function readPad() {
  const pad = [...(navigator.getGamepads?.() ?? [])].find(p => p && p.connected);
  if (!pad) return null;
  let held = 0;
  for (const [i, bit] of Object.entries(PAD_BITS)) if (pad.buttons[i]?.pressed) held |= bit;
  const axis = i => pad.axes[i] ?? 0, trigger = i => pad.buttons[i]?.value ?? 0;
  if (held) padUsed = true;
  return { held, name: pad.id, axes: [axis(0), axis(1), axis(2), axis(3), trigger(6), trigger(7)] };
}

// ------------------------------------------------------------------ the page

document.getElementById('fullscreen').addEventListener('click', () => {
  wakeAudio();
  if (document.fullscreenElement) document.exitFullscreen();
  else document.getElementById('stage').requestFullscreen?.().catch(() => { });
});

say('Loading the game…');
let game;
try {
  const runtime = await dotnet.create();
  runtime.setModuleImports('host', host);
  game = (await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName)).HexenSharp.WebHost;
  // served by a matchmaker (at /play/), play through it; ?matchmaker= picks another
  const matchmaker = new URLSearchParams(location.search).get('matchmaker')
    ?? (location.pathname.startsWith('/play') ? location.origin + '/' : '');
  game.Start(matchmaker, storedFiles());
} catch (e) {
  say(`The game couldn't start: ${e.message ?? e}`);
  throw e;
}
overlay.hidden = true;
addEventListener('visibilitychange', () => { if (document.hidden) game.Flush(); });
addEventListener('pagehide', () => game.Flush());
window.hexen = game; // (for the tests: hexen.SyncProbe())

let last = performance.now();
function frame(now) {
  const dt = Math.min(0.1, Math.max(0, (now - last) / 1000));
  last = now;
  const pad = readPad();
  const locked = document.pointerLockElement === canvas;
  wantsMouse = game.Frame(dt, down, pressed, mouseX, mouseY, typed,
    !!pad, pad ? pad.axes : [0, 0, 0, 0, 0, 0], pad ? pad.held : 0, pad ? pad.name : '', locked);
  pressed.fill(0);
  typed = '';
  mouseX = mouseY = 0;
  if (!wantsMouse && locked) { releasing = true; document.exitPointerLock(); }
  hint.hidden = !(wantsMouse && !locked && !padUsed);
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
