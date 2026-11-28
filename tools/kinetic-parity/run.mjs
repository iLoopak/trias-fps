#!/usr/bin/env node
// Movement parity between the authoritative rules (shared/rules.js movePlayer) and the Unity client's Motor
// (TriasMotor.cs). Every step starts from a wire row packed by the real packSnapshot, so the Motor also reads
// the snapshot exactly as the client does (including the kinetic grace bits).
//
//   node tools/kinetic-parity/run.mjs        needs the .NET 8 SDK (`dotnet`); exits 0 when the Motor matches
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ARENAS, movePlayer, packCover, RELAY_SERVICE, TICK } from '../../shared/rules.js';
import { packSnapshot, unpackSnapshot } from '../../shared/netcode.js';

const here = dirname(fileURLToPath(import.meta.url));
const MOTION = ['x', 'y', 'z', 'vx', 'vy', 'vz', 'grounded', 'airJump', 'airDodge', 'dodgeCooldown', 'dodgeTime',
  'padCooldown', 'crouch', 'jumpBuffer', 'dodgeBuffer', 'coyote'];
const FLAT = { ...RELAY_SERVICE, id: 'lab-flat', length: 400, cover: [], launchPads: [] };
const LEDGE = { ...FLAT, id: 'lab-ledge', cover: packCover([{ x: 0, z: 0, w: 12, d: 400, bottom: 0, h: 2.4 }]) };
const LAYOUTS = { flat: FLAT, ledge: LEDGE, service: RELAY_SERVICE, relay: ARENAS.relay, foundry: ARENAS.foundry, basin: ARENAS.basin };

const arenaJson = layout => ({
  id: layout.id, length: layout.length, edgeRadius: layout.edgeRadius ?? 0, fallY: layout.fallY ?? 0, walkways: Boolean(layout.walkways),
  cover: layout.cover.map(({ x, y = 0, z, w, d, h, bottom, angle = 0, structure }) => ({ x, y, z, w, d, h, bottom, angle, structure })),
  launchPads: layout.launchPads.map(({ x, y = 0, z, radius, vx, vz }) => ({ x, y, z, radius, vx, vz })),
});

function row(state) {
  const player = { id: 'p', name: 'p', team: 0, slot: 0, character: 'vector', hp: 1, armor: 0, surge: 0, weapon: 'pulse',
    loadout: [], kills: 0, deaths: 0, spawn: 1, ack: 0, turret: -1, vehicle: -1, grenades: 1, yaw: 0, pitch: 0,
    respawn: 0, reloadRemaining: 0, bot: false, sprint: false, reload: false, shield: false, ...state };
  return packSnapshot({ players: [player], vehicles: [], projectiles: [] }, 0, 0).players[0];
}

const bits = input => (input.crouch ? 1 : 0) | (input.sprint ? 2 : 0) | (input.jump ? 32 : 0) | (input.dodge ? 64 : 0);

function scenario(name, arena, start, ticks, script) {
  const layout = LAYOUTS[arena];
  const state = { x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, grounded: true, airJump: true, airDodge: true, dodgeCooldown: 0,
    dodgeTime: 0, padCooldown: 0, crouch: false, jumpBuffer: 0, dodgeBuffer: 0, coyote: 0, ...start };
  const steps = [];
  for (let tick = 0; tick < ticks; tick++) {
    const input = { x: 0, z: 0, yaw: 0, sprint: false, jump: false, dodge: false, crouch: false, ...script(tick, state) };
    input.yaw = Math.round(input.yaw * 1000) / 1000;
    const packed = row(state);
    const decoded = unpackSnapshot({ v: 2, players: [packed], vehicles: [], projectiles: [] }).players[0];
    const expected = Object.fromEntries(MOTION.map(key => [key, decoded[key]]));
    const signal = movePlayer(expected, input, TICK, layout) ?? null;
    steps.push({ row: packed, command: { bits: bits(input), x: input.x, z: input.z, yaw: input.yaw }, expected: { ...expected, signal } });
    movePlayer(state, input, TICK, layout);
    if (layout.fallY !== undefined && state.y < layout.fallY) break;
  }
  return { name, arena, steps };
}

function mulberry32(seed) {
  return () => {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

function fuzz(arena, seed, ticks = 240) {
  const random = mulberry32(seed);
  const layout = LAYOUTS[arena];
  const spawn = layout.spawns[seed % 3][seed % 4];
  let x = 0, z = 1, yaw = random() * 6.28, sprint = true, crouch = false;
  return scenario(`${arena}-fuzz-${seed}`, arena, { x: spawn.x, y: spawn.y ?? 0, z: spawn.z }, ticks, () => {
    if (random() < 0.12) { x = Math.round(random() * 2 - 1); z = Math.round(random() * 2 - 1); }
    if (random() < 0.05) sprint = !sprint;
    if (random() < 0.03) crouch = !crouch;
    yaw += (random() - 0.5) * 0.4;
    return { x, z, yaw, sprint, crouch, jump: random() < 0.07, dodge: random() < 0.05 };
  });
}

const cases = [
  scenario('flat-ground', 'flat', {}, 60, tick => ({ z: tick < 20 ? 1 : tick < 30 ? 0 : -1, x: tick > 40 ? (tick % 8 < 4 ? 1 : -1) : 0, sprint: tick < 15 })),
  scenario('flat-jumps', 'flat', {}, 90, tick => ({ z: 1, sprint: true, jump: [0, 12, 37, 38, 39].includes(tick), dodge: [45, 50, 70].includes(tick) })),
  scenario('flat-air-carry', 'flat', {}, 60, tick => ({ z: tick <= 6 ? 1 : tick < 20 ? 0 : -1, x: tick > 10 && tick < 30 ? 1 : 0, jump: tick === 0, dodge: tick === 6 })),
  scenario('flat-crouch', 'flat', {}, 50, tick => ({ z: 1, crouch: tick < 20 || tick > 35, jump: tick === 5, dodge: tick === 18 })),
  scenario('ledge-coyote', 'ledge', { x: 5.4, y: 2.4 }, 40, (tick, now) => ({ x: -1, jump: !now.grounded && tick % 2 === 0 })),
  // Off the deck into the Relay shaft (no floor until death below -14), and down into the Vanta channel.
  scenario('relay-shaft-fall', 'relay', { x: -15, z: -8 }, 80, tick => ({ x: 1, sprint: true, dodge: tick === 5, jump: tick === 24 })),
  scenario('foundry-channel-drop', 'foundry', { x: -7, z: -12 }, 50, tick => ({ x: 1, sprint: tick < 20, jump: tick === 30 })),
  ...[1, 2, 3, 4, 5, 6].map(seed => fuzz('service', seed)),
  ...[7, 8].map(seed => fuzz('relay', seed)),
  ...[9, 10].map(seed => fuzz('foundry', seed)),
  ...[11, 12].map(seed => fuzz('basin', seed)),
];

const arenas = Object.fromEntries(Object.entries(LAYOUTS).map(([key, layout]) => [key, arenaJson(layout)]));
const work = mkdtempSync(join(tmpdir(), 'trias-parity-'));
const file = join(work, 'cases.json');
writeFileSync(file, JSON.stringify({ arenas, cases }));
try {
  const output = execFileSync('dotnet', ['run', '--project', join(here, 'Parity.csproj'), '-c', 'Release', '--', file],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  process.stdout.write(output);
} catch (error) {
  process.stdout.write(error.stdout ?? '');
  process.stderr.write(error.stderr ?? String(error));
  process.exitCode = 1;
} finally {
  rmSync(work, { recursive: true, force: true });
}
