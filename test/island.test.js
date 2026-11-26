import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS, BODY_RADIUS, blocked, creekCenter, islandFord, movePlayer, riverCenter, wallDistance } from '../public/rules.js';
import { ISLAND_TRAIL, islandHeight } from '../public/island-terrain.js';

const island = ARENAS.island;

test('Mechový ostrov keeps all match markers accessible inside a curved boundary', () => {
  for (const { x, y = 0, z } of [...island.spawns.flat(), ...island.bases, ...island.points, ...island.pickups]) {
    assert.equal(blocked(x, z, BODY_RADIUS, y, island), false, `blocked match marker at ${x}, ${z}`);
  }
  assert.equal(blocked(24, 24, BODY_RADIUS, 4, island), true);
  assert.equal(blocked(18, 18, BODY_RADIUS, 4, island), false);
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
  const x = 23.9;
  const z = -7.7;
  const ground = islandHeight(x, z);
  assert.equal(wallDistance({ x, y: ground + 4, z }, { x: 0, y: -1, z: 0 }, 10, island), 4);
  const origin = { x: 14, y: 6, z: -7 };
  const east = { x: 1, y: 0, z: 0 };
  const impact = wallDistance(origin, east, 12, island);
  assert(impact > 2 && impact < 10);
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
  assert.equal(islandFord(0, riverCenter(0)), false);
  assert.equal(islandFord(-16, riverCenter(-16)), false);
  assert.equal(islandFord(15, riverCenter(15)), false);
  assert.equal(islandFord(8, riverCenter(8)), true);
  assert.equal(islandFord(8, riverCenter(8) + 3), false);
  assert.equal(islandFord(creekCenter(-13), -13), true);
  assert.equal(islandFord(creekCenter(-13) + 2, -13), false);
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
