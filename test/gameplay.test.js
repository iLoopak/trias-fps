import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS, PILOT_HP, WEAPONS, GANTRIES, GRENADE, LAUNCH_PADS, PICKUPS, TICK, TURRET, blocked, eyeHeight, movePlayer, wallDistance } from '../public/rules.js';

const neutral = { x: 0, z: 0, yaw: 0 };

test('all three launch pads reach their gantry and leave a route underneath', () => {
  for (const [i, pad] of LAUNCH_PADS.entries()) {
    const gantry = GANTRIES[i * 2];
    assert(!blocked(gantry.x, gantry.z));
    assert.equal(wallDistance(
      { x: gantry.x, y: 2, z: gantry.z }, { x: 0, y: 1, z: 0 }, 3,
    ), gantry.bottom - 2);

    const below = { x: gantry.x, y: 0, z: gantry.z, grounded: true };
    for (let tick = 0; tick < 12; tick++) movePlayer(below, { ...neutral, jump: tick === 0 });
    assert(below.y + 1.78 <= gantry.bottom + 0.00001);

    for (const sprint of [false, true]) {
      const pilot = { x: pad.x, y: 0, z: pad.z, grounded: true };
      const input = { ...neutral, z: Number(sprint), sprint, yaw: pad.angle };
      assert.equal(movePlayer(pilot, input), 'launch');
      let landedOnDeck = false;
      for (let tick = 0; tick < 50; tick++) {
        movePlayer(pilot, input);
        landedOnDeck ||= pilot.grounded && pilot.y === gantry.h;
      }
      assert(landedOnDeck, 'Expected to land in sector ' + i + ' with sprint=' + sprint);
    }
  }
});

test('air dodge does not come back until landing even after its cooldown runs out', () => {
  const pilot = { x: 28, y: 22, z: 27, grounded: false, airDodge: true };
  assert.equal(movePlayer(pilot, { ...neutral, dodge: true }), 'dodge');
  for (let tick = 0; tick < 34; tick++) movePlayer(pilot, neutral);
  assert(!pilot.grounded);
  assert.equal(pilot.dodgeCooldown, 0);
  assert.notEqual(movePlayer(pilot, { ...neutral, dodge: true }), 'dodge');

  for (let tick = 0; tick < 30; tick++) movePlayer(pilot, neutral);
  assert(pilot.grounded);
  assert.equal(movePlayer(pilot, { ...neutral, dodge: true }), 'dodge');
});

test('Rift is acquired on the inner gantry and damages only opponents after traveling', () => {
  const match = new Match('RIFT', 'tdm', false);
  const [attacker, direct, splash, ally] = ['one', 'two', 'three', 'four'].map(id => {
    const socket = { id };
    match.join(socket, id, 0);
    return socket.player;
  });
  const index = PICKUPS.findIndex(item => item.type === 'rift');
  Object.assign(attacker, PICKUPS[index]);
  match.updatePickups(2);
  assert.equal(attacker.weapon, 'rift');
  assert(match.pickups[index] > 2);

  Object.assign(attacker, { x: -26, y: 0, z: 15, yaw: 0, pitch: 0 });
  Object.assign(direct, { x: -26, y: 0, z: 10 });
  Object.assign(splash, { x: -24.6, y: 0, z: 9.9 });
  Object.assign(ally, { x: -25, y: 0, z: 9.9 });
  match.shoot(attacker, 3);
  assert.equal(match.projectiles.length, 1);
  assert.equal(direct.hp, PILOT_HP);
  for (let tick = 0; tick < 14 && match.projectiles.length; tick++) match.updateProjectiles(3 + tick * TICK);
  assert.equal(match.projectiles.length, 0);
  assert.equal(direct.hp, PILOT_HP - WEAPONS.rift.damage);
  assert(splash.hp > PILOT_HP - WEAPONS.rift.projectile.splash && splash.hp < PILOT_HP);
  assert.equal(ally.hp, PILOT_HP);
  assert.equal(match.events.find(event => event.type === 'burst').hits.length, 2);
});

test('a flag dropped over a gantry can be recovered at its upper level', () => {
  const match = new Match('FLAGS', 'ctf', false);
  const socket = { id: 'carrier' };
  match.join(socket, 'Carrier', 0);
  const deck = GANTRIES[3];
  Object.assign(socket.player, { x: deck.x, y: deck.h + 0.4, z: deck.z });
  match.flags[1].carrier = socket.id;
  match.dropFlag(socket.player, 3);
  assert.equal(match.flags[1].y, deck.h);
  socket.player.y = 0;
  match.updateFlags(4);
  assert.equal(match.flags[1].status, 'dropped');
  socket.player.y = deck.h;
  match.updateFlags(5);
  assert.equal(match.flags[1].carrier, socket.id);
});

test('a pilot stays crouched under a Foundry service duct until outside', () => {
  const layout = ARENAS.foundry;
  const duct = layout.cover.find(item => item.duct);
  const pilot = { x: duct.x, z: duct.z, y: 0, grounded: true, crouch: true };
  assert(blocked(pilot.x, pilot.z, undefined, pilot.y, layout));
  movePlayer(pilot, neutral, TICK, layout);
  assert(pilot.crouch);
  assert.equal(eyeHeight(pilot), 0.92);
  for (let tick = 0; tick < 33; tick++) movePlayer(pilot, { ...neutral, x: 1 }, TICK, layout);
  assert(!pilot.crouch);
});

test('a grenade bounces and only bursts after its fuse', () => {
  const room = new Match('GRENADES', 'ffa', false);
  const shooter = { id: 'thrower' };
  room.join(shooter, 'Thrower', 0, 'wraith');
  Object.assign(shooter.player, { x: -26, z: 20, y: 0, yaw: 0, pitch: -0.3 });
  room.throwGrenade(shooter.player, 3);
  const projectile = room.projectiles[0];
  assert.equal(shooter.player.character, 'wraith');
  assert.equal(shooter.player.grenades, 0);
  let touchedGround = false;
  for (let tick = 1; tick * TICK < GRENADE.fuse; tick++) {
    room.updateProjectiles(3 + tick * TICK);
    if (projectile.position.y < 0.10 && projectile.velocity.y > 0) touchedGround = true;
    assert.equal(room.projectiles.length, 1);
  }
  assert(touchedGround);
  room.updateProjectiles(3 + GRENADE.fuse);
  assert.equal(room.projectiles.length, 0);
  assert.equal(room.events.at(-1).weapon, 'grenade');
});

test('the Foundry turret is exclusive and overheats without consuming the pilot weapon', () => {
  const room = new Match('TURRETS', 'tdm', false, ARENAS.foundry);
  const first = { id: 'one' };
  const second = { id: 'two' };
  room.join(first, 'One', 0, 'bulwark');
  room.join(second, 'Two', 0);
  const turret = room.turrets[0];
  for (const socket of [first, second]) {
    socket.player.x = turret.x;
    socket.player.z = turret.z;
    room.useTurret(socket.player, 1);
  }
  assert.equal(first.player.turret, 0);
  assert.equal(second.player.turret, -1);
  for (let i = 0; i < 20; i++) room.operateTurret(first.player, { yaw: turret.yaw + 1.8, pitch: 0.9, fire: true }, 2 + TICK * i * 4);
  assert(turret.hot);
  assert(Math.abs(first.player.yaw - turret.yaw - TURRET.turn) < 0.000001);
  assert.equal(first.player.loadout.pulse.ammo, 24);
  room.useTurret(first.player, 5);
  assert.equal(turret.user, null);
  room.useTurret(second.player, 5);
  assert.equal(second.player.turret, 0);
});

test('Scatter focused fire consumes two rounds and uses a single ray', () => {
  const room = new Match('SCATTER', 'ffa', false);
  const shooter = { id: 'pilot' };
  room.join(shooter, 'Pilot', 0);
  shooter.player.weapon = 'scatter';
  shooter.player.loadout.scatter = { ammo: 6, reserve: 12 };
  room.input(shooter.player, { type: 'input', seq: 1, ...neutral, pitch: 0, alt: true }, 3);
  room.step(3);
  const shot = room.events.find(event => event.type === 'shot');
  assert(shot.alt);
  assert.equal(shot.ends.length, 1);
  assert.equal(shooter.player.loadout.scatter.ammo, 4);
});
