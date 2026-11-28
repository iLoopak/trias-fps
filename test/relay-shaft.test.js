import assert from 'node:assert/strict';
import { test } from 'node:test';
import { route } from '../bots.js';
import { Match } from '../match.js';
import { ARENAS, blocked, floorBelow, movePlayer, BODY_RADIUS, TICK, wallDistance } from '../shared/rules.js';

const map = ARENAS.relay;
const routeTargets = [
  { x: 0, y: 0, z: 31 }, { x: 0, y: 6.6, z: 8.5 }, { x: -8, y: 3.3, z: -17 },
];

test('all deployment slots are free and the three Relay decks connect using the real motor', () => {
  for (const spawn of map.spawns.flat()) {
    assert.equal(blocked(spawn.x, spawn.z, BODY_RADIUS, 0, map), false);
    assert.equal(floorBelow(spawn.x, spawn.z, 0, BODY_RADIUS, map), 0);
  }
  for (const [from, to] of [[routeTargets[0],routeTargets[1]], [map.spawns[1][0],routeTargets[1]],
    [routeTargets[1],routeTargets[0]], [{ x: 26, y: 0, z: 3 },routeTargets[2]]]) {
    const pilot = { ...from, y: from.y ?? 0, grounded: true, airJump: true };
    const steps = route(pilot, to, map);
    assert(steps.length > 0);
    for (const step of steps) {
      const arrived = () => Math.hypot(pilot.x-step.x,pilot.z-step.z) < .25 && Math.abs(pilot.y-step.y) < .08;
      for (let tick=0; tick<100 && !arrived(); tick++) {
        const distance = Math.hypot(pilot.x-step.x,pilot.z-step.z);
        movePlayer(pilot,{ x:0, z:Math.min(.55,distance*1.7+.06), yaw:Math.atan2(pilot.x-step.x,pilot.z-step.z) },TICK,map);
      }
      assert(arrived(), `stair route failed at ${JSON.stringify(step)}`);
    }
    assert(Math.abs(pilot.y-to.y) < .08);
    assert(Math.hypot(pilot.x-to.x,pilot.z-to.z) < 1);
  }
});

test('a shaft fall waits for respawn and awards no kill, score or streak', () => {
  const match = new Match('SHAFT','tdm',false,map);
  const events = [];
  const peer = { id:'pilot', readyState:1, protocolVersion:2, bufferedAmount:0,
    send: payload=>events.push(...(JSON.parse(payload).events ?? [])) };
  assert(match.join(peer,'Pilot',0));
  const pilot = peer.player;
  Object.assign(pilot,{ x:8,y:0,z:8,grounded:false });
  const down = { x:0, y:-1, z:0 };
  assert.equal(wallDistance({x:8,y:0,z:8},down,40,map),40);
  assert(Math.abs(wallDistance({x:0,y:10,z:7},down,40,map)-3.4)<.0001);
  for (let tick=1;tick<90&&pilot.hp>0;tick++) match.step(tick*TICK);
  assert.equal(pilot.hp,0);
  assert.equal(pilot.kills,0);
  assert.deepEqual(match.scores,[0,0,0]);
  assert(events.some(event=>event.type==='kill'&&event.weapon==='fall'));
  match.step(pilot.respawnAt + TICK);
  assert(pilot.hp>0 && pilot.y===0);
});

test('standing beneath a gallery point does not capture it', () => {
  const match = new Match('GALLERY','dom',false,map);
  const pilot = match.addPlayer('pilot','Pilot',0,0,0);
  const point = map.points[0];
  Object.assign(pilot,point,{ y:0 });
  for (let tick=0;tick<75;tick++) match.updateDominance(tick*TICK);
  assert.equal(match.points[0].owner,-1);
  pilot.y=point.y;
  for (let tick=75;tick<150;tick++) match.updateDominance(tick*TICK);
  assert.equal(match.points[0].owner,0);
});

test('each gallery launch pad reaches the bridge from its edges while holding forward', () => {
  for (const pad of map.launchPads) for (let bearing=0; bearing<8; bearing++) {
    const angle = bearing * Math.PI / 4;
    const pilot = { ...pad, x:pad.x+Math.cos(angle)*.8, z:pad.z+Math.sin(angle)*.8, grounded:true, airJump:true };
    const input = { x:0,z:.49,yaw:Math.atan2(pilot.x-pad.x,pilot.z-pad.z) };
    assert.equal(movePlayer(pilot,input,TICK,map),'launch');
    for (let tick=0;tick<60&&!pilot.grounded;tick++)
      movePlayer(pilot,{ ...input, z:tick<7 ? .49 : 0 },TICK,map);
    assert(pilot.grounded && Math.abs(pilot.y-6.6)<.001);
  }
});
