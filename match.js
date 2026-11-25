import {
  aimVector, ARMORY, BASES, CAPACITY, DEFAULT_WEAPON, distanceSquared, EYE, MODES, movePlayer,
  PICKUPS, POINTS, POWERUPS, rayBox, RESPAWN, SPAWNS, TEAM_SIZE, TEAMS, TICK, wallDistance, WEAPONS,
} from './public/rules.js';
import { botInput } from './bots.js';

const EMPTY = { x: 0, z: 0, sprint: false, fire: false, reload: false, jump: false };
const BOT_NAMES = ['Apex', 'Flux', 'Helix', 'Ghost', 'Nova', 'Pulse', 'Drift', 'Forge', 'Rift', 'Echo', 'Shade', 'Volt'];
const ROUND_PAUSE = 9;

export class Match {
  constructor(code, mode, bots) {
    this.code = code;
    this.mode = mode;
    this.bots = bots;
    this.players = new Map();
    this.humans = new Set();
    this.events = [];
    this.tickNumber = 0;
    this.reset(0);
  }

  reset(now) {
    this.endsAt = now + MODES[this.mode].time;
    this.restartAt = 0;
    this.winner = null;
    this.scores = TEAMS.map(() => 0);
    this.points = POINTS.map(() => ({ owner: -1, capture: -1, progress: 0, contested: false }));
    this.flags = BASES.map(base => ({ ...base, y: 0, status: 'home', carrier: null, returnAt: 0 }));
    this.pickups = PICKUPS.map(() => 0);
    this.nextPointTick = now + 1;
    for (const player of this.players.values()) {
      player.kills = player.deaths = 0;
      this.spawn(player, now);
    }
  }

  spawn(player, now) {
    const ffa = this.mode === 'ffa';
    const team = ffa ? (player.team + player.deaths) % TEAMS.length : player.team;
    const slot = ffa ? (player.slot + player.deaths) % TEAM_SIZE : player.slot;
    const start = SPAWNS[team][slot];
    const toward = POINTS[team];
    Object.assign(player, start, {
      hp: 100, armor: 0, surgeUntil: 0, y: 0, vy: 0, grounded: true, airJump: true,
      weapon: DEFAULT_WEAPON, loadout: { [DEFAULT_WEAPON]: { ammo: WEAPONS[DEFAULT_WEAPON].magazine, reserve: Infinity } },
      yaw: Math.atan2(start.x - toward.x, start.z - toward.z), pitch: 0, fireAt: now + 0.2,
      safeUntil: now + 1.25, reloadAt: 0, respawnAt: 0, input: EMPTY, inputAt: now, fireRequest: false, reloadRequest: false, jumpRequest: false, weaponRequest: null,
      spawn: player.spawn + 1, target: null, routeAt: 0, path: [],
    });
  }

  addPlayer(id, name, team, slot, now, bot = false) {
    const player = { id, name, team, slot, bot, kills: 0, deaths: 0, spawn: 0, ack: 0 };
    this.spawn(player, now);
    this.players.set(id, player);
    return player;
  }

  join(socket, name, now) {
    if (this.humans.size >= CAPACITY) return false;
    const counts = TEAMS.map((_, team) => [...this.players.values()].filter(p => !p.bot && p.team === team).length);
    const team = counts.indexOf(Math.min(...counts));
    const slot = SPAWNS[team].findIndex((_, i) => ![...this.players.values()].some(p => p.team === team && p.slot === i && !p.bot));
    this.removePlayer('bot-' + team + '-' + slot, now);
    socket.player = this.addPlayer(socket.id, name, team, slot, now);
    this.humans.add(socket);
    this.fillBots(now);
    return true;
  }

  fillBots(now) {
    if (!this.bots) return;
    for (const [team, spawns] of SPAWNS.entries()) {
      for (const slot of spawns.keys()) {
        if ([...this.players.values()].some(player => player.team === team && player.slot === slot)) continue;
        this.addPlayer('bot-' + team + '-' + slot, BOT_NAMES[team * TEAM_SIZE + slot], team, slot, now, true);
      }
    }
  }

  removePlayer(id, now) {
    const player = this.players.get(id);
    if (!player) return;
    this.dropFlag(player, now);
    this.players.delete(id);
  }

  dropFlag(player, now) {
    const flag = this.flags.find(item => item.carrier === player.id);
    if (flag) Object.assign(flag, { status: 'dropped', carrier: null, x: player.x, z: player.z, y: 0, returnAt: now + 15 });
  }

  returnFlag(team) {
    Object.assign(this.flags[team], BASES[team], { y: 0, status: 'home', carrier: null, returnAt: 0 });
  }

  input(player, message, now) {
    if (!Number.isSafeInteger(message.seq) || message.seq <= player.ack
      || !['x', 'z', 'yaw', 'pitch'].every(key => Number.isFinite(message[key]))) return;
    player.ack = message.seq;
    player.inputAt = now;
    player.input = {
      x: Math.max(-1, Math.min(1, message.x)), z: Math.max(-1, Math.min(1, message.z)),
      yaw: message.yaw % (Math.PI * 2), pitch: Math.max(-1.48, Math.min(1.48, message.pitch)),
      sprint: message.sprint === true, fire: message.fire === true, reload: message.reload === true,
    };
    player.fireRequest ||= player.input.fire;
    player.reloadRequest ||= player.input.reload;
    player.jumpRequest ||= message.jump === true;
    if (typeof message.weapon === 'string' && Object.hasOwn(WEAPONS, message.weapon)) player.weaponRequest = message.weapon;
  }

  shoot(player, now) {
    const weapon = WEAPONS[player.weapon];
    player.loadout[player.weapon].ammo--;
    player.fireAt = now + weapon.interval;
    player.safeUntil = now;
    const origin = { x: player.x, y: player.y + EYE, z: player.z };
    const damage = new Map();
    const ends = weapon.pattern.map((offset, index) => {
      const direction = aimVector(player.yaw + offset, player.pitch + (index % 2 ? offset * 0.7 : -offset * 0.7));
      let distance = wallDistance(origin, direction, weapon.range);
      let hitPlayer;
      for (const other of this.players.values()) {
        if (player === other || other.hp <= 0 || (this.mode !== 'ffa' && player.team === other.team)) continue;
        const hit = rayBox(origin, direction,
          { x: other.x - 0.44, y: other.y + 0.08, z: other.z - 0.44 },
          { x: other.x + 0.44, y: other.y + 1.78, z: other.z + 0.44 });
        if (hit >= distance) continue;
        distance = hit;
        hitPlayer = other;
      }
      if (hitPlayer && hitPlayer.safeUntil <= now) {
        damage.set(hitPlayer, (damage.get(hitPlayer) ?? 0) + weapon.damage * (player.surgeUntil > now ? 1.6 : 1));
      }
      const impact = hitPlayer
        ? (hitPlayer.armor || hitPlayer.safeUntil > now ? 'shield' : 'pilot')
        : distance < weapon.range ? 'wall' : null;
      return [origin.x + direction.x * distance, origin.y + direction.y * distance, origin.z + direction.z * distance, impact];
    });
    const hits = [];
    this.events.push({ type: 'shot', id: player.id, weapon: player.weapon,
      from: [origin.x, origin.y - 0.12, origin.z], ends, hits });
    for (const [target, value] of damage) {
      const amount = Math.round(value);
      const absorbed = Math.min(target.armor, Math.ceil(amount * 0.7));
      target.armor -= absorbed;
      const health = Math.min(target.hp, amount - absorbed);
      target.hp -= health;
      hits.push({ id: target.id, health, absorbed, eliminated: target.hp === 0 });
      if (target.hp) continue;
      player.kills++;
      target.deaths++;
      target.respawnAt = now + RESPAWN;
      this.dropFlag(target, now);
      if (this.mode === 'tdm') this.scores[player.team]++;
      this.events.push({ type: 'kill', attacker: player.name, victim: target.name, team: player.team,
        attackerId: player.id, victimId: target.id });
    }
  }

  selectWeapon(player, weapon, now) {
    if (weapon === player.weapon || !Object.hasOwn(player.loadout, weapon)) return;
    player.weapon = weapon;
    player.reloadAt = 0;
    player.fireAt = Math.max(player.fireAt, now + 0.24);
  }

  updatePickups(now) {
    for (const [i, pickup] of PICKUPS.entries()) {
      if (this.pickups[i] > now) continue;
      const player = [...this.players.values()].find(candidate => candidate.hp > 0
        && Math.abs(candidate.y - pickup.y) < 0.78 && distanceSquared(candidate, pickup) < 1.14 ** 2
        && (pickup.type !== 'med' || candidate.hp < 100)
        && (pickup.type !== 'shield' || candidate.armor < 50));
      if (!player) continue;
      const { type } = pickup;
      const weapon = WEAPONS[type];
      if (weapon) {
        const hasWeapon = Object.hasOwn(player.loadout, type);
        player.loadout[type] = { ammo: weapon.magazine, reserve: weapon.reserve };
        if (!hasWeapon) this.selectWeapon(player, type, now);
      } else if (type === 'med') player.hp = Math.min(100, player.hp + 55);
      else if (type === 'shield') player.armor = 50;
      else if (type === 'surge') player.surgeUntil = now + 18;
      this.pickups[i] = now + (POWERUPS[type]?.respawn ?? 22);
      this.events.push({ type: 'pickup', id: player.id, item: type, position: [pickup.x, pickup.y, pickup.z] });
    }
  }

  updateDominance(now) {
    for (const [i, location] of POINTS.entries()) {
      const point = this.points[i];
      const present = new Set([...this.players.values()].filter(p => p.hp > 0 && p.y < 1.6 && distanceSquared(p, location) <= 3.05 ** 2).map(p => p.team));
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
      const near = [...this.players.values()].filter(p => p.hp > 0 && p.y < 1.25 && distanceSquared(p, flag) < 1.7 ** 2);
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
      if (!carrier || this.flags[carrier.team].status !== 'home' || carrier.y >= 1.25 || distanceSquared(carrier, BASES[carrier.team]) >= 2 ** 2) continue;
      this.scores[carrier.team]++;
      this.returnFlag(team);
      this.events.push({ type: 'notice', text: carrier.name + ' doručil vlajku pro ' + TEAMS[carrier.team].name });
    }
  }

  checkEnd(now) {
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
    if (this.restartAt && now >= this.restartAt) this.reset(now);
    if (!this.restartAt) {
      for (const player of this.players.values()) {
        if (player.hp <= 0) {
          if (now >= player.respawnAt) this.spawn(player, now);
          continue;
        }
        const input = player.bot ? botInput(this, player, now) : now - player.inputAt < 0.24 ? player.input : EMPTY;
        player.yaw = input.yaw ?? player.yaw;
        player.pitch = input.pitch ?? player.pitch;
        movePlayer(player, { ...input, yaw: player.yaw, jump: input.jump || player.jumpRequest });
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
        if ((input.fire || player.fireRequest) && active.ammo > 0 && !player.reloadAt && now >= player.fireAt) this.shoot(player, now);
        player.fireRequest = player.reloadRequest = player.jumpRequest = false;
        player.weaponRequest = null;
      }
      this.updatePickups(now);
      if (this.mode === 'dom') this.updateDominance(now);
      if (this.mode === 'ctf') this.updateFlags(now);
      this.checkEnd(now);
    }
    if (this.tickNumber % 2 === 0) this.broadcast(now);
  }

  broadcast(now) {
    const players = [...this.players.values()].map(p => ({
      id: p.id, name: p.name, team: p.team, slot: p.slot, bot: p.bot, x: Math.round(p.x * 100) / 100, z: Math.round(p.z * 100) / 100,
      y: Math.round(p.y * 1000) / 1000, vy: p.vy, grounded: p.grounded, airJump: p.airJump,
      yaw: p.yaw, pitch: p.pitch, hp: p.hp, armor: p.armor, surge: Math.max(0, Math.ceil(p.surgeUntil - now)), weapon: p.weapon,
      loadout: ARMORY.map(key => p.loadout[key] ? [p.loadout[key].ammo, Number.isFinite(p.loadout[key].reserve) ? p.loadout[key].reserve : -1] : null), kills: p.kills, deaths: p.deaths, spawn: p.spawn, ack: p.ack,
      respawn: Math.max(0, p.respawnAt - now), reload: p.reloadAt > now, shield: p.safeUntil > now,
    }));
    const message = JSON.stringify({
      type: 'state', players, scores: this.scores, time: Math.max(0, Math.ceil(this.endsAt - now)),
      points: this.mode === 'dom' ? this.points : undefined, flags: this.mode === 'ctf' ? this.flags : undefined,
      pickups: this.pickups.map(time => time <= now), winner: this.restartAt ? this.winner : null, restart: Math.max(0, Math.ceil(this.restartAt - now)), events: this.events,
    });
    for (const socket of this.humans) {
      if (socket.readyState === 1 && socket.bufferedAmount < 160_000) socket.send(message);
    }
    this.events.length = 0;
  }
}
