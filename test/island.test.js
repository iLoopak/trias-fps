import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS, BODY_RADIUS, blocked, creekCenter, islandFord, movePlayer, riverCenter, wallDistance } from '../shared/rules.js';
import { ISLAND_CROSSINGS, ISLAND_SCALE as scale, ISLAND_TRAIL, islandHeight } from '../shared/island-terrain.js';

const island = ARENAS.island;

test('Kordilera keeps all match markers accessible inside a curved boundary', () => {
  for (const { x, y = 0, z } of [...island.spawns.flat(), ...island.bases, ...island.points, ...island.pickups]) {
    assert.equal(blocked(x, z, BODY_RADIUS, y, island), false, `blocked match marker at ${x}, ${z}`);
  }
  assert.equal(blocked(24 * scale, 24 * scale, BODY_RADIUS, 4, island), true);
  assert.equal(blocked(18 * scale, 18 * scale, BODY_RADIUS, 4, island), false);
  const diagonal = { x: Math.SQRT1_2, y: 0, z: Math.SQRT1_2 };
  assert(Math.abs(wallDistance({ x: 0, y: 16, z: 0 }, diagonal, 65, island) - island.edgeRadius) < 1e-10);
});

test('pilots can run the full ridge trail in both directions without needing to jump', () => {
  for (const route of [ISLAND_TRAIL, [...ISLAND_TRAIL].reverse()]) {
    const [startX, startZ] = route[0];
    const pilot = { x: startX, z: startZ, y: islandHeight(startX, startZ), grounded: true, airJump: true };
    let peak = pilot.y;
    for (const [x, z] of route.slice(1)) {
      for (let i = 0; i < 110 && Math.hypot(x - pilot.x, z - pilot.z) > 0.32; i++) {
        const yaw = Math.atan2(pilot.x - x, pilot.z - z);
        movePlayer(pilot, { x: 0, z: 1, yaw }, undefined, island);
        peak = Math.max(peak, pilot.y);
      }
      assert(Math.hypot(x - pilot.x, z - pilot.z) <= 0.32, `ridge trail blocked at ${x}, ${z}`);
    }
    assert(peak > 9);
  }
});

test('the hillside intercepts low shots, and a pilot lands on the hillside after a double jump', () => {
  const x = 23.9 * scale;
  const z = -7.7 * scale;
  const ground = islandHeight(x, z);
  assert.equal(wallDistance({ x, y: ground + 4, z }, { x: 0, y: -1, z: 0 }, 10, island), 4);
  const start = 14 * scale;
  const origin = { x: start, y: (islandHeight(start, z) + ground) / 2, z };
  const east = { x: 1, y: 0, z: 0 };
  const impact = wallDistance(origin, east, x - start, island);
  assert(impact > 2 && impact < x - start);
  assert(Math.abs(islandHeight(origin.x + impact, origin.z) - origin.y) < 1e-8);

  const pilot = { x, z, y: ground, grounded: true, airJump: true };
  const input = { x: 0, z: 0, yaw: 0, jump: true };
  assert.equal(movePlayer(pilot, input, undefined, island), 'jump');
  assert.equal(movePlayer(pilot, input, undefined, island), 'double');
  assert(pilot.y > ground);
  for (let i = 0; i < 90 && !pilot.grounded; i++) movePlayer(pilot, { ...input, jump: false }, undefined, island);
  assert.equal(pilot.y, ground);
  assert.equal(pilot.grounded, true);
});

test('the river and tributary slow pilots in water, with dry crossings over the river', () => {
  for (const x of ISLAND_CROSSINGS) assert.equal(islandFord(x, riverCenter(x)), false);
  assert.equal(islandFord(8 * scale, riverCenter(8 * scale)), true);
  assert.equal(islandFord(8 * scale, riverCenter(8 * scale) + 3 * scale), false);
  assert.equal(islandFord(creekCenter(-13 * scale), -13 * scale), true);
  assert.equal(islandFord(creekCenter(-13 * scale) + 2 * scale, -13 * scale), false);
});

test('flags can be taken and scored at their actual hillside elevations', () => {
  const match = new Match('ISLAND', 'ctf', false, island);
  const socket = { id: 'carrier' };
  assert(match.join(socket, 'Carrier', 0));
  const carrier = socket.player;
  const opponent = (carrier.team + 1) % island.bases.length;
  const target = island.bases[opponent];
  assert(target.y >= 1.25);
  Object.assign(carrier, target);
  match.updateFlags(1);
  assert.equal(match.flags[opponent].carrier, carrier.id);
  Object.assign(carrier, island.bases[carrier.team]);
  match.updateFlags(2);
  assert.equal(match.scores[carrier.team], 1);
  assert.equal(match.flags[opponent].y, target.y);
});

test('tree canopies leave deployment sights and the first steps unobstructed', () => {
  for (const [team, starts] of island.spawns.entries()) {
    const target = island.points[team];
    for (const start of starts) {
      const length = Math.hypot(target.x - start.x, target.z - start.z);
      for (const step of [0, 1, 2]) {
        const x = start.x + (target.x - start.x) * step / length;
        const z = start.z + (target.z - start.z) * step / length;
        assert(island.cover.every(box => box.nature !== 'tree' || Math.hypot(x - box.x, z - box.z) > 2),
          `canopy encroaches on team ${team} deployment at ${x}, ${z}`);
      }
    }
  }
});
