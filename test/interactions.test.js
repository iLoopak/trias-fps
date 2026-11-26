import assert from 'node:assert/strict';
import { test } from 'node:test';
import { explosionImpulse, interactionFlags } from '../interactions.js';
import { Match } from '../match.js';
import { ARENAS, eyeHeight, packCover, TICK, WEAPONS } from '../public/rules.js';
import { NETWORK_VERSION } from '../public/netcode.js';

const time = 10;

function setup(interactions = {}, mode = 'tdm', layout = {}) {
  const arena = { ...ARENAS.relay, launchPads: [], ...layout, cover: packCover(layout.cover ?? []) };
  const match = new Match('W6', mode, false, arena, { interactions });
  const pilot = match.addPlayer('pilot', 'Pilot', 0, 0, 0);
  const rival = match.addPlayer('rival', 'Rival', 1, 0, 0);
  Object.assign(pilot, { x: 0, y: 0, z: 0, yaw: 0, pitch: 0, safeUntil: 0, grounded: true, signature: true });
  Object.assign(rival, { x: 9, y: 0, z: -6, safeUntil: 0 });
  return { match, pilot, rival, signature: match.signature };
}

function grenade(fixture, owner = fixture.rival, z = -6) {
  const { match, pilot } = fixture;
  match.throwGrenade(owner, time);
  const projectile = match.projectiles.at(-1);
  Object.assign(projectile.position, { x: 0, z, y: eyeHeight(pilot) });
  return projectile;
}

function makeEcho({ pilot, signature }) {
  signature.dodge(pilot, { ...pilot }, time);
  const echo = signature.echoes.get(pilot.id);
  pilot.z = 4;
  return echo;
}

function recall({ pilot, signature }, echo, action = 1, at = time + .4) {
  signature.request(pilot, { type: 'echo-swap', echo: echo.serial, action });
  const request = pilot.echoRequest;
  pilot.echoRequest = null;
  return signature.fire(pilot, request, at);
}

test('W6 flags default off and the v2 snapshot advertises only the enabled room configuration', () => {
  const off = setup();
  const packets = [];
  const peer = { id: 'peer', protocolVersion: NETWORK_VERSION, readyState: 1, bufferedAmount: 0, send: data => packets.push(JSON.parse(data)) };
  off.match.humans.add(peer);
  off.match.broadcast(time);
  assert.equal(packets.at(-1).interactions, undefined);
  const flags = interactionFlags({ TRIAS_SHIELD_BRACE: '1', TRIAS_GRENADE_INTERCEPT: 'true' });
  assert.deepEqual(flags, { intercept: false, recall: false, brace: true, traversal: false });
  const on = setup(flags);
  on.match.humans.add(peer);
  on.match.broadcast(time);
  assert.deepEqual(packets.at(-1).interactions, flags);
  grenade(off);
  off.match.shoot(off.pilot, time);
  assert.equal(off.match.projectiles.length, 1);
  assert.equal(recall(off, makeEcho(off)), false);
});

test('hitscan pellets intercept a hostile grenade once and preserve its owner, ammo and style rules', () => {
  const f = setup({ intercept: true, brace: true });
  const { match, pilot, rival, signature } = f;
  const ally = match.addPlayer('ally', 'Ally', 0, 1, 0);
  Object.assign(ally, { x: 1, y: 0, z: -6, armor: 40, safeUntil: 0 });
  Object.assign(pilot, { weapon: 'scatter', grounded: false, lastDodge: time - .2 });
  pilot.loadout.scatter = { ammo: WEAPONS.scatter.magazine, reserve: 7 };
  const grenadeFuse = grenade(f).fuseAt;
  match.shoot(pilot, time + .02, true);
  assert.equal(match.projectiles.length, 0);
  assert.equal(match.events.filter(evt => evt.type === 'intercept').length, 1);
  const burst = match.events.filter(evt => evt.type === 'burst');
  assert.equal(burst.length, 1);
  assert.equal(burst[0].id, rival.id);
  assert.equal(burst[0].hits.find(hit => hit.id === ally.id).braced, true);
  assert.deepEqual(pilot.loadout.scatter, { ammo: WEAPONS.scatter.magazine - WEAPONS.scatter.alt.cost, reserve: 7 });
  assert.deepEqual(signature.chains.get(pilot.id).recent, ['intercept']);
  match.updateProjectiles(grenadeFuse + TICK);
  assert.equal(match.events.filter(evt => evt.type === 'burst').length, 1);
});

test('grenade interception respects intervening targets and walls, teams, fuse and weapon type', () => {
  for (const reason of ['wall', 'body', 'own', 'allied', 'fuse', 'rocket']) {
    const cover = reason === 'wall' ? [{ x: 0, z: -3, w: 1, d: 1, h: 2, angle: 0 }] : [];
    const f = setup({ intercept: true }, 'tdm', { cover });
    const { match, pilot, rival } = f;
    if (reason === 'body') Object.assign(rival, { x: 0, z: -3 });
    if (reason === 'allied') rival.team = pilot.team;
    const projectile = grenade(f, reason === 'own' ? pilot : rival);
    if (reason === 'fuse') projectile.fuseAt = time + .08;
    if (reason === 'rocket') {
      pilot.weapon = 'rift';
      pilot.loadout.rift = { ammo: 1, reserve: 0 };
    }
    match.shoot(pilot, time);
    assert(match.projectiles.includes(projectile), reason);
    assert(!match.events.some(evt => evt.type === 'intercept'), reason);
  }
});

test('echo recall shares the action counter and consumes one echo without duplicating ammo, vitals or movement charges', () => {
  const f = setup({ recall: true });
  const { match, pilot, signature } = f;
  const echo = makeEcho(f);
  Object.assign(pilot, { vx: 4, vz: 3, yaw: .44, pitch: -.34, hp: 83, armor: 9, airJump: false, dodgeCooldown: .65 });
  const before = structuredClone(pilot.loadout);
  signature.request(pilot, { type: 'echo-fire', echo: echo.serial, action: 1, yaw: 0, pitch: 0 });
  signature.request(pilot, { type: 'echo-swap', echo: echo.serial, action: 2 });
  assert.equal(pilot.echoRequest.swap, undefined);
  pilot.echoRequest = null;
  assert.equal(recall(f, echo, 2), false);
  assert.equal(recall(f, echo, 3), true);
  assert.deepEqual([pilot.x, pilot.y, pilot.z, pilot.vx, pilot.vy, pilot.vz], [0, 0, 0, 0, 0, 0]);
  assert.deepEqual([pilot.yaw, pilot.pitch, pilot.hp, pilot.armor, pilot.airJump, pilot.dodgeCooldown], [.44, -.34, 83, 9, false, .65]);
  assert.deepEqual(pilot.loadout, before);
  assert.equal(pilot.safeUntil, time + .4);
  assert(pilot.fireAt >= time + .6);
  assert.equal(signature.echoes.size, 0);
  assert.equal(recall(f, echo, 4), false);
  assert.equal(match.events.filter(evt => evt.type === 'echo-recall').length, 1);
  assert.equal(match.events.filter(evt => evt.type === 'shot').length, 0);
});

test('a real grounded dodge on Relay accepts recall after landing', () => {
  const f = setup({ recall: true });
  const { match, pilot, signature } = f;
  Object.assign(pilot, { x: 0, z: -24, yaw: -Math.PI });
  match.input(pilot, { seq: 1, x: 0, z: 0, yaw: -Math.PI, pitch: 0, dodge: true }, time);
  match.step(time);
  const echo = signature.echoes.get(pilot.id);
  assert(echo);
  assert.equal(pilot.grounded, false);
  let at = time;
  while (!pilot.grounded && at < time + .8) {
    at += TICK;
    match.step(at);
  }
  assert.equal(pilot.grounded, true);
  assert(pilot.z > -23);
  signature.request(pilot, { type: 'echo-swap', echo: echo.serial, action: 1 });
  match.step(at + TICK);
  assert.equal(pilot.z, -24);
  assert.equal(signature.echoes.has(pilot.id), false);
});

test('echo recall rejects blocked space, pilot and vehicle states, expired echoes and objective shortcuts', () => {
  for (const [label, mode, reason] of [
    ['wall', 'tdm', 'blocked'], ['edge', 'tdm', 'range'], ['pilot', 'tdm', 'occupied'],
    ['hull', 'tdm', 'occupied'], ['vehicle', 'tdm', 'ground'], ['dodge', 'tdm', 'ground'],
    ['airborne', 'tdm', 'ground'], ['air echo', 'tdm', 'ground'], ['pad', 'tdm', 'blocked'],
    ['expired', 'tdm', 'unavailable'], ['flag', 'ctf', 'objective'], ['capture', 'dom', 'objective'],
  ]) {
    const f = setup({ recall: true }, mode, {
      cover: label === 'wall' ? [{ x: 0, z: 2, w: 2, d: .1, h: 2.2, angle: 0 }] : [],
      points: label === 'capture' ? [...ARENAS.relay.points, { x: 0, y: 0, z: 2 }] : ARENAS.relay.points,
      launchPads: label === 'pad' ? [{ x: 0, y: 0, z: 0, radius: 1, vx: 0, vz: 3 }] : [],
    });
    const { match, pilot, rival, signature } = f;
    const echo = makeEcho(f);
    if (label === 'edge') echo.x = 40;
    if (label === 'pilot') Object.assign(rival, { x: 0, z: 0 });
    if (label === 'hull') match.fleet.hulls.push({ kind: 'bastion', x: 0, z: -2.35, hp: 100 });
    if (label === 'vehicle') pilot.vehicle = 0;
    if (label === 'dodge') pilot.dodgeTime = .1;
    if (label === 'airborne') pilot.grounded = false;
    if (label === 'air echo') echo.grounded = false;
    if (label === 'flag') match.flags[1].carrier = pilot.id;
    assert.equal(recall(f, echo, 1, label === 'expired' ? echo.expires + .1 : time + .4), false, label);
    assert.equal(match.events.at(-1).reason, reason, label);
    assert.equal(signature.echoes.get(pilot.id), echo, label);
    assert.equal(pilot.z, 4, label);
  }
});

test('shield bracing reduces displacement with identical damage and preserves faster prior momentum', () => {
  function blast(brace, armor) {
    const fixture = setup({ brace });
    const { match, pilot, rival } = fixture;
    Object.assign(pilot, { x: 1, z: -6, armor });
    const projectile = grenade(fixture);
    projectile.position.y = .8;
    match.explode(projectile, null, time + TICK);
    const event = match.events.at(-1);
    assert(!event.hits.some(hit => hit.id === rival.id));
    return { hp: pilot.hp, armor: pilot.armor, vx: pilot.vx, vy: pilot.vy, braced: event.hits[0].braced };
  }
  const normal = blast(false, 33), braced = blast(true, 33);
  assert.deepEqual([normal.hp, normal.armor], [braced.hp, braced.armor]);
  assert(braced.vx < normal.vx && braced.vy < normal.vy);
  assert(braced.braced);
  assert.deepEqual(blast(false, 0), blast(true, 0));
  const player = { vx: 18, vy: 4, vz: 0 };
  for (let i = 0; i < 10; i++) explosionImpulse(player, { x: 1, z: .7 }, 1, {}, true, true);
  assert(Math.abs(Math.hypot(player.vx, player.vz) - 18) < .001);
  assert.equal(player.vy, 4);
});

test('launch-and-hit recognition needs its own flag and a real launch and can score once per flight', () => {
  for (const traversal of [false, true]) {
    const f = setup({ traversal }, 'tdm', { launchPads: [{ x: 0, y: 0, z: 0, radius: 1.6, vx: 0, vz: -9 }] });
    const { match, pilot, rival, signature } = f;
    match.step(time);
    assert.equal(pilot.grounded, false);
    assert(match.events.some(event => event.type === 'movement' && event.action === 'launch') || pilot.vy > 5);
    const hit = { id: rival.id, absorbed: 12 };
    signature.combat(pilot, { id: rival.id, absorbed: 0 }, time + .08);
    signature.combat(pilot, hit, time + .10, true);
    assert.equal(signature.chains.get(pilot.id).seen.has('launchHit:rival'), false);
    signature.combat(pilot, hit, time + .12);
    signature.combat(pilot, hit, time + .14);
    assert.equal(signature.chains.get(pilot.id).seen.get('launchHit:rival'), traversal ? 1 : undefined);
  }
});
