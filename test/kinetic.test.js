import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { Match } from '../match.js';
import { NETWORK_VERSION, packSnapshot, unpackSnapshot } from '../shared/netcode.js';
import { DODGE_COOLDOWN, KINETIC_GRACE, movePlayer, packCover, RELAY_SERVICE, TICK } from '../shared/rules.js';

const FLAT = { ...RELAY_SERVICE, id: 'flat', length: 400, cover: [], launchPads: [] };
const LEDGE = { ...FLAT, cover: packCover([{ x: 0, z: 0, w: 12, d: 400, bottom: 0, h: 2.4 }]) };
const pilot = extra => ({ x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, grounded: true, airJump: true, airDodge: true,
  dodgeCooldown: 0, dodgeTime: 0, padCooldown: 0, crouch: false, ...extra });
const input = extra => ({ x: 0, z: 0, yaw: 0, sprint: false, jump: false, dodge: false, crouch: false, ...extra });
const run = (state, ticks, script, layout = FLAT) => Array.from({ length: ticks }, (_, tick) => movePlayer(state, script(tick, state), TICK, layout));

function landingTick() {
  const signals = run(pilot(), 80, tick => input({ jump: tick === 0 || tick === 12 }));
  const state = pilot();
  run(state, 13, tick => input({ jump: tick === 0 || tick === 12 }));
  let tick = 13;
  while (!state.grounded) { movePlayer(state, input(), TICK, FLAT); tick++; }
  assert.equal(signals.filter(Boolean).length >= 2, true);
  return tick;
}

test('a jump pressed up to the grace window before landing becomes one ground jump, and no earlier press does', () => {
  const possible = landingTick();
  for (let early = 1; early <= KINETIC_GRACE + 1; early++) {
    const signals = run(pilot(), possible + 15, tick => input({ jump: tick === 0 || tick === 12 || tick === possible - early }));
    const after = signals.slice(possible - early);
    const jumps = after.filter(signal => signal === 'jump').length;
    assert.equal(jumps, early <= KINETIC_GRACE ? 1 : 0, `pressed ${early} ticks early`);
    assert(!after.includes('double'), 'a late press never spends a double jump that does not exist');
  }
});

test('walking off a ledge keeps a ground jump for the grace window and the double jump after it', () => {
  for (let late = 0; late <= KINETIC_GRACE; late++) {
    const state = pilot({ x: -5.4, y: 2.4 });
    let left = -1;
    const signals = run(state, 40, (tick, now) => {
      if (left < 0 && !now.grounded) left = tick;
      return input({ x: -1, jump: left >= 0 && tick === left + late });
    }, LEDGE);
    const jump = signals.find(signal => signal === 'jump' || signal === 'double');
    assert.equal(jump, late < KINETIC_GRACE ? 'jump' : 'double', `jump ${late} ticks after leaving`);
  }
  const state = pilot({ x: -5.4, y: 2.4 });
  let left = -1, signals = [];
  for (let tick = 0; tick < 30; tick++) {
    if (left < 0 && !state.grounded) left = tick;
    signals.push(movePlayer(state, input({ x: -1, jump: left >= 0 && (tick === left + 1 || tick === left + 9) }), TICK, LEDGE));
  }
  assert.deepEqual(signals.filter(s => s === 'jump' || s === 'double'), ['jump', 'double']);
});

test('a buffered dodge fires on the first ready tick and never before the cooldown ends', () => {
  const ready = Math.ceil(DODGE_COOLDOWN / TICK - 1e-9);
  for (let early = 1; early <= KINETIC_GRACE + 1; early++) {
    const signals = run(pilot(), ready + 10, tick => input({ z: 1, dodge: tick === 0 || tick === ready - early }));
    const dodges = signals.map((signal, tick) => signal === 'dodge' ? tick : -1).filter(tick => tick >= 0);
    if (early <= KINETIC_GRACE) assert.deepEqual(dodges, [0, ready], `pressed ${early} ticks early`);
    else assert.deepEqual(dodges, [0], `pressed ${early} ticks early`);
  }
});

test('one press is at most one action: launch pads and dodges clear buffered presses', () => {
  const pad = { x: 0, y: 0, z: -3, radius: 1, vx: 0, vz: -5 };
  const layout = { ...FLAT, launchPads: [pad] };
  // A jump pressed just before touching down on a pad: the pad launches and the press is spent, so it
  // cannot become a jump or a double jump once the pad has restored the air jump.
  const flying = pilot({ z: -3, y: 0.05, vy: -1, grounded: false, airJump: false });
  const after = run(flying, 30, tick => input({ jump: tick === 0 }), layout);
  assert.equal(after[1], 'launch');
  assert(!after.includes('double') && !after.includes('jump'));
  assert.equal(flying.jumpBuffer, 0);
  // Dodge then jump buffer: the dodge clears an older buffered jump.
  const airborne = pilot({ y: 5, grounded: false, airJump: false });
  const mixed = run(airborne, 40, tick => input({ z: 1, jump: tick === 0, dodge: tick === 1 }));
  assert.equal(mixed.filter(s => s === 'jump').length, 0);
});

test('air carry steers without braking, never adds speed, and leaves the reach of letting go unchanged', () => {
  let random = 7;
  const next = () => ((random = (random * 1103515245 + 12345) % 2147483648) / 2147483648);
  for (let trial = 0; trial < 200; trial++) {
    const state = pilot({ y: 30, grounded: false, vx: (next() - 0.5) * 36, vz: (next() - 0.5) * 36 });
    for (let tick = 0; tick < 20; tick++) {
      const before = Math.hypot(state.vx, state.vz);
      movePlayer(state, input({ x: Math.round(next() * 2 - 1), z: Math.round(next() * 2 - 1), yaw: next() * 6, sprint: next() < 0.5 }), TICK, FLAT);
      assert(Math.hypot(state.vx, state.vz) <= Math.max(before, 10.1) + 1e-9);
    }
  }
  const reach = hold => {
    const state = pilot();
    for (let tick = 0; tick < 60; tick++) {
      movePlayer(state, input({ z: tick <= 6 || hold ? 1 : 0, jump: tick === 0, dodge: tick === 6 }), TICK, FLAT);
      if (tick > 6 && state.grounded) break;
    }
    return -state.z;
  };
  assert(Math.abs(reach(true) - reach(false)) < 1e-6, 'holding forward reaches as far as letting go, no farther');
  // Pulling back still brakes hard in the air.
  const braking = pilot({ y: 20, grounded: false, vz: -16 });
  movePlayer(braking, input({ z: -1 }), TICK, FLAT);
  assert(Math.hypot(braking.vx, braking.vz) < 16 - 0.5);
});

test('grace ticks travel in the snapshot and positions keep millimetres', () => {
  const player = { id: 'p', name: 'p', team: 0, slot: 0, character: 'vector', hp: 1, armor: 0, surge: 0, weapon: 'pulse',
    loadout: [], kills: 0, deaths: 0, spawn: 1, ack: 0, turret: -1, vehicle: -1, grenades: 1, x: 1.2345, y: 0, z: -3.4567,
    vx: 0, vy: 0, vz: 0, yaw: 0, pitch: 0, dodgeCooldown: 0, dodgeTime: 0, padCooldown: 0, respawn: 0, reloadRemaining: 0,
    grounded: true, airJump: true, jumpBuffer: 2, dodgeBuffer: 3, coyote: 1 };
  const row = packSnapshot({ players: [player], vehicles: [], projectiles: [] }, 0, 0).players[0];
  const read = unpackSnapshot({ v: 2, players: [row], vehicles: [], projectiles: [] }).players[0];
  assert.deepEqual([read.jumpBuffer, read.dodgeBuffer, read.coyote, read.grounded, read.airJump], [2, 3, 1, true, true]);
  const match = new Match('NET', 'tdm', false, FLAT);
  const packets = [];
  const socket = { id: 's', protocolVersion: NETWORK_VERSION, readyState: 1, bufferedAmount: 0, send: m => packets.push(m), terminate() {} };
  match.join(socket, 'S', 0);
  Object.assign(socket.player, { x: 1.2344, z: -3.4566 });
  match.step(TICK);
  const own = unpackSnapshot(JSON.parse(packets[0])).players.find(p => p.id === socket.player.id);
  assert.equal(own.x, 1.234);
  assert.equal(own.z, -3.457);
});

function human(layout = FLAT) {
  const match = new Match('QUEUE', 'tdm', false, layout);
  const packets = [];
  const socket = { id: 'q', protocolVersion: NETWORK_VERSION, readyState: 1, bufferedAmount: 0, send: m => packets.push(m), terminate() {} };
  match.join(socket, 'Q', 0);
  Object.assign(socket.player, { x: 0, z: 0, y: 0, yaw: 0, safeUntil: 0 });
  return { match, player: socket.player };
}

const command = (seq, extra = {}) => ({ seq, x: 0, z: 1, yaw: 0, pitch: 0, sprint: false, ...extra });

test('two commands arriving in one tick are simulated on consecutive ticks, in order', () => {
  const { match, player } = human();
  let now = 0;
  for (let seq = 1; seq <= 3; seq++) { match.input(player, command(seq), now); match.step(now += TICK); }
  const reference = pilot({ x: player.x, z: player.z, vx: player.vx, vz: player.vz });
  match.input(player, command(4), now);
  match.input(player, command(5, { jump: true }), now);
  match.step(now += TICK);
  assert.equal(player.ack, 4);
  movePlayer(reference, input({ z: 1 }), TICK, FLAT);
  assert(Math.abs(player.z - reference.z) < 1e-9);
  match.input(player, command(6), now);
  match.step(now += TICK);
  assert.equal(player.ack, 5);
  assert(player.vy > 0, 'the jump from command 5 happened');
});

test('a late command holds the pilot, the repay never exceeds the ticks elapsed, and 60 Hz input is not faster', () => {
  const { match, player } = human();
  let now = 0, seq = 0;
  for (let i = 0; i < 6; i++) { match.input(player, command(++seq), now); match.step(now += TICK); }
  const z = player.z;
  match.step(now += TICK);
  assert.equal(player.z, z, 'held while the next command is late');
  match.input(player, command(++seq), now);
  match.input(player, command(++seq), now);
  match.input(player, command(++seq), now);
  match.step(now += TICK);
  assert.equal(player.ack, seq - 1, 'repays the held tick with a second command and keeps one in reserve');

  const steady = human(), flood = human();
  let a = 0, b = 0, t = 0;
  for (let tick = 0; tick < 90; tick++) {
    steady.match.input(steady.player, command(++a, { sprint: true }), t);
    flood.match.input(flood.player, command(++b, { sprint: true }), t);
    flood.match.input(flood.player, command(++b, { sprint: true }), t + TICK / 2);
    steady.match.step(t += TICK);
    flood.match.step(t);
  }
  assert(Math.abs(flood.player.z) <= Math.abs(steady.player.z) + 1e-6, 'twice the commands never means twice the distance');
});

test('commands from a dead pilot are acknowledged and do not replay after respawn', () => {
  const { match, player } = human();
  match.input(player, command(1), 0);
  match.step(TICK);
  player.hp = 0;
  player.respawnAt = 99;
  for (let seq = 2; seq < 12; seq++) match.input(player, command(seq, { jump: true }), seq * TICK);
  match.step(12 * TICK);
  assert.equal(player.ack, 11);
  assert.equal(player.inputQueue.length, 0);
});

const dotnet = spawnSync('dotnet', ['--version'], { encoding: 'utf8' }).status === 0;
test('the Unity Motor matches the rules step for step from real snapshot rows', { skip: dotnet ? false : 'needs the .NET 8 SDK', timeout: 240_000 }, () => {
  const output = execFileSync('node', ['tools/kinetic-parity/run.mjs'], { encoding: 'utf8', env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' } });
  assert.match(output, /PARITY steps=\d+ mismatches=0/);
  assert.match(output, /VIEW frame-rate independent: yes/);
});
