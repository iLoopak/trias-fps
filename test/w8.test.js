import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { Practice } from '../practice.js';
import { invitePage } from '../room-invite.js';
import { RESUME_SECONDS, RoomSessions } from '../room-sessions.js';
import { TICK } from '../public/rules.js';

function peer(id) { return { id, close() { this.closed = true; } }; }

test('portable invite names the same server and room and refuses unsafe host or path markup', () => {
  const page = invitePage('/join/relay-qa', { host: 'test.onrender.com', 'x-forwarded-proto': 'https' });
  assert.match(page, /trias:\/\/join\/RELAY-QA\?server=https%3A%2F%2Ftest\.onrender\.com/);
  assert.match(invitePage('/join/LAN', { host: '127.0.0.1:3000' }), /http%3A%2F%2F127\.0\.0\.1%3A3000/);
  assert.equal(invitePage('/join/W7"><script>', { host: 'example.com' }), null);
  assert.equal(invitePage('/join/ROOM', { host: 'example.com"><img' }), null);
});

test('a disconnected seat retains team, inventory and score while a flag drops', () => {
  const room = new Match('RESUME', 'ctf', true);
  const first = peer('first');
  assert(room.join(first,'First',1));
  const sessions = new RoomSessions(room);
  first.room = room;
  const token = sessions.register(first);
  const pilot = first.player;
  Object.assign(pilot, { hp: 92, armor: 15, kills: 3, chain: 3, perks: 1, receivedSeq: 81, lastFireId: 12, lastEchoAction: 8, fireRequest: true });
  pilot.loadout.pulse.ammo = 7;
  room.flags[1].carrier = pilot.id;
  room.flags[1].status = 'carried';
  sessions.disconnect(first,4);
  assert(pilot.away);
  assert.equal(room.flags[1].status,'dropped');
  assert.equal(pilot.fireRequest,false);
  assert.equal(room.players.get(pilot.id),pilot);
  assert(!sessions.attach(peer('intruder'),'not-the-ticket',7));
  const comeback = peer('comeback');
  assert(sessions.attach(comeback,token,7));
  assert.equal(comeback.player,pilot);
  assert.equal(comeback.id,'first');
  assert(!pilot.away);
  assert.deepEqual([pilot.hp,pilot.armor,pilot.kills,pilot.chain,pilot.perks,pilot.loadout.pulse.ammo],[92,15,3,3,1,7]);
  assert.deepEqual([pilot.receivedSeq,pilot.lastFireId,pilot.lastEchoAction],[0,0,0]);
});

test('replaced connections cannot remove a seat and expired or voluntary exits release it', () => {
  const room = new Match('RESUME', 'horde', false);
  const first = peer('first');
  room.join(first,'First',0);
  const sessions = new RoomSessions(room);
  first.room = room;
  const token = sessions.register(first);
  const replacement = peer('new');
  assert(sessions.attach(replacement,token,1));
  assert(first.closed);
  assert.equal(first.room,null);
  assert.equal(room.humans.size,1);
  assert.deepEqual([...room.players.keys()],['first']);
  sessions.disconnect(replacement,3);
  assert(!sessions.attach(peer('late'),token,3 + RESUME_SECONDS));
  sessions.expire(3 + RESUME_SECONDS);
  assert.equal(room.players.size,0);
  assert.equal(sessions.seats.size,0);
  const second = peer('second');
  room.join(second,'Second',70);
  second.room = room;
  const secondToken = sessions.register(second);
  sessions.disconnect(second,71,true);
  assert(!sessions.attach(peer('newer'),secondToken,72));
  assert.equal(room.players.size,0);
});

test('practice advances only on a real dodge, echo shot, three eliminations and scan activation', () => {
  const room = new Match('TRAINING','tdm',false);
  const pilot = room.addPlayer('pilot','Pilot',0,0,0);
  pilot.signature = true;
  room.practice = new Practice(room,pilot,0);
  const drill = room.practice;
  assert.deepEqual(drill.snapshot(),[0,0]);
  room.input(pilot,{ seq:1, x:1, z:0, yaw:-Math.PI, pitch:0, dodge:true },1);
  room.step(1);
  assert.deepEqual(drill.snapshot(),[1,0]);
  const echo = room.signature.echoes.get(pilot.id);
  assert(room.signature.fire(pilot,{ echo:echo.serial,yaw:-Math.PI,pitch:0 },1.4));
  assert.deepEqual(drill.snapshot(),[2,0]);
  drill.step(2.1);
  let clock = 3;
  for (let kill = 1; kill <= 3; kill++) {
    for (let shot = 0; shot < 3; shot++) {
      room.shoot(pilot,clock);
      clock += .2;
    }
    assert.equal(drill.target.hp,0);
    assert.deepEqual(drill.snapshot(),[2,kill]);
    clock += 1;
    drill.step(clock);
  }
  assert.equal(room.streaks.activate(pilot,{ perk:'scan' },clock),null);
  assert.deepEqual(drill.snapshot(),[3,3]);
});

test('the Horde council accepts one editable vote per defender and resolves support once', () => {
  const room = new Match('HORDE', 'horde', false);
  const first = peer('first'), second = peer('second');
  room.join(first,'First',0);
  room.join(second,'Second',0);
  const horde = room.horde;
  horde.wave = 2;
  horde.nextWaveAt = 12;
  horde.core.hp = 200;
  assert(!horde.vote({ id: 'unknown' },'scrap',10));
  assert(horde.vote(first.player,'repair',10));
  assert(horde.vote(first.player,'scrap',10.1));
  assert(horde.vote(second.player,'repair',11));
  assert(!horde.vote(second.player,'armor',12));
  assert.deepEqual(horde.supportCounts(),[1,0,1]);
  horde.start(12);
  assert.equal(horde.lastSupport,'repair');
  assert.equal(horde.core.hp,200 + 115 + 90);
  assert.equal(horde.scrap,40);
  assert.deepEqual(horde.supportCounts(),[0,0,0]);
  assert(!horde.vote(first.player,'repair',12 + TICK));
});

test('Meridian vehicles can speed up connected infantry captures but cannot trigger or contest them', () => {
  const room = new Match('MERIDIAN','siege',false);
  const { siege, fleet } = room;
  assert(siege.coverLanes.slice(3).every(lanes => lanes.length === 2));
  const lane = siege.coverLanes[3][0];
  Object.assign(siege.nodes[3], { capture: 0, progress: .1 });
  assert.deepEqual(siege.escortGoal(lane.road,0), { goal:lane.post, holding:false });
  assert.deepEqual(siege.escortGoal(lane.post,0), { goal:lane.post, holding:true });
  siege.nodes[3].progress = 0;
  const rifle = room.addPlayer('rifle','Rifle',0,0,0);
  const driver = room.addPlayer('driver','Driver',0,1,0);
  const enemy = room.addPlayer('enemy','Enemy',1,0,0);
  const relay = siege.layout.nodes[3];
  const hull = fleet.hulls.find(item => item.team === 0);
  room.layout = { ...room.layout, cover: [] };
  Object.assign(hull,{x:relay.x + relay.radius + 3,z:relay.z,driver:driver.id});
  driver.vehicle = fleet.hulls.indexOf(hull);
  assert(siege.covered(relay,0));
  siege.step();
  assert.equal(siege.nodes[3].progress,0);
  Object.assign(rifle,relay);
  siege.step();
  assert(siege.nodes[3].escort);
  assert(siege.nodes[3].progress > TICK / 4);
  Object.assign(enemy,relay);
  const before = siege.nodes[3].progress;
  siege.step();
  assert.equal(siege.nodes[3].progress,before);
  assert(!siege.nodes[3].escort);
  enemy.away = true;
  hull.driver = null;
  siege.step();
  assert(Math.abs(siege.nodes[3].progress - before - TICK / 4) < 1e-8);
});
