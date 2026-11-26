import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { SIGNATURE } from '../signature.js';
import { ARMORY, WEAPONS } from '../public/rules.js';

function setup(flags) {
  const match = new Match('ECHO', 'tdm', false, undefined, flags);
  const player = match.addPlayer('pilot', 'Pilot', 0, 0, 0);
  player.signature = true;
  Object.assign(player, { x: 0, y: 0, z: -24, yaw: -Math.PI, pitch: 0 });
  return { match, player, signature: match.signature };
}

function dodge(match, player, time) {
  player.dodgeRequest = true;
  match.step(time);
  return match.signature.echoes.get(player.id);
}

const request = echo => ({ echo: echo.serial, yaw: -Math.PI, pitch: 0, alt: false });

test('only a successful dodge creates an echo at the pre-dodge pose, and a new echo replaces it', () => {
  const { match, signature, player } = setup();
  player.dodgeCooldown = .5;
  assert.equal(dodge(match, player, 1), undefined);
  player.dodgeCooldown = 0;
  const first = dodge(match, player, 1.1);
  assert.equal(first.z, -24);
  assert(player.z > first.z);
  Object.assign(player, { x: 0, y: 2, z: -24, grounded: false, airDodge: true, dodgeCooldown: 0 });
  const second = dodge(match, player, 1.2);
  assert(second.serial > first.serial);
  assert.equal(second.y, 2);
  assert.equal(signature.echoes.size, 1);
  signature.step(second.expires);
  assert.equal(signature.echoes.size, 0);
});

test('legacy peers stay compatible and feature flags are independent', () => {
  const { match, signature, player } = setup({ echo: false });
  assert.equal(dodge(match, player, 1), undefined);
  assert(signature.chains.get(player.id).score > 0);
  const second = setup({ momentum: false });
  assert(dodge(second.match, second.player, 1));
  assert.equal(second.signature.chains.size, 0);
  const third = setup();
  third.player.signature = false;
  assert.equal(dodge(third.match, third.player, 1), undefined);
});

test('all echo weapons consume normal ammo and cadence and Rift uses the stored origin', () => {
  for (const weapon of ARMORY) {
    const { match, signature, player } = setup();
    player.weapon = weapon;
    player.loadout[weapon] = { ammo: 3, reserve: 7 };
    const echo = dodge(match, player, 1);
    player.x = 2;
    const time = 1.3;
    const alt = weapon === 'scatter';
    assert(signature.fire(player, { ...request(echo), alt }, time), weapon);
    assert.equal(player.loadout[weapon].ammo, alt ? 1 : 2);
    assert.equal(player.fireAt, time + (alt ? WEAPONS.scatter.alt.interval : WEAPONS[weapon].interval));
    assert.equal(signature.echoes.size, 0);
    const shot = match.events.find(evt => evt.type === 'shot');
    assert.equal(shot.echo, echo.serial);
    assert.equal(shot.from[0], echo.x);
    assert.equal(shot.ends.length, weapon === 'rift' ? 0 : 1);
    if (weapon === 'rift') assert.deepEqual(match.projectiles[0].position, { x: echo.x, y: echo.y + 1.58, z: echo.z });
    assert.equal(signature.fire(player, request(echo), time + 2), false);
  }
});

test('echo activation checks ownership, ammo, cooldown, reload and obstruction', () => {
  const { match, signature, player } = setup();
  const echo = dodge(match, player, 1);
  const enemy = match.addPlayer('opponent', 'Opponent', 1, 0, 0);
  enemy.signature = true;
  assert.equal(signature.fire(enemy, request(echo), 1.2), false);
  player.fireAt = 2;
  assert.equal(signature.fire(player, request(echo), 1.2), false);
  player.fireAt = 0;
  player.reloadAt = 2;
  assert.equal(signature.fire(player, request(echo), 1.2), false);
  player.reloadAt = 0;
  player.loadout.pulse.ammo = 0;
  assert.equal(signature.fire(player, request(echo), 1.2), false);
  player.loadout.pulse.ammo = 3;
  echo.x = 30;
  assert.equal(signature.fire(player, request(echo), 1.2), false);
  assert.equal(player.loadout.pulse.ammo, 3);
});

test('shooting an enemy echo can destroy it but cannot score a hit, kill or momentum', () => {
  const { match, signature, player } = setup();
  const echo = dodge(match, player, 1);
  player.x = -7;
  const enemy = match.addPlayer('opponent', 'Opponent', 1, 0, 0, false, 'bulwark');
  Object.assign(enemy, { signature: true, weapon: 'lance', x: 0, y: 0, z: -27, yaw: -Math.PI, pitch: -.10, grounded: false });
  enemy.loadout.lance = { ammo: 4, reserve: 12 };
  match.shoot(enemy, 1.3);
  assert.equal(signature.echoes.has(player.id), false);
  assert.equal(enemy.kills, 0);
  assert.equal(signature.chains.has(enemy.id), false);
  assert.equal(match.events.find(evt => evt.type === 'shot').hits.length, 0);
  assert(match.events.some(evt => evt.type === 'echo-end' && evt.echo === echo.serial && evt.reason === 'rupture'));
});

test('an echo cannot shoot through geometry to hit hidden targets', () => {
  const { match, signature, player } = setup();
  Object.assign(player, { x: -3, z: 0, y: 0, grounded: true });
  const echo = dodge(match, player, 1);
  assert(echo);
  const enemy = match.addPlayer('target', 'Target', 1, 0, 0);
  Object.assign(enemy, { x: 6, y: 0, z: 0, safeUntil: 0 });
  assert(signature.fire(player, { ...request(echo), yaw: -Math.PI / 2 }, 1.25));
  assert.equal(enemy.hp, 150);
  assert.equal(match.events.find(evt => evt.type === 'shot').ends[0][3], 'wall');
});

test('echo requests deduplicate and lifecycle clears on death, reset and disconnect', () => {
  const { match, signature, player } = setup();
  const echo = dodge(match, player, 1);
  signature.request(player, { ...request(echo), action: 1 });
  signature.request(player, { ...request(echo), action: 1 });
  match.step(1.2);
  assert.equal(player.loadout.pulse.ammo, 23);
  assert.equal(player.echoRequest, null);
  Object.assign(player, { dodgeCooldown: 0, airDodge: true });
  assert(dodge(match, player, 2.3));
  const foe = match.addPlayer('foe', 'Foe', 1, 0, 0);
  match.damage(foe, player, 200, 2.4, foe, 'lance');
  assert.equal(signature.echoes.size, 0);
  match.spawn(player, 5);
  assert(dodge(match, player, 6));
  match.removePlayer(player.id, 6.1);
  assert.equal(signature.echoes.size, 0);
  foe.signature = true;
  assert(dodge(match, foe, 7));
  match.reset(8);
  assert.equal(signature.echoes.size, 0);
  assert.equal(signature.chains.size, 0);
});

test('momentum uses diversity, penalizes repetition and expires without changing gameplay stats', () => {
  const { signature, player, match } = setup();
  const before = [player.hp, player.armor, player.fireAt, player.vx, player.loadout.pulse.ammo];
  signature.action(player, 'sprint', 1);
  signature.action(player, 'sprint', 1.1);
  const chain = signature.chains.get(player.id);
  assert.equal(chain.score, SIGNATURE.actions.sprint);
  assert.equal(chain.last, 1);
  signature.action(player, 'airDodge', 1.2);
  assert(chain.multi > 1);
  const after = chain.score;
  signature.action(player, 'sprint', 1.3);
  assert(chain.score - after < SIGNATURE.actions.sprint);
  const enemy = match.addPlayer('target', 'Target', 1, 0, 0);
  signature.combat(player, { id: enemy.id, health: 25, absorbed: 0, eliminated: true }, 2, true);
  assert(chain.tier >= 2 && chain.echoKills === 1);
  assert.deepEqual([player.hp, player.armor, player.fireAt, player.vx, player.loadout.pulse.ammo], before);
  signature.step(2 + SIGNATURE.chainWindow);
  assert.equal(chain.score, 0);
  assert(chain.peak >= chain.done);
});
