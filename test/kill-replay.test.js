import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS } from '../shared/rules.js';

test('a lethal hitscan records its weapon and impact for an isolated replay', () => {
  const match = new Match('DEATH', 'tdm', false, { ...ARENAS.relay, cover: [] });
  const attacker = match.addPlayer('shooter', 'Shooter', 0, 0, 0);
  const victim = match.addPlayer('target', 'Target', 1, 0, 0);
  Object.assign(attacker, { x: 0, y: 0, z: 0, yaw: 0, pitch: -0.06 });
  Object.assign(victim, { x: 0, y: 0, z: -5, hp: 1, safeUntil: 0 });
  match.shoot(attacker, 2);
  const event = match.events.find(event => event.type === 'kill');
  assert.equal(event.weapon, 'pulse');
  assert.equal(event.attackerId, attacker.id);
  assert.equal(event.victimId, victim.id);
  assert(event.impact[2] < -4.4 && event.impact[2] > -5);
});
