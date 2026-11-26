import test from 'node:test';
import assert from 'node:assert/strict';
import { Match } from '../match.js';
import { PILOT_HP, TICK } from '../public/rules.js';

function enter(match, id) {
  const peer = { id, readyState: 0 };
  assert.equal(match.join(peer, id, 0), true);
  return peer.player;
}

test('Horde puts four human defenders on one team and scales and rewards eight waves', () => {
  const match = new Match('COOP', 'horde', true);
  const defenders = Array.from({ length: 4 }, (_, i) => enter(match, 'Pilot-' + i));
  assert.equal(match.join({ id: 'Extra' }, 'Extra', 0), false);
  assert.ok(defenders.every(pilot => pilot.team === 0 && !pilot.bot));
  assert.equal(match.players.size, 4);
  assert.equal(match.hostile(defenders[0], defenders[1]), false);
  assert.equal(match.horde.available({ type: 'rift' }), false);
  const variants = new Set();
  for (let tick = 1; tick <= 30_000 && !match.winner; tick++) {
    const now = tick * TICK;
    match.step(now);
    const active = match.horde.alive;
    assert.ok(active.length <= 8);
    for (const enemy of active) {
      variants.add(enemy.hordeType);
      assert.equal(match.hostile(defenders[0], enemy), true);
      if (active.length > 1) assert.equal(match.hostile(enemy, active.find(other => other !== enemy)), false);
      if (now - enemy.safeUntil > 1.1) {
        const pilot = defenders[0];
        match.damage(pilot, enemy, 1000, now, pilot, 'pulse');
      }
    }
  }
  assert.deepEqual(variants, new Set(['runner', 'striker', 'crusher', 'sentinel']));
  assert.equal(match.horde.wave, 8);
  assert.deepEqual(match.winner, { label: 'Jádro ubráněno' });
  assert.ok(defenders.every(pilot => pilot.loadout.rift?.reserve > 0));
});

test('unopposed enemies reach the core and end the round in defeat', () => {
  const match = new Match('LOSS', 'horde', false);
  enter(match, 'Pilot');
  for (let tick = 1; tick <= 6000 && !match.winner; tick++) match.step(tick * TICK);
  assert.equal(match.horde.core.hp, 0);
  assert.deepEqual(match.winner, { label: 'Jádro padlo' });
});

test('building spends shared combat salvage once, adds cover and clears it on destruction and reset', () => {
  const match = new Match('BUILD', 'horde', false);
  const pilot = enter(match, 'Pilot');
  const horde = match.horde;
  const center = horde.structures[1];
  const { x, z } = center;
  assert.ok(horde.build(pilot, center.id, 'barricade', 1), 'out of range');
  Object.assign(pilot, { x, z: z - 2 });
  const origin = { x, y: .65, z: z - 1.4 };
  const forward = { x: 0, y: 0, z: 1 };
  const shot = () => match.trace(pilot, origin, forward, 2).distance;
  assert.equal(shot(), 2);
  assert.equal(horde.build(pilot, center.id, 'barricade', 1), null);
  assert.ok(shot() < 1.1, 'barricade blocks shots');
  assert.equal(horde.scrap, 10);
  assert.ok(horde.build(pilot, center.id, 'barricade', 1), 'occupied socket');
  assert.equal(horde.scrap, 10);
  assert.ok(horde.build(pilot, 0, 'turret', 2), 'out of funds or range');
  horde.start(2);
  assert.ok(horde.build(pilot, 0, 'turret', 2), 'active wave rejects construction');
  horde.dispatch(2, []);
  const enemy = horde.alive[0];
  Object.assign(enemy, { x, y: 0, z: z + 1.5 });
  horde.input(enemy, 3);
  assert.ok(center.hp < center.maxHp, 'enemies attack nearby defenses');
  match.damage(pilot, enemy, 1000, 3, pilot, 'pulse');
  assert.equal(horde.scrap, 18);
  horde.damageStructure(center, center.hp);
  assert.equal(shot(), 2);
  assert.equal(horde.snapshot(4).build.sites[1].kind, null);
  match.reset(5);
  assert.equal(horde.scrap, 40);
  assert.ok(horde.structures.every(site => site.kind === null));
});

test('a sentry shoots visible enemies, earns shared salvage and leaves allies alone', () => {
  const match = new Match('SENTRY', 'horde', false);
  const pilot = enter(match, 'Pilot');
  const horde = match.horde;
  const center = horde.structures[1];
  const { x, z } = center;
  Object.assign(pilot, { x, z: z - 1.8 });
  horde.scrap = 65;
  assert.equal(horde.build(pilot, center.id, 'turret', 1), null);
  assert.equal(horde.scrap, 0);
  horde.start(2);
  horde.dispatch(2, []);
  const enemy = horde.alive[0];
  Object.assign(enemy, { x, y: 0, z: z + 1.75, hp: 15 });
  horde.fireTurrets(3, [enemy]);
  assert.equal(enemy.hp, 0);
  assert.equal(pilot.hp, PILOT_HP);
  assert.equal(horde.scrap, 8);
  assert.equal(match.events.some(event => event.type === 'tower-shot'), true);
});
