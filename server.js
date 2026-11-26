import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { networkInterfaces } from 'node:os';
import { WebSocketServer } from 'ws';
import { ARENAS, CAPACITY, CHARACTERS, DEFAULT_ARENA, DEFAULT_CHARACTER, DEFAULT_MODE, MODES, TEAM_SIZE, TICK, roomCode } from './public/rules.js';
import { Match } from './match.js';
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
const assets = new Map(await Promise.all([...files].map(async ([route, [file, type]]) =>
  [route, { type, data: await readFile(new URL(file, import.meta.url)) }],
)));
const rooms = new Map();
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
      if (message.type === 'input') {
        const input = socket.protocolVersion === NETWORK_VERSION ? unpackInput(message.data) : message;
        if (input) socket.room.input(socket.player, input, time);
      }
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'trigger') socket.room.trigger(socket.player, message, time);
      if (socket.protocolVersion === NETWORK_VERSION && message.type === 'build' && socket.room.horde
        && typeof message.kind === 'string') {
        const error = socket.room.horde.build(socket.player, message.site, message.kind, time);
        if (error) socket.send(JSON.stringify({ type: 'build-error', message: error }));
      }
      if (message.type === 'ping' && Number.isSafeInteger(message.id) && socket.bufferedAmount < 8192) {
        socket.send(JSON.stringify({ type: 'pong', id: message.id }));
      }
      return;
    }
    if (message.type !== 'join') return;
    const code = roomCode(message.room);
    if (!code) return reject(socket, 'Kód místnosti musí mít 3–24 znaků (A–Z, 0–9 nebo pomlčka).');
    const name = typeof message.name === 'string'
      ? Array.from(message.name.replace(/[\p{Cc}\p{Cf}]/gu, '').trim()).slice(0, 18).join('') : '';
    let room = rooms.get(code);
    if (!room) {
      if (rooms.size >= 16) return reject(socket, 'Server má plno. Zkus existující místnost.');
      const mode = message.mode === 'horde' && message.v === NETWORK_VERSION && !request.headers.origin
        ? 'horde' : pick(MODES, message.mode, DEFAULT_MODE);
      const arena = MODES[mode]?.arena ?? pick(ARENAS, message.arena, DEFAULT_ARENA);
      room = new Match(code, mode, message.bots !== false, ARENAS[arena]);
      room.reset(time);
      rooms.set(code, room);
    }
    if (room.horde && (message.v !== NETWORK_VERSION || request.headers.origin))
      return reject(socket, 'Horda je dostupná v nativním klientu TRIAS.');
    if (!room.join(socket, name || 'Pilot', time, pick(CHARACTERS, message.character, DEFAULT_CHARACTER)))
      return reject(socket, room.horde ? 'Obrana má plný tým čtyř hráčů.' : 'Místnost už má 12 hráčů.');
    socket.room = room;
    socket.protocolVersion = message.v === NETWORK_VERSION ? NETWORK_VERSION : 1;
    socket.send(JSON.stringify({ type: 'joined', id: socket.id, room: code, mode: room.mode, arena: room.layout.id, bots: room.bots, capacity: room.horde ? TEAM_SIZE : CAPACITY, v: socket.protocolVersion }));
  });
  socket.on('close', () => {
    const room = socket.room;
    if (!room) return;
    room.humans.delete(socket);
    if (!room.humans.size) {
      rooms.delete(room.code);
      return;
    }
    const time = now();
    room.removePlayer(socket.id, time);
    room.fillBots(time);
  });
});

let nextTick = now();
setInterval(() => {
  const time = now();
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
