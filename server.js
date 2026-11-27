import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { networkInterfaces } from 'node:os';
import { WebSocketServer } from 'ws';
import { ARENAS, CAPACITY, CHARACTERS, DEFAULT_ARENA, DEFAULT_CHARACTER, DEFAULT_MODE, MODES, LAYOUT_SET, RELAY_HORDE, TEAM_SIZE, TICK, roomCode } from './public/rules.js';
import { fireProfileData, Match } from './match.js';
import { interactionFlags } from './interactions.js';
import { directorOptions } from './combat-director.js';
import { RoomSessions, RESUME_SECONDS } from './room-sessions.js';
import { invitePage } from './room-invite.js';
import { Practice } from './practice.js';
import { NETWORK_VERSION, unpackInput } from './public/netcode.js';

const files = new Map([
  ...['pulse', 'scatter', 'lance', 'arc', 'pickup'].map(name => [
    '/sfx/' + name + '.mp3', ['public/sfx/' + name + '.mp3', 'audio/mpeg'],
  ]),
  ['/', ['public/index.html', 'text/html']], ['/style.css', ['public/style.css', 'text/css']],
  ['/client.js', ['public/client.js', 'text/javascript']], ['/arena.js', ['public/arena.js', 'text/javascript']],
  ['/pilot.js', ['public/pilot.js', 'text/javascript']],
  ['/netcode.js', ['public/netcode.js', 'text/javascript']],
  ['/snapshot-clock.js', ['public/snapshot-clock.js', 'text/javascript']],
  ['/death-view.js', ['public/death-view.js', 'text/javascript']],
  ['/pilot-portraits.png', ['public/pilot-portraits.png', 'image/png']],
  ['/weapon-motion.js', ['public/weapon-motion.js', 'text/javascript']],
  ['/game-audio.js', ['public/game-audio.js', 'text/javascript']],
  ['/island.js', ['public/island.js', 'text/javascript']],
  ['/island-terrain.js', ['public/island-terrain.js', 'text/javascript']],
  ['/meadow.jpg', ['public/meadow.jpg', 'image/jpeg']],
  ['/siege-view.js', ['public/siege-view.js', 'text/javascript']],
  ['/tactical-map.js', ['public/tactical-map.js', 'text/javascript']],
  ['/vehicle-view.js', ['public/vehicle-view.js', 'text/javascript']],
  ['/vehicles.js', ['public/vehicles.js', 'text/javascript']],
  ['/basin.jpg', ['public/basin.jpg', 'image/jpeg']],
  ['/station.js', ['public/station.js', 'text/javascript']],
  ['/effects.js', ['public/effects.js', 'text/javascript']],
  ['/rules.js', ['public/rules.js', 'text/javascript']], ['/orbit.jpg', ['public/orbit.jpg', 'image/jpeg']],
  ['/armor.jpg', ['public/armor.jpg', 'image/jpeg']],
  ['/foundry-deck.jpg', ['public/foundry-deck.jpg', 'image/jpeg']],
  ['/deck.jpg', ['public/deck.jpg', 'image/jpeg']],
  ['/bulkhead.jpg', ['public/bulkhead.jpg', 'image/jpeg']],
  ['/three.module.js', ['node_modules/three/build/three.module.min.js', 'text/javascript']],
  ['/three.core.min.js', ['node_modules/three/build/three.core.min.js', 'text/javascript']],
  ['/geometry-utils.js', ['node_modules/three/examples/jsm/utils/BufferGeometryUtils.js', 'text/javascript']],
]);
const assets = new Map(await Promise.all((process.env.TRIAS_NATIVE_ONLY === '1' ? [] : [...files]).map(async ([route, [file, type]]) =>
  [route, { type, data: await readFile(new URL(file, import.meta.url)) }],
)));
const rooms = new Map();
const director = directorOptions(process.env);
const pick = (options, value, fallback) => typeof value === 'string' && Object.hasOwn(options, value) ? value : fallback;
const now = () => performance.now() / 1000;
const server = createServer((request, response) => {
  const route = (request.url ?? '').split('?')[0];
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.writeHead(405, { Allow: 'GET, HEAD' }).end();
    return;
  }
  if (route === '/health') {
    response.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({ status: 'ok', rooms: rooms.size }));
    return;
  }
  if (route.startsWith('/join/')) {
    const body = invitePage(route, request.headers, request.socket.encrypted);
    if (body) {
      response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store',
        'Content-Security-Policy': "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'",
        'Referrer-Policy': 'no-referrer' }).end(request.method === 'HEAD' ? undefined : body);
      return;
    }
  }
  const asset = assets.get(route);
  if (!asset) {
    response.writeHead(404).end('Not found');
    return;
  }
  response.writeHead(200, {
    'Content-Type': asset.type,
    'Content-Length': asset.data.length,
    'X-Content-Type-Options': 'nosniff',
    'Cache-Control': asset.type.startsWith('image/') || asset.type.startsWith('audio/') ? 'public, max-age=3600' : 'no-cache',
  }).end(request.method === 'HEAD' ? undefined : asset.data);
});

const wss = new WebSocketServer({ noServer: true, maxPayload: 1024, perMessageDeflate: false });
server.on('upgrade', (request, socket, head) => {
  let origin;
  try { origin = new URL(request.headers.origin); } catch { /* Native clients can omit Origin. */ }
  if (request.url !== '/play' || wss.clients.size >= 256 || (request.headers.origin && origin?.host !== request.headers.host)) {
    socket.end('HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n');
    return;
  }
  wss.handleUpgrade(request, socket, head, ws => wss.emit('connection', ws, request));
});

function reject(socket, message) {
  socket.send(JSON.stringify({ type: 'error', message }));
  socket.close(1008);
}

function welcome(socket, room, message, token, resumed = false) {
  socket.room = room;
  socket.protocolVersion = message.v === NETWORK_VERSION ? NETWORK_VERSION : 1;
  socket.player.signature = socket.protocolVersion === NETWORK_VERSION && message.signature === true;
  socket.botRadio = socket.protocolVersion === NETWORK_VERSION && message.botRadio === true;
  const fireProfiles = socket.protocolVersion === NETWORK_VERSION && message.predictFire === true ? fireProfileData() : undefined;
  socket.send(JSON.stringify({ type: 'joined', id: socket.id, room: room.code, mode: room.mode, arena: room.layout.id, bots: room.bots,
    arenaLabel: room.layout.label, geometry: room.layout.revision, worldExtent: room.layout.edgeRadius ?? room.layout.length, capacity: room.horde ? TEAM_SIZE : CAPACITY, v: socket.protocolVersion, fireProfiles, pace: room.pace,
    resume: token, resumeSeconds: token ? RESUME_SECONDS : undefined, resumed: resumed || undefined, training: Boolean(room.practice) }));
}

function depart(socket, now, permanent = false) {
  const room = socket.room;
  if (!room) return;
  room.sessions.disconnect(socket, now, permanent);
  if (!room.humans.size && !room.sessions.seats.size) rooms.delete(room.code);
}

wss.on('connection', (socket, request) => {
  Object.assign(socket, { id: randomUUID(), created: now(), alive: true, messages: 0, window: now() });
  socket.on('error', () => socket.terminate());
  socket.on('pong', () => { socket.alive = true; });
  socket.on('message', (data, binary) => {
    const time = now();
    if (time - socket.window >= 1) {
      socket.window = time;
      socket.messages = 0;
    }
    if (++socket.messages > 90) {
      socket.close(1008, 'Rate limit');
      return;
    }
    let message;
    if (binary) return;
    try { message = JSON.parse(data); } catch { return; }
    if (!message || typeof message !== 'object') return;
    if (socket.room) {
      if (message.type === 'leave') {
        depart(socket,time,true);
        socket.close(1000);
        return;
      }
      if (message.type === 'input') {
        const input = socket.protocolVersion === NETWORK_VERSION ? unpackInput(message.data) : message;
        if (input) socket.room.input(socket.player, input, time);
      }
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'trigger') socket.room.trigger(socket.player, message, time);
      if (socket.protocolVersion === NETWORK_VERSION && (message.type === 'echo-fire' || message.type === 'echo-swap')) socket.room.signature.request(socket.player, message);
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'perk' && !request.headers.origin) {
        const error = socket.room.streaks.activate(socket.player, message, time);
        if (error) socket.send(JSON.stringify({ type: 'perk-error', message: error }));
      }
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'build' && socket.room.horde
        && typeof message.kind === 'string') {
        const error = socket.room.horde.build(socket.player, message.site, message.kind, time);
        if (error) socket.send(JSON.stringify({ type: 'build-error', message: error }));
      }
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'horde-vote' && !request.headers.origin)
        socket.room.horde?.vote(socket.player, message.option, time);
      if (message.type === 'ping' && Number.isSafeInteger(message.id) && socket.bufferedAmount < 8192) {
        socket.send(JSON.stringify({ type: 'pong', id: message.id }));
      }
      return;
    }
    if (message.type !== 'join') return;
    const native = message.v === NETWORK_VERSION && !request.headers.origin;
    const training = message.intent === 'practice' && native;
    if (message.intent === 'practice' && !native) return reject(socket, 'Výcvik je dostupný v nativním klientu TRIAS.');
    const code = training ? 'DRILL-' + randomUUID().slice(0, 8).toUpperCase() : roomCode(message.room);
    if (!code) return reject(socket, 'Kód místnosti musí mít 3–24 znaků (A–Z, 0–9 nebo pomlčka).');
    const name = typeof message.name === 'string'
      ? Array.from(message.name.replace(/[\p{Cc}\p{Cf}]/gu, '').trim()).slice(0, 18).join('') : '';
    let room = rooms.get(code);
    const supports = layout => !layout.revision || message.layoutSet === LAYOUT_SET;
    const outdated = () => reject(socket, 'Pro tuto mapu potřebuješ aktuální verzi TRIAS.');
    if (room && !supports(room.layout)) return outdated();
    if (message.intent === 'resume') {
      if (!native || !room?.sessions.attach(socket, message.ticket, time))
        return reject(socket, 'Místo v zápase již nelze obnovit.');
      return welcome(socket, room, message, socket.seat.token, true);
    }
    if (room?.practice) return reject(socket, 'Místnost není otevřená.');
    if (!room && message.intent === 'join') return reject(socket, 'Místnost neexistuje. Zkontroluj kód a server.');
    if (room && message.intent === 'create') return reject(socket, 'Tenhle kód už někdo používá. Zvol jiný nebo se připoj.');
    if (!room) {
      if (rooms.size >= 16) return reject(socket, 'Server má plno. Zkus existující místnost.');
      const mode = training ? 'tdm' : message.mode === 'horde' && native
        ? 'horde' : pick(MODES, message.mode, DEFAULT_MODE);
      const arena = training ? 'relay' : MODES[mode]?.arena ?? pick(ARENAS, message.arena, DEFAULT_ARENA);
      const layout = mode === 'horde' ? RELAY_HORDE : ARENAS[arena];
      if (!supports(layout)) return outdated();
      room = new Match(code, mode, !training && message.bots !== false, layout, {
        echo: training || process.env.TRIAS_COMBAT_ECHO !== '0', momentum: process.env.TRIAS_MOMENTUM !== '0',
        botDifficulty: process.env.TRIAS_BOT_DIFFICULTY, botDebug: process.env.TRIAS_BOT_DEBUG === '1',
        interactions: training ? undefined : interactionFlags(process.env),
        director: training ? undefined : director,
        hordeSupport: mode === 'horde' && (message.hordeBots === true || process.env.TRIAS_HORDE_BOTS === '1'),
      });
      room.reset(time);
      room.sessions = new RoomSessions(room);
      rooms.set(code, room);
    }
    if (room.horde && (message.v !== NETWORK_VERSION || request.headers.origin))
      return reject(socket, 'Horda je dostupná v nativním klientu TRIAS.');
    if (!room.join(socket, name || 'Pilot', time, pick(CHARACTERS, message.character, DEFAULT_CHARACTER), message.frame))
      return reject(socket, room.horde ? 'Obrana má plný tým čtyř hráčů.' : 'Místnost už má 12 hráčů.');
    if (training) room.practice = new Practice(room,socket.player,time);
    const token = native && message.resume === true ? room.sessions.register(socket) : undefined;
    welcome(socket, room, message, token);
  });
  socket.on('close', () => depart(socket,now()));
});

let nextTick = now();
let nextSeatSweep = nextTick;
setInterval(() => {
  const time = now();
  if (time >= nextSeatSweep) {
    nextSeatSweep = time + 1;
    for (const room of rooms.values()) {
      room.sessions.expire(time);
      if (!room.humans.size && !room.sessions.seats.size) rooms.delete(room.code);
    }
  }
  nextTick = Math.max(nextTick, time - TICK * 3);
  while (nextTick <= time) {
    for (const room of rooms.values()) room.step(nextTick);
    nextTick += TICK;
  }
}, 8).unref();

setInterval(() => {
  const time = now();
  for (const socket of wss.clients) {
    if (!socket.alive || (!socket.room && time - socket.created > 8)) {
      socket.terminate();
      continue;
    }
    socket.alive = false;
    socket.ping();
  }
}, 8000).unref();

const port = Number(process.env.PORT ?? 3000);
server.listen(port, process.env.HOST ?? '0.0.0.0', () => {
  const actual = server.address().port;
  console.log('TRIAS → http://localhost:' + actual);
  if (!process.env.HOST || process.env.HOST === '0.0.0.0') {
    for (const addresses of Object.values(networkInterfaces())) {
      for (const address of addresses ?? []) {
        if (address.family === 'IPv4' && !address.internal) console.log('LAN → http://' + address.address + ':' + actual);
      }
    }
  }
});
