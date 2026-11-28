import test from 'node:test';
import assert from 'node:assert/strict';
import { Match } from '../match.js';
import { PILOT_HP } from '../shared/rules.js';

const origin = { x: 0, y: 1, z: -24 };

test('personal eliminations unlock each perk once and dying clears unspent charges', () => {
  const match = new Match('CHAIN', 'tdm', false);
  const pilot = match.addPlayer('pilot', 'Pilot', 0, 0, 0);
  const target = match.addPlayer('target', 'Target', 1, 0, 0);
  match.damage(pilot, target, 1000, 1, origin, 'rift', undefined, false);
  assert.equal(pilot.chain, 0);
  for (let i = 1; i <= 10; i++) {
    match.spawn(target, i);
    match.damage(pilot, target, 1000, i, origin, 'pulse');
  }
  assert.equal(pilot.perks, 15);
  assert.deepEqual(match.events.filter(event => event.type === 'perk-ready').map(event => event.perk), ['scan', 'module', 'strike', 'overdrive']);
  assert.equal(match.streaks.activate(pilot, { perk: 'scan' }, 12), null);
  assert.equal(match.streaks.snapshot(13).scans[0][0], '0');
  assert.ok(match.streaks.activate(pilot, { perk: 'scan' }, 13));
  match.damage(target, pilot, 1000, 13, origin, 'pulse');
  assert.equal(pilot.perks, 0);
  assert.equal(pilot.chain, 0);
  match.streaks.step(21);
  assert.equal(match.streaks.snapshot(21).scans.length, 0);
});

test('the class module protects nearby allies and overdrive adds shield and surge', () => {
  const match = new Match('MODULE', 'horde', false);
  const pilot = match.addPlayer('caller', 'Caller', 0, 0, 0, false, 'bulwark');
  const ally = match.addPlayer('ally', 'Ally', 0, 1, 0);
  const enemy = match.addPlayer('enemy', 'Enemy', 1, 0, 0);
  Object.assign(enemy, { x: ally.x, z: ally.z, hordeType: 'runner' });
  pilot.perks = 10;
  assert.equal(match.streaks.activate(pilot, { perk: 'module' }, 2), null);
  assert.equal(ally.armor, 40);
  assert.equal(enemy.armor, 0);
  assert.equal(match.streaks.activate(pilot, { perk: 'overdrive' }, 3), null);
  assert.equal(pilot.armor, 45);
  assert.equal(pilot.surgeUntil, 15);
});

test('orbital impact requires aim, gives advance warning and does not chain itself', () => {
  const match = new Match('ORBIT', 'tdm', false);
  const pilot = match.addPlayer('pilot', 'Pilot', 0, 0, 0);
  const ally = match.addPlayer('ally', 'Ally', 0, 1, 0);
  const target = match.addPlayer('enemy', 'Enemy', 1, 0, 0);
  Object.assign(pilot, { x: 0, z: -24, chain: 7, perks: 4 });
  assert.ok(match.streaks.activate(pilot, { perk: 'strike', yaw: Math.PI, pitch: 0 }, 2));
  assert.equal(pilot.perks, 4);
  assert.equal(match.streaks.activate(pilot, { perk: 'strike', yaw: Math.PI, pitch: -.35 }, 2), null);
  const [, x, y, z] = match.streaks.snapshot(2).strikes[0];
  Object.assign(target, { x, y, z, hp: 45 });
  Object.assign(ally, { x, y, z });
  match.streaks.step(4);
  assert.equal(target.hp, 45);
  match.streaks.step(4.2);
  assert.equal(target.hp, 0);
  assert.equal(ally.hp, PILOT_HP);
  assert.equal(pilot.chain, 7);
  assert.equal(match.streaks.snapshot(5).strikes.length, 0);
});
