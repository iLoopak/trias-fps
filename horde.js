import { guideBotAim, route, visible } from './bots.js';
import { brainFor } from './bot-profiles.js';
import { senseBots } from './bot-perception.js';
import { distanceSquared, eyeHeight, packCover, PILOT_HP, TEAM_SIZE, wallDistance, WEAPONS } from './public/rules.js';

const WAVES = 8;
const MAX_ACTIVE = 8;
const CORE_HP = 740;
const PREPARATION = 22;
const BUILD_RANGE = 6;
const SITES = [[-8, -20, .25], [0, -17, 0], [8, -20, -.25], [-5.5, -24, .55], [5.5, -24, -.55]];
const STRUCTURES = {
  barricade: { cost: 30, hp: 220, w: 2.9, d: .8, h: 1.2 },
  turret: { cost: 65, hp: 130, w: 1.15, d: 1.15, h: .89 },
};
const SALVAGE = { runner: 8, striker: 11, crusher: 17, sentinel: 23 };
const TYPES = {
  runner: { name: 'Lovec', character: 'kestrel', hp: 64, armor: 0, reach: 1.85, damage: 13, pace: 1.0, delay: .9 },
  striker: { name: 'Střelec', character: 'vector', hp: 100, armor: 10, reach: 14, damage: 13, pace: .82, delay: 1.2, weapon: 'arc' },
  crusher: { name: 'Drtič', character: 'bulwark', hp: 218, armor: 25, reach: 2.6, damage: 39, pace: .55, delay: 1.5 },
  sentinel: { name: 'Strážce', character: 'wraith', hp: 270, armor: 45, reach: 17, damage: 30, pace: .64, delay: 1.45, weapon: 'lance' },
};
const UNLOCK = { scatter: 2, arc: 3, surge: 4, lance: 5, rift: 6 };
const REWARDS = ['Doplňte munici', 'Scatter a granát', 'Arc a oprava jádra', 'Overcharge a léčení',
  'Lance a štít', 'Rift a granáty', 'Overcharge a oprava jádra', 'Veškeré zásoby'];
const SUPPORT = ['repair', 'armor', 'scrap'];

export class Horde {
  constructor(room) { this.room = room; }

  reset(now) {
    for (const pilot of this.room.players.values()) if (pilot.hordeType) this.room.players.delete(pilot.id);
    this.core = { ...this.room.layout.bases[0], y: .78, hp: CORE_HP, maxHp: CORE_HP };
    this.wave = this.remaining = 0;
    this.scrap = 40;
    this.votes = new Map();
    this.lastSupport = null;
    this.structures = SITES.map(([x, z, angle], id) => ({ id, x, z, angle, kind: null }));
    this.layout = this.room.layout;
    this.nextEnemy = this.nextEnemy ?? 0;
    this.nextWaveAt = now + PREPARATION;
    this.deployAt = now;
  }

  get alive() {
    return [...this.room.players.values()].filter(pilot => pilot.hordeType && pilot.hp > 0);
  }

  available(item) { return this.wave >= (UNLOCK[item.type] ?? 0); }

  get council() { return this.nextWaveAt > 0 && this.wave > 0 && this.wave < WAVES && this.wave % 2 === 0; }

  vote(pilot, option, now) {
    if (!this.council || now >= this.nextWaveAt || this.room.restartAt || !SUPPORT.includes(option)
        || ![...this.room.humans].some(peer => peer.player === pilot)) return false;
    this.votes.set(pilot.id, option);
    return true;
  }

  supportCounts() {
    const tally = [0, 0, 0];
    for (const peer of this.room.humans) {
      const index = SUPPORT.indexOf(this.votes.get(peer.player.id));
      if (index >= 0) tally[index]++;
    }
    return tally;
  }

  resolveSupport() {
    const tally = this.supportCounts();
    const max = Math.max(...tally);
    if (!max) return null;
    const priority = Math.floor(this.wave / 2 - 1) % SUPPORT.length;
    for (let offset = 0; offset < SUPPORT.length; offset++) {
      const i = (priority + offset) % SUPPORT.length;
      if (tally[i] === max) return SUPPORT[i];
    }
  }

  build(pilot, id, kind, now) {
    const site = Number.isInteger(id) ? this.structures[id] : null;
    const spec = Object.hasOwn(STRUCTURES, kind) ? STRUCTURES[kind] : null;
    if (!pilot || pilot.bot || pilot.hp <= 0 || this.room.restartAt || !this.nextWaveAt || now >= this.nextWaveAt)
      return 'Stavět můžeš jen během přípravy.';
    if (!site || !spec || site.kind) return 'Vyber volné stavební místo.';
    if (distanceSquared(pilot, site) > BUILD_RANGE ** 2 || Math.abs(pilot.y) > 2)
      return 'Přibliž se ke stavebnímu místu.';
    if (this.scrap < spec.cost) return 'Týmu chybí šrot.';
    const { w, d, h, hp, cost } = spec;
    const [box] = packCover([{ x: site.x, z: site.z, angle: site.angle, w, d, h }]);
    if ([...this.room.players.values()].some(p => {
      if (p.hp <= 0 || p.y >= h) return false;
      const dx = p.x - site.x, dz = p.z - site.z;
      return Math.abs(dx * box.cos - dz * box.sin) < w / 2 + .43 && Math.abs(dx * box.sin + dz * box.cos) < d / 2 + .43;
    })) return 'Na stavebním místě někdo stojí.';
    Object.assign(site, { kind, box, hp, maxHp: hp, yaw: 0, pitch: 0, readyAt: now, builder: pilot });
    this.scrap -= cost;
    this.refreshCover();
    this.room.events.push({ type: 'built', position: [site.x, .25, site.z], kind });
    return null;
  }

  refreshCover() {
    this.layout = { ...this.room.layout, cover: [...this.room.layout.cover, ...this.structures.filter(s => s.kind).map(s => s.box)] };
  }

  salvage(enemy) { this.scrap += SALVAGE[enemy.hordeType]; }

  damageStructure(site, damage) {
    site.hp = Math.max(0, site.hp - damage);
    if (site.hp) return;
    this.room.events.push({ type: 'wreck', position: [site.x, .42, site.z] });
    this.structures[site.id] = { id: site.id, x: site.x, z: site.z, angle: site.angle, kind: null };
    this.refreshCover();
  }

  fireTurrets(now, active) {
    for (const site of this.structures) {
      if (site.kind !== 'turret' || now < site.readyAt) continue;
      const from = { x: site.x, y: 1.27, z: site.z };
      let target, best = 19;
      for (const enemy of active) {
        if (enemy.hp <= 0 || enemy.safeUntil > now) continue;
        const dx = enemy.x - from.x, dy = enemy.y + .82 - from.y, dz = enemy.z - from.z;
        const distance = Math.hypot(dx, dy, dz);
        if (distance >= best || distance < .001) continue;
        if (wallDistance(from, { x: dx / distance, y: dy / distance, z: dz / distance }, distance, this.layout) < distance - .04) continue;
        target = enemy; best = distance;
      }
      if (!target) continue;
      site.readyAt = now + .47;
      const to = [target.x, target.y + .82, target.z];
      site.yaw = Math.atan2(to[0] - from.x, to[2] - from.z);
      site.pitch = Math.asin((to[1] - from.y) / best);
      this.room.events.push({ type: 'tower-shot', from: [from.x, from.y, from.z], position: to });
      this.room.damage(site.builder, target, 15, now, from, 'arc', to, false);
    }
  }

  reward(pilot, now) {
    for (const [weapon, wave] of Object.entries(UNLOCK)) {
      if (wave > this.wave || !Object.hasOwn(WEAPONS, weapon)) continue;
      const spec = WEAPONS[weapon];
      pilot.loadout[weapon] = { ammo: spec.magazine, reserve: spec.reserve };
    }
    if (this.wave > 1) pilot.grenades = this.wave >= 6 ? 2 : 1;
    if (this.wave >= 4) pilot.hp = Math.min(PILOT_HP, pilot.hp + 30);
    if (this.wave >= 5) pilot.armor = Math.max(pilot.armor, 30);
    if (this.wave === 7 || this.wave === 8) pilot.surgeUntil = now + 18;
  }

  start(now) {
    const support = this.council ? this.resolveSupport() : null;
    this.votes.clear();
    this.lastSupport = support;
    this.wave++;
    this.nextWaveAt = 0;
    this.deployAt = now;
    this.remaining = 4 + this.wave * 2 + Math.max(0, this.room.humans.size - 1) * 2;
    this.core.hp = Math.min(CORE_HP, this.core.hp + (this.wave === 3 || this.wave === 7 ? 115 : 42));
    for (const pilot of this.room.players.values()) if (pilot.hp > 0 && !pilot.hordeType) this.reward(pilot, now);
    if (support === 'scrap') this.scrap += 45;
    if (support === 'repair') {
      this.core.hp = Math.min(CORE_HP, this.core.hp + 90);
      for (const site of this.structures) if (site.kind) site.hp = Math.min(site.maxHp, site.hp + 35);
    }
    if (support === 'armor') for (const pilot of this.room.players.values()) if (pilot.hp > 0 && !pilot.hordeType) {
      pilot.armor = Math.max(pilot.armor, 40);
      pilot.grenades = Math.min(2, pilot.grenades + 1);
    }
    this.room.events.push({ type: 'notice', text: `Vlna ${this.wave}/${WAVES} · ${REWARDS[this.wave - 1]}` });
  }

  dispatch(now, active) {
    const index = Array.from({ length: MAX_ACTIVE }, (_, i) => i).find(i => !active.some(pilot => pilot.hordeSlot === i));
    const team = 1 + Math.floor(index / TEAM_SIZE);
    const slot = index % TEAM_SIZE;
    const variant = this.wave >= 5 && this.remaining % 6 === 1 ? 'sentinel'
      : this.wave >= 3 && this.remaining % 4 === 0 ? 'crusher'
        : this.wave >= 2 && this.remaining % 3 === 0 ? 'striker' : 'runner';
    const type = TYPES[variant];
    const id = 'horde-' + ++this.nextEnemy;
    const enemy = this.room.addPlayer(id, type.name, team, slot, now, true, type.character, this.nextEnemy % 2 === 0 ? 'male' : undefined);
    const director = this.room.director;
    const flank = director?.hordeFlank(this.wave, this.remaining) ?? 1 + (this.remaining + this.wave) % 2;
    Object.assign(enemy, this.room.layout.spawns[flank][slot], { hordeType: variant, hordeSlot: index,
      hp: Math.round(type.hp * (1 + (this.wave - 1) * .10)),
      armor: type.armor, safeUntil: now + .35, strikeAt: now + .8, routeAt: now, weapon: type.weapon ?? 'pulse' });
    enemy.loadout[enemy.weapon] = { ammo: WEAPONS[enemy.weapon].magazine, reserve: Infinity };
    this.remaining--;
    const interval = Math.max(.43, 1.25 - this.wave * .10);
    this.deployAt = now + (director?.hordeInterval(interval, this.wave, this.remaining) ?? interval);
  }

  input(enemy, now) {
    const type = TYPES[enemy.hordeType];
    const core = this.core;
    const brain = brainFor(this.room,enemy);
    senseBots(this.room,enemy,brain,now);
    const observed = brain.target;
    const candidate = observed && observed.distance <= type.reach ? this.room.players.get(observed.id) : null;
    const player = candidate?.hp > 0 && candidate.safeUntil <= now && visible(enemy,candidate,this.layout) ? candidate : null;
    const nearCore = distanceSquared(enemy, core) <= (type.weapon ? type.reach * .69 : type.reach) ** 2
      && visible(enemy, core, this.layout);
    const barrier = this.structures.find(site => site.kind
      && distanceSquared(enemy, site) <= Math.max(type.reach * (type.weapon ? .55 : 1), 2.25) ** 2
      && visible(enemy, { ...site, y: 0 }, this.room.layout));
    const focus = player ?? barrier ?? (nearCore ? core : null);
    const firingYaw = focus ? Math.atan2(enemy.x-focus.x,enemy.z-focus.z) : enemy.yaw;
    const attack = focus && now >= enemy.strikeAt && (!player || brain.reactionAt <= now &&
      (!type.weapon || Math.abs(Math.atan2(Math.sin(firingYaw-enemy.yaw),Math.cos(firingYaw-enemy.yaw))) < .16));
    const from = { x: enemy.x, y: enemy.y + eyeHeight(enemy), z: enemy.z };
    if (attack) {
      enemy.strikeAt = now + type.delay;
      if (focus === barrier) {
        this.damageStructure(barrier, Math.round(type.damage * (1 + (this.wave - 1) * .06)));
        this.room.events.push({ type: 'core-hit', from: [from.x, from.y, from.z],
          position: [barrier.x, .72, barrier.z], weapon: type.weapon ?? 'scatter' });
      } else if (!player) {
        this.core.hp = Math.max(0, core.hp - Math.round(type.damage * (1 + (this.wave - 1) * .06)));
        this.room.events.push({ type: 'core-hit', from: [enemy.x, from.y, enemy.z], position: [core.x, core.y, core.z], weapon: type.weapon ?? 'scatter' });
      } else if (!type.weapon) {
        const hit = this.room.damage(enemy, player, type.damage, now, from, 'scatter');
        this.room.events.push({ type: 'damage', id: enemy.id, from: [from.x, from.y, from.z], hits: [hit] });
      }
    }
    if (now >= enemy.routeAt) {
      enemy.routeAt = now + .48 + enemy.slot * .07;
      enemy.path = route(enemy, core, this.room.layout);
    }
    while (enemy.path.length && distanceSquared(enemy, enemy.path[0]) < 1.1 ** 2) enemy.path.shift();
    const target = focus ?? enemy.path[0] ?? core;
    const yaw = Math.atan2(enemy.x - target.x, enemy.z - target.z);
    const pitch = player ? Math.atan2(player.y + eyeHeight(player) - .43 - from.y,Math.sqrt(distanceSquared(enemy,player)))
      + Math.sin(now*6+enemy.slot)*brain.tuning.error*.42 : 0;
    const aim = focus ? guideBotAim(brain,yaw,pitch) : { yaw,pitch: 0 };
    if (!focus) brain.lookYaw = yaw;
    return { x: 0, z: focus ? 0 : type.pace, ...aim,
      fire: Boolean(attack && player && type.weapon), sprint: !focus && enemy.hordeType === 'runner', reload: true };
  }

  step(now) {
    for (const pilot of this.room.players.values()) if (pilot.hordeType && pilot.hp <= 0) this.room.removePlayer(pilot.id, now);
    const active = this.alive;
    if (this.core.hp === 0) return 'Jádro padlo';
    if (this.nextWaveAt && now >= this.nextWaveAt) this.start(now);
    if (!this.nextWaveAt) this.fireTurrets(now, active);
    if (this.remaining > 0 && now >= this.deployAt && active.length < MAX_ACTIVE) this.dispatch(now, active);
    if (!this.nextWaveAt && !this.remaining && !active.some(enemy => enemy.hp > 0)) {
      if (this.wave === WAVES) return 'Jádro ubráněno';
      this.nextWaveAt = now + PREPARATION;
      this.room.events.push({ type: 'notice', text: `Vlna ${this.wave} odražena · ${PREPARATION} sekund na přípravu` });
    }
  }

  snapshot(now) {
    return { wave: this.wave, waves: WAVES, enemies: this.remaining + this.alive.length,
      countdown: Math.max(0, Math.ceil(this.nextWaveAt - now)), core: this.core,
      council: this.council ? { counts: this.supportCounts(), votes: [...this.votes] } : undefined,
      support: this.lastSupport ?? undefined,
      build: { scrap: this.scrap, range: BUILD_RANGE, types: STRUCTURES,
        sites: this.structures.map(({ id, x, z, angle, kind, hp, maxHp, yaw, pitch }) => ({ id, x, z, angle, kind, hp, maxHp, yaw, pitch })) } };
  }
}
