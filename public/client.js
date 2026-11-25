import { GameAudio } from './game-audio.js';
import { ARMORY, DEFAULT_MODE, MODES, POWERUPS, TEAMS, TICK, WEAPONS, movePlayer, pilotColor, roomCode } from './rules.js';

const $ = id => document.getElementById(id);
const lobby = $('lobby');
const game = $('game');
const canvas = $('arena');
const pause = $('pause');
const scoreboard = $('scoreboard');
const params = new URLSearchParams(location.search);
const modeButtons = new Map();
const keys = new Set();
const gearWidgets = ARMORY.map((weapon, i) => {
  const slot = node('span');
  slot.style.setProperty('--item-color', WEAPONS[weapon].color);
  slot.append(node('kbd', i + 1), node('b', WEAPONS[weapon].label));
  $('weapons').append(slot);
  return slot;
});
let selectedMode = Object.hasOwn(MODES, params.get('mode')) ? params.get('mode') : DEFAULT_MODE;
let arena;
let socket;
let joined;
let own;
let state;
let seq = 0;
let queue = [];
let view;
let predicted;
let jumpClick = false;
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
let pickupTimer;

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
  joined = own = state = undefined;
  manualAim = false;
  queue = [];
  seq = 0;
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
    const { Arena } = await import('./arena.js');
    arena ??= new Arena(canvas);
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
    type: 'join', room, name, mode: selectedMode, bots: $('bots').checked,
  })));
  connection.addEventListener('message', event => {
    if (socket !== connection) return;
    const message = JSON.parse(event.data);
    if (message.type === 'error') endMatch(message.message);
    if (message.type === 'joined') {
      clearTimeout(timeout);
      onJoin(message);
    }
    if (message.type === 'state' && joined) onState(message);
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
  if (!joined.bots) url.searchParams.set('bots', '0');
  return url;
}

function onJoin(message) {
  joined = message;
  void audio.preload();
  chooseMode(message.mode);
  $('match-mode').textContent = MODES[message.mode].label;
  $('match-room').textContent = message.room;
  $('room').value = message.room;
  $('bots').checked = message.bots;
  $('feed').replaceChildren();
  $('toast').hidden = true;
  $('pickup-feedback').hidden = true;
  resetCombatFeedback();
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
  isFiring = fireClick = reloadClick = jumpClick = dragAim = false;
  weaponClick = null;
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
  keys.add(event.code);
  if (event.code === 'Space') {
    event.preventDefault();
    if (!event.repeat) jumpClick = true;
  }
  if (event.code === 'KeyR' && !event.repeat) reloadClick = true;
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
  view.yaw = (view.yaw - dx * 0.00215) % (Math.PI * 2);
  view.pitch = Math.max(-1.48, Math.min(1.48, view.pitch - dy * 0.00215));
});
document.addEventListener('mousedown', event => {
  if (!canControl() || !scoreboard.hidden || !own?.hp || state?.winner) return;
  if (manualAim && event.target !== canvas) return;
  if (event.button === 2 && manualAim) {
    dragAim = true;
    previousAim = { x: event.clientX, y: event.clientY };
  }
  if (event.button === 0) isFiring = fireClick = true;
});
document.addEventListener('mouseup', event => {
  if (event.button === 0) isFiring = false;
  if (event.button === 2) dragAim = false;
});
canvas.addEventListener('contextmenu', event => event.preventDefault());

const down = (a, b) => Number(keys.has(a) || keys.has(b));
setInterval(() => {
  if (!own || socket?.readyState !== WebSocket.OPEN || socket.bufferedAmount > 2048) return;
  const control = canControl() && scoreboard.hidden && own.hp && !state.winner;
  const input = {
    type: 'input', seq: ++seq,
    x: control ? down('KeyD', 'ArrowRight') - down('KeyA', 'ArrowLeft') : 0,
    z: control ? down('KeyW', 'ArrowUp') - down('KeyS', 'ArrowDown') : 0,
    yaw: view.yaw, pitch: view.pitch, sprint: Boolean(control && down('ShiftLeft', 'ShiftRight')),
    fire: Boolean(control && (isFiring || fireClick)), reload: Boolean(control && reloadClick),
    jump: Boolean(control && jumpClick), weapon: control ? weaponClick : null,
  };
  fireClick = reloadClick = jumpClick = false;
  weaponClick = null;
  socket.send(JSON.stringify(input));
  queue.push(input);
  if (queue.length > 24) queue.shift();
  if (own.hp && !state.winner) {
    const launched = input.jump && (predicted.grounded || predicted.airJump);
    movePlayer(predicted, input);
    if (launched) audio.tone(116, 235, 0.12, 'triangle', 0.075);
  }
}, TICK * 1000);

function onState(message) {
  const player = message.players.find(p => p.id === joined.id);
  if (!player) return;
  const spawn = own?.spawn !== player.spawn;
  if (spawn) {
    view = { x: player.x, y: player.y, z: player.z, yaw: player.yaw, pitch: player.pitch };
    queue = [];
  }
  if (spawn && own) resetCombatFeedback();
  predicted = { x: player.x, y: player.y, z: player.z, vy: player.vy, grounded: player.grounded, airJump: player.airJump };
  queue = queue.filter(input => input.seq > player.ack);
  if (player.hp && !message.winner) for (const input of queue) movePlayer(predicted, input);
  own = player;
  state = message;
  arena.snapshot(message, player, joined.mode);
  for (const event of message.events) {
    if (event.type === 'shot') playShot(event);
    else if (event.type === 'pickup') {
      arena.collect(event);
      if (event.id === own.id) playPickup(event.item);
    } else addFeed(event);
  }
  updateHUD();
  if (!scoreboard.hidden) renderScoreboard();
}

function playShot(event) {
  const local = event.id === own.id;
  arena.shot(event, local);
  const dx = event.from[0] - own.x;
  const dz = event.from[2] - own.z;
  const distance = Math.hypot(dx, dz, event.from[1] - own.y - 1.46);
  if (distance < 28) {
    const pan = local ? 0 : Math.max(-0.85, Math.min(0.85, (dx * Math.cos(view.yaw) - dz * Math.sin(view.yaw)) / Math.max(2, distance)));
    audio.shot(event.weapon, local, distance, pan);
  }
  if (local && event.hits.length) confirmHit(event.hits);
  if (!local) {
    const hit = event.hits.find(item => item.id === own.id);
    if (hit) receiveHit(dx, dz, hit);
  }
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
  const type = hits.some(hit => hit.eliminated) ? 'eliminated' : hits.some(hit => hit.absorbed > 0) ? 'shield' : 'body';
  const priority = { body: 1, shield: 2, eliminated: 3 }[type];
  if (hitAnimation?.playState === 'running' && hitPriority > priority) return;
  hitPriority = priority;
  const marker = $('hit-marker');
  const caption = $('hit-caption');
  const label = { body: '', shield: 'ŠTÍT', eliminated: 'VYŘAZENÍ' }[type];
  const duration = { body: 155, shield: 235, eliminated: 455 }[type];
  marker.dataset.kind = caption.dataset.kind = type;
  caption.textContent = label;
  hitAnimation?.cancel();
  hitCaptionAnimation?.cancel();
  const motion = reducedMotion.matches ? 1 : (type === 'eliminated' ? 1.29 : 1.15);
  hitAnimation = marker.animate([
    { opacity: 1, scale: motion }, { opacity: 1, scale: 1, offset: 0.31 }, { opacity: 0, scale: 1 },
  ], { duration, easing: 'ease-out' });
  if (label) hitCaptionAnimation = caption.animate([{ opacity: 1 }, { opacity: 1, offset: 0.6 }, { opacity: 0 }], duration + 100);
  audio.confirm(type);
}

function receiveHit(dx, dz, hit) {
  const lateral = dx * Math.cos(view.yaw) - dz * Math.sin(view.yaw);
  const forward = -dx * Math.sin(view.yaw) - dz * Math.cos(view.yaw);
  const angle = Math.atan2(lateral, forward);
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
    audio.damage(shield, hit.eliminated, Math.sin(angle) * 0.44);
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
  $('health-card').classList.toggle('critical', own.hp > 0 && own.hp <= 25);
  $('health').textContent = own.hp;
  $('health-bar').style.width = own.hp + '%';
  $('armor').textContent = own.armor ? 'ŠTÍT ' + own.armor : '';
  const activeIndex = ARMORY.indexOf(own.weapon);
  const [rounds, reserve] = own.loadout[activeIndex];
  $('weapon-name').textContent = WEAPONS[own.weapon].label.toUpperCase();
  $('weapon-name').style.color = WEAPONS[own.weapon].color;
  $('ammo').textContent = rounds;
  $('reserve').textContent = ' / ' + (reserve < 0 ? '∞' : reserve);
  gearWidgets.forEach((item, i) => {
    item.classList.toggle('active', i === activeIndex);
    item.classList.toggle('owned', Boolean(own.loadout[i]));
  });
  const leader = Math.max(...state.players.map(player => player.kills));
  updateLabels($('scores'), ffa ? [
    { label: 'TY', score: own.kills, color: accent }, { label: 'VEDENÍ', score: leader },
  ] : TEAMS.map((item, i) => ({ label: item.name, score: state.scores[i], color: item.color })));
  const objectives = [];
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
  $('combat-status').textContent = state.winner ? result + ' · další kolo za ' + state.restart + ' s'
    : own.respawn ? 'Návrat za ' + own.respawn.toFixed(1) + ' s'
      : own.reload ? 'Přebíjíš…'
        : state.flags?.some(flag => flag.carrier === own.id) ? 'Neseš vlajku · vrať se na základnu'
          : own.surge ? 'OVERCHARGE · ' + own.surge + ' s'
            : rounds === 0 && reserve === 0 ? 'Bez munice · klávesa 1 pro Pulse' : '';
  $('crosshair').hidden = !own.hp || Boolean(state.winner);
}

function renderScoreboard() {
  const humans = state.players.filter(player => !player.bot).length;
  const signature = JSON.stringify([state.scores, state.players.map(p => [p.id, p.kills, p.deaths])]);
  if (signature === lastScoreSignature) return;
  lastScoreSignature = signature;
  $('occupancy').textContent = 'Hráči ' + humans + ' / ' + joined.capacity + ' · Boti ' + (state.players.length - humans);
  const groups = joined.mode === 'ffa'
    ? [{ name: 'Všichni hráči', players: state.players }]
    : TEAMS.map((team, i) => ({ ...team, name: team.name + ' · ' + state.scores[i], players: state.players.filter(p => p.team === i) }));
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
    const blend = 1 - Math.exp(-dt * 29);
    view.x += (predicted.x - view.x) * blend;
    view.y += (predicted.y - view.y) * blend;
    view.z += (predicted.z - view.z) * blend;
    const moving = canControl() && own.hp && ['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].some(key => keys.has(key));
    arena.render(time, view, own, moving);
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
