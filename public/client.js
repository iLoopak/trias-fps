import { drawTacticalMap } from './tactical-map.js';
import { KILL_CAM_MS, nextAlly } from './death-view.js';
import { NETWORK_VERSION, packInput, unpackSnapshot } from './netcode.js';
import { boardingVehicle, moveVehicle, VEHICLES } from './vehicles.js';
import { GameAudio } from './game-audio.js';
import {
  ARENAS, ARMORY, CHARACTERS, DEFAULT_ARENA, DEFAULT_CHARACTER, DEFAULT_MODE, DODGE_COOLDOWN, GRENADE, MODES,
  PILOT_HP, POWERUPS, TEAMS, TICK, TURRET, WEAPONS, canDodge, distanceSquared, eyeHeight, movePlayer, pilotColor, roomCode, turretYaw,
} from './rules.js';

const $ = id => document.getElementById(id);
const lobby = $('lobby');
const game = $('game');
const canvas = $('arena');
const pause = $('pause');
const scoreboard = $('scoreboard');
const params = new URLSearchParams(location.search);
const modeButtons = new Map();
const mapButtons = new Map();
const pilotButtons = new Map();
const keys = new Set();
const recentTap = new Map();
const tapDirection = {
  KeyW: 'forward', ArrowUp: 'forward', KeyS: 'back', ArrowDown: 'back',
  KeyA: 'left', ArrowLeft: 'left', KeyD: 'right', ArrowRight: 'right',
};
const gearWidgets = ARMORY.map((weapon, i) => {
  const slot = node('span');
  slot.style.setProperty('--item-color', WEAPONS[weapon].color);
  slot.append(node('kbd', i + 1), node('b', WEAPONS[weapon].label));
  $('weapons').append(slot);
  return slot;
});
let selectedMode = Object.hasOwn(MODES, params.get('mode')) ? params.get('mode') : DEFAULT_MODE;
let selectedMap = Object.hasOwn(ARENAS, params.get('arena')) ? params.get('arena') : DEFAULT_ARENA;
let selectedPilot = DEFAULT_CHARACTER;
let layout = ARENAS[selectedMap];
let ArenaView;
let arena;
let socket;
let joined;
let own;
let state;
let seq = 0;
let queue = [];
let view;
let predicted;
let predictedVehicle;
let jumpClick = false;
let dodgeClick = false;
let altClick = false;
let grenadeClick = false;
let useClick = false;
let lastMovement;
let weaponClick = null;
let manualAim = false;
let dragAim = false;
let previousAim;
let isFiring = false;
let fireClick = false;
let reloadClick = false;
const audio = new GameAudio();
let toastTimer;
let lastScoreSignature;
let hitAnimation;
let hitCaptionAnimation;
let damageAnimation;
let hitPriority = 0;
let lastDamageAudio = -1000;
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
let deathReplay, observedId, observerSignature, reentryAnimation;
let pickupTimer;
let sensitivity = 1;
try { $('sensitivity').value = localStorage.getItem('trias.sensitivity') ?? '100'; } catch { /* Storage can be unavailable. */ }
function updateSensitivity() {
  sensitivity = $('sensitivity').valueAsNumber / 100;
  $('sensitivity-value').value = $('sensitivity').value + ' %';
}
$('sensitivity').addEventListener('input', updateSensitivity);
$('sensitivity').addEventListener('change', () => {
  try { localStorage.setItem('trias.sensitivity', $('sensitivity').value); } catch { /* Storage can be unavailable. */ }
});
updateSensitivity();

function node(tag, text, css) {
  const item = document.createElement(tag);
  if (text !== undefined) item.textContent = text;
  if (css) item.className = css;
  return item;
}

function newRoomCode() {
  const alphabet = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';
  return 'ORB-' + Array.from(crypto.getRandomValues(new Uint8Array(6)), n => alphabet[n % alphabet.length]).join('');
}

for (const team of TEAMS) {
  const chip = node('span');
  chip.style.setProperty('--team', team.color);
  chip.append(node('i'), document.createTextNode(team.name));
  $('teams').append(chip);
}

function chooseMode(mode) {
  selectedMode = mode;
  for (const [id, button] of modeButtons) button.setAttribute('aria-pressed', id === mode);
  $('mode-description').textContent = MODES[mode].description;
  $('selected-format').textContent = mode === 'ffa' ? 'AŽ 12 HRÁČŮ' : '4 VS 4 VS 4';
  if (MODES[mode].arena && selectedMap !== MODES[mode].arena) chooseMap(MODES[mode].arena);
}

for (const [id, mode] of Object.entries(MODES)) {
  const button = node('button', undefined, 'mode');
  button.type = 'button';
  button.append(node('b', mode.short), node('span', mode.label));
  button.addEventListener('click', () => chooseMode(id));
  modeButtons.set(id, button);
  $('mode-list').append(button);
}
chooseMode(selectedMode);
function chooseMap(id) {
  selectedMap = id;
  for (const [key, button] of mapButtons) button.setAttribute('aria-pressed', key === id);
  $('hero-map').textContent = ARENAS[id].label.toUpperCase();
  if (MODES[selectedMode].arena && MODES[selectedMode].arena !== id) chooseMode(DEFAULT_MODE);
}
for (const [id, item] of Object.entries(ARENAS)) {
  const button = node('button', undefined, 'preset map-preset');
  button.type = 'button';
  button.dataset.theme = id;
  button.append(node('b', item.label), node('small', item.description));
  button.addEventListener('click', () => chooseMap(id));
  mapButtons.set(id, button);
  $('map-options').append(button);
}
chooseMap(selectedMap);

try {
  const stored = localStorage.getItem('trias.pilot');
  if (Object.hasOwn(CHARACTERS, stored)) selectedPilot = stored;
} catch { /* Storage can be unavailable. */ }
for (const [id, item] of Object.entries(CHARACTERS)) {
  const button = node('button', undefined, 'preset pilot-preset');
  button.type = 'button';
  button.dataset.pilot = id;
  const weapon = WEAPONS[item.weapon];
  button.title = item.description + (item.weapon !== 'pulse' ? ' Plný zásobník, jedno přebití a Pulse v záloze.' : '');
  button.append(node('i'), node('b', item.label), node('small', weapon.label));
  button.setAttribute('aria-pressed', id === selectedPilot);
  button.addEventListener('click', () => {
    selectedPilot = id;
    for (const [key, other] of pilotButtons) other.setAttribute('aria-pressed', key === id);
    try { localStorage.setItem('trias.pilot', id); } catch { /* Storage can be unavailable. */ }
  });
  pilotButtons.set(id, button);
  $('pilot-options').append(button);
}
$('room').value = roomCode(params.get('room')) ?? newRoomCode();
$('bots').checked = params.get('bots') !== '0';
try { $('nickname').value = localStorage.getItem('trias.name') ?? ''; } catch { /* Storage can be unavailable. */ }
$('new-code').addEventListener('click', () => { $('room').value = newRoomCode(); });
for (const button of document.querySelectorAll('[data-help]')) {
  button.addEventListener('click', () => $('help').showModal());
}

fetch('/health').then(response => {
  if (!response.ok) throw new Error('Health check');
  $('server-status').textContent = 'SERVER ONLINE';
  $('server-status').parentElement.classList.add('online');
}).catch(() => { $('server-status').textContent = 'SERVER OFFLINE'; });

function showError(message) {
  $('lobby-error').textContent = message;
  $('lobby-error').hidden = !message;
}

function endMatch(message = '') {
  const connection = socket;
  socket = undefined;
  if (connection && connection.readyState < WebSocket.CLOSING) connection.close();
  if (document.pointerLockElement === canvas) document.exitPointerLock();
  joined = own = state = predictedVehicle = undefined;
  resetObservation();
  audio.engine(null);
  manualAim = false;
  queue = [];
  seq = 0;
  $('network-latency').hidden = true;
  ping = undefined;
  lastScoreSignature = undefined;
  clearControls();
  game.hidden = scoreboard.hidden = true;
  lobby.hidden = false;
  $('join-button').disabled = false;
  showError(message);
  $('join-button').focus();
}

$('join-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (socket) return;
  const room = roomCode($('room').value.trim());
  if (!room) return showError('Kód místnosti musí mít 3–24 písmen, číslic nebo pomlček.');
  if (typeof canvas.requestPointerLock !== 'function') return showError('Hra vyžaduje desktopový prohlížeč s podporou zámku myši.');
  $('join-button').disabled = true;
  showError('');
  try {
    ArenaView ??= (await import('./arena.js')).Arena;
  } catch (error) {
    console.error(error);
    $('join-button').disabled = false;
    return showError('3D arénu se nepodařilo spustit. Zkontroluj, že má prohlížeč povolený WebGL 2.');
  }

  const name = $('nickname').value.trim();
  try { localStorage.setItem('trias.name', name); } catch { /* Storage can be unavailable. */ }
  const connection = new WebSocket((location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/play');
  socket = connection;
  const timeout = setTimeout(() => {
    if (socket === connection && !joined) endMatch('Server neodpověděl včas.');
  }, 5000);
  connection.addEventListener('open', () => connection.send(JSON.stringify({
    type: 'join', v: NETWORK_VERSION, room, name, mode: selectedMode, arena: selectedMap, character: selectedPilot, bots: $('bots').checked,
  })));
  connection.addEventListener('message', event => {
    if (socket !== connection) return;
    const message = JSON.parse(event.data);
    if (message.type === 'error') endMatch(message.message);
    if (message.type === 'joined') {
      clearTimeout(timeout);
      onJoin(message);
    }
    if (message.type === 'state' && joined) onState(unpackSnapshot(message));
    if (message.type === 'pong' && ping?.id === message.id && ping.sent) {
      const elapsed = performance.now() - ping.sent;
      const latency = $('network-latency');
      latency.textContent = Math.round(elapsed) + ' ms';
      latency.classList.toggle('slow', elapsed >= 120);
      latency.hidden = false;
      ping.sent = null;
    }
  });
  connection.addEventListener('close', () => {
    clearTimeout(timeout);
    if (socket === connection) endMatch('Spojení se serverem skončilo. Zkus se připojit znovu.');
  });
});

function inviteURL() {
  const url = new URL('/', location.href);
  url.searchParams.set('room', joined.room);
  url.searchParams.set('mode', joined.mode);
  url.searchParams.set('arena', layout.id);
  if (!joined.bots) url.searchParams.set('bots', '0');
  return url;
}

function onJoin(message) {
  layout = ARENAS[message.arena] ?? ARENAS[DEFAULT_ARENA];
  try {
    if (arena?.layout !== layout) {
      arena?.dispose();
      arena = undefined;
      arena = new ArenaView(canvas, layout);
    } else arena.reset();
  } catch (error) {
    console.error(error);
    endMatch('3D arénu se nepodařilo spustit. Zkontroluj, že má prohlížeč povolený WebGL 2.');
    return;
  }
  joined = message;
  lastHUDUpdate = lastMapUpdate = lastEngineUpdate = -Infinity;
  if (joined.v === NETWORK_VERSION) probeLatency();
  void audio.preload();
  chooseMode(message.mode);
  chooseMap(layout.id);
  $('pause-tag').textContent = layout.label.toUpperCase();
  $('tactical-map').hidden = layout.id !== 'basin' && layout.id !== 'island';
  $('match-mode').textContent = MODES[message.mode].label;
  $('match-room').textContent = layout.label + ' · ' + message.room;
  $('room').value = message.room;
  $('bots').checked = message.bots;
  $('feed').replaceChildren();
  $('toast').hidden = true;
  $('pickup-feedback').hidden = true;
  resetCombatFeedback();
  lastMovement = undefined;
  history.replaceState(null, '', inviteURL());
  game.hidden = false;
  lobby.hidden = true;
  pause.hidden = false;
  $('pause-note').textContent = 'Kliknutím převezmeš ovládání myši.';
  $('manual-aim').hidden = true;
  $('resume').focus();
}

function clearControls() {
  keys.clear();
  recentTap.clear();
  isFiring = fireClick = reloadClick = jumpClick = dodgeClick = dragAim = false;
  weaponClick = null;
  altClick = grenadeClick = useClick = false;
}

function resetObservation() {
  deathReplay = observedId = observerSignature = undefined;
  $('observer').hidden = true;
  game.classList.remove('observing');
  reentryAnimation?.cancel();
}

function observation(time) {
  if (own.hp || state.winner) return null;
  if (deathReplay && time < deathReplay.end) {
    deathReplay.progress = Math.max(0, (time - deathReplay.start) / (deathReplay.end - deathReplay.start));
    return deathReplay;
  }
  deathReplay = undefined;
  const target = state.players.find(player => player.id === observedId && player.hp > 0 && player.team === own.team)
    ?? nextAlly(state.players, own, joined.mode);
  observedId = target?.id;
  return { target, team: joined.mode === 'ffa' ? null : own.team };
}

function updateObservation(frame) {
  const element = $('observer');
  const phase = frame?.event ? 'replay' : frame?.target ? 'follow' : frame ? 'wait' : '';
  const seconds = Math.ceil(own.respawn);
  const signature = phase + ':' + (frame?.target?.id ?? frame?.event?.attackerId ?? '') + ':' + seconds;
  if (signature === observerSignature) return;
  observerSignature = signature;
  element.hidden = !phase;
  game.classList.toggle('observing', Boolean(phase));
  if (!phase) return;
  element.dataset.phase = phase;
  const event = frame.event;
  const weapon = event?.weapon === 'grenade' ? GRENADE.label : event?.weapon === 'collision' ? 'Náraz vozidla' : WEAPONS[event?.weapon]?.label;
  element.style.setProperty('--observer-accent', event ? '#ffac78' : TEAMS[own.team].color);
  $('observer-mode').textContent = event ? 'KILL CAM' : frame.target ? 'SLEDUJEŠ' : 'VYŘAZENÍ';
  $('observer-time').textContent = seconds > 0 ? 'RESPAWN ' + seconds + ' s' : 'DALŠÍ KOLO';
  $('observer-name').textContent = event?.attacker ?? frame.target?.name ?? 'Čekáš na návrat';
  $('observer-detail').textContent = event ? [weapon, 'Mezerník · přeskočit'].filter(Boolean).join('  /  ')
    : frame.target ? 'E · další spoluhráč' : '';
}

function canControl() {
  return document.pointerLockElement === canvas || manualAim;
}

function syncLock() {
  pause.hidden = canControl();
  if (!pause.hidden) clearControls();
}

function offerManualAim() {
  if (!joined) return;
  $('pause-note').textContent = 'Zámek myši je nedostupný. Bez něj se míří tažením pravého tlačítka.';
  $('manual-aim').hidden = false;
}

$('resume').addEventListener('click', async () => {
  if (!joined) return;
  void audio.unlock();
  try { await canvas.requestPointerLock(); }
  catch { offerManualAim(); }
});
$('manual-aim').addEventListener('click', () => {
  if (!joined) return;
  manualAim = true;
  canvas.focus();
  syncLock();
  notify('Míření tažením pravého tlačítka myši.');
  void audio.unlock();
});
document.addEventListener('pointerlockchange', syncLock);
document.addEventListener('pointerlockerror', offerManualAim);
$('leave').addEventListener('click', () => endMatch());

function notify(text) {
  $('toast').textContent = text;
  $('toast').hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, 3600);
}

$('invite').addEventListener('click', async () => {
  if (!joined) return;
  const url = inviteURL().href;
  try {
    await navigator.clipboard.writeText(url);
    notify('Pozvánka zkopírována.');
  } catch {
    const field = node('input');
    field.value = url;
    field.style.position = 'fixed';
    field.style.opacity = '0';
    document.body.append(field);
    field.select();
    const copied = document.execCommand('copy');
    field.remove();
    if (copied) notify('Pozvánka zkopírována.');
    else window.prompt('Zkopíruj odkaz do místnosti.', url);
  }
});

function toggleScores(open = scoreboard.hidden) {
  if (!joined) return;
  scoreboard.hidden = !open;
  $('score-button').setAttribute('aria-expanded', open);
  clearControls();
  if (open && state) renderScoreboard();
}
$('score-button').addEventListener('click', () => toggleScores());
$('close-score').addEventListener('click', () => toggleScores(false));

document.addEventListener('keydown', event => {
  if (!joined || $('help').open) return;
  if (event.code === 'Tab' && (canControl() || !scoreboard.hidden)) {
    event.preventDefault();
    if (!event.repeat) toggleScores();
    return;
  }
  if (event.code === 'Escape') {
    if (!scoreboard.hidden) toggleScores(false);
    else if (manualAim) {
      manualAim = false;
      syncLock();
    }
    return;
  }
  if (!canControl() || !scoreboard.hidden) return;
  if (own?.hp === 0) {
    if (event.code === 'Space' || event.code === 'KeyE') {
      event.preventDefault();
      if (!event.repeat) {
        if (deathReplay) deathReplay = undefined;
        else if (event.code === 'KeyE') observedId = nextAlly(state.players, own, joined.mode, observedId)?.id;
      }
    }
    return;
  }
  const direction = tapDirection[event.code];
  if (direction) {
    event.preventDefault();
    if (!event.repeat) {
      const time = performance.now();
      if (time - (recentTap.get(direction) ?? -1000) <= 224) {
        dodgeClick = true;
        recentTap.delete(direction);
      } else recentTap.set(direction, time);
    }
  }
  if (event.code === 'KeyQ' && !event.repeat) dodgeClick = true;
  keys.add(event.code);
  if (event.code === 'Space') {
    event.preventDefault();
    if (!event.repeat) jumpClick = true;
  }
  if (!event.repeat) {
    if (event.code === 'KeyR') reloadClick = true;
    if (event.code === 'KeyF' && WEAPONS[own?.weapon]?.alt && own.turret < 0 && own.vehicle < 0) altClick = !sendTrigger(true);
    if (event.code === 'KeyG') grenadeClick = true;
    if (event.code === 'KeyE') useClick = true;
  }
  if (event.code.startsWith('Digit') && !event.repeat) weaponClick = ARMORY[Number(event.code.slice(5)) - 1] ?? null;
});
document.addEventListener('keyup', event => keys.delete(event.code));
window.addEventListener('blur', clearControls);
let lastMouse = 0;
document.addEventListener('pointerlockchange', () => { lastMouse = performance.now(); });
document.addEventListener('mousemove', event => {
  if (!canControl() || !own?.hp || state?.winner || !scoreboard.hidden || performance.now() - lastMouse < 50) return;
  if (manualAim && !dragAim) return;
  const dx = manualAim ? event.clientX - previousAim.x : event.movementX;
  const dy = manualAim ? event.clientY - previousAim.y : event.movementY;
  if (manualAim) previousAim = { x: event.clientX, y: event.clientY };
  view.yaw = (view.yaw - dx * 0.00215 * sensitivity) % (Math.PI * 2);
  view.pitch = Math.max(-1.48, Math.min(1.48, view.pitch - dy * 0.00215 * sensitivity));
  clampMountedAim();
});
function clampMountedAim() {
  if (predictedVehicle) {
    view.pitch = Math.max(-0.66, Math.min(0.67, view.pitch));
    return;
  }
  if (own.turret < 0) return;
  view.yaw = turretYaw(view.yaw, layout.turrets[own.turret].yaw);
  view.pitch = Math.max(-0.47, Math.min(0.56, view.pitch));
}
document.addEventListener('mousedown', event => {
  if (!canControl() || !scoreboard.hidden || !own?.hp || state?.winner) return;
  if (manualAim && event.target !== canvas) return;
  if (event.button === 2 && manualAim) {
    dragAim = true;
    previousAim = { x: event.clientX, y: event.clientY };
  }
  if (event.button === 2 && !manualAim && WEAPONS[own.weapon]?.alt && own.turret < 0 && own.vehicle < 0)
    altClick = !sendTrigger(true);
  if (event.button === 0) {
    isFiring = true;
    fireClick = !sendTrigger(false);
    const [rounds, reserve] = own.loadout[ARMORY.indexOf(own.weapon)];
    if (own.turret < 0 && own.vehicle < 0 && !own.reload) {
      if (rounds) arena.anticipate();
      else if (!reserve) audio.gunAction(own.weapon, 'empty');
    }
  }
});
document.addEventListener('mouseup', event => {
  if (event.button === 0) isFiring = false;
  if (event.button === 2) dragAim = false;
});
canvas.addEventListener('contextmenu', event => event.preventDefault());
document.addEventListener('wheel', event => {
  if (!canControl() || !scoreboard.hidden || !own?.hp || state?.winner || !event.deltaY) return;
  event.preventDefault();
  const owned = ARMORY.filter((_, index) => own.loadout[index]);
  if (owned.length < 2) return;
  const index = owned.indexOf(weaponClick ?? own.weapon);
  weaponClick = owned[(index + Math.sign(event.deltaY) + owned.length) % owned.length];
}, { passive: false });

const down = (a, b) => Number(keys.has(a) || keys.has(b));
function sendTrigger(alt) {
  if (joined?.v !== NETWORK_VERSION || socket?.readyState !== WebSocket.OPEN || socket.bufferedAmount >= 2048) return false;
  socket.send(JSON.stringify({ type: 'trigger', yaw: view.yaw, pitch: view.pitch, alt }));
  return true;
}
let lastHUDUpdate = -Infinity;
let lastMapUpdate = -Infinity;
let lastEngineUpdate = -Infinity;
let ping;
function probeLatency() {
  if (joined?.v !== NETWORK_VERSION || socket?.readyState !== WebSocket.OPEN || socket.bufferedAmount >= 2048) return;
  ping = { id: (ping?.id ?? 0) + 1, sent: performance.now() };
  socket.send(JSON.stringify({ type: 'ping', id: ping.id }));
}
setInterval(probeLatency, 2500);

setInterval(() => {
  if (!own || socket?.readyState !== WebSocket.OPEN || socket.bufferedAmount > 2048) return;
  const control = canControl() && scoreboard.hidden && own.hp && !state.winner;
  const input = {
    type: 'input', seq: ++seq,
    x: control ? down('KeyD', 'ArrowRight') - down('KeyA', 'ArrowLeft') : 0,
    z: control ? down('KeyW', 'ArrowUp') - down('KeyS', 'ArrowDown') : 0,
    yaw: view.yaw, pitch: view.pitch, sprint: Boolean(control && down('ShiftLeft', 'ShiftRight')), brake: Boolean(control && keys.has('Space')),
    fire: Boolean(control && (isFiring || fireClick)), reload: Boolean(control && reloadClick),
    jump: Boolean(control && jumpClick), dodge: Boolean(control && dodgeClick), crouch: Boolean(control && keys.has('KeyC')),
    alt: Boolean(control && altClick), grenade: Boolean(control && grenadeClick), use: Boolean(control && useClick),
    weapon: control ? weaponClick : null,
  };
  fireClick = reloadClick = jumpClick = dodgeClick = altClick = grenadeClick = useClick = false;
  weaponClick = null;
  socket.send(JSON.stringify(joined.v === NETWORK_VERSION ? { type: 'input', data: packInput(input) } : input));
  queue.push(input);
  if (queue.length > 24) queue.shift();
  if (own.hp && !state.winner && own.turret < 0) {
    if (predictedVehicle) {
      moveVehicle(predictedVehicle, input, layout, state.vehicles);
      return;
    }
    const signal = movePlayer(predicted, input, TICK, layout);
    if (signal) {
      lastMovement = { action: signal, time: performance.now() };
      showMotion({ action: signal, position: [predicted.x, predicted.y, predicted.z], id: own.id, team: own.team }, true);
    }
  }
}, TICK * 1000);

function onState(message) {
  const player = message.players.find(p => p.id === joined.id);
  if (!player) return;
  const spawn = own?.spawn !== player.spawn;
  const wasAlive = own?.hp > 0;
  const mounted = own?.turret !== player.turret || own?.vehicle !== player.vehicle;
  const immediateHUD = spawn || mounted || own?.hp !== player.hp || own?.armor !== player.armor
    || own?.weapon !== player.weapon || own?.reload !== player.reload || own?.grenades !== player.grenades
    || own?.loadout[ARMORY.indexOf(player.weapon)]?.[0] !== player.loadout[ARMORY.indexOf(player.weapon)]?.[0]
    || state?.time !== message.time || Boolean(state?.winner) !== Boolean(message.winner);
  if (spawn || mounted) {
    view = { x: player.x, y: player.y, z: player.z, yaw: player.yaw, pitch: player.pitch };
    queue = [];
  }
  if (spawn && own) {
    resetCombatFeedback();
    resetObservation();
    arena.respawn(player, joined.mode);
    audio.signature('respawn');
    const reentry = $('reentry');
    reentry.style.setProperty('--arrival', pilotColor(player, joined.mode));
    reentryAnimation = reentry.animate([{ opacity: reducedMotion.matches ? 0.22 : 0.53 }, { opacity: 0 }],
      { duration: reducedMotion.matches ? 360 : 615, easing: 'ease-out' });
    lastMovement = undefined;
  }
  if (wasAlive && player.hp <= 0) clearControls();
  if (!spawn && own?.weapon === player.weapon && own.reload !== player.reload && player.hp > 0) {
    const index = ARMORY.indexOf(player.weapon);
    if (player.reload) audio.gunAction(player.weapon, 'open');
    else if (player.loadout[index]?.[0] > own.loadout[index]?.[0]) audio.gunAction(player.weapon, 'close');
  }
  predicted = {
    x: player.x, y: player.y, z: player.z, vx: player.vx, vy: player.vy, vz: player.vz, grounded: player.grounded,
    airJump: player.airJump, airDodge: player.airDodge, dodgeCooldown: player.dodgeCooldown, dodgeTime: player.dodgeTime,
    padCooldown: player.padCooldown, crouch: player.crouch,
  };
  const vehicle = message.vehicles?.[player.vehicle];
  predictedVehicle = vehicle ? { ...vehicle } : null;
  queue = queue.filter(input => input.seq > player.ack);
  if (player.hp && player.turret < 0 && !message.winner) {
    for (const input of queue) {
      if (predictedVehicle) moveVehicle(predictedVehicle, input, layout, message.vehicles);
      else movePlayer(predicted, input, TICK, layout);
    }
  }
  own = player;
  state = message;
  arena.snapshot(message, player, joined.mode);
  for (const event of message.events) {
    if (event.type === 'shot' || event.type === 'burst' || event.type === 'collision') playShot(event);
    else if ((event.type === 'wreck' || event.type === 'core-wreck')) {
      arena.wreck(event);
      const { distance, pan } = spatial(event.position);
      if (event.type === 'core-wreck') audio.signature('core', Math.max(0.12, 1 - distance / 80), pan);
      else audio.explosion(distance, pan, true);
    }
    else if (event.type === 'grenade') {
      const { distance, pan } = spatial(event.from);
      audio.grenadeThrow(distance, pan);
    } else if (event.type === 'movement') {
      const local = event.id === own.id;
      const predictedCue = local && lastMovement?.action === event.action && performance.now() - lastMovement.time < 330;
      if (!predictedCue) showMotion(event, local);
    }
    else if (event.type === 'pickup') {
      arena.collect(event);
      if (event.id === own.id) playPickup(event.item);
    } else {
      if (event.type === 'kill') {
        const local = event.victimId === own.id;
        arena.eliminate(event, local, joined.mode);
        if (local && !own.hp && event.body && own.respawn > 0.4 && !message.winner) {
          const start = performance.now();
          deathReplay = { event, team: joined.mode === 'ffa' ? null : own.team, start,
            end: start + Math.min(KILL_CAM_MS, own.respawn * 430), progress: 0 };
          audio.signature('replay');
        }
      }
      addFeed(event);
    }
  }
  const time = performance.now();
  if (immediateHUD || time - lastHUDUpdate >= 90) {
    updateHUD();
    if (!scoreboard.hidden) renderScoreboard();
    lastHUDUpdate = time;
  }
  if (!$('tactical-map').hidden && time - lastMapUpdate >= 80) {
    drawTacticalMap($('minimap'), layout, state, own, joined.mode, view.yaw);
    lastMapUpdate = time;
  }
}

function spatial(position) {
  const dx = position[0] - own.x;
  const dz = position[2] - own.z;
  const distance = Math.hypot(dx, dz, position[1] - own.y - eyeHeight(own));
  const pan = Math.max(-0.85, Math.min(0.85, (dx * Math.cos(view.yaw) - dz * Math.sin(view.yaw)) / Math.max(2, distance)));
  return { dx, dz, distance, pan };
}

function playShot(event) {
  const local = event.id === own.id;
  const { dx, dz, distance, pan } = spatial(event.from);
  if (event.type === 'burst') {
    arena.burst(event);
    if (event.weapon === 'rift') audio.signature('rift', Math.max(0, 1 - distance / 35), pan);
    audio.explosion(distance, pan, event.weapon === 'grenade');
  } else if (event.type === 'shot') {
    arena.shot(event, local);
    if (distance < 28) audio.shot(event.weapon, local, distance, local ? 0 : pan, event.alt);
    if (local && own.hp) {
      const rise = { pulse: 0.013, scatter: 0.050, lance: 0.030, arc: 0.009, rift: 0.048 }[event.weapon];
      const kick = event.alt ? rise * 1.38 : (event.mounted || event.vehicle) ? rise * 0.45 : rise;
      view.pitch = Math.min(1.48, view.pitch + kick);
      view.yaw += (Math.random() - 0.5) * kick * 0.38;
      clampMountedAim();
    }
  }
  if (local && event.hits.length) confirmHit(event.hits);
  if (!local) {
    const hit = event.hits.find(item => item.id === own.id);
    if (hit) receiveHit(dx, dz, hit);
  }
}

function showMotion(event, local) {
  if (event.action !== 'step') {
    const player = local ? own : state.players.find(item => item.id === event.id);
    arena.move(event, local, player ? pilotColor(player, joined.mode) : TEAMS[event.team].color);
  }
  if (local) {
    if (event.action === 'step') audio.footstep();
    else audio.movement(event.action);
    return;
  }
  const { distance, pan } = spatial(event.position);
  if (event.action === 'step' && distance < 10) audio.footstep((1 - distance / 10) * 0.82, pan);
  if (event.action === 'launch' && distance < 18) audio.movement('launch', (1 - distance / 18) * 0.55, pan);
}

function resetCombatFeedback() {
  hitAnimation?.cancel();
  hitCaptionAnimation?.cancel();
  damageAnimation?.cancel();
  $('damage-directions').replaceChildren();
  $('health-card').classList.remove('critical');
  hitPriority = 0;
  lastDamageAudio = -1000;
}

function confirmHit(hits) {
  const type = hits.some(hit => hit.kind !== 'core' && hit.eliminated) ? 'eliminated'
    : hits.some(hit => hit.kind === 'core') ? 'core' : hits.some(hit => hit.absorbed > 0) ? 'shield' : 'body';
  const priority = { body: 1, shield: 2, core: 2, eliminated: 3 }[type];
  if (hitAnimation?.playState === 'running' && hitPriority > priority) return;
  hitPriority = priority;
  const marker = $('hit-marker');
  const caption = $('hit-caption');
  const label = { body: '', shield: 'ŠTÍT', core: 'JÁDRO', eliminated: 'VYŘAZENÍ' }[type];
  const duration = { body: 155, shield: 235, core: 270, eliminated: 455 }[type];
  marker.dataset.kind = caption.dataset.kind = type;
  caption.textContent = label;
  hitAnimation?.cancel();
  hitCaptionAnimation?.cancel();
  const motion = reducedMotion.matches ? 1 : (type === 'eliminated' ? 1.29 : 1.15);
  hitAnimation = marker.animate([
    { opacity: 1, scale: motion }, { opacity: 1, scale: 1, offset: 0.31 }, { opacity: 0, scale: 1 },
  ], { duration, easing: 'ease-out' });
  if (label) hitCaptionAnimation = caption.animate([{ opacity: 1 }, { opacity: 1, offset: 0.6 }, { opacity: 0 }], duration + 100);
  audio.confirm(type === 'core' ? 'shield' : type);
}

function receiveHit(dx, dz, hit) {
  const angle = Math.atan2(dx, -dz);
  const shield = hit.absorbed > 0 && !hit.eliminated;
  const shade = shield ? '#66c8ff' : '#ff594c';
  const strength = Math.min(0.87, 0.29 + (hit.health + hit.absorbed) / 112);
  const damage = $('damage');
  damage.style.setProperty('--damage-color', shade);
  damageAnimation?.cancel();
  damageAnimation = damage.animate(
    [{ opacity: reducedMotion.matches ? strength * 0.55 : strength }, { opacity: strength * 0.28, offset: 0.24 }, { opacity: 0 }],
    { duration: hit.eliminated ? 430 : 290, easing: 'ease-out' },
  );

  const directions = $('damage-directions');
  if (directions.childElementCount >= 4) directions.firstElementChild.remove();
  const indicator = node('i', undefined, 'damage-direction');
  indicator.style.setProperty('--angle', angle + 'rad');
  indicator.style.setProperty('--damage-color', shade);
  directions.append(indicator);
  const animation = indicator.animate([{ opacity: 1 }, { opacity: 0.85, offset: 0.37 }, { opacity: 0 }], { duration: 550 });
  animation.onfinish = () => indicator.remove();
  if (performance.now() - lastDamageAudio > 58 || hit.eliminated) {
    lastDamageAudio = performance.now();
    audio.damage(shield, hit.eliminated, Math.sin(angle + view.yaw) * 0.44);
  }
}

function playPickup(key) {
  const item = WEAPONS[key] ?? POWERUPS[key];
  const feedback = $('pickup-feedback');
  feedback.textContent = '+ ' + item.label;
  feedback.style.setProperty('--item-color', item.color);
  feedback.hidden = false;
  clearTimeout(pickupTimer);
  pickupTimer = setTimeout(() => { feedback.hidden = true; }, 1800);
  audio.pickup();
}

function addFeed(event) {
  const row = node('div');
  if (event.type === 'kill') {
    const attacker = node('b', event.attacker);
    const player = state.players.find(item => item.id === event.attackerId);
    attacker.style.setProperty('--team', player ? pilotColor(player, joined.mode) : TEAMS[event.team].color);
    row.append(attacker, document.createTextNode(' › ' + event.victim));
  } else {
    row.textContent = event.text;
  }
  $('feed').prepend(row);
  if ($('feed').children.length > 5) $('feed').lastElementChild.remove();
  setTimeout(() => row.remove(), 5200);
}

function updateLabels(container, items) {
  if (container.childElementCount !== items.length) container.replaceChildren(...items.map(() => node('span')));
  items.forEach((item, i) => {
    const element = container.children[i];
    element.style.setProperty('--team', item.color ?? '#ecf6f4');
    element.style.setProperty('--progress', (item.progress ?? 0) * 100 + '%');
    if (element.dataset.label === item.label + '/' + item.score) return;
    element.dataset.label = item.label + '/' + item.score;
    element.textContent = item.label;
    if (item.score !== undefined) element.append(node('b', item.score));
  });
}

function updateHUD() {
  const minutes = String(Math.floor(state.time / 60)).padStart(2, '0');
  const seconds = String(state.time % 60).padStart(2, '0');
  $('clock').textContent = minutes + ':' + seconds;
  const team = TEAMS[own.team];
  const ffa = joined.mode === 'ffa';
  $('team-label').textContent = ffa ? 'PILOT' : team.name;
  const accent = pilotColor(own, joined.mode);
  $('health-card').style.setProperty('--team', accent);
  $('health-card').classList.toggle('critical', own.hp > 0 && own.hp <= 35);
  $('health').textContent = own.hp;
  $('health-bar').style.width = (own.hp / PILOT_HP * 100) + '%';
  $('armor').textContent = own.armor ? 'ŠTÍT ' + own.armor : '';
  const activeIndex = ARMORY.indexOf(own.weapon);
  const [rounds, reserve] = own.loadout[activeIndex];
  const turret = own.turret >= 0 ? state.turrets[own.turret] : null;
  const vehicle = state.vehicles?.[own.vehicle];
  const vehicleSpec = vehicle && VEHICLES[vehicle.kind];
  const weapon = vehicleSpec ? vehicleSpec.gun.key : turret ? 'arc' : own.weapon;
  $('crosshair').dataset.weapon = weapon;
  $('weapon-name').textContent = vehicleSpec ? vehicleSpec.label.toUpperCase()
    : turret ? 'ENERGETICKÉ HNÍZDO' : WEAPONS[own.weapon].label.toUpperCase();
  $('weapon-name').style.color = vehicleSpec?.color ?? WEAPONS[weapon].color;
  $('ammo').textContent = vehicle ? vehicle.hp : turret ? Math.round(turret.heat * 100) : rounds;
  $('ammo').classList.toggle('dry', !vehicle && !turret && rounds === 0);
  $('reserve').textContent = vehicle ? ' / ' + vehicleSpec.hp : turret ? ' %' : ' / ' + (reserve < 0 ? '∞' : reserve);
  $('weapon-action').textContent = vehicle ? 'TRUP · E VYSTOUPIT' : turret ? 'E · SESTOUPIT'
    : rounds === 0 && reserve === 0 && own.weapon !== 'pulse' ? '1 · ZÁLOŽNÍ PULSE'
      : 'G · GRANÁT ' + own.grenades + '  /  F · ' + WEAPONS[own.weapon].alt.label.toUpperCase() + ' (' + WEAPONS[own.weapon].alt.cost + ')';
  $('weapons').hidden = $('dodge-readout').hidden = Boolean(vehicle);
  $('drive-readout').hidden = !vehicle;
  if (vehicle) $('drive-ability').textContent = vehicle.kind === 'skimmer' ? 'SHIFT · TURBO' : 'MEZERNÍK · BRZDA';
  gearWidgets.forEach((item, i) => {
    item.classList.toggle('active', i === activeIndex);
    item.classList.toggle('owned', Boolean(own.loadout[i]));
  });
  const leader = Math.max(...state.players.map(player => player.kills));
  const siege = state.siege;
  const defeated = siege && siege.cores[own.team].hp === 0;
  updateLabels($('scores'), siege ? siege.cores.map((core, i) => ({
    label: TEAMS[i].name + (core.hp ? core.opened ? ' · ODKRYTÉ' : ' · ŠTÍT' : ' · MIMO HRU'),
    score: core.hp, color: TEAMS[i].color,
  })) : ffa ? [
    { label: 'TY', score: own.kills, color: accent }, { label: 'VEDENÍ', score: leader },
  ] : TEAMS.map((item, i) => ({ label: item.name, score: state.scores[i], color: item.color })));
  const objectives = [];
  if (siege) siege.nodes.forEach((point, i) => {
    const tag = layout.siege.nodes[i].label;
    objectives.push({
      label: tag + (point.contested ? ' ×' : point.progress > 0 ? ' ' + Math.floor(point.progress * 100) + '%'
        : point.linked ? ' ↔' : ''),
      color: TEAMS[point.capture >= 0 ? point.capture : point.owner]?.color,
      progress: point.progress,
    });
  });
  if (state.points) {
    state.points.forEach((point, i) => objectives.push({
      label: 'ABC'[i] + (point.contested ? ' · SPORNÝ' : ''),
      color: TEAMS[point.capture >= 0 ? point.capture : point.owner]?.color,
      progress: point.progress,
    }));
  }
  if (state.flags) {
    const labels = { home: 'DOMA', dropped: 'NA ZEMI', carried: 'NESE SE' };
    state.flags.forEach((flag, i) => objectives.push({
      label: TEAMS[i].name + ' · ' + labels[flag.status], color: TEAMS[i].color,
    }));
  }
  updateLabels($('objectives'), objectives);
  const result = state.winner?.label === 'Remíza' ? 'Remíza' : 'Vítězí ' + state.winner?.label;
  const nearbyTurret = own.turret < 0 && own.hp && own.grounded && !own.crouch
    && layout.turrets.some((item, i) => !state.turrets[i].user && distanceSquared(own, item) < TURRET.dismountRadius ** 2);
  const canBoard = !vehicle && own.turret < 0 && own.hp && !state.flags?.some(flag => flag.carrier === own.id);
  const nearbyVehicle = canBoard && boardingVehicle(own, state.vehicles ?? []);
  const repairing = vehicle && vehicle.hp < vehicleSpec.hp && Math.abs(vehicle.speed) < 2
    && layout.repairs.some(pad => distanceSquared(pad, vehicle) < pad.radius ** 2);
  const ownCore = siege?.cores[own.team];
  const exposedEnemy = siege?.cores.findIndex(core => core.opened & (1 << own.team));
  $('combat-status').textContent = state.winner ? result + ' · další kolo za ' + state.restart + ' s'
    : defeated ? 'ZÁKLADNA PADLA · DALŠÍ KOLO PO ZÁPASE'
      : own.respawn ? 'Návrat za ' + own.respawn.toFixed(1) + ' s'
      : repairing ? 'OPRAVUJEŠ TRUP'
        : vehicle && Math.abs(vehicle.speed) > 5.5 ? 'ZPOMAL PRO VÝSTUP'
          : nearbyVehicle ? 'E · NASTOUPIT · ' + VEHICLES[nearbyVehicle.kind].label.toUpperCase()
            : turret?.hot ? 'HNÍZDO SE CHLADÍ'
        : nearbyTurret ? 'E · OBSLOUŽIT HNÍZDO'
          : ownCore?.opened ? 'BRAŇ VLASTNÍ JÁDRO'
            : exposedEnemy >= 0 ? 'ÚTOČ NA JÁDRO ' + TEAMS[exposedEnemy].name
          : own.reload ? 'Přebíjíš…'
        : state.flags?.some(flag => flag.carrier === own.id) ? 'Neseš vlajku · vrať se na základnu'
          : own.surge ? 'OVERCHARGE · ' + own.surge + ' s'
            : rounds === 0 && reserve === 0 ? 'Bez munice · klávesa 1 pro Pulse' : '';
  $('crosshair').hidden = !own.hp || Boolean(state.winner);
}

function renderScoreboard() {
  const humans = state.players.filter(player => !player.bot).length;
  const metrics = state.siege?.cores.map(core => core.hp) ?? state.scores;
  const signature = JSON.stringify([metrics, state.players.map(p => [p.id, p.kills, p.deaths])]);
  if (signature === lastScoreSignature) return;
  lastScoreSignature = signature;
  $('occupancy').textContent = 'Hráči ' + humans + ' / ' + joined.capacity + ' · Boti ' + (state.players.length - humans);
  const groups = joined.mode === 'ffa'
    ? [{ name: 'Všichni hráči', players: state.players }]
    : TEAMS.map((team, i) => ({ ...team, name: team.name + ' · ' + metrics[i], players: state.players.filter(p => p.team === i) }));
  $('score-tables').replaceChildren(...groups.map(group => {
    const holder = node('div', undefined, 'score-group');
    if (group.color) holder.style.setProperty('--team', group.color);
    const table = node('table');
    const caption = node('caption', group.name);
    const header = node('tr');
    for (const title of ['Pilot', 'Vyřazení', 'Úmrtí']) header.append(node('th', title));
    const head = node('thead');
    head.append(header);
    const body = node('tbody');
    const ranked = [...group.players].sort((a, b) => b.kills - a.kills || a.deaths - b.deaths);
    for (const player of ranked) {
      const row = node('tr', undefined, player.id === own.id ? 'local' : '');
      const name = node('td');
      const swatch = node('i', undefined, 'pilot-swatch');
      swatch.style.setProperty('--pilot-color', pilotColor(player, joined.mode));
      name.append(swatch, document.createTextNode(player.name));
      if (player.bot) name.append(node('small', 'BOT'));
      row.append(name, node('td', player.kills), node('td', player.deaths));
      body.append(row);
    }
    if (!ranked.length) {
      const info = node('td', 'Volná místa');
      info.colSpan = 3;
      const row = node('tr');
      row.append(info);
      body.append(row);
    }
    table.append(caption, head, body);
    holder.append(table);
    return holder;
  }));
}

let lastFrame = performance.now();
function frame(time) {
  const dt = Math.max(0, Math.min(0.1, (time - lastFrame) / 1000));
  lastFrame = time;
  if (own && !game.hidden) {
    const blend = 1 - Math.exp(-dt * 47);
    const motion = predictedVehicle ?? predicted;
    view.x += (motion.x - view.x) * blend;
    view.y += (predicted.y - view.y) * blend;
    view.z += (motion.z - view.z) * blend;
    if (time - lastEngineUpdate >= 80) {
      lastEngineUpdate = time;
      if (predictedVehicle) {
        $('drive-speed').textContent = Math.round(Math.abs(predictedVehicle.speed) * 3.6) + ' km/h';
        if (predictedVehicle.kind === 'skimmer') $('drive-ability').textContent = 'SHIFT · TURBO ' + Math.round(predictedVehicle.boost * 100) + ' %';
      }
      audio.engine(predictedVehicle);
    }
    const readiness = Math.max(0, 1 - predicted.dodgeCooldown / DODGE_COOLDOWN);
    const dodge = $('dodge-ready');
    dodge.style.transform = 'scaleX(' + (own.hp ? readiness : 0) + ')';
    dodge.classList.toggle('ready', Boolean(own.hp && own.turret < 0 && canDodge(predicted)));
    const directions = $('damage-directions');
    if (directions.childElementCount) directions.style.setProperty('--look-yaw', view.yaw + 'rad');
    const watching = observation(time);
    updateObservation(watching);
    arena.render(time, view, own, predicted, reducedMotion.matches, predictedVehicle, watching);
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
