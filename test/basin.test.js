import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { ARENAS, blocked, movePlayer, radial, TICK } from '../shared/rules.js';
import { moveVehicle, VEHICLES, vehicleBlocked } from '../shared/vehicles.js';

const layout = ARENAS.basin;

function join(room, id) {
  const socket = { id };
  assert(room.join(socket, id, 0));
  return socket.player;
}

test('both vehicles have a complete circuit and the three inner connectors', () => {
  assert(layout.length > ARENAS.relay.length * 1.7);
  for (const kind of Object.keys(VEHICLES)) {
    for (let i = 0; i < 180; i++) {
      const point = radial(i * Math.PI / 90, layout.roadRadius);
      assert(!vehicleBlocked(point.x, point.z, kind, layout));
    }
    for (const baseAngle of [Math.PI, Math.PI / 3, -Math.PI / 3]) {
      for (let distance = 9; distance < layout.roadRadius; distance += 0.35) {
        const point = radial(baseAngle - 0.53, distance);
        assert(!vehicleBlocked(point.x, point.z, kind, layout));
      }
    }
    for (const point of layout.repairs) assert(!vehicleBlocked(point.x, point.z, kind, layout));
    for (const point of [...layout.points, ...layout.bases]) {
      assert(!blocked(point.x, point.z, undefined, 0, layout));
      assert(vehicleBlocked(point.x, point.z, kind, layout));
    }
  }
});

test('infantry launch pads land on the upper weapon route in all sectors', () => {
  for (const [i, pad] of layout.launchPads.entries()) {
    const pilot = { x: pad.x, z: pad.z, y: 0, grounded: true };
    const input = { x: 0, z: 0, yaw: pad.angle };
    assert.equal(movePlayer(pilot, input, TICK, layout), 'launch');
    let reached = false;
    for (let step = 0; step < 44; step++) {
      movePlayer(pilot, input, TICK, layout);
      reached ||= pilot.grounded && pilot.y === layout.gantries[2 * i].h;
    }
    assert(reached);
  }
});

test('the Skimmer boosts and brakes, without changing the Bastion top speed', () => {
  const run = kind => {
    const vehicle = { id: kind, kind, x: 53, z: 25, heading: 0, hp: VEHICLES[kind].hp, speed: 0, boost: 1 };
    for (let n = 0; n < 48; n++) moveVehicle(vehicle, { x: 0, z: 1, sprint: true }, layout, []);
    const peak = vehicle.speed;
    for (let n = 0; n < 33; n++) moveVehicle(vehicle, { x: 0, z: 1, brake: true }, layout, []);
    assert.equal(vehicle.speed, 0);
    return peak;
  };
  assert(run('skimmer') > VEHICLES.skimmer.speed);
  assert.equal(run('bastion'), VEHICLES.bastion.speed);
});

test('a chassis can be captured, repaired while stopped, destroyed and respawned', () => {
  const room = new Match('VEHICLES', 'tdm', false, layout);
  const rider = join(room, 'rider');
  const opponent = join(room, 'opponent');
  const hull = room.fleet.hulls[0];
  Object.assign(rider, { x: hull.x, z: hull.z });
  assert(room.fleet.use(rider, 3));
  assert.equal(hull.driver, rider.id);
  assert.equal(rider.vehicle, 0);
  const carriedAmmo = rider.loadout.pulse.ammo;
  room.fleet.drive(rider, { x: 0, z: 0, fire: true, yaw: hull.yaw, pitch: 0 }, 4);
  assert(room.events.some(event => event.vehicle === hull.id));
  assert.equal(rider.loadout.pulse.ammo, carriedAmmo);
  Object.assign(hull, { ...layout.repairs[0], speed: 5, hp: 80 });
  room.fleet.tick(4);
  assert.equal(hull.hp, 80);
  hull.speed = 0;
  room.fleet.tick(4 + TICK);
  assert(hull.hp > 80);
  const hit = room.damage(opponent, hull, 999, 5, { x: hull.x + 1, z: hull.z, y: 1 });
  assert(hit.eliminated);
  assert.equal(opponent.kills, 1);
  assert.equal(rider.vehicle, -1);
  assert.equal(hull.driver, null);
  assert.equal(hull.hp, 0);
  room.fleet.tick(5 + VEHICLES.skimmer.respawn);
  const replaced = room.fleet.hulls[0];
  assert.equal(replaced.hp, VEHICLES.skimmer.hp);
  assert.equal(replaced.x, layout.vehicles[0].x);
});

test('a flag carrier cannot enter a vehicle and a seated pilot cannot capture an objective', () => {
  const ctf = new Match('FLAG', 'ctf', false, layout);
  const player = join(ctf, 'runner');
  const hull = ctf.fleet.hulls[0];
  Object.assign(player, { x: hull.x, z: hull.z });
  ctf.flags[1].carrier = player.id;
  assert.equal(ctf.fleet.use(player, 3), false);

  const dom = new Match('POINT', 'dom', false, layout);
  const pilot = join(dom, 'driver');
  Object.assign(pilot, layout.points[0], { vehicle: 0 });
  dom.updateDominance(3);
  assert.equal(dom.points[0].capture, -1);
  pilot.vehicle = -1;
  dom.updateDominance(3 + TICK);
  assert.equal(dom.points[0].capture, pilot.team);
});
