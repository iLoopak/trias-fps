import { performance } from 'node:perf_hooks';
import { WebSocketServer } from 'ws';
import { fireProfileData, Match } from '../match.js';
import { interactionFlags } from '../interactions.js';
import { aimVector, ARMORY, eyeHeight, TICK, wallDistance, WEAPONS } from '../public/rules.js';
import { NETWORK_VERSION, unpackInput } from '../public/netcode.js';

const port = 3035;
const now = () => performance.now() / 1000;
const server = new WebSocketServer({ host: '127.0.0.1', port, path: '/play', maxPayload: 4096, clientTracking: false });
const sessions = new Set();
const stations = [
  { x: 0, z: -12.1, name: 'Kinetický terč', armor: 0, mobile: false },
  { x: -9.0, z: -21.5, name: 'Pohyblivý terč', armor: 0, mobile: true },
  { x: 8.3, z: -17.5, name: 'Štítový terč', armor: 50, mobile: false },
];

function drill(room, player, targets, start) {
  for (const key of ARMORY) player.loadout[key] = { ammo: WEAPONS[key].magazine, reserve: Infinity };
  for (const [i, point] of stations.entries()) {
    const target = targets[i] ?? room.addPlayer('dummy-' + i, point.name, 1, i, start, false, 'bulwark');
    targets[i] = target;
    Object.assign(target, { x: point.x, y: 0, z: point.z, hp: 150, armor: point.armor, safeUntil: 0, yaw: 0, fireAt: Infinity });
  }
}

server.on('connection', (socket, request) => {
  if (request.headers.origin) { socket.close(1008); return; }
  const start = now();
  const match = new Match('FEEL LAB', 'tdm', false, undefined, { interactions: interactionFlags(process.env) });
  const targets = [];
  const send = socket.send.bind(socket);
  let lag = 0;
  let lastIncoming = 0, lastOutgoing = 0;
  const after = (outgoing, action) => {
    const time = Date.now();
    const through = Math.max(time + lag, outgoing ? lastOutgoing : lastIncoming);
    if (outgoing) lastOutgoing = through;
    else lastIncoming = through;
    if (through === time) action();
    else setTimeout(() => { if (socket.readyState === 1) action(); }, through - time);
  };
  socket.send = data => after(true, () => send(data));
  Object.assign(socket, { id: 'lab-pilot', protocolVersion: NETWORK_VERSION });
  match.reset(start);
  socket.on('error', () => socket.terminate());
  socket.on('message', raw => {
    let message;
    try { message = JSON.parse(raw); } catch { return; }
    if (!message || typeof message !== 'object') return;
    if (!socket.player)
    {
      if (message.type !== 'join' || message.v !== NETWORK_VERSION) { socket.close(1008); return; }
      match.join(socket, 'Pilot', now());
      socket.player.signature = true;
      Object.assign(socket.player, { x: 0, z: -24, yaw: -Math.PI });
      drill(match, socket.player, targets, now());
      socket.send(JSON.stringify({ type: 'joined', id: socket.id, room: 'FEEL LAB', mode: 'tdm',
        arena: 'relay', v: NETWORK_VERSION, fireProfiles: fireProfileData(), lab: true }));
      sessions.add(tick);
      return;
    }
    if (message.type === 'lab-delay') { lag = message.active === true ? 80 : 0; return; }
    if (message.type === 'lab-reset') { drill(match, socket.player, targets, now()); return; }
    if (message.type === 'lab-signature') {
      match.signature.echoEnabled = message.echo === true;
      match.signature.momentumEnabled = message.momentum === true;
      if (!match.signature.echoEnabled) match.signature.echoes.clear();
      if (!match.signature.momentumEnabled) match.signature.chains.clear();
      return;
    }
    after(false, () => {
      const at = now();
      if (message.type === 'input') {
        const input = unpackInput(message.data);
        if (input) match.input(socket.player, input, at);
      } else if (message.type === 'trigger') match.trigger(socket.player, message, at);
      else if (message.type === 'echo-fire' || message.type === 'echo-swap') match.signature.request(socket.player, message);
      else if (message.type === 'lab-grenade' && match.interactions.intercept) practiceGrenade(socket.player, at);
      else if (message.type === 'ping') socket.send(JSON.stringify({ type: 'pong', id: message.id }));
    });
  });
  socket.on('close', () => sessions.delete(tick));

  function practiceGrenade(player, at) {
    const target = targets[0];
    if (!player.hp || match.projectiles.some(projectile => projectile.weapon === 'grenade')) return;
    const direction = aimVector(player.yaw, player.pitch);
    const eye = { x: player.x, y: player.y + eyeHeight(player), z: player.z };
    if (wallDistance(eye, direction, 7, match.combatLayout) < 6.99) return;
    target.grenades = Math.max(1, target.grenades);
    match.throwGrenade(target, at);
    const grenade = match.projectiles.at(-1);
    Object.assign(grenade.position, { x: eye.x + direction.x * 6.6, y: eye.y + direction.y * 6.6, z: eye.z + direction.z * 6.6 });
    Object.assign(grenade.velocity, { x: -direction.x * 1.65, y: 3.2, z: -direction.z * 1.65 });
    match.events.at(-1).from = [grenade.position.x, grenade.position.y, grenade.position.z];
  }

  function tick(at) {
    match.endsAt = at + 360;
    match.scores.fill(0);
    for (const [i, point] of stations.entries()) {
      const target = targets[i];
      if (!target.hp && at >= target.respawnAt - 1.9) {
        match.spawn(target, at);
        Object.assign(target, { x: point.x, y: 0, z: point.z, armor: point.armor, safeUntil: 0, yaw: 0, fireAt: Infinity });
      }
      if (!point.mobile || !target.hp) continue;
      target.input = { ...target.input, x: Math.cos((at - start) * 1.55) * .55 };
      target.inputAt = at;
    }
    match.step(at);
  }
});

const timer = setInterval(() => { const at = now(); for (const tick of sessions) tick(at); }, TICK * 1000);
server.on('close', () => clearInterval(timer));
server.on('listening', () => console.log('TRIAS Game Feel Lab → ws://127.0.0.1:' + port));
