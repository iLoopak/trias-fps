import test from 'node:test';
import assert from 'node:assert/strict';
import { Match } from '../match.js';
import { TICK } from '../public/rules.js';

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
