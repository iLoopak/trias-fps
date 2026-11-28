import assert from 'node:assert/strict';
import { test } from 'node:test';
import { route } from '../bots.js';
import { Match } from '../match.js';
import { Practice } from '../practice.js';
import { ARENAS, BODY_RADIUS, RELAY_HORDE, TICK, blocked, floorBelow, movePlayer, radial } from '../shared/rules.js';

const { foundry, basin, island, relay } = ARENAS;

function walkRoute(map, from, to, maxSteps) {
  const pilot = { ...from, y: from.y ?? 0, grounded: true, airJump: true };
  const steps = route(pilot, to, map);
  assert(steps.length > 0 && steps.length <= maxSteps, `${map.id}: disconnected or indirect route`);
  for (const step of steps) {
    const arrived = () => Math.hypot(pilot.x-step.x,pilot.z-step.z) < .25
      && Math.abs(pilot.y-step.y) < (map.terrain ? .22 : .08);
    for (let tick=0; tick<100 && !arrived(); tick++) {
      const distance = Math.hypot(pilot.x-step.x,pilot.z-step.z);
      movePlayer(pilot, { x:0, z:Math.min(.55,distance*1.7+.06), yaw:Math.atan2(pilot.x-step.x,pilot.z-step.z) }, TICK, map);
    }
    assert(arrived(), `${map.id} could not walk to ${JSON.stringify(step)}`);
  }
  assert(Math.abs(pilot.y-to.y) < .08);
  assert(Math.hypot(pilot.x-to.x,pilot.z-to.z) < 1);
}

test('all new layouts deploy safely and mode markers are on reachable surfaces', () => {
  for (const map of [...Object.values(ARENAS), RELAY_HORDE]) {
    for (const spot of [...map.spawns.flat(), ...map.bases, ...map.points, ...map.pickups, ...(map.construction ?? [])]) {
      const y = spot.y ?? 0;
      assert(!blocked(spot.x,spot.z,BODY_RADIUS,y,map), `${map.revision} blocked at ${JSON.stringify(spot)}`);
      assert(Math.abs(floorBelow(spot.x,spot.z,y,BODY_RADIUS,map)-y) < .04);
    }
  }
});

test('new buildings provide a clear entrance and standing room under their roof', () => {
  for (const map of Object.values(ARENAS)) {
    const roofs = map.cover.filter(box=>box.structure==='roof');
    assert(roofs.length>=2);
    for (const roof of roofs) {
      for (let depth=0;depth<=3.5;depth+=.25) {
        const x=roof.x-Math.sin(roof.angle)*depth;
        const z=roof.z-Math.cos(roof.angle)*depth;
        const y=map.terrain?.(x,z) ?? 0;
        assert(!blocked(x,z,BODY_RADIUS,y,map), `${map.id} blocked at a building entrance`);
      }
    }
  }
});

test('Foundry connects the quench channel at both ends and the two crane bridges', () => {
  for (const side of [-1,1]) {
    walkRoute(foundry,{x:0,y:0,z:side*31},foundry.points[1],48);
    const bridge = foundry.points.find(point=>point.z===side*10);
    walkRoute(foundry,{x:24,y:0,z:side*10},bridge,60);
  }
});

test('Meridian infantry can reach all three conveyors from both ends without a pad or vehicle', () => {
  for (const deck of basin.cover.filter(box=>box.structure==='catwalk')) {
    for (const radius of [41.4,11]) {
      walkRoute(basin, radial(deck.angle,radius,16.4), {x:deck.x,y:deck.h,z:deck.z},52);
    }
  }
});

test('both shores of Kordilera reach the high bridge', () => {
  const bridge = island.pickups.at(-1);
  for (const z of [-9,17.2]) walkRoute(island,{ x:14,z,y:island.terrain(14,z) },bridge,40);
});

test('Horde enemies can reach the elevated core and construction applies only at its level', () => {
  const room = new Match('GALLERY', 'horde', false, relay);
  const peer = { id:'defender' };
  assert(room.join(peer,'Defender',0));
  assert.equal(room.horde.core.y,4.08);
  const site = room.horde.structures[3];
  const pilot = peer.player;
  const position = { ...pilot };
  Object.assign(pilot,{ x:site.x, y:0, z:site.z-1.3 });
  assert(room.horde.build(pilot,site.id,'barricade',1));
  Object.assign(pilot,position);
  assert.equal(room.horde.build(pilot,site.id,'barricade',1),null);
  assert.equal(site.box.bottom,3.3);
  for (const group of room.layout.spawns.slice(1)) walkRoute(room.layout,group[1],room.layout.bases[0],95);
  let sawWave = false, reachedCore = false;
  for (let tick=1; tick<=55/TICK; tick++) {
    room.step(tick*TICK);
    sawWave ||= room.horde.wave === 1;
    reachedCore ||= room.horde.core.hp < room.horde.core.maxHp;
    if (reachedCore) break;
  }
  assert(sawWave && reachedCore, 'first Horde wave should reach and damage the defended gallery');
});

test('Relay practice opens the same training loop on the new service lane', () => {
  const room = new Match('DRILL', 'tdm', false, relay);
  const pilot = room.addPlayer('pilot','Pilot',0,0,0);
  const drill = new Practice(room,pilot,0);
  assert(!blocked(pilot.x,pilot.z,BODY_RADIUS,pilot.y,relay));
  drill.dodge(pilot);
  drill.echo(pilot,1);
  drill.step(2);
  assert.equal(drill.target.hp,65);
  room.shoot(pilot,2);
  assert(drill.target.hp<65);
});

test('Foundry plasma hurts on contact, leaves jumps and bridges safe, and grants no kill', () => {
  const match = new Match('PLASMA','tdm',false,foundry);
  const peer = { id:'tester' };
  assert(match.join(peer,'Tester',0));
  const pilot = peer.player;
  const hp = pilot.hp;
  Object.assign(pilot,{x:0,y:3.3,z:10,grounded:true});
  match.step(TICK);
  assert.equal(pilot.hp,hp);
  Object.assign(pilot,{x:0,y:-1.9,z:0,grounded:false,vx:0,vy:1,vz:0});
  match.step(TICK*2);
  assert.equal(pilot.hp,hp);
  Object.assign(pilot,{x:0,y:-3.3,z:0,grounded:true,vx:0,vy:0,vz:0});
  match.step(TICK*3);
  assert.equal(pilot.hp,hp-foundry.hazard.damage);
  match.step(TICK*4);
  assert.equal(pilot.hp,hp-foundry.hazard.damage);
  for (let tick=5; tick<100 && pilot.hp>0; tick++) match.step(tick*TICK);
  assert.equal(pilot.hp,0);
  assert.equal(pilot.kills,0);
  assert.deepEqual(match.scores,[0,0,0]);
});
