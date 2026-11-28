import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS, MED_HEAL, PILOT_HP, SIEGE_CORE_HP, TICK, WEAPONS } from '../shared/rules.js';

function roomWithPilots() {
  const match = new Match('SIEGE', 'siege', false);
  const pilots = ['ion', 'ember', 'violet'].map(id => {
    const socket = { id };
    assert(match.join(socket, id, 0));
    return socket.player;
  });
  return { match, siege: match.siege, pilots };
}

function capture(siege, pilot, index) {
  Object.assign(pilot, siege.layout.nodes[index]);
  for (let tick = 0; tick < 130; tick++) siege.step();
}

test('all three teams can build a route to the next core, with no third-party access', () => {
  for (let i = 0; i < 3; i++) {
    const { match, siege, pilots } = roomWithPilots();
    const attacker = pilots[i];
    const defender = (i + 1) % 3;
    const third = (i + 2) % 3;
    assert.equal(match.layout, ARENAS.basin);

    capture(siege, attacker, defender);
    assert.equal(siege.nodes[defender].owner, defender);
    capture(siege, attacker, 3 + i);
    capture(siege, attacker, defender);
    assert.equal(siege.cores[defender].opened, 1 << i);
    assert.equal(siege.damage(pilots[third], siege.cores[defender], 80, 6), null);
    assert.equal(siege.damage(attacker, siege.cores[defender], 80, 6).health, 80);

    siege.nodes[3 + i].owner = -1;
    siege.link();
    assert.equal(siege.cores[defender].opened, 0);
    assert.equal(siege.damage(attacker, siege.cores[defender], 80, 7), null);
    assert.equal(siege.cores[defender].hp, SIEGE_CORE_HP - 80);
  }
});

test('enemy infantry contests a relay and vehicle crews do not capture', () => {
  const { siege, pilots } = roomWithPilots();
  pilots[0].vehicle = 0;
  capture(siege, pilots[0], 3);
  assert.equal(siege.nodes[3].progress, 0);
  assert.equal(siege.nodes[3].owner, -1);
  pilots[0].vehicle = -1;
  Object.assign(pilots[1], siege.layout.nodes[3]);
  capture(siege, pilots[0], 3);
  assert(siege.nodes[3].contested);
  assert.equal(siege.nodes[3].owner, -1);
});

test('actual weapon hits respect the shield and destroying two cores ends and resets the round', () => {
  const { match, siege, pilots } = roomWithPilots();
  const attacker = pilots[0];
  const first = siege.cores[1];
  Object.assign(attacker, { x: first.x, y: 0, z: first.z - 2.3, yaw: Math.PI, pitch: -0.12 });
  match.shoot(attacker, 3);
  assert.equal(first.hp, SIEGE_CORE_HP);
  assert.equal(match.events.at(-1).ends[0][3], 'shield');

  siege.nodes[1].owner = siege.nodes[3].owner = 0;
  siege.link();
  match.shoot(attacker, 4);
  assert.equal(first.hp, SIEGE_CORE_HP - WEAPONS.pulse.damage);
  match.damage(attacker, first, 1000, 5, attacker);
  match.step(5 + TICK);
  assert.equal(pilots[1].hp, 0);
  assert.equal(match.winner, null);

  siege.nodes[2].owner = siege.nodes[5].owner = 0;
  siege.link();
  match.damage(attacker, siege.cores[2], 1000, 6, attacker);
  match.step(6 + TICK);
  assert.equal(match.winner.team, 0);
  match.step(match.restartAt + TICK);
  assert.equal(pilots[1].hp, PILOT_HP);
  assert.equal(siege.cores[1].hp, SIEGE_CORE_HP);
});

test('a healthy pilot survives nine Pulse hits and a med pack respects maximum health', () => {
  const { match, pilots } = roomWithPilots();
  const [attacker, target] = pilots;
  for (let index = 0; index < 9; index++) match.damage(attacker, target, WEAPONS.pulse.damage, 2 + index, attacker);
  assert(target.hp > 0);
  assert(match.damage(attacker, target, WEAPONS.pulse.damage, 11, attacker).eliminated);
  const med = match.layout.pickups.find(item => item.type === 'med');
  Object.assign(attacker, med, { hp: PILOT_HP - MED_HEAL + 1 });
  match.updatePickups(12);
  assert.equal(attacker.hp, PILOT_HP);
});
