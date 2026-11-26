import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { NETWORK_VERSION, packInput, unpackInput, unpackSnapshot } from '../public/netcode.js';
import { presentedPose, rememberPose, SnapshotClock } from '../public/snapshot-clock.js';

function connect(match, id, protocolVersion) {
  const packets = [];
  const socket = {
    id, protocolVersion, readyState: 1, bufferedAmount: 0,
    send(message) { packets.push(message); },
    terminate() { this.readyState = 3; },
  };
  assert(match.join(socket, id, 0));
  return { socket, player: socket.player, packets };
}

test('compact and legacy peers share a room and each gets an event once', () => {
  const match = new Match('NET', 'siege', false);
  const modern = connect(match, 'modern', NETWORK_VERSION);
  const legacy = connect(match, 'legacy', 1);
  match.events.push({ type: 'notice', text: 'one' });
  match.step(1 / 30);
  assert.equal(legacy.packets.length, 0);
  match.events.push({ type: 'notice', text: 'two' });
  match.step(2 / 30);

  const raw = modern.packets.map(message => JSON.parse(message));
  assert(raw.every(packet => Array.isArray(packet.players[0])));
  const snapshots = raw.map(unpackSnapshot);
  assert.deepEqual(snapshots.map(packet => packet.tick), [1, 2]);
  assert.deepEqual(snapshots.flatMap(packet => packet.events).map(event => event.text), ['one', 'two']);
  const original = JSON.parse(legacy.packets[0]);
  assert.deepEqual(original.events.map(event => event.text), ['one', 'two']);
  const precise = original.players.map(player => ({ ...player, yaw: Math.round(player.yaw * 1000) / 1000 }));
  assert.deepEqual(snapshots[1].players, precise);
  assert.deepEqual(snapshots[1].siege, original.siege);
  assert.equal(snapshots[1].vehicles.length, 6);
  assert(modern.packets[1].length < legacy.packets[0].length);
});

test('a delayed peer cannot queue unbounded stale world updates', () => {
  const match = new Match('NET', 'tdm', false);
  const peer = connect(match, 'lagger', NETWORK_VERSION);
  const normal = connect(match, 'other', NETWORK_VERSION);
  peer.socket.bufferedAmount = 65_536;
  match.step(1);
  assert.equal(peer.socket.readyState, 3);
  assert.equal(peer.packets.length, 0);
  assert.equal(normal.packets.length, 1);
});

test('input flags round-trip and sequence acknowledgement is sent after simulation', () => {
  const command = {
    seq: 12, x: -1, z: 1, yaw: 1.222, pitch: -0.331, weapon: 'scatter',
    fire: true, sprint: false, jump: true, use: false, reload: true, alt: false, grenade: true,
    brake: false, dodge: true, crouch: false,
  };
  assert.deepEqual(unpackInput(packInput(command)), command);
  assert.equal(unpackInput([1, 0, 0, 0, 0, -1, 'pulse']), null);
  const match = new Match('NET', 'tdm', false);
  const { player, packets } = connect(match, 'pilot', NETWORK_VERSION);
  match.input(player, command, 1);
  assert.equal(player.ack, 0);
  match.input(player, { ...command, seq: 11, yaw: 0 }, 1.01);
  match.step(1.02);
  const result = unpackSnapshot(JSON.parse(packets[0])).players[0];
  assert.equal(result.ack, 12);
  assert.equal(result.yaw, command.yaw);
  assert(result.y > 0);
});

test('a click can fire on the next server tick without waiting for movement input', () => {
  const match = new Match('NET', 'tdm', false);
  const { player, packets } = connect(match, 'pilot', NETWORK_VERSION);
  match.trigger(player, { yaw: -1, pitch: 0.2 }, 1);
  match.step(1 + 1 / 30);
  const packet = unpackSnapshot(JSON.parse(packets[0]));
  assert.equal(packet.players[0].ack, 0);
  assert.equal(packet.players[0].yaw, -1);
  assert.equal(packet.players[0].loadout[0][0], 23);
  assert.equal(packet.events.filter(event => event.type === 'shot').length, 1);
  match.step(1 + 2 / 30);
  assert.equal(unpackSnapshot(JSON.parse(packets[1])).events.filter(event => event.type === 'shot').length, 0);
});

test('tagged fire confirms exactly one shot and rejects an outdated loadout', () => {
  const match = new Match('NET', 'tdm', false);
  const { player, packets } = connect(match, 'pilot', NETWORK_VERSION);
  const request = { fireId: 1, spawn: player.spawn, weapon: 'pulse', yaw: -1, pitch: .2 };
  match.trigger(player, request, 1);
  match.trigger(player, request, 1.001);
  match.step(1.02);
  const first = unpackSnapshot(JSON.parse(packets[0]));
  assert.equal(first.players[0].loadout[0][0], 23);
  assert.deepEqual(first.events.filter(event => event.type === 'shot').map(event => event.fireId), [1]);
  match.trigger(player, { ...request, fireId: 2, spawn: player.spawn - 1 }, 1.2);
  match.step(1.21);
  const second = unpackSnapshot(JSON.parse(packets[1]));
  assert.equal(second.players[0].loadout[0][0], 23);
  assert.deepEqual(second.events.filter(event => event.type === 'trigger-rejected').map(event => event.fireId), [2]);
});

test('near-cooldown fire waits for authority with the original aim and alt-fire cost', () => {
  const match = new Match('NET', 'tdm', false);
  const { player, packets } = connect(match, 'pilot', NETWORK_VERSION);
  player.weapon = 'scatter';
  player.loadout.scatter = { ammo: 2, reserve: 0 };
  player.fireAt = 1.04;
  const request = { fireId: 1, spawn: player.spawn, weapon: 'scatter', yaw: -1, pitch: .2, alt: true };
  match.trigger(player, request, 1);
  match.step(1.01);
  assert.equal(player.loadout.scatter.ammo, 2);
  match.step(1.045);
  const packet = unpackSnapshot(JSON.parse(packets[1]));
  const shot = packet.events.find(event => event.type === 'shot');
  assert.equal(shot.fireId, 1);
  assert.equal(shot.alt, true);
  assert.equal(shot.ends.length, 1);
  assert(shot.ends[0][0] > shot.from[0]);
  assert.equal(player.loadout.scatter.ammo, 0);
  assert(Math.abs(player.fireAt - 1.955) < 1e-10);
});

test('only an authoritative depletion of a nonempty shield reports a break', () => {
  const match = new Match('NET', 'tdm', false);
  const attacker = match.addPlayer('a', 'A', 0, 0, 0);
  const victim = match.addPlayer('b', 'B', 1, 0, 0);
  victim.armor = 17;
  assert.equal(match.damage(attacker, victim, 20, 2, attacker, 'arc').shieldBreak, undefined);
  assert.equal(match.damage(attacker, victim, 20, 2.1, attacker, 'arc').shieldBreak, true);
  assert.equal(match.damage(attacker, victim, 20, 2.2, attacker, 'arc').shieldBreak, undefined);
});

test('remote playhead is monotonic under jitter and stops advancing in a network gap', () => {
  const clock = new SnapshotClock();
  const received = [[10, 1200], [43, 1235], [76, 1235], [109, 1306], [142, 1306]];
  const rendered = received.map(([server, local]) => {
    clock.receive(server, local);
    return clock.time(local);
  });
  assert(rendered.every((value, i) => !i || value >= rendered[i - 1]));
  assert.equal(clock.time(5000), 184);
  assert.equal(clock.receive(600, 5300), true);

  const history = [];
  const pose = { x: 0, y: 1, z: 3, yaw: 3.12, pitch: 0, vx: 10, vz: 0, grounded: true, vehicle: -1, turret: -1 };
  rememberPose(history, 10, pose);
  rememberPose(history, 43, { ...pose, x: 0.33, yaw: -3.12 });
  const out = {};
  assert(presentedPose(history, 26.5, out));
  assert(Math.abs(out.x - 0.165) < 1e-8);
  assert(Math.abs(out.yaw - Math.PI) < 1e-7);
  presentedPose(history, 900, out);
  assert(Math.abs(out.x - 0.75) < 1e-8);
});
