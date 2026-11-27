import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS } from '../public/rules.js';
import { cameraInCover, deathFrame, followFrame, nextAlly } from '../public/death-view.js';

test('spectating prefers living human teammates and never selects opponents', () => {
  const own = { id: 'dead', team: 0, hp: 0 };
  const players = [own, { id: 'ember', team: 1, hp: 150 }, { id: 'bot', team: 0, bot: true, hp: 10, slot: 0 },
    { id: 'friend', team: 0, bot: false, hp: 80, slot: 1 }];
  assert.equal(nextAlly(players, own, 'tdm').id, 'friend');
  assert.equal(nextAlly(players, own, 'tdm', 'friend').id, 'bot');
  assert.equal(nextAlly(players, own, 'tdm', 'bot').id, 'friend');
  assert.equal(nextAlly(players, own, 'ffa'), null);
  players.at(-1).hp = 0;
  assert.equal(nextAlly(players, own, 'tdm', 'friend').id, 'bot');
});

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
  const fixed = deathFrame(event, 0, true);
  assert.deepEqual(deathFrame(event, 0.91, true), fixed);
  assert.notDeepEqual(deathFrame(event, 0, false).camera, deathFrame(event, 1, false).camera);
});

test('the follow camera stays inside the arena boundary', () => {
  const edge = ARENAS.relay.length;
  const frame = followFrame({ x: 0, y: 0, z: edge - 1, yaw: 0, pitch: 0, crouch: false });
  const camera = cameraInCover(frame, { ...ARENAS.relay, cover: [] });
  assert(camera.z < edge && camera.z > edge - 1);
});
