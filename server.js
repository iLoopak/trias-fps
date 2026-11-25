import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { networkInterfaces } from 'node:os';
import { WebSocketServer } from 'ws';
import { CAPACITY, DEFAULT_MODE, MODES, TICK, roomCode } from './public/rules.js';
import { Match } from './match.js';

const files = new Map([
  ...['pulse', 'scatter', 'lance', 'arc', 'pickup'].map(name => [
    '/sfx/' + name + '.mp3', ['public/sfx/' + name + '.mp3', 'audio/mpeg'],
  ]),
  ['/', ['public/index.html', 'text/html']], ['/style.css', ['public/style.css', 'text/css']],
  ['/client.js', ['public/client.js', 'text/javascript']], ['/arena.js', ['public/arena.js', 'text/javascript']],
  ['/game-audio.js', ['public/game-audio.js', 'text/javascript']],
  ['/effects.js', ['public/effects.js', 'text/javascript']],
  ['/rules.js', ['public/rules.js', 'text/javascript']], ['/orbit.jpg', ['public/orbit.jpg', 'image/jpeg']],
  ['/bulkhead.jpg', ['public/bulkhead.jpg', 'image/jpeg']],
  ['/three.module.js', ['node_modules/three/build/three.module.min.js', 'text/javascript']],
  ['/three.core.min.js', ['node_modules/three/build/three.core.min.js', 'text/javascript']],
]);
const assets = new Map(await Promise.all([...files].map(async ([route, [file, type]]) =>
  [route, { type, data: await readFile(new URL(file, import.meta.url)) }],
)));
const rooms = new Map();
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
  wss.handleUpgrade(request, socket, head, ws => wss.emit('connection', ws));
});

function reject(socket, message) {
  socket.send(JSON.stringify({ type: 'error', message }));
  socket.close(1008);
}

wss.on('connection', socket => {
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
      if (message.type === 'input') socket.room.input(socket.player, message, time);
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
      room = new Match(code, typeof message.mode === 'string' && Object.hasOwn(MODES, message.mode) ? message.mode : DEFAULT_MODE, message.bots !== false);
      room.reset(time);
      rooms.set(code, room);
    }
    if (!room.join(socket, name || 'Pilot', time)) return reject(socket, 'Místnost už má 12 hráčů.');
    socket.room = room;
    socket.send(JSON.stringify({ type: 'joined', id: socket.id, room: code, mode: room.mode, bots: room.bots, capacity: CAPACITY }));
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
