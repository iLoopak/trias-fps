import {
  aimVector, ARENAS, ARMORY, CAPACITY, CHARACTERS, DEFAULT_CHARACTER, DEFAULT_WEAPON, distanceSquared, eyeHeight, floorBelow,
  GRENADE, hitHeight, MED_HEAL, MODES, movePlayer, PILOT_HP, POWERUPS, SHIELD_ARMOR, rayBox, RESPAWN, TEAM_SIZE, TEAMS, TICK, TURRET, turretYaw, wallDistance, WEAPONS,
} from './public/rules.js';
import { botInput } from './bots.js';
import { normalizeDifficulty } from './bot-profiles.js';
import { botSound, noteBotHit } from './bot-perception.js';
import { Fleet } from './fleet.js';
import { Siege } from './siege.js';
import { Horde } from './horde.js';
import { Streaks } from './streaks.js';
import { hullDistance, vehicleAimFrame, VEHICLES } from './public/vehicles.js';
import { NETWORK_VERSION, packSnapshot } from './public/netcode.js';
import { Signature } from './signature.js';
import { explosionImpulse, grenadeOnRay } from './interactions.js';

const EMPTY = { x: 0, z: 0, sprint: false, fire: false, reload: false, jump: false, dodge: false, crouch: false };
const BOT_CHARACTERS = Object.keys(CHARACTERS);
const BOT_NAMES = ['Apex', 'Flux', 'Helix', 'Ghost', 'Nova', 'Pulse', 'Drift', 'Forge', 'Rift', 'Echo', 'Shade', 'Volt'];
const ROUND_PAUSE = 9;

export const fireProfileData = () => ARMORY.map(key => {
  const spec = WEAPONS[key];
  return [key, spec.interval, spec.magazine, spec.reload, spec.alt?.interval ?? 0, spec.alt?.cost ?? 0];
});

export class Match {
  constructor(code, mode, bots, layout = ARENAS.relay, signature = {}) {
    this.code = code;
    this.mode = mode;
    this.layout = mode === 'siege' ? ARENAS.basin : mode === 'horde' ? ARENAS.relay : layout;
    this.bots = bots;
    this.botDifficulty = normalizeDifficulty(signature.botDifficulty);
    this.botDebug = signature.botDebug === true;
    this.hordeSupport = signature.hordeSupport === true;
    const { intercept = false, recall = false, brace = false, traversal = false } = signature.interactions ?? {};
    this.interactions = { intercept, recall, brace, traversal };
    this.players = new Map();
    this.humans = new Set();
    this.events = [];
    this.legacyEvents = [];
    this.nextProjectile = 0;
    this.tickNumber = 0;
    this.fleet = new Fleet(this);
    this.siege = mode === 'siege' ? new Siege(this, this.layout.siege) : null;
    this.horde = mode === 'horde' ? new Horde(this) : null;
    this.streaks = new Streaks(this);
    this.signature = new Signature(this, signature);
    this.reset(0);
  }

  reset(now) {
    this.signature.reset();
    this.botSound = [];
    this.botSoundIndex = -1;
    this.botMetrics = { stuck: 0 };
    this.radioAt = [];
    this.radioReadyAt = now + 2.2;
    this.endsAt = now + (MODES[this.mode]?.time ?? 0);
    this.restartAt = 0;
    this.winner = null;
    this.scores = TEAMS.map(() => 0);
    this.points = this.layout.points.map(() => ({ owner: -1, capture: -1, progress: 0, contested: false }));
    this.flags = this.layout.bases.map(base => ({ ...base, y: base.y ?? 0, status: 'home', carrier: null, returnAt: 0 }));
    this.pickups = this.layout.pickups.map(() => 0);
    this.projectiles = [];
    this.siege?.reset();
    this.horde?.reset(now);
    this.streaks.reset();
    this.fleet.reset(now);
    this.turrets = this.layout.turrets.map(item => ({ ...item, user: null, heat: 0, hot: false, firedAt: -100 }));

    this.nextPointTick = now + 1;
    for (const player of this.players.values()) {
      player.kills = player.deaths = 0;
      this.spawn(player, now);
    }
  }

  spawn(player, now) {
    this.signature.respawn(player);
    const ffa = this.mode === 'ffa';
    const team = ffa ? (player.team + player.deaths) % TEAMS.length : player.team;
    const slot = ffa ? (player.slot + player.deaths) % TEAM_SIZE : player.slot;
    const start = this.layout.spawns[team][slot];
    const toward = this.layout.points[team];
    const kit = CHARACTERS[player.character] ?? CHARACTERS[DEFAULT_CHARACTER];
    const loadout = { [DEFAULT_WEAPON]: { ammo: WEAPONS[DEFAULT_WEAPON].magazine, reserve: Infinity } };
    if (kit.weapon !== DEFAULT_WEAPON) loadout[kit.weapon] = { ammo: kit.rounds, reserve: 0 };
    Object.assign(player, start, {
      hp: this.siege && !this.siege.alive(player.team) ? 0 : PILOT_HP, armor: 0, surgeUntil: 0, y: start.y ?? 0, vx: 0, vy: 0, vz: 0, grounded: true, airJump: true,
      dodgeCooldown: 0, dodgeTime: 0, airDodge: true, padCooldown: 0, crouch: false, grenades: 1, turret: -1, vehicle: -1, vehiclePlanAt: -100, impactAt: 0,
      weapon: kit.weapon, loadout,
      yaw: Math.atan2(start.x - toward.x, start.z - toward.z), pitch: 0, fireAt: now + 0.2,
      safeUntil: now + 1.4, reloadAt: 0, respawnAt: 0, input: EMPTY, inputAt: now, fireRequest: false, reloadRequest: false, jumpRequest: false, dodgeRequest: false, altRequest: false, grenadeRequest: false, useRequest: false, weaponRequest: null,
      spawn: player.spawn + 1, predictedTrigger: null, target: null, routeAt: 0, path: [], usedLaunch: false, launchAt: -100, dodgeDecisionAt: 0, stride: 0, sprint: false,
      chain: 0, perks: 0,
    });
    this.horde?.reward(player, now);
  }

  addPlayer(id, name, team, slot, now, bot = false, character = DEFAULT_CHARACTER, frame) {
    const player = { id, name, team, slot, bot, character, frame: frame === 'male' ? 'male' : undefined, kills: 0, deaths: 0, spawn: 0, ack: 0 };
    this.spawn(player, now);
    this.players.set(id, player);
    return player;
  }

  join(socket, name, now, character = DEFAULT_CHARACTER, frame) {
    if (this.horde && this.humans.size >= TEAM_SIZE) return false;
    if (this.humans.size >= CAPACITY) return false;
    const counts = TEAMS.map((_, team) => [...this.players.values()].filter(p => !p.bot && p.team === team).length);
    const open = counts.map((count, team) => ({ count, team })).filter(item => item.count < TEAM_SIZE);
    open.sort((a, b) => {
      const alive = this.siege ? Number(this.siege.alive(b.team)) - Number(this.siege.alive(a.team)) : 0;
      return alive || a.count - b.count || a.team - b.team;
    });
    const team = this.horde ? 0 : open[0].team;
    const slot = this.layout.spawns[team].findIndex((_, i) => ![...this.players.values()].some(p => p.team === team && p.slot === i && !p.bot));
    this.removePlayer('bot-' + team + '-' + slot, now);
    socket.player = this.addPlayer(socket.id, name, team, slot, now, false, character, frame);
    this.humans.add(socket);
    this.fillBots(now);
    return true;
  }

  fillBots(now) {
    if (this.horde ? !this.hordeSupport : !this.bots) return;
    for (const [team, spawns] of this.layout.spawns.entries()) {
      if (this.horde && team !== 0) continue;
      for (const slot of spawns.keys()) {
        if ([...this.players.values()].some(player => player.team === team && player.slot === slot)) continue;
        this.addPlayer('bot-' + team + '-' + slot, BOT_NAMES[team * TEAM_SIZE + slot], team, slot, now, true, BOT_CHARACTERS[(team + slot) % BOT_CHARACTERS.length], slot % 2 === 0 ? 'male' : undefined);
      }
    }
  }

  radioBot(player, line, now) {
    if (this.mode === 'ffa' || now < (this.radioAt[player.team] ?? this.radioReadyAt) ||
        now < (player.radioAt ?? 0)) return false;
    let message;
    for (const socket of this.humans) {
      if (!socket.botRadio || socket.player.team !== player.team || socket.player.hp <= 0 ||
          socket.readyState !== 1 || socket.bufferedAmount >= 8192) continue;
      message ??= JSON.stringify({ type: 'bot-radio', name: player.name, line, frame: player.frame === 'male' ? 'male' : 'female' });
      socket.send(message);
    }
    if (!message) return false;
    this.radioAt[player.team] = now + 8.5;
    player.radioAt = now + 19;
    return true;
  }

  removePlayer(id, now) {
    const player = this.players.get(id);
    if (!player) return;
    this.signature.leave(player);
    this.unmount(player);
    this.fleet.release(player, now);
    this.dropFlag(player, now);
    this.players.delete(id);
  }

  dropFlag(player, now) {
    const flag = this.flags.find(item => item.carrier === player.id);
    if (flag) Object.assign(flag, {
      status: 'dropped', carrier: null, x: player.x, z: player.z, y: floorBelow(player.x, player.z, player.y, 0, this.layout), returnAt: now + 15,
    });
  }

  returnFlag(team) {
    const base = this.layout.bases[team];
    Object.assign(this.flags[team], base, { y: base.y ?? 0, status: 'home', carrier: null, returnAt: 0 });
  }

  input(player, message, now) {
    if (!Number.isSafeInteger(message.seq) || message.seq <= (player.receivedSeq ?? player.ack)
      || !['x', 'z', 'yaw', 'pitch'].every(key => Number.isFinite(message[key]))) return;
    player.receivedSeq = message.seq;
    player.inputAt = now;
    player.input = {
      x: Math.max(-1, Math.min(1, message.x)), z: Math.max(-1, Math.min(1, message.z)),
      yaw: message.yaw % (Math.PI * 2), pitch: Math.max(-1.48, Math.min(1.48, message.pitch)),
      crouch: message.crouch === true, brake: message.brake === true, sprint: message.sprint === true, fire: message.fire === true, reload: message.reload === true,
    };
    player.fireRequest ||= player.input.fire;
    player.reloadRequest ||= player.input.reload;
    player.jumpRequest ||= message.jump === true;
    player.dodgeRequest ||= message.dodge === true;
    player.altRequest ||= message.alt === true;
    player.grenadeRequest ||= message.grenade === true;
    player.useRequest ||= message.use === true;
    if (typeof message.weapon === 'string' && Object.hasOwn(WEAPONS, message.weapon)) player.weaponRequest = message.weapon;
  }

  trigger(player, message, now) {
    if (message.fireId !== undefined) {
      const fireId = message.fireId;
      if (!Number.isSafeInteger(fireId) || fireId <= (player.lastFireId ?? 0)) return;
      player.lastFireId = fireId;
      if (player.hp <= 0 || this.restartAt || player.turret >= 0 || player.vehicle >= 0 || player.predictedTrigger
        || player.spawn !== message.spawn || player.weapon !== message.weapon
        || !Number.isFinite(message.yaw) || !Number.isFinite(message.pitch)) {
        this.rejectTrigger(player, fireId);
        return;
      }
      player.predictedTrigger = { fireId, weapon: message.weapon, alt: message.alt === true, time: now,
        yaw: message.yaw % (Math.PI * 2), pitch: Math.max(-1.48, Math.min(1.48, message.pitch)) };
      return;
    }
    if (player.hp <= 0 || this.restartAt || !Number.isFinite(message.yaw) || !Number.isFinite(message.pitch)) return;
    player.triggerAim = {
      yaw: message.yaw % (Math.PI * 2),
      pitch: Math.max(-1.48, Math.min(1.48, message.pitch)),
      time: now,
    };
    if (message.alt === true) player.altRequest = true;
    else player.fireRequest = true;
  }

  rejectTrigger(player, fireId) {
    this.events.push({ type: 'trigger-rejected', id: player.id, fireId });
  }

  hostile(attacker, target) {
    return target !== attacker && target.hp > 0 && target.vehicle < 0
      && (this.horde ? Boolean(target.hordeType) !== Boolean(attacker.hordeType) : this.mode === 'ffa' || target.team !== attacker.team);
  }

  get combatLayout() { return this.horde?.layout ?? this.layout; }

  trace(player, origin, direction, range, width = 0) {
    let distance = wallDistance(origin, direction, range, this.combatLayout);
    let target;
    for (const other of this.players.values()) {
      if (!this.hostile(player, other)) continue;
      const hit = rayBox(origin, direction,
        { x: other.x - 0.44 - width, y: other.y + 0.08 - width, z: other.z - 0.44 - width },
        { x: other.x + 0.44 + width, y: other.y + hitHeight(other) + width, z: other.z + 0.44 + width });
      if (hit >= distance) continue;
      distance = hit;
      target = other;
    }
    for (const hull of this.fleet.hulls) {
      if (!this.fleet.hostile(player, hull)) continue;
      const hit = hullDistance(hull, origin, direction, width);
      if (hit >= distance) continue;
      distance = hit;
      target = hull;
    }
    for (const core of this.siege?.cores ?? []) {
      if (core.team === player.team || !core.hp) continue;
      const hit = this.siege.distance(core, origin, direction);
      if (hit >= distance) continue;
      distance = hit;
      target = core;
    }
    const echo = this.signature.trace(player, origin, direction, distance, width);
    return echo.target ? echo : { target, distance };
  }

  damage(attacker, target, value, now, origin, weapon, impact, direct = true) {
    if (target.opened !== undefined) return this.siege.damage(attacker, target, value, now);
    if (target.kind) return this.fleet.damage(attacker, target, value, now, origin, weapon, impact);
    const amount = Math.round(value);
    const absorbed = Math.min(target.armor, Math.ceil(amount * 0.7));
    target.armor -= absorbed;
    const shieldBreak = absorbed > 0 && target.armor === 0;
    const health = Math.min(target.hp, amount - absorbed);
    target.hp -= health;
    noteBotHit(attacker,target.id,health,now);
    if (!target.hp) {
      this.signature.respawn(target);
      this.streaks.kill(attacker, target, direct);
      if (target.hordeType) this.horde.salvage(target);
      attacker.kills++;
      target.deaths++;
      target.respawnAt = now + (this.horde ? 5.5 : RESPAWN);
      this.unmount(target);
      this.fleet.release(target, now);
      this.dropFlag(target, now);
      if (this.mode === 'tdm') this.scores[attacker.team]++;
      this.events.push({ type: 'kill', attacker: attacker.name, victim: target.name, team: attacker.team,
        attackerId: attacker.id, victimId: target.id, weapon, impact,
        body: { x: target.x, y: target.y, z: target.z, vx: target.vx, vy: target.vy, vz: target.vz,
          yaw: target.yaw, crouch: target.crouch, team: target.team, slot: target.slot, character: target.character, frame: target.frame,
          weapon: target.weapon, hordeType: target.hordeType, from: [origin.x, origin.y, origin.z] },
      });
    }
    return { id: target.id, health, absorbed, eliminated: target.hp === 0, ...(shieldBreak ? { shieldBreak: true } : {}) };
  }

  vehicleAim(player, vehicle, range) {
    const frame = vehicleAimFrame(vehicle, player.yaw, player.pitch, this.layout);
    const { camera, gun, sight, aim, stop } = frame;
    const visible = this.trace(player, camera, sight, range + stop);
    const dx = camera.x + visible.distance * sight.x - gun.x;
    const dy = camera.y + visible.distance * sight.y - gun.y;
    const dz = camera.z + visible.distance * sight.z - gun.z;
    const distance = Math.hypot(dx, dy, dz);
    const forward = dx * aim.x + dy * aim.y + dz * aim.z;
    const direction = forward > 0.22 ? { x: dx / distance, y: dy / distance, z: dz / distance } : aim;
    return { origin: gun, direction };
  }

  shoot(player, now, alt = false, mounted = false, vehicle = null, trigger = null) {
    const vehicleGun = vehicle ? VEHICLES[vehicle.kind].gun : null;
    const key = vehicleGun?.key ?? (mounted ? 'arc' : player.weapon);
    const base = WEAPONS[key];
    const weapon = vehicleGun ?? (mounted ? TURRET : alt ? base.alt : base);
    if (!mounted && !vehicle) player.loadout[key].ammo -= weapon.cost ?? 1;
    if (!vehicle) player.fireAt = now + weapon.interval;
    player.safeUntil = now;
    if (player.bot && player.brain) player.brain.lastDischargeAt = now;
    const mountedAim = vehicle && this.vehicleAim(player, vehicle, weapon.range);
    const origin = trigger?.origin ?? mountedAim?.origin ?? { x: player.x, y: player.y + eyeHeight(player), z: player.z };
    const from = [origin.x, origin.y - 0.12, origin.z];
    botSound(this,player,origin,now,key === 'arc' ? 21 : 26);
    const hits = [];
    const ends = [];
    this.events.push({ type: 'shot', id: player.id, weapon: key, from, ends, hits, alt, mounted, vehicle: vehicle?.id, fireId: trigger?.fireId, echo: trigger?.echo });
    const shotYaw = trigger?.yaw ?? player.yaw;
    const shotPitch = trigger?.pitch ?? player.pitch;
    const power = player.surgeUntil > now ? 1.6 : 1;
    if (weapon.projectile) {
      this.projectiles.push({
        id: ++this.nextProjectile, owner: player, weapon: key, position: origin,
        direction: mountedAim?.direction ?? aimVector(shotYaw, shotPitch), range: weapon.range, power, spec: weapon,
        echo: trigger?.echo,
      });
      return;
    }
    const damage = new Map();
    const intercepts = this.interactions.intercept && !mounted && !vehicle && !trigger?.echo ? new Set() : null;
    for (const [index, offset] of weapon.pattern.entries()) {
      const direction = mountedAim?.direction ?? aimVector(shotYaw + offset, shotPitch + (index % 2 ? offset * 0.7 : -offset * 0.7));
      const { target, distance } = this.trace(player, origin, direction, weapon.range);
      const cut = intercepts && grenadeOnRay(this,player,origin,direction,distance,now);
      if (cut) {
        intercepts.add(cut.grenade);
        ends.push([origin.x + direction.x * cut.distance, origin.y + direction.y * cut.distance,
          origin.z + direction.z * cut.distance, 'grenade']);
        continue;
      }
      if (target && (target.isEcho || target.opened !== undefined || target.kind || target.safeUntil <= now)) {
        const hit = damage.get(target);
        if (hit) hit.damage += weapon.damage * power;
        else damage.set(target, { damage: weapon.damage * power, impact: [
          origin.x + direction.x * distance, origin.y + direction.y * distance, origin.z + direction.z * distance,
        ] });
      }
      const impact = target?.isEcho ? 'echo' : target?.opened !== undefined
        ? (target.opened & (1 << player.team) ? 'core' : 'shield')
        : target ? (target.kind || target.armor || target.safeUntil > now ? 'shield' : 'pilot')
          : distance < weapon.range ? 'wall' : null;
      ends.push([origin.x + direction.x * distance, origin.y + direction.y * distance, origin.z + direction.z * distance, impact]);
    }
    for (const [target, value] of damage) {
      if (target.isEcho) {
        this.signature.damage(target, value.damage);
        continue;
      }
      const hit = this.damage(player, target, value.damage, now, origin, key, value.impact);
      if (hit) {
        hits.push(hit);
        this.signature.combat(player, hit, now, Boolean(trigger?.echo));
      }
    }
    for (const grenade of intercepts ?? []) {
      const index = this.projectiles.indexOf(grenade);
      if (index < 0) continue;
      this.projectiles.splice(index,1);
      const position = grenade.position;
      this.events.push({ type: 'intercept', id: player.id, position: [position.x,position.y,position.z] });
      if ((!player.grounded || now - player.lastDodge < 1.12) && distanceSquared(player,position) < 12 ** 2)
        this.signature.action(player,'intercept',now,grenade.owner.id);
      this.explode(grenade,null,now);
    }
  }

  throwGrenade(player, now) {
    player.grenades--;
    player.safeUntil = now;
    const aim = aimVector(player.yaw, Math.max(-0.67, Math.min(0.78, player.pitch)));
    const position = { x: player.x, y: player.y + eyeHeight(player) - 0.13, z: player.z };
    this.projectiles.push({
      id: ++this.nextProjectile, owner: player, weapon: 'grenade', position, power: 1, fuseAt: now + GRENADE.fuse,
      velocity: { x: aim.x * 13.3, y: aim.y * 12.0 + 4.0, z: aim.z * 13.3 },
    });
    this.events.push({ type: 'grenade', id: player.id, from: [position.x, position.y, position.z] });
  }

  rollGrenade(projectile) {
    const { position, velocity } = projectile;
    velocity.y -= 14.7 * TICK;
    for (const axis of ['x', 'y', 'z']) {
      const step = Math.abs(velocity[axis]) * TICK;
      if (!step) continue;
      const sign = Math.sign(velocity[axis]);
      const direction = { x: 0, y: 0, z: 0 };
      direction[axis] = sign;
      const travel = wallDistance(position, direction, step, this.combatLayout);
      if (travel < step - 0.00001) {
        position[axis] += sign * Math.max(0, travel - 0.026);
        velocity[axis] *= axis === 'y' ? -0.53 : -0.58;
        if (axis === 'y') {
          velocity.x *= 0.73;
          velocity.z *= 0.73;
        }
      } else position[axis] += sign * step;
    }
  }

  explode(projectile, target, now) {
    const { owner, direction, position, weapon, power } = projectile;
    const spec = weapon === 'grenade' ? GRENADE : projectile.spec.projectile;
    const from = [position.x, position.y, position.z];
    const hits = [];
    this.events.push({ type: 'burst', id: owner.id, weapon, from, hits, echo: projectile.echo });
    const origin = direction ? {
      x: position.x - direction.x * 0.041,
      y: position.y - direction.y * 0.041,
      z: position.z - direction.z * 0.041,
    } : position;
    for (const other of [...this.players.values(), ...this.fleet.hulls, ...(this.siege?.cores ?? []), ...this.signature.echoes.values()]) {
      const vehicle = Boolean(other.kind);
      const core = other.opened !== undefined;
      if (other.isEcho ? !this.signature.hostile(owner, other) : core ? !(other.opened & (1 << owner.team))
        : vehicle ? !this.fleet.hostile(owner, other) : !this.hostile(owner, other) || other.safeUntil > now) continue;
      const x = other.x - origin.x;
      const y = core ? other.y - origin.y : vehicle ? VEHICLES[other.kind].height * 0.55 - origin.y : other.y + hitHeight(other) * 0.52 - origin.y;
      const z = other.z - origin.z;
      const gap = Math.hypot(x, y, z);
      let strength = 1;
      if (other !== target) {
        if (gap >= spec.radius + (core ? other.radius : 0)) continue;
        const norm = gap || 1;
        if (wallDistance(origin, { x: x / norm, y: y / norm, z: z / norm }, gap, this.combatLayout) < gap - 0.04) continue;
        strength = 1 - Math.max(0, gap - (core ? other.radius : 0)) / spec.radius;
      }
      const amount = other === target ? projectile.spec.damage : Math.max(8, spec.splash * strength);
      if (other.isEcho) {
        this.signature.damage(other, amount * power);
        continue;
      }
      const shielded = !vehicle && !core && other.armor > 0;
      const hit = this.damage(owner, other, amount * power * (vehicle ? 1.32 : 1), now, origin, weapon);
      if (!hit) continue;
      hits.push(hit);
      this.signature.combat(owner, hit, now, Boolean(projectile.echo));
      if (!hit.eliminated && !vehicle && !core) {
        explosionImpulse(other,{ x,z },strength,projectile,shielded,this.interactions.brace);
        if (shielded && this.interactions.brace) hit.braced = true;
      }
    }
  }

  updateProjectiles(now) {
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const projectile = this.projectiles[i];
      const { owner, direction, position, weapon } = projectile;
      if (this.players.get(owner.id) !== owner) {
        this.projectiles.splice(i, 1);
        continue;
      }
      let target;
      if (weapon === 'grenade') {
        this.rollGrenade(projectile);
        if (now < projectile.fuseAt) continue;
      } else {
        const travel = Math.min(projectile.range, projectile.spec.projectile.speed * TICK);
        const hit = this.trace(owner, position, direction, travel, 0.16);
        target = hit.target;
        position.x += direction.x * hit.distance;
        position.y += direction.y * hit.distance;
        position.z += direction.z * hit.distance;
        projectile.range -= travel;
        if (!target && hit.distance >= travel - 0.00001 && projectile.range > 0) continue;
      }
      this.projectiles.splice(i, 1);
      this.explode(projectile, target, now);
    }
  }

  unmount(player) {
    if (player.turret < 0) return;
    this.turrets[player.turret].user = null;
    player.turret = -1;
  }

  useTurret(player, now) {
    if (player.turret >= 0) {
      this.unmount(player);
      return;
    }
    if (!player.grounded || player.y >= 0.6 || player.crouch) return;
    const index = this.turrets.findIndex(turret =>
      !turret.user && distanceSquared(turret, player) < TURRET.dismountRadius ** 2);
    if (index < 0) return;
    const turret = this.turrets[index];
    turret.user = player.id;
    Object.assign(player, {
      turret: index, x: turret.x, y: 0, z: turret.z, vx: 0, vy: 0, vz: 0,
      yaw: turretYaw(player.yaw, turret.yaw), pitch: 0, fireAt: Math.max(player.fireAt, now + 0.14),
    });
  }

  operateTurret(player, input, now) {
    const turret = this.turrets[player.turret];
    player.yaw = turretYaw(input.yaw ?? player.yaw, turret.yaw);
    player.pitch = Math.max(-0.47, Math.min(0.56, input.pitch ?? player.pitch));
    if (!(input.fire || player.fireRequest) || turret.hot || now < player.fireAt) return;
    this.shoot(player, now, false, true);
    turret.firedAt = now;
    turret.heat = Math.min(1, turret.heat + 0.081);
    if (turret.heat === 1) turret.hot = true;
  }

  selectWeapon(player, weapon, now) {
    if (weapon === player.weapon || !Object.hasOwn(player.loadout, weapon)) return;
    player.weapon = weapon;
    player.reloadAt = 0;
    player.fireAt = Math.max(player.fireAt, now + 0.24);
  }

  updatePickups(now) {
    for (const [i, pickup] of this.layout.pickups.entries()) {
      if (this.horde && !this.horde.available(pickup)) continue;
      if (this.pickups[i] > now) continue;
      const player = [...this.players.values()].find(candidate => candidate.hp > 0 && candidate.vehicle < 0
        && !candidate.hordeType
        && Math.abs(candidate.y - pickup.y) < 0.78 && distanceSquared(candidate, pickup) < 1.14 ** 2
        && (pickup.type !== 'med' || candidate.hp < PILOT_HP)
        && (pickup.type !== 'shield' || candidate.armor < SHIELD_ARMOR));
      if (!player) continue;
      const { type } = pickup;
      const weapon = WEAPONS[type];
      if (weapon) {
        const hasWeapon = Object.hasOwn(player.loadout, type);
        player.loadout[type] = { ammo: weapon.magazine, reserve: weapon.reserve };
        if (!hasWeapon) this.selectWeapon(player, type, now);
      } else if (type === 'med') player.hp = Math.min(PILOT_HP, player.hp + MED_HEAL);
      else if (type === 'shield') player.armor = SHIELD_ARMOR;
      else if (type === 'surge') player.surgeUntil = now + 18;
      this.pickups[i] = now + (POWERUPS[type]?.respawn ?? 22);
      this.events.push({ type: 'pickup', id: player.id, item: type, position: [pickup.x, pickup.y, pickup.z] });
    }
  }

  updateDominance(now) {
    for (const [i, location] of this.layout.points.entries()) {
      const point = this.points[i];
      const present = new Set([...this.players.values()].filter(p => p.hp > 0 && p.vehicle < 0
        && distanceSquared(p, location) <= 3.05 ** 2 && p.y - (this.layout.terrain?.(p.x, p.z) ?? 0) < 1.6).map(p => p.team));
      point.contested = present.size > 1;
      if (point.contested) continue;
      const [team] = present;
      if (team === undefined || team === point.owner) {
        point.progress = Math.max(0, point.progress - TICK / 2);
        if (!point.progress) point.capture = -1;
        continue;
      }
      if (point.capture !== team) {
        point.capture = team;
        point.progress = 0;
      }
      point.progress += TICK / 2.2;
      if (point.progress >= 1) {
        Object.assign(point, { owner: team, capture: -1, progress: 0 });
        this.events.push({ type: 'notice', text: TEAMS[team].name + ' obsadil stanici ' + location.label });
      }
    }
    if (now >= this.nextPointTick) {
      this.nextPointTick += 1;
      for (const point of this.points) {
        if (point.owner >= 0) this.scores[point.owner]++;
      }
    }
  }

  updateFlags(now) {
    for (const [team, flag] of this.flags.entries()) {
      if (flag.status === 'dropped' && now >= flag.returnAt) this.returnFlag(team);
      const carrier = this.players.get(flag.carrier);
      if (carrier) {
        flag.x = carrier.x;
        flag.z = carrier.z;
        flag.y = carrier.y;
        continue;
      }
      const near = [...this.players.values()].filter(p => p.hp > 0 && p.vehicle < 0 && Math.abs(p.y - flag.y) < 1.25 && distanceSquared(p, flag) < 1.7 ** 2);
      if (flag.status === 'dropped' && near.some(p => p.team === team)) {
        this.returnFlag(team);
        continue;
      }
      const taker = near.find(p => p.team !== team && !this.flags.some(item => item.carrier === p.id));
      if (taker) {
        Object.assign(flag, { carrier: taker.id, status: 'carried' });
        this.events.push({ type: 'notice', text: taker.name + ' sebral vlajku týmu ' + TEAMS[team].name });
      }
    }
    for (const [team, flag] of this.flags.entries()) {
      const carrier = this.players.get(flag.carrier);
      if (!carrier || this.flags[carrier.team].status !== 'home') continue;
      const base = this.layout.bases[carrier.team];
      if (carrier.y - (this.layout.terrain?.(carrier.x, carrier.z) ?? 0) >= 1.25 || distanceSquared(carrier, base) >= 2 ** 2) continue;
      this.scores[carrier.team]++;
      this.returnFlag(team);
      this.events.push({ type: 'notice', text: carrier.name + ' doručil vlajku pro ' + TEAMS[carrier.team].name });
    }
  }

  checkEnd(now) {
    if (this.horde) {
      const label = this.horde.step(now);
      if (label) { this.winner = { label }; this.restartAt = now + ROUND_PAUSE; }
      return;
    }
    if (this.siege) {
      const winner = this.siege.winner(now >= this.endsAt);
      if (winner) {
        this.winner = winner;
        this.restartAt = now + ROUND_PAUSE;
      }
      return;
    }
    const ranked = this.mode === 'ffa'
      ? [...this.players.values()].map(player => ({ label: player.name, score: player.kills, id: player.id }))
      : this.scores.map((score, i) => ({ label: TEAMS[i].name, score, team: i }));
    ranked.sort((a, b) => b.score - a.score);
    if (now < this.endsAt && (ranked[0]?.score ?? 0) < MODES[this.mode].goal) return;
    this.winner = ranked[0]?.score === ranked[1]?.score ? { label: 'Remíza' } : ranked[0];
    this.restartAt = now + ROUND_PAUSE;
  }

  step(now) {
    this.tickNumber++;
    for (const socket of this.humans) socket.player.ack = socket.player.receivedSeq ?? socket.player.ack;
    if (this.restartAt && now >= this.restartAt) this.reset(now);
    if (!this.restartAt) {
      this.signature.step(now);
      for (const turret of this.turrets) {
        if (now - turret.firedAt > 0.23) turret.heat = Math.max(0, turret.heat - TICK * (turret.hot ? 0.43 : 0.62));
        if (turret.hot && turret.heat < 0.29) turret.hot = false;
      }
      for (const player of this.players.values()) {
        if (player.hp <= 0) {
          if (player.predictedTrigger) {
            this.rejectTrigger(player, player.predictedTrigger.fireId);
            player.predictedTrigger = null;
          }
          if (!player.hordeType && now >= player.respawnAt && (!this.siege || this.siege.alive(player.team))) this.spawn(player, now);
          continue;
        }
        let input = player.hordeType ? this.horde.input(player, now)
          : player.bot ? botInput(this, player, now) : now - player.inputAt < 0.24 ? player.input : EMPTY;
        if (player.triggerAim && (now - player.triggerAim.time) <= 0.12 && player.triggerAim.time >= player.inputAt) {
          input = { ...input, yaw: player.triggerAim.yaw, pitch: player.triggerAim.pitch };
        }
        if ((player.useRequest || input.use) && !this.fleet.use(player, now)) this.useTurret(player, now);
        if (player.vehicle >= 0 || player.turret >= 0) {
          this.signature.clearEcho(player, 'exit');
          player.echoRequest = null;
        }
        if ((player.vehicle >= 0 || player.turret >= 0) && player.predictedTrigger) {
          this.rejectTrigger(player, player.predictedTrigger.fireId);
          player.predictedTrigger = null;
        }
        if (player.vehicle >= 0) {
          this.fleet.drive(player, input, now);
          this.clearRequests(player);
          continue;
        }
        if (player.turret >= 0) {
          this.operateTurret(player, input, now);
          this.clearRequests(player);
          continue;
        }
        player.yaw = input.yaw ?? player.yaw;
        player.pitch = input.pitch ?? player.pitch;
        player.sprint = Boolean(input.sprint);
        const startX = player.x;
        const startZ = player.z;
        const pose = { x: startX, y: player.y, z: startZ, yaw: player.yaw, crouch: player.crouch,
          vy: player.vy, grounded: player.grounded, character: player.character, frame: player.frame, weapon: player.weapon, team: player.team, slot: player.slot };
        let signal = movePlayer(player, {
          ...input, yaw: player.yaw, jump: input.jump || player.jumpRequest, dodge: input.dodge || player.dodgeRequest,
        }, TICK, this.combatLayout);
        this.signature.movement(player, pose, signal, now);
        if (signal === 'launch') {
          player.usedLaunch = true;
          player.launchAt = now;
        }
        if (player.grounded && !signal) {
          player.stride += Math.hypot(player.x - startX, player.z - startZ);
          if (player.stride >= 2.4) {
            player.stride -= 2.4;
            signal = 'step';
          }
        } else player.stride = 0;
        if (signal) {
          this.events.push({ type: 'movement',id: player.id,action: signal,position: [player.x,player.y,player.z],team: player.team });
          botSound(this,player,{ x: player.x,y: player.y + .6,z: player.z },now,signal === 'step' ? 9 : 13);
        }
        const switchTo = input.weapon ?? player.weaponRequest;
        if (switchTo) this.selectWeapon(player, switchTo, now);
        const spec = WEAPONS[player.weapon];
        const active = player.loadout[player.weapon];
        if (player.reloadAt && now >= player.reloadAt) {
          const reload = Math.min(active.reserve, spec.magazine - active.ammo);
          active.ammo += reload;
          active.reserve -= reload;
          player.reloadAt = 0;
        }
        if (!player.reloadAt && active.reserve > 0 && active.ammo < spec.magazine
          && (input.reload || player.reloadRequest || active.ammo === 0)) player.reloadAt = now + spec.reload;
        if (player.bot && !active.ammo && !active.reserve) this.selectWeapon(player, DEFAULT_WEAPON, now);
        if (player.grenadeRequest && player.grenades > 0) this.throwGrenade(player, now);
        const echoRequest = player.echoRequest ?? (input.echo ? {
          echo: this.signature.echoes.get(player.id)?.serial, yaw: input.echo.yaw, pitch: input.echo.pitch, alt: false,
        } : null);
        player.echoRequest = null;
        const echoFired = this.signature.fire(player, echoRequest, now);
        const predicted = player.predictedTrigger;
        if (predicted) {
          const cost = predicted.alt ? spec.alt?.cost : 1;
          if (echoFired || predicted.weapon !== player.weapon || !cost || active.ammo < cost || player.reloadAt
            || now - predicted.time > 0.12 || player.fireAt - now > 1.5 * TICK) {
            this.rejectTrigger(player, predicted.fireId);
            player.predictedTrigger = null;
          } else if (now >= player.fireAt) {
            this.shoot(player, now, predicted.alt, false, null, predicted);
            player.predictedTrigger = null;
          }
        } else if (!echoFired && !player.reloadAt && now >= player.fireAt) {
          const alt = player.altRequest && spec.alt && active.ammo >= spec.alt.cost;
          if (alt || ((input.fire || player.fireRequest) && active.ammo > 0)) this.shoot(player, now, Boolean(alt));
        }
        this.clearRequests(player);
      }
      this.updateProjectiles(now);
      this.fleet.tick(now);
      this.updatePickups(now);
      if (this.mode === 'dom') this.updateDominance(now);
      if (this.mode === 'ctf') this.updateFlags(now);
      this.siege?.step();
      this.streaks.step(now);
      this.checkEnd(now);
    }
    this.broadcast(now);
  }

  clearRequests(player) {
    player.fireRequest = player.reloadRequest = player.jumpRequest = player.dodgeRequest = false;
    player.altRequest = player.grenadeRequest = player.useRequest = false;
    player.weaponRequest = null;
    player.triggerAim = null;
  }

  broadcast(now) {
    const legacyTick = this.tickNumber % 2 === 0;
    const modern = [];
    const legacy = [];
    for (const socket of this.humans) {
      if (socket.readyState !== 1) continue;
      if (socket.bufferedAmount >= 65_536) {
        socket.terminate();
        continue;
      }
      (socket.protocolVersion === NETWORK_VERSION ? modern : legacy).push(socket);
    }
    if (!legacyTick && !modern.length) return;
    if (legacy.length) this.legacyEvents.push(...this.events);
    if (!modern.length && !legacy.length) {
      this.events.length = this.legacyEvents.length = 0;
      return;
    }
    const players = [...this.players.values()].map(p => ({
      id: p.id, name: p.name, team: p.team, slot: p.slot, character: p.character, frame: p.frame, bot: p.bot, x: Math.round(p.x * 100) / 100, z: Math.round(p.z * 100) / 100,
      y: Math.round(p.y * 1000) / 1000, vx: p.vx, vy: p.vy, vz: p.vz, grounded: p.grounded, airJump: p.airJump,
      airDodge: p.airDodge, dodgeCooldown: p.dodgeCooldown, dodgeTime: p.dodgeTime, padCooldown: p.padCooldown, crouch: p.crouch, sprint: p.sprint, grenades: p.grenades, turret: p.turret, vehicle: p.vehicle,
      yaw: p.yaw, pitch: p.pitch, hp: p.hp, armor: p.armor, surge: Math.max(0, Math.ceil(p.surgeUntil - now)), weapon: p.weapon,
      loadout: ARMORY.map(key => p.loadout[key] ? [p.loadout[key].ammo, Number.isFinite(p.loadout[key].reserve) ? p.loadout[key].reserve : -1] : null), kills: p.kills, deaths: p.deaths, spawn: p.spawn, ack: p.ack,
      respawn: Math.max(0, p.respawnAt - now), reload: p.reloadAt > now, reloadRemaining: Math.max(0, p.reloadAt - now), shield: p.safeUntil > now,
      hordeType: p.hordeType,
    }));
    const state = {
      type: 'state', players, scores: this.scores, siege: this.siege?.snapshot(), horde: this.horde?.snapshot(now), time: Math.max(0, Math.ceil(this.endsAt - now)),
      signature: this.signature.snapshot(now),
      interactions: Object.values(this.interactions).some(Boolean) ? this.interactions : undefined,
      streaks: this.streaks.snapshot(now),
      points: this.mode === 'dom' ? this.points : undefined, flags: this.mode === 'ctf' ? this.flags : undefined,
      pickups: this.pickups.map((time, i) => this.horde && !this.horde.available(this.layout.pickups[i]) ? 1 : Math.max(0, Math.ceil(time - now))),
      vehicles: this.fleet.snapshot(now),
      turrets: this.turrets.map(item => ({ user: item.user, heat: item.heat, hot: item.hot })),
      projectiles: this.projectiles.map(shot => ({
        id: shot.id, weapon: shot.weapon, x: shot.position.x, y: shot.position.y, z: shot.position.z,
        fuse: shot.fuseAt === undefined ? undefined : Math.max(0, shot.fuseAt - now),
      })), winner: this.restartAt ? this.winner : null, restart: Math.max(0, Math.ceil(this.restartAt - now)), events: this.events,
    };
    if (modern.length) {
      const message = JSON.stringify(packSnapshot(state, this.tickNumber, now));
      for (const socket of modern) socket.send(message);
    }
    if (legacyTick) {
      if (legacy.length) {
        const message = JSON.stringify({ ...state, events: this.legacyEvents });
        for (const socket of legacy) socket.send(message);
      }
      this.legacyEvents.length = 0;
    }
    this.events.length = 0;
  }
}
