import { performance } from 'node:perf_hooks';
import { Match } from '../match.js';
import { normalizeDifficulty } from '../bot-profiles.js';
import { ARENAS, TICK } from '../public/rules.js';
import { NETWORK_VERSION } from '../public/netcode.js';
import { directorOptions } from '../combat-director.js';

const scenarios = [
  ['tdm','relay'], ['tdm','foundry'], ['tdm','island'], ['ffa','relay'],
  ['dom','relay'], ['ctf','relay'], ['siege','basin'], ['horde','relay'],
];
const duration = Number(process.env.TRIAS_SCRIMMAGE_SECONDS ?? 720);
const difficulty = normalizeDifficulty(process.env.TRIAS_BOT_DIFFICULTY);
const director = directorOptions(process.env);
for (const [mode,arena] of scenarios) {
  const match = new Match(`bench-${mode}-${arena}`,mode,true,ARENAS[arena],
    { echo: true,momentum: true,botDifficulty: difficulty,hordeSupport: mode === 'horde',director });
  const stats = { shots: 0, hits: 0, echoAttempts: 0, kills: 0, movement: {}, byPilot: {},
    fights: [], captures: new Set(), radio: 0 };
  const fightStart = new Map();
  const phases = {};
  let idleCombat = 0, lastHitAt = 0, crowdedSamples = 0, directorSamples = 0;
  let time = 0;
  let liveObserver = false;
  const observer = {
    player: { id: 'observer',team: 0,signature: true,ack: 0 },
    protocolVersion: NETWORK_VERSION,readyState: 1,bufferedAmount: 0,botRadio: true,
    send(raw) {
      const row = JSON.parse(raw);
      if (row.type === 'bot-radio') { stats.radio++; return; }
      for (const event of row.events) {
        if (event.type === 'shot') {
          stats.shots++;
          if (event.echo) stats.echoAttempts++;
        } else if (event.type === 'movement') {
          stats.movement[event.action] = (stats.movement[event.action] ?? 0)+1;
        } else if (event.type === 'kill') {
          stats.kills++;
          stats.byPilot[event.attacker] = (stats.byPilot[event.attacker] ?? 0)+1;
          if (fightStart.has(event.victimId)) stats.fights.push(time - fightStart.get(event.victimId));
          fightStart.delete(event.victimId);
        } else if (event.type === 'notice') stats.captures.add(event.text);
        if (event.hits?.some(hit => hit && (hit.health || hit.absorbed))) {
          idleCombat = Math.max(idleCombat,time - lastHitAt);
          lastHitAt = time;
          if (event.type === 'shot' || event.type === 'burst') stats.hits++;
          for (const hit of event.hits) if (hit && !hit.eliminated && !fightStart.has(hit.id)) fightStart.set(hit.id,time);
        }
      }
    },
  };
  match.humans.add(observer);
  match.fillBots(0);
  if (mode === 'horde' && ![...match.players.values()].some(player=>!player.hordeType)) {
    // Older builds use an idle human defender, which provides a useful Horde baseline.
    const fallback = match.addPlayer('observer','Observer',0,0,0);
    fallback.signature = true;
    observer.player = fallback;
    liveObserver = true;
  }
  const elapsedAt = performance.now();
  let ticks = 0;
  do {
    time = ++ticks * TICK;
    match.step(time);
    if (match.director?.active && ticks % 15 === 0) {
      const { phase, clustered } = match.director;
      phases[phase] = (phases[phase] ?? 0) + .5;
      crowdedSamples += Number(clustered >= 5);
      directorSamples++;
    }
  } while (time < duration && !match.winner);
  const mean = stats.fights.length ? stats.fights.reduce((sum,value)=>sum+value,0)/stats.fights.length : null;
  console.log(JSON.stringify({ mode,arena,difficulty,seconds: Math.round(time),winner:match.winner?.label ?? null,
    tickMs: +( (performance.now()-elapsedAt)/ticks ).toFixed(3),shots:stats.shots,
    hitRatio: stats.shots ? +(stats.hits/stats.shots).toFixed(3) : null,
    kills:stats.kills,meanFightSeconds:mean == null ? null : +mean.toFixed(2), byPilot:stats.byPilot,
    movement:stats.movement,echoShots:stats.echoAttempts, objectiveNotices:stats.captures.size,
    scores:match.scores,core:match.horde?.core.hp,wave:match.horde?.wave,stuck: match.botMetrics?.stuck,
    radio:stats.radio, idleHuman:liveObserver, longestCombatGap:+Math.max(idleCombat,time-lastHitAt).toFixed(1),
    ...(directorSamples ? { director:phases, crowdedSamples, directorSamples } : {}) }));
}
