import { route } from './bots.js';
import { distanceSquared, packCover, PILOT_HP, rayBox, TEAM_SIZE, TICK, wallDistance, WEAPONS } from './public/rules.js';

const WAVES = 8;
const MAX_ACTIVE = 8;
const CORE_HP = 740;
const PREPARATION = 22;
// When every defender is ready the preparation is cut to this many seconds.
const READY_LEAD = 3;
const BUILD_RANGE = 6;
// Building during a wave is allowed while no infected is this close to the socket.
const BUILD_CLEARANCE = 5;
// Repairs restore this much structure health for this much shared salvage.
const REPAIR = { cost: 15, hp: 80 };
const SITES = [[-8, -20, .25], [0, -17, 0], [8, -20, -.25], [-5.5, -24, .55], [5.5, -24, -.55]];
const STRUCTURES = {
  barricade: { cost: 30, hp: 220, w: 2.9, d: .8, h: 1.2 },
  turret: { cost: 65, hp: 130, w: 1.15, d: 1.15, h: .89 },
};
const SALVAGE = { runner: 8, spitter: 11, crusher: 17, thrower: 23 };
const TYPES = {
  runner: { name: 'Nakažený', character: 'kestrel', hp: 72, armor: 0, reach: 1.65, damage: 17, pace: 0.88, delay: 1.06, windup: .35 },
  spitter: { name: 'Plivač', character: 'vector', hp: 82, armor: 0, reach: 12, damage: 22, pace: .61, delay: 2.45, windup: .66,
    shot: 'spore', speed: 13.5, gravity: 0 },
  crusher: { name: 'Drtič', character: 'bulwark', hp: 214, armor: 20, reach: 2.20, damage: 42, pace: .53, delay: 1.77, windup: .61 },
  thrower: { name: 'Vrhač', character: 'wraith', hp: 178, armor: 0, reach: 15, damage: 35, pace: .54, delay: 3.15, windup: .88,
    shot: 'rubble', speed: 9.5, gravity: 7.3 },
};
const UNLOCK = { scatter: 2, arc: 3, surge: 4, lance: 5, rift: 6 };
const REWARDS = ['Doplňte munici', 'Scatter a granát', 'Arc a oprava jádra', 'Overcharge a léčení',
  'Lance a štít', 'Rift a granáty', 'Overcharge a oprava jádra', 'Veškeré zásoby'];
const SUPPORT = ['repair', 'armor', 'scrap'];

function attackLine(enemy, target, height, layout) {
  const from = { x: enemy.x, y: enemy.y + 1.34, z: enemy.z };
  const x = target.x - from.x, y = height - from.y, z = target.z - from.z;
  const length = Math.hypot(x, y, z);
  return length < .01 || wallDistance(from, { x: x / length, y: y / length, z: z / length },
    length, layout) >= length - .04;
}

export class Horde {
  constructor(room) { this.room = room; }

  reset(now) {
    for (const pilot of this.room.players.values()) if (pilot.hordeType) this.room.players.delete(pilot.id);
    const base = this.room.layout.bases[0];
    this.core = { ...base, y: (base.y ?? 0) + .78, hp: CORE_HP, maxHp: CORE_HP };
    this.wave = this.remaining = 0;
    this.scrap = 40;
    this.votes = new Map();
    this.ready = new Set();
    this.lastSupport = null;
    const sites = this.room.layout.construction ?? SITES.map(([x,z,angle])=>({x,y:0,z,angle}));
    this.structures = sites.map((site,id)=>({ ...site,id,kind:null }));
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

  // Defenders mark themselves ready during preparation; when all are, the next wave starts in a few seconds.
  markReady(pilot, now) {
    if (!this.nextWaveAt || now >= this.nextWaveAt || this.room.restartAt || pilot?.hordeType
        || ![...this.room.humans].some(peer => peer.player === pilot)) return false;
    if (this.ready.has(pilot.id)) this.ready.delete(pilot.id);
    else this.ready.add(pilot.id);
    const everyone = [...this.room.humans].every(peer => this.ready.has(peer.player.id));
    if (everyone && this.nextWaveAt - now > READY_LEAD) {
      this.nextWaveAt = now + READY_LEAD;
      this.room.events.push({ type: 'notice', text: 'Všichni připraveni · vlna za ' + READY_LEAD + ' s' });
    }
    return true;
  }

  // Sockets stay buildable during a wave, just not with infected at arm's length.
  contested(site, now) {
    return !!this.nextWaveAt && now < this.nextWaveAt ? false
      : this.alive.some(enemy => distanceSquared(enemy, site) < BUILD_CLEARANCE ** 2);
  }

  repair(pilot, site, now) {
    if (!site?.kind) return 'Vyber postavenou obranu.';
    if (site.hp >= site.maxHp) return 'Obrana je v pořádku.';
    if (distanceSquared(pilot, site) > BUILD_RANGE ** 2 || Math.abs(pilot.y - site.y) > 1.4)
      return 'Přibliž se k obraně.';
    if (this.contested(site, now)) return 'Nepřátelé jsou u obrany.';
    if (this.scrap < REPAIR.cost) return 'Týmu chybí šrot.';
    this.scrap -= REPAIR.cost;
    site.hp = Math.min(site.maxHp, site.hp + REPAIR.hp);
    this.room.events.push({ type: 'repaired', position: [site.x, site.y + .25, site.z], kind: site.kind });
    return null;
  }

  build(pilot, id, kind, now) {
    const site = Number.isInteger(id) ? this.structures[id] : null;
    if (!pilot || pilot.bot || pilot.hp <= 0 || this.room.restartAt || this.core.hp <= 0)
      return 'Teď stavět nemůžeš.';
    if (kind === 'repair') return this.repair(pilot, site, now);
    const spec = Object.hasOwn(STRUCTURES, kind) ? STRUCTURES[kind] : null;
    if (!site || !spec || site.kind) return 'Vyber volné stavební místo.';
    if (distanceSquared(pilot, site) > BUILD_RANGE ** 2 || Math.abs(pilot.y - site.y) > 1.4)
      return 'Přibliž se ke stavebnímu místu.';
    if (this.contested(site, now)) return 'Nepřátelé jsou u stavebního místa.';
    if (this.scrap < spec.cost) return 'Týmu chybí šrot.';
    const { w, d, h, hp, cost } = spec;
    const [box] = packCover([{ x: site.x, z: site.z, angle: site.angle, w, d, bottom:site.y, h:site.y + h }]);
    if ([...this.room.players.values()].some(p => {
      if (p.hp <= 0 || p.y >= site.y + h || p.y + 1.78 <= site.y) return false;
      const dx = p.x - site.x, dz = p.z - site.z;
      return Math.abs(dx * box.cos - dz * box.sin) < w / 2 + .43 && Math.abs(dx * box.sin + dz * box.cos) < d / 2 + .43;
    })) return 'Na stavebním místě někdo stojí.';
    Object.assign(site, { kind, box, hp, maxHp: hp, yaw: 0, pitch: 0, readyAt: now, builder: pilot });
    this.scrap -= cost;
    this.refreshCover();
    this.room.events.push({ type: 'built', position: [site.x, site.y + .25, site.z], kind });
    return null;
  }

  refreshCover() {
    this.layout = { ...this.room.layout, cover: [...this.room.layout.cover, ...this.structures.filter(s => s.kind).map(s => s.box)] };
  }

  salvage(enemy) {
    const amount = SALVAGE[enemy.hordeType];
    this.scrap += amount;
    this.room.events.push({ type: 'salvage', amount, position: [enemy.x, enemy.y + 1.2, enemy.z] });
  }

  damageStructure(site, damage) {
    site.hp = Math.max(0, site.hp - damage);
    this.room.events.push({ type: 'structure-hit', site: site.id, hp: site.hp, position: [site.x, site.y + .6, site.z] });
    if (site.hp) return;
    this.room.events.push({ type: 'wreck', position: [site.x, site.y + .42, site.z], structure: site.kind });
    this.structures[site.id] = { id: site.id, x: site.x, y: site.y, z: site.z, angle: site.angle, kind: null };
    this.refreshCover();
  }

  fireTurrets(now, active) {
    for (const site of this.structures) {
      if (site.kind !== 'turret' || now < site.readyAt) continue;
      const from = { x: site.x, y: site.y + 1.27, z: site.z };
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
    this.ready.clear();
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
    const variant = this.wave >= 5 && this.remaining % 8 === 1 ? 'thrower'
      : this.wave >= 3 && this.remaining % 5 === 0 ? 'crusher'
        : this.wave >= 3 && this.remaining % 7 === 0 ? 'spitter' : 'runner';
    const type = TYPES[variant];
    const id = 'horde-' + ++this.nextEnemy;
    const enemy = this.room.addPlayer(id, type.name, team, slot, now, true, type.character, this.nextEnemy % 2 === 0 ? 'male' : undefined);
    const director = this.room.director;
    const flank = director?.hordeFlank(this.wave, this.remaining) ?? 1 + (this.remaining + this.wave) % 2;
    Object.assign(enemy, this.room.layout.spawns[flank][slot], { hordeType: variant, hordeSlot: index,
      hp: Math.round(type.hp * (1 + (this.wave - 1) * .10)),
      armor: type.armor, safeUntil: now + .35, strikeAt: now + .8, routeAt: now, weapon: null, loadout: {} });
    this.remaining--;
    const interval = Math.max(.43, 1.25 - this.wave * .10);
    this.deployAt = now + (director?.hordeInterval(interval, this.wave, this.remaining) ?? interval);
  }

  input(enemy, now) {
    const type = TYPES[enemy.hordeType];
    const core = this.core;
    const around = (enemy.hordeSlot / MAX_ACTIVE) * Math.PI * 2;
    const approach = { x: core.x + Math.cos(around) * 1.05, y:core.y - .78, z: core.z + Math.sin(around) * 1.05 };
    const position = target => ({ x: target.x, y: target === core ? core.y : target.y ?? 0, z: target.z });
    let focus;
    if (enemy.attack) {
      focus = enemy.attack.at;
      if (now >= enemy.attack.release) this.release(enemy, type, now);
    } else {
      const inReach = target => distanceSquared(enemy, target) <= type.reach ** 2 && Math.abs(enemy.y - (target.y ?? 0)) <= 1.25;
      const barricade = this.structures.find(site => site.kind && inReach(site)
        && attackLine(enemy, site, site.y + .72, this.room.layout));
      const coreInReach = inReach(core) && attackLine(enemy, core, core.y, this.layout);
      const defender = [...this.room.players.values()].find(pilot => this.room.hostile(enemy, pilot)
        && !pilot.away && pilot.safeUntil <= now && inReach(pilot) && attackLine(enemy, pilot, pilot.y + .8, this.layout));
      const victim = barricade ?? (coreInReach ? core : defender);
      if (victim) {
        focus = position(victim);
        if (now >= enemy.strikeAt) {
          enemy.attack = { target: victim, at: focus, release: now + type.windup };
          this.room.events.push({ type: 'horde-windup', id: enemy.id, from: [enemy.x, enemy.y, enemy.z],
            attack: enemy.hordeType, duration: type.windup });
        }
      }
    }
    if (now >= enemy.routeAt) {
      enemy.routeAt = now + .48 + enemy.slot * .07;
      enemy.path = route(enemy, approach, this.room.layout);
    }
    const layered = this.room.layout.fallY !== undefined;
    while (enemy.path.length && distanceSquared(enemy, enemy.path[0]) < (layered ? .43 : 1.1) ** 2
      && (!layered || Math.abs(enemy.y - enemy.path[0].y) < .085)) enemy.path.shift();
    const target = focus ?? enemy.path[0] ?? approach;
    const yaw = Math.atan2(enemy.x - target.x, enemy.z - target.z);
    const pace = layered && enemy.path.length
      ? Math.min(type.pace, Math.sqrt(distanceSquared(enemy,target)) * 1.69 + .07)
      : type.pace;
    return { x: 0, z: focus ? 0 : pace, yaw, pitch: 0 };
  }

  release(enemy, type, now) {
    const { target, at } = enemy.attack;
    enemy.attack = null;
    enemy.strikeAt = now + type.delay;
    const from = { x: enemy.x, y: enemy.y + 1.34, z: enemy.z };
    const damage = Math.round(type.damage * (1 + (this.wave - 1) * .06));
    if (type.shot) {
      const dx = at.x - from.x, dz = at.z - from.z;
      const time = Math.max(.12, Math.hypot(dx, dz) / type.speed);
      const height = target === this.core ? at.y : at.y + .8;
      this.room.projectiles.push({ id: ++this.room.nextProjectile, owner: enemy, weapon: type.shot,
        position: from, velocity: { x: dx / time, y: (height - from.y) / time + type.gravity * time * .5, z: dz / time },
        gravity: type.gravity, fuseAt: now + time + .18, damage, hordeAttack: true });
      this.room.events.push({ type: 'horde-release', id: enemy.id, attack: enemy.hordeType, from: [from.x, from.y, from.z] });
      return;
    }
    const structure = target === this.structures[target.id] && target.kind;
    const valid = target === this.core || structure || (this.room.hostile(enemy, target) && !target.away && target.safeUntil <= now);
    const inReach = valid && distanceSquared(enemy, target) <= type.reach ** 2
      && Math.abs(enemy.y - (target.y ?? 0)) <= 1.25;
    const aim = inReach && Math.hypot(target.x-at.x, target.z-at.z) <= 1.2;
    const aimHeight = target === this.core ? target.y : (target.y ?? 0) + .76;
    if (!aim || !attackLine(enemy, target, aimHeight, structure ? this.room.layout : this.layout)) {
      this.room.events.push({ type: 'horde-release', id: enemy.id, attack: enemy.hordeType, from: [from.x, from.y, from.z] });
      return;
    }
    this.impact(enemy, target, from, { x: target.x, y: aimHeight, z: target.z },
      damage, 'claw', now);
  }

  impact(enemy, target, from, at, damage, weapon, now) {
    const hits = [];
    if (target === this.core) target.hp = Math.max(0, target.hp - damage);
    else if (target && this.structures[target.id] === target && target.kind) this.damageStructure(target, damage);
    else if (target && this.room.hostile(enemy, target) && !target.away && target.safeUntil <= now)
      hits.push(this.room.damage(enemy, target, damage, now, from, weapon, [at.x, at.y, at.z]));
    this.room.events.push({ type: 'horde-impact', id: enemy.id, weapon, attack: enemy.hordeType,
      from: [from.x, from.y, from.z], position: [at.x, at.y, at.z], objective: target === this.core, hits });
  }

  advanceShot(shot, now) {
    const { position: origin, velocity } = shot;
    const vertical = velocity.y - shot.gravity * TICK * .5;
    const speed = Math.hypot(velocity.x, vertical, velocity.z);
    if (speed < .001) return true;
    const direction = { x: velocity.x / speed, y: vertical / speed, z: velocity.z / speed };
    const travel = speed * TICK;
    let { target, distance } = this.room.trace(shot.owner, origin, direction, travel, .09);
    // Constructed cover participates in trace; intersecting its box again identifies the structure that was hit.
    for (const site of this.structures) {
      if (!site.kind) continue;
      const { box } = site;
      const x = origin.x - site.x, z = origin.z - site.z;
      const local = { x: x * box.cos - z * box.sin, y: origin.y, z: x * box.sin + z * box.cos };
      const heading = { x: direction.x * box.cos - direction.z * box.sin, y: direction.y,
        z: direction.x * box.sin + direction.z * box.cos };
      const struck = rayBox(local, heading, box.min, box.max);
      if (struck <= distance && struck < travel) { target = site; distance = struck; }
    }
    const { x, z, y } = this.core;
    const coreHit = rayBox(origin, direction, { x: x - .61, y: y - .68, z: z - .61 },
      { x: x + .61, y: y + .68, z: z + .61 });
    if (coreHit < distance) { target = this.core; distance = coreHit; }
    const at = { x: origin.x + direction.x * distance, y: origin.y + direction.y * distance, z: origin.z + direction.z * distance };
    if (target || distance < travel - 0.00001 || now >= shot.fuseAt) {
      this.impact(shot.owner, target, origin, at, shot.damage, shot.weapon, now);
      return true;
    }
    shot.position = at;
    velocity.y -= shot.gravity * TICK;
    return false;
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
      // Clearing a wave pays the team, so every preparation has something to build or repair.
      const bonus = 10 + this.wave * 5;
      this.scrap += bonus;
      this.room.events.push({ type: 'notice', text: `Vlna ${this.wave} odražena · +${bonus} šrotu · ${PREPARATION} s na přípravu` });
    }
  }

  snapshot(now) {
    return { wave: this.wave, waves: WAVES, enemies: this.remaining + this.alive.length,
      countdown: Math.max(0, Math.ceil(this.nextWaveAt - now)), core: this.core,
      council: this.council ? { counts: this.supportCounts(), votes: [...this.votes] } : undefined,
      support: this.lastSupport ?? undefined,
      ready: this.nextWaveAt ? { ids: [...this.ready], needed: this.room.humans.size } : undefined,
      build: { scrap: this.scrap, range: BUILD_RANGE, clearance: BUILD_CLEARANCE, repair: REPAIR, types: STRUCTURES,
        sites: this.structures.map(({ id, x, y, z, angle, kind, hp, maxHp, yaw, pitch }) => ({ id, x, y, z, angle, kind, hp, maxHp, yaw, pitch })) } };
  }
}
