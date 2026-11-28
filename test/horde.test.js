import test from 'node:test';
import assert from 'node:assert/strict';
import { Match } from '../match.js';
import { packCover, PILOT_HP, TICK } from '../shared/rules.js';

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
      assert.equal(enemy.weapon, null);
      assert.deepEqual(enemy.loadout, {});
      assert.equal(match.hostile(defenders[0], enemy), true);
      if (active.length > 1) assert.equal(match.hostile(enemy, active.find(other => other !== enemy)), false);
      if (now - enemy.safeUntil > 1.1) {
        const pilot = defenders[0];
        match.damage(pilot, enemy, 1000, now, pilot, 'pulse');
      }
    }
  }
  assert.deepEqual(variants, new Set(['runner', 'spitter', 'crusher', 'thrower']));
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
  assert.equal(center.hp, center.maxHp, 'wind-up gives time to react');
  horde.input(enemy, 3.4);
  assert.ok(center.hp < center.maxHp, 'enemies claw nearby defenses');
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

test('zombies wind up a melee attack and a defender can dodge out of reach', () => {
  const match = new Match('DODGE-WAVE', 'horde', false);
  const defender = enter(match, 'Pilot');
  const horde = match.horde;
  horde.start(2);
  horde.dispatch(2, []);
  const zombie = horde.alive[0];
  Object.assign(zombie, { x: 0, y: 0, z: 0 });
  Object.assign(defender, { x: 0, y: 0, z: -1.2, safeUntil: 0 });
  horde.layout = { ...horde.layout, cover: [] };
  horde.input(zombie, 3);
  assert.equal(match.events.at(-1).type, 'horde-windup');
  assert.equal(defender.hp, PILOT_HP);
  defender.x = 2.8;
  horde.input(zombie, 3.4);
  assert.equal(defender.hp, PILOT_HP);
  defender.x = 0;
  horde.input(zombie, 4.5);
  horde.input(zombie, 4.9);
  assert.equal(defender.hp, PILOT_HP - 17);
  assert.equal(match.events.at(-1).weapon, 'claw');
});

test('rare unarmed ranged variants launch dodgeable attacks that can strike the core', () => {
  for (const [variant, remaining, key] of [['spitter', 7, 'spore'], ['thrower', 9, 'rubble']]) {
    const match = new Match('RANGED-WAVE', 'horde', false);
    const defender = enter(match, 'Pilot');
    const horde = match.horde;
    const core = horde.core;
    horde.wave = 5;
    horde.remaining = remaining;
    horde.dispatch(2, []);
    const zombie = horde.alive[0];
    assert.equal(zombie.hordeType, variant);
    Object.assign(zombie, { x: core.x, y: 0, z: core.z + 7 });
    Object.assign(defender, { x: core.x, y: 0, z: core.z + 3 });
    horde.layout = { ...horde.layout, cover: [] };
    horde.input(zombie, 3);
    horde.input(zombie, 3.9);
    assert.equal(match.projectiles[0]?.weapon, key);
    assert.equal(match.projectiles[0]?.hordeAttack, true);
    defender.x += 3;
    for (let i = 0; i < 50 && match.projectiles.length; i++) match.updateProjectiles(3.9 + i * TICK);
    assert(core.hp < core.maxHp, variant);
    assert.equal(defender.hp, PILOT_HP, variant);
  }
});

test('solid cover blocks airborne infected attacks even if the core was visible at launch', () => {
  const match = new Match('COVER-WAVE', 'horde', false);
  const defender = enter(match, 'Pilot');
  const horde = match.horde;
  const core = horde.core;
  horde.wave = 5;
  horde.remaining = 9;
  horde.dispatch(2, []);
  const zombie = horde.alive[0];
  Object.assign(zombie, { x: core.x, y: 0, z: core.z + 7 });
  Object.assign(defender, { x: core.x - 6, y: 0, z: core.z });
  horde.layout = { ...horde.layout, cover: [] };
  horde.input(zombie, 3);
  horde.input(zombie, 3.9);
  assert.equal(match.projectiles[0]?.weapon, 'rubble');
  horde.layout.cover = packCover([{ x: core.x, z: core.z + 3.5, w: 4, d: .4, h: 4 }]);
  for (let i = 0; i < 50 && match.projectiles.length; i++) match.updateProjectiles(3.9 + i * TICK);
  assert.equal(core.hp, core.maxHp);
  assert.equal(match.events.at(-1).type, 'horde-impact');
  assert.equal(match.events.at(-1).objective, false);
});

test('defenders can build and repair during a wave, but not with infected at the socket', () => {
  const match = new Match('WAVEBUILD', 'horde', false);
  const pilot = enter(match, 'Pilot');
  const horde = match.horde;
  const site = horde.structures[1];
  Object.assign(pilot, { x: site.x, z: site.z - 2 });
  horde.scrap = 200;
  horde.start(2);
  horde.dispatch(2, []);
  const enemy = horde.alive[0];
  Object.assign(enemy, { x: site.x + 1, y: 0, z: site.z + 1 });
  assert.equal(horde.build(pilot, site.id, 'barricade', 3), 'Nepřátelé jsou u stavebního místa.');
  Object.assign(enemy, { x: site.x + 20, z: site.z + 20 });
  assert.equal(horde.build(pilot, site.id, 'barricade', 3), null);
  assert.equal(horde.scrap, 170);
  assert.equal(horde.build(pilot, site.id, 'repair', 4), 'Obrana je v pořádku.');
  horde.damageStructure(site, 150);
  assert.ok(match.events.some(event => event.type === 'structure-hit' && event.site === site.id));
  assert.equal(horde.build(pilot, site.id, 'repair', 4), null);
  assert.equal(site.hp, 150);
  assert.equal(horde.scrap, 155);
  horde.scrap = 5;
  assert.equal(horde.build(pilot, site.id, 'repair', 5), 'Týmu chybí šrot.');
});

test('ready defenders cut the preparation, and a cleared wave pays salvage', () => {
  const match = new Match('READY', 'horde', false);
  const first = enter(match, 'First');
  const second = enter(match, 'Second');
  const horde = match.horde;
  const start = horde.nextWaveAt;
  assert.ok(horde.markReady(first, 1));
  assert.equal(horde.nextWaveAt, start, 'one of two ready keeps the timer');
  assert.ok(horde.markReady(first, 1.5));
  assert.ok(horde.markReady(first, 2));
  assert.ok(horde.markReady(second, 2));
  assert.equal(horde.nextWaveAt, 5, 'both ready: wave in three seconds');
  assert.deepEqual(new Set(horde.snapshot(2).ready.ids), new Set([first.id, second.id]));
  horde.start(5);
  assert.equal(horde.ready.size, 0);
  assert.equal(horde.markReady(first, 6), false, 'no readiness during a wave');
  const scrap = horde.scrap;
  horde.remaining = 0;
  horde.step(7);
  assert.equal(horde.scrap, scrap + 15);
  assert.ok(match.events.some(event => event.type === 'notice' && event.text.includes('+15 šrotu')));
});
