import { blocked, distanceSquared, PILOT_HP } from './shared/rules.js';

const SAMPLE = .5;
const RULES = Object.freeze({ engage: .29, peak: .67, cool: .27, minRise: 3, minPeak: 3.5, maxPeak: 8, recovery: 6.5 });
const CASUAL = new Set(['tdm', 'ffa', 'dom', 'ctf', 'siege']);
const clamp = (n, min, max) => Math.max(min, Math.min(max, n));

export function directorOptions(env) {
  const seed = /^\d{1,9}$/.test(env.TRIAS_DIRECTOR_SEED ?? '') ? Number(env.TRIAS_DIRECTOR_SEED) : undefined;
  return { bots: env.TRIAS_DIRECTOR_CASUAL === '1', horde: env.TRIAS_DIRECTOR_HORDE === '1',
    presentation: env.TRIAS_DIRECTOR_PRESENTATION === '1', debug: env.TRIAS_DIRECTOR_DEBUG === '1', seed };
}

function mix(value) {
  return (Math.imul(value ^ (value >>> 16), 0x45d9f3b) ^ (value >>> 9)) >>> 0;
}

export class CombatDirector {
  constructor(room, options) {
    this.room = room;
    this.options = options;
    this.seed = options.seed ?? [...room.code].reduce((seed, letter) => mix(seed ^ letter.codePointAt(0)), 71);
    this.round = -1;
  }

  reset(now) {
    this.round++;
    this.roundSeed = mix(this.seed ^ Math.imul(this.round + 1, 1779033703)) % 1_000_000_000;
    this.phase = 'build-up';
    this.startedAt = now;
    this.phaseAt = now;
    this.intensity = 0;
    this.nextSample = now + SAMPLE;
    this.lastEngagement = now - 30;
    this.shots = this.impact = this.kills = this.clustered = 0;
    this.nextDebug = now + 8;
    this.suspended = this.room.humans.size > 1;
  }

  get directsBots() {
    return this.options.bots && this.room.bots && CASUAL.has(this.room.mode) && !this.suspended && this.room.humans.size <= 1;
  }

  get directsHorde() { return this.options.horde && Boolean(this.room.horde); }
  get active() { return this.directsBots || this.directsHorde; }

  onJoin() {
    if (!this.options.bots || !CASUAL.has(this.room.mode) || this.room.humans.size <= 1 || this.suspended) return;
    this.suspended = true;
    for (const player of this.room.players.values()) if (player.brain) {
      player.brain.planAt = player.brain.routeAt = -1;
    }
  }

  record(now, damage = 0, eliminated = false) {
    if (!this.active) return;
    if (damage > 0) {
      this.impact += damage;
      this.lastEngagement = now;
      this.kills += Number(eliminated);
    } else this.shots++;
  }

  step(now) {
    if (!this.active || now < this.nextSample) return;
    this.nextSample = now + SAMPLE;
    let alive = 0, moving = 0, pressure = 0, watching = 0, clustered = 0, bots = 0;
    const groups = new Map();
    for (const pilot of this.room.players.values()) {
      if (pilot.hp <= 0) continue;
      alive++;
      if (pilot.vx ** 2 + pilot.vz ** 2 > 25) moving++;
      if (pilot.brain?.target) watching++;
      if (!pilot.hordeType) pressure += 1 - Math.min(1, (pilot.hp + .55 * pilot.armor) / PILOT_HP);
      if (!pilot.bot) continue;
      bots++;
      const cell = Math.floor(pilot.x / 8) + ':' + Math.floor(pilot.z / 8);
      const count = (groups.get(cell) ?? 0) + 1;
      groups.set(cell, count);
      clustered = Math.max(clustered, count);
    }
    const room = this.room;
    const objective = room.mode === 'dom' ? room.points.some(point => point.contested || point.progress > .02)
      : room.mode === 'ctf' ? room.flags.some(flag => flag.status !== 'home')
      : Boolean(room.siege?.nodes.some(node => node.contested || node.progress > .02));
    const engagement = Math.max(0, 1 - (now - this.lastEngagement) / 5);
    const raw = clamp(Math.min(.19, this.shots * .024) + Math.min(.30, this.impact / 390)
      + Math.min(.17, this.kills * .085) + engagement * .21 + Number(objective) * .12
      + (.11 * pressure + .08 * moving + .12 * watching) / Math.max(1, alive)
      + (bots >= 4 && clustered >= 4 ? .05 : 0), 0, 1);
    this.intensity += (raw - this.intensity) * (raw > this.intensity ? .43 : .25);
    this.shots = this.impact = this.kills = 0;
    this.clustered = clustered;
    this.advance(now);
    if (this.options.debug && now >= this.nextDebug) {
      this.nextDebug = now + 8;
      console.error(JSON.stringify({ kind: 'combat-director', room: room.code, ...this.diagnostics(),
        presentation: Boolean(this.options.presentation) }));
    }
  }

  advance(now) {
    const age = now - this.phaseAt;
    let phase = this.phase;
    if (this.room.horde?.nextWaveAt && this.room.horde.wave > 0) phase = 'recovery';
    else if (this.phase === 'build-up' && age >= RULES.minRise && this.intensity >= RULES.engage) phase = 'engagement';
    else if (this.phase === 'engagement' && this.intensity >= RULES.peak && age >= 2) phase = 'peak';
    else if (this.phase === 'engagement' && age >= 5 && this.intensity <= RULES.cool) phase = 'recovery';
    else if (this.phase === 'peak' && age >= RULES.minPeak && (age >= RULES.maxPeak || this.intensity < RULES.cool)) phase = 'recovery';
    else if (this.phase === 'recovery' && age >= RULES.recovery) phase = this.intensity >= RULES.engage ? 'engagement' : 'build-up';
    if (phase !== this.phase) { this.phaseAt = now; this.phase = phase; }
  }

  get retreatScale() {
    if (!this.directsBots) return 1;
    return this.phase === 'recovery' ? 1.15 : this.phase === 'engagement' ? .94 : 1;
  }

  get rangeOffset() {
    if (!this.directsBots) return 0;
    return this.phase === 'engagement' ? -1.5 : this.phase === 'recovery' ? 1.8 : 0;
  }

  // Uses public map landmarks and allied assignments. No enemy coordinates are accepted here.
  patrol(player, brain, now) {
    if (!this.directsBots || !['tdm', 'ffa'].includes(this.room.mode)) return null;
    if (brain.directorPatrol && now < brain.directorPatrol.until &&
        distanceSquared(player, brain.directorPatrol.point) > 3.2 ** 2) return brain.directorPatrol;
    const layout = this.room.layout;
    const stage = this.phase !== 'engagement' && brain.traits.team > .48;
    const regions = stage ? layout.points.flatMap((point, index) => {
      const base = layout.bases[index];
      return [{ x: .58 * point.x + .42 * base.x, z: .58 * point.z + .42 * base.z }];
    }) : layout.points;
    if (!regions.length) return null;
    const anchor = mix(this.roundSeed + player.team * 127 + player.slot * 17 + Math.floor((now - this.startedAt) / 13)) % regions.length;
    let best, score = -Infinity;
    for (const [index, point] of regions.entries()) {
      if (!point || blocked(point.x, point.z, .65, layout.terrain?.(point.x, point.z) ?? 0, this.room.combatLayout)) continue;
      let near = 0, assigned = 0;
      for (const ally of this.room.players.values()) {
        if (ally === player || ally.hp <= 0 || !ally.bot || this.room.mode === 'ffa' || ally.team !== player.team) continue;
        if (distanceSquared(ally, point) < 5.5 ** 2) near++;
        if (ally.brain?.directorPatrol && distanceSquared(ally.brain.directorPatrol.point, point) < 2 ** 2) assigned++;
      }
      const weight = (anchor === index ? 7 : 0) - (near + assigned) * 8
        - Math.sqrt(distanceSquared(player, point)) * .32
        - (brain.directorPatrol && distanceSquared(brain.directorPatrol.point, point) < 2 ** 2 ? 9 : 0);
      if (weight > score) { best = point; score = weight; }
    }
    if (!best) return null;
    brain.directorPatrol = { point: best, intent: stage ? 'director-stage' : 'director-sweep', until: now + 5.5 };
    return brain.directorPatrol;
  }

  hordeInterval(base, wave, remaining) {
    if (!this.directsHorde) return base;
    const beat = (this.roundSeed + wave + remaining) % 3;
    return clamp(base * (beat === 0 ? 1.30 : .88), .43, 1.75);
  }

  hordeFlank(wave, remaining) {
    return this.directsHorde ? 1 + (this.roundSeed + wave + remaining) % 2 : 1 + (wave + remaining) % 2;
  }

  publicFrame(now) {
    if (!this.options.presentation) return undefined;
    // Cosmetic clock uses only elapsed round time and the announced Horde preparation state.
    return [this.roundSeed, Math.floor(Math.max(0, now - this.startedAt)), this.room.horde?.nextWaveAt ? 1 : 0];
  }

  diagnostics() {
    const intents = {};
    for (const bot of this.room.players.values()) if (bot.bot && bot.hp > 0) {
      const intent = bot.brain?.intent ?? (bot.hordeType ? 'wave' : 'seeking');
      intents[intent] = (intents[intent] ?? 0) + 1;
    }
    return { phase: this.phase, intensity: +this.intensity.toFixed(2), seed: this.roundSeed, bots: this.directsBots,
      horde: this.directsHorde, retreatScale: this.retreatScale, rangeOffset: this.rangeOffset,
      cluster: this.clustered ?? 0, thresholds: RULES, intents };
  }
}
