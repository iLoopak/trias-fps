import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { brainFor } from '../bot-profiles.js';
import { directorOptions } from '../combat-director.js';
import { ARENAS, PILOT_HP, TICK } from '../shared/rules.js';
import { NETWORK_VERSION } from '../shared/netcode.js';

function room(director, mode = 'tdm', layout) {
  return new Match('W7-TEST', mode, true, layout, { director, botDifficulty: 'regular', hordeSupport: mode === 'horde' });
}

function addHuman(match, id, received = []) {
  const peer = { id, readyState: 1, bufferedAmount: 0, protocolVersion: NETWORK_VERSION, botRadio: true,
    send(data) { received.push(JSON.parse(data)); } };
  assert(match.join(peer, id, 0));
  return peer;
}

test('combat intensity builds, stays bounded through a peak and permits a quiet recovery', () => {
  const match = room({ bots: true, seed: 37 });
  const director = match.director;
  match.fillBots(0);
  for (let tick = 1; tick <= 10; tick++) director.step(tick / 2);
  assert.equal(director.phase, 'build-up');
  assert(director.intensity < .29);
  const phases = new Set();
  for (let tick = 11; tick <= 39; tick++) {
    const now = tick / 2;
    for (let event = 0; event < 25; event++) director.record(now);
    director.record(now, 210, true);
    director.record(now, 210, true);
    director.step(now);
    phases.add(director.phase);
    assert(director.intensity >= 0 && director.intensity <= 1);
  }
  assert.deepEqual([...phases], ['engagement','peak','recovery']);
  assert.equal(director.retreatScale, 1.15);
  for (let tick = 40; tick < 68; tick++) director.step(tick / 2);
  assert.equal(director.phase, 'build-up');
  assert(director.intensity < .20);
});

test('competitive joins suspend solo orchestration while the public cosmetic clock remains usable', () => {
  const match = room({ bots: true, presentation: true, seed: 57 });
  const first = addHuman(match, 'alpha');
  assert(match.director.directsBots);
  match.director.phase = 'recovery';
  const bot = [...match.players.values()].find(pilot => pilot.bot);
  brainFor(match, bot).planAt = 30;
  const second = addHuman(match, 'beta');
  assert.equal(match.director.directsBots, false);
  assert.equal(match.director.retreatScale, 1);
  assert.equal(match.director.rangeOffset, 0);
  if (match.players.has(bot.id)) assert.equal(bot.brain.planAt, -1);
  assert.equal(match.director.publicFrame(8.4)[1], 8);
  match.humans.delete(second);
  match.removePlayer('beta', 9);
  assert.equal(match.director.directsBots, false);
  match.reset(12);
  assert(match.humans.has(first) && match.director.directsBots);
  assert.equal(directorOptions({ TRIAS_DIRECTOR_SEED: '-42' }).seed, undefined);
});

test('public arena frames are identical under hidden combat and never include tactical diagnostics', () => {
  const pass = (attacking) => {
    const match = room({ bots: true, presentation: true, seed: 115 });
    const packets = [];
    addHuman(match, 'human', packets);
    if (attacking) {
      for (let i = 0; i < 12; i++) match.director.record(4.5, 30);
      match.director.step(5);
      assert(match.director.intensity > 0);
    }
    match.broadcast(5);
    const packet = packets.findLast(row => row.type === 'state');
    return packet.arenaDirector;
  };
  assert.deepEqual(pass(false), pass(true));
  assert.equal(pass(true).length, 3);
  assert.equal(room({}).director, null);
  assert.equal(room({ debug: true }).director, null);
});

test('cosmetic-only mode leaves a seeded match simulation unchanged', () => {
  const simulate = (presentation) => {
    const match = room({ presentation, seed: 103 });
    match.fillBots(0);
    for (let frame = 1; frame <= 150; frame++) match.step(frame * TICK);
    return { scores: match.scores, pilots: [...match.players.values()].map(pilot => [
      pilot.x, pilot.y, pilot.z, pilot.hp, pilot.kills, pilot.brain.kind, pilot.brain.intent, pilot.loadout,
    ]) };
  };
  assert.deepEqual(simulate(true), simulate(false));
});

test('patrol never reads invisible enemy locations and seeded rosters retain their chosen difficulty', () => {
  function trial(seed, enemyAt) {
    const match = room({ bots: true, seed }, 'tdm', { ...ARENAS.relay, cover: [], launchPads: [] });
    match.fillBots(0);
    const first = match.players.get('bot-0-0');
    const enemy = match.players.get('bot-1-0');
    Object.assign(enemy, enemyAt);
    const brain = brainFor(match, first);
    const decision = match.director.patrol(first, brain, 5);
    return { point: decision.point, profiles: [...match.players.values()].map(pilot => brainFor(match, pilot).kind),
      difficulty: brain.difficulty, vitality: first.hp };
  }
  assert.deepEqual(trial(51, { x: 4, z: 10 }), trial(51, { x: -21, z: 12 }));
  assert.notDeepEqual(trial(51, {}).profiles, trial(70521, {}).profiles);
  assert.equal(trial(51, {}).difficulty, 'regular');
  assert.equal(trial(51, {}).vitality, PILOT_HP);
});

test('Horde pressure changes only bounded time and existing spawns for the same wave roster', () => {
  function wave(horde) {
    const match = room(horde ? { horde: true, seed: 33 } : {});
    assert(!match.horde);
    const siege = room(horde ? { horde: true, seed: 33 } : {}, 'horde');
    const mode = siege.horde;
    mode.wave = 4;
    mode.start(1);
    const entries = [];
    while (mode.remaining > 0) {
      const now = mode.deployAt;
      const size = mode.remaining;
      mode.dispatch(now, []);
      const enemy = siege.players.get('horde-' + mode.nextEnemy);
      entries.push([enemy.hordeType, enemy.hp, enemy.armor, mode.deployAt - now, enemy.x, enemy.z]);
      assert.equal(mode.remaining, size - 1);
    }
    return entries;
  }
  const legacy = wave(false), paced = wave(true);
  assert.deepEqual(wave(true), paced);
  assert.deepEqual(paced.map(row => row.slice(0,3)), legacy.map(row => row.slice(0,3)));
  assert.notDeepEqual(paced.map(row => row[3]), legacy.map(row => row[3]));
  assert(paced.every(row => row[3] >= .43 - TICK / 10 && row[3] <= 1.75));
});

test('team radio checks in once even when a director patrol has no tactical line', () => {
  const match = room({ bots: true, seed: 107 });
  const heard = [];
  const peer = addHuman(match, 'first', heard);
  match.checkTeamRadio(3);
  match.checkTeamRadio(4);
  match.checkTeamRadio(48);
  assert.equal(heard.length, 1);
  assert.equal(heard[0].type, 'bot-radio');
  assert.equal(heard[0].line, 'cover');
  assert([...match.players.values()].some(pilot => pilot.name === heard[0].name && pilot.bot && pilot.team === peer.player.team));
});
