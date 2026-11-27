import assert from 'node:assert/strict';
import { test } from 'node:test';
import { botInput, route } from '../bots.js';
import { chooseObjective, chooseWeapon } from '../bot-choices.js';
import { botSound, senseBots } from '../bot-perception.js';
import { BOT_DIFFICULTIES, brainFor, normalizeDifficulty } from '../bot-profiles.js';
import { Match } from '../match.js';
import { ARENAS, RELAY_SERVICE, PILOT_HP, TICK, packCover, aimVector } from '../public/rules.js';

function setup(mode = 'tdm', flags = {}) {
  const match = new Match('bot-fixture',mode,true,{ ...RELAY_SERVICE,cover:[],launchPads:[],turrets:[] },flags);
  const bot = match.addPlayer('bot','Scout',0,0,0,true,'vector');
  Object.assign(bot,{ x:0,y:0,z:0,yaw:0,pitch:0,safeUntil:0 });
  return { match,bot,brain:brainFor(match,bot) };
}

function addTarget(match,id,x,z,team = 1) {
  const pilot = match.addPlayer(id,id,team,1,0);
  Object.assign(pilot,{ x,y:0,z,safeUntil:0 });
  return pilot;
}

test('perception sees only the cone, samples occlusion, and loses hidden positions', () => {
  const { match,bot,brain } = setup();
  const front = addTarget(match,'front',0,-12);
  addTarget(match,'behind',0,2);
  senseBots(match,bot,brain,1);
  assert.deepEqual(brain.sensed.map(target=>target.id),['front']);
  assert(brain.reactionAt > 1);
  match.layout.cover = packCover([{ x:0,z:-5,w:7,d:1,h:3 }]);
  front.z = -18;
  senseBots(match,bot,brain,1.5);
  assert.equal(brain.target,null);
  assert.equal(brain.memory.z,-12);
  senseBots(match,bot,brain,4);
  assert.equal(brain.memory,null);
});

test('audible cues give a temporary approximate search point and no target lock', () => {
  const { match,bot,brain } = setup();
  const behind = addTarget(match,'behind',4,7);
  botSound(match,behind,behind,1,23);
  senseBots(match,bot,brain,1.1);
  assert.equal(brain.target,null);
  assert.deepEqual(brain.hearing,{x:3,z:6,heardAt:1});
  assert.equal(chooseObjective(match,bot,brain,1.1).intent,'listen');
  const input = botInput(match,bot,1.1);
  assert.equal(input.fire,false);
  senseBots(match,bot,brain,3.2);
  assert.equal(brain.hearing,null);
});

test('acquisition respects reaction time, turning rate and a newly inserted obstruction', () => {
  const { match,bot,brain } = setup();
  const enemy = addTarget(match,'seen',6,-12);
  const first = botInput(match,bot,1);
  assert.equal(first.fire,false);
  assert(Math.abs(first.yaw) < .05);
  let live;
  for (let i=1;i<=38;i++) {
    live = botInput(match,bot,1+i*TICK);
    Object.assign(bot,{yaw:live.yaw,pitch:live.pitch});
  }
  assert.equal(live.fire,true);
  match.layout.cover = packCover([{x:3,z:-6,w:12,d:2,h:5}]);
  brain.senseAt = 10; // Even a still-valid perception cache cannot authorize a covered shot.
  assert.equal(botInput(match,bot,2.4).fire,false);
  assert.equal(enemy.hp,PILOT_HP);
});

test('weapons and risk preferences depend on observed range and personality', () => {
  const { match,bot,brain } = setup();
  Object.assign(bot.loadout,{ scatter:{ammo:4,reserve:0},lance:{ammo:2,reserve:0} });
  assert.equal(chooseWeapon(bot,brain,{distance:6}),'scatter');
  assert.equal(chooseWeapon(bot,brain,{distance:30}),'lance');
  bot.loadout.lance.ammo = 0;
  assert.equal(chooseWeapon(bot,brain,{distance:30}),'pulse');
  const enemy = { x:0,y:0,z:-9,distance:9 };
  bot.hp = 46;
  brain.target = enemy;
  assert.equal(brain.kind,'aggressor');
  assert.equal(chooseObjective(match,bot,brain,1).intent,'engage');
  const sentinel = match.addPlayer('sentinel','Guard',0,2,0,true);
  Object.assign(sentinel,{x:0,y:0,z:0,hp:46});
  const cautious = brainFor(match,sentinel);
  cautious.target = enemy;
  assert.equal(cautious.kind,'sentinel');
  assert.equal(chooseObjective(match,sentinel,cautious,1).intent,'retreat');
});

test('respawn preserves the bot identity and difficulty without changing combat attributes', () => {
  const { match,bot,brain } = setup('tdm',{botDifficulty:'elite'});
  const old = brain.kind;
  brain.memory = { x: 10,z: 10 };
  match.spawn(bot,4);
  const after = brainFor(match,bot);
  assert.equal(after.kind,old);
  assert.equal(after.memory,null);
  assert.equal(after.difficulty,'elite');
  const recruit = setup('tdm',{botDifficulty:'recruit'});
  assert.equal(recruit.bot.hp,bot.hp);
  assert.deepEqual(recruit.bot.loadout,bot.loadout);
  assert(BOT_DIFFICULTIES.recruit.reaction > after.tuning.reaction);
  assert.equal(normalizeDifficulty('unknown'),'recruit');
  assert.equal(normalizeDifficulty('mixed'),'mixed');
});

test('objective choices spread teammates and preserve CTF and Siege goals', () => {
  const { match, bot, brain } = setup('dom');
  const teammate = match.addPlayer('team','Wing',0,1,0,true);
  const assigned = chooseObjective(match,bot,brain,1);
  brain.objective = assigned;
  assert.notEqual(chooseObjective(match,teammate,brainFor(match,teammate),1).point,assigned.point);
  const ctf = setup('ctf');
  ctf.match.flags[1].carrier = ctf.bot.id;
  assert.equal(chooseObjective(ctf.match,ctf.bot,ctf.brain,1).intent,'return-flag');
  ctf.match.flags[1].carrier = null;
  ctf.match.flags[0].status = 'dropped';
  assert.equal(chooseObjective(ctf.match,ctf.bot,ctf.brain,1).intent,'recover-flag');
  const siege = setup('siege');
  assert.equal(chooseObjective(siege.match,siege.bot,siege.brain,1).intent,'siege-link');
});

test('a blocked route replans and a stationary traveller recovers', () => {
  const { match,bot,brain } = setup();
  match.layout.cover = packCover([{ x:0,z:-5,w:4,d:2,h:4 }]);
  assert(route(bot,{x:0,z:-10},match.layout).some(point=>Math.abs(point.x)>2));
  brain.objective = {point:{ x:0,z:-10 },intent:'patrol'};
  brain.planAt = 8;
  botInput(match,bot,1);
  const recovery = botInput(match,bot,1.8);
  assert.equal(match.botMetrics.stuck,1);
  assert.equal(recovery.z,-.42);
  assert(brain.recoverUntil > 1.8);
});

test('a bot Echo consumes real ammunition and requires a recently seen, reachable target', () => {
  const { match,bot,brain } = setup('tdm',{botDifficulty:'elite'});
  const enemy = addTarget(match,'target',0,-14);
  match.humans.add({player:{signature:true}});
  brain.traits = { ...brain.traits,echo:1,mobility:0 };
  brain.tuning = { ...brain.tuning,error:0,tactics:1,mistake:0 };
  match.signature.dodge(bot,{ x:0,y:0,z:2,crouch:false,weapon:'pulse',team:0,slot:0,character:'vector' },1);
  let action;
  for (let i=0;i<38 && !action;i++) {
    const input = botInput(match,bot,1+i*TICK);
    Object.assign(bot,{yaw:input.yaw,pitch:input.pitch});
    if (input.echo) action = { ...input.echo,at:1+i*TICK };
  }
  assert(action);
  const echo = match.signature.echoes.get(bot.id);
  const rounds = bot.loadout.pulse.ammo;
  assert(match.signature.fire(bot,{...action,echo:echo.serial},action.at));
  assert.equal(bot.loadout.pulse.ammo,rounds-1);
  assert.equal(match.signature.echoes.has(bot.id),false);
  assert(enemy.hp<PILOT_HP);
  const direction = aimVector(bot.yaw,bot.pitch);
  assert(direction.z < -.9);
});

test('Horde support is opt-in and a human takes over a bot slot', () => {
  const { match } = setup('horde',{hordeSupport:true});
  match.removePlayer('bot',0);
  match.fillBots(0);
  assert.equal(match.players.size,4);
  assert.match(match.players.get('bot-0-0').id,/^bot/);
  const socket = {id:'human'};
  assert(match.join(socket,'Human',1));
  assert.equal(match.players.size,4);
  assert.equal(match.players.has('bot-0-0'),false);
  assert.equal(socket.player.team,0);
  const defenders = [...match.players.values()].filter(player=>player.bot);
  assert(defenders.every(player=>chooseObjective(match,player,brainFor(match,player),1).intent==='defend-core'));
  const regular = new Match('no-support','horde',true);
  regular.fillBots(0);
  assert.equal(regular.players.size,0);
});

test('radio is opt-in, team-only, rate-limited and silent in FFA', () => {
  const { match,bot } = setup();
  function listener(team,botRadio) {
    const packets = [];
    match.humans.add({player:{team},readyState:1,bufferedAmount:0,botRadio,
      send(packet){ packets.push(JSON.parse(packet)); }});
    return packets;
  }
  const allies = listener(0,true);
  const legacy = listener(0,false);
  const rivals = listener(1,true);
  match.radioBot(bot,'cover',4);
  match.radioBot(bot,'retreat',5);
  assert.deepEqual(allies,[{type:'bot-radio',name:'Scout',line:'cover',frame:'female'}]);
  assert.equal(legacy.length,0);
  assert.equal(rivals.length,0);
  bot.frame = 'male';
  match.radioBot(bot,'contact',24);
  assert.equal(allies[1].frame,'male');
  match.mode = 'ffa';
  match.radioBot(bot,'contact',49);
  assert.equal(allies.length,2);
});
