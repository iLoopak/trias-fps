import { aimVector, blocked, eyeHeight, floorBelow, hitHeight, rayBox, wallDistance, WEAPONS } from './public/rules.js';

export const SIGNATURE = {
  echoLifetime: 2.5,
  echoHealth: 25,
  chainWindow: 3.5,
  thresholds: [18, 45, 80, 125],
  actions: {
    sprint: 7, dodge: 10, airDodge: 16, double: 13, launch: 12, land: 9, turn: 8,
    airHit: 13, airKill: 26, dodgeKill: 25, moveKill: 18, echo: 11, echoHit: 22, echoKill: 39,
  },
};

export class Signature {
  constructor(match, flags) {
    this.match = match;
    this.echoEnabled = flags.echo !== false;
    this.momentumEnabled = flags.momentum !== false;
    this.echoes = new Map();
    this.chains = new Map();
    this.nextEcho = 0;
  }

  reset() {
    this.echoes.clear();
    this.chains.clear();
  }

  enabled(player) {
    return player.signature === true || player.bot && !player.hordeType &&
      [...this.match.humans].some(socket => socket.player?.signature);
  }

  clearEcho(player, reason) {
    const echo = this.echoes.get(player.id);
    if (!echo) return;
    this.echoes.delete(player.id);
    this.match.events.push({ type: 'echo-end', id: player.id, echo: echo.serial, reason, position: [echo.x, echo.y, echo.z] });
  }

  leave(player) {
    this.clearEcho(player, 'exit');
    this.chains.delete(player.id);
  }

  respawn(player) {
    this.clearEcho(player, 'death');
    player.echoRequest = null;
    player.styleSprint = false;
    player.lastDodge = -Infinity;
    player.lastTurn = -Infinity;
    player.styleHeading = null;
    const chain = this.chains.get(player.id);
    if (chain) this.finish(chain, 0);
  }

  valid(pose) {
    const layout = this.match.combatLayout;
    return floorBelow(pose.x, pose.z, pose.y, 0, layout) <= pose.y + .065 &&
      !blocked(pose.x, pose.z, .19, pose.y + .065, layout, eyeHeight(pose) - .025);
  }

  dodge(player, pose, now) {
    if (!this.echoEnabled || !this.enabled(player) || !this.valid(pose)) return;
    this.clearEcho(player, 'replace');
    this.echoes.set(player.id, {
      ...pose, owner: player, isEcho: true, serial: ++this.nextEcho, expires: now + SIGNATURE.echoLifetime, hp: SIGNATURE.echoHealth,
    });
  }

  hostile(attacker, echo) {
    return this.enabled(attacker) && echo.owner !== attacker &&
      (this.match.horde ? Boolean(attacker.hordeType) !== Boolean(echo.owner.hordeType)
        : this.match.mode === 'ffa' || attacker.team !== echo.owner.team);
  }

  trace(attacker, origin, direction, distance, width) {
    let target;
    for (const echo of this.echoes.values()) {
      if (!this.hostile(attacker, echo)) continue;
      const hit = rayBox(origin, direction,
        { x: echo.x - .37 - width, y: echo.y + .08 - width, z: echo.z - .37 - width },
        { x: echo.x + .37 + width, y: echo.y + hitHeight(echo) + width, z: echo.z + .37 + width });
      if (hit >= distance) continue;
      distance = hit;
      target = echo;
    }
    return { target, distance };
  }

  damage(echo, amount) {
    if (this.echoes.get(echo.owner.id) !== echo) return;
    echo.hp -= Math.round(amount);
    if (echo.hp <= 0) this.clearEcho(echo.owner, 'rupture');
  }

  request(player, message) {
    if (!this.echoEnabled || !this.enabled(player) || !Number.isSafeInteger(message.echo) ||
      !Number.isSafeInteger(message.action) || message.action <= (player.lastEchoAction ?? 0) ||
      !Number.isFinite(message.yaw) || !Number.isFinite(message.pitch)) return;
    player.lastEchoAction = message.action;
    if (player.echoRequest) return;
    player.echoRequest = { echo: message.echo, yaw: message.yaw % (Math.PI * 2),
      pitch: Math.max(-1.48, Math.min(1.48, message.pitch)), alt: message.alt === true };
  }

  fire(player, request, now) {
    if (!request || !this.echoEnabled) return false;
    const echo = this.echoes.get(player.id);
    const weapon = WEAPONS[player.weapon];
    const spec = request.alt ? weapon.alt : weapon;
    const inventory = player.loadout[player.weapon];
    const origin = echo && { x: echo.x, y: echo.y + eyeHeight(echo), z: echo.z };
    const eye = { x: player.x, y: player.y + eyeHeight(player), z: player.z };
    const aim = aimVector(request.yaw, request.pitch);
    const layout = this.match.combatLayout;
    let direction, gap;
    if (echo && spec) {
      // Only world geometry sets the convergence point. Hidden opponents cannot steer the shot.
      const distance = wallDistance(eye, aim, spec.range, layout);
      const delta = { x: eye.x + aim.x * distance - origin.x, y: eye.y + aim.y * distance - origin.y,
        z: eye.z + aim.z * distance - origin.z };
      gap = Math.hypot(delta.x, delta.y, delta.z);
      if (gap > .12) direction = { x: delta.x / gap, y: delta.y / gap, z: delta.z / gap };
    }
    const reason = !echo || echo.serial !== request.echo || echo.expires <= now ? 'unavailable'
      : !spec || player.hp <= 0 || player.vehicle >= 0 || player.turret >= 0 || this.match.restartAt ||
        player.reloadAt || player.fireAt > now || inventory.ammo < (spec.cost ?? 1) ? 'cooldown'
      : !direction || !this.valid(echo) || wallDistance(origin, direction, .14, layout) < .14 ? 'blocked' : null;
    if (reason) {
      if (!player.bot) this.match.events.push({ type: 'echo-reject', id: player.id, reason });
      return false;
    }
    const yaw = Math.atan2(-direction.x, -direction.z);
    const pitch = Math.asin(direction.y);
    this.action(player, 'echo', now);
    this.match.shoot(player, now, request.alt, false, null, { origin, yaw, pitch, echo: echo.serial });
    this.clearEcho(player, 'fire');
    return true;
  }

  movement(player, pose, signal, now) {
    if (signal === 'dodge') {
      player.lastDodge = now;
      this.dodge(player, pose, now);
    }
    if (!this.momentumEnabled || !this.enabled(player)) return;
    const speed = Math.hypot(player.vx, player.vz);
    const distance = Math.hypot(player.x - pose.x, player.z - pose.z);
    const sprinting = player.sprint && player.grounded && speed > 8.5 && distance > .20;
    if (sprinting && !player.styleSprint) this.action(player, 'sprint', now);
    player.styleSprint = sprinting;
    if (signal === 'dodge' && distance >= .26) this.action(player, pose.grounded ? 'dodge' : 'airDodge', now);
    if (signal === 'double' && speed > 4.5) this.action(player, 'double', now);
    if (signal === 'launch') this.action(player, 'launch', now);
    if (signal === 'land' && pose.vy < -7.5 && speed > 5.5) this.action(player, 'land', now);
    if (speed < 7 || distance < .20) return;
    const heading = Math.atan2(player.vx, player.vz);
    if (player.styleHeading == null) player.styleHeading = heading;
    const angle = heading - player.styleHeading;
    if (Math.abs(Math.atan2(Math.sin(angle), Math.cos(angle))) > .95 && now - player.lastTurn > .80) {
      player.lastTurn = now;
      player.styleHeading = heading;
      this.action(player, 'turn', now);
    }
  }

  combat(player, hit, now, echo = false) {
    if (!hit || (!hit.health && !hit.absorbed) || !this.match.players.has(hit.id) || player.id === hit.id || !player.hp) return;
    const moved = now - player.lastDodge < 1.65;
    const action = echo ? (hit.eliminated ? 'echoKill' : 'echoHit')
      : hit.eliminated ? (!player.grounded ? 'airKill' : moved ? 'dodgeKill'
        : Math.hypot(player.vx, player.vz) > 6 ? 'moveKill' : null) : !player.grounded ? 'airHit' : null;
    if (action) this.action(player, action, now, hit.id);
  }

  action(player, action, now, victim = '') {
    if (!this.momentumEnabled || !this.enabled(player)) return;
    let chain = this.chains.get(player.id);
    if (!chain) {
      chain = { score: 0, peak: 0, tier: 0, bestTier: 0, multi: 1, bestMulti: 1, last: 0, done: 0,
        recent: [], seen: new Map(), distinct: new Set(), serial: 0, echoKills: 0 };
      this.chains.set(player.id, chain);
    }
    if (now - chain.last >= SIGNATURE.chainWindow) this.finish(chain, now);
    const key = action + ':' + victim;
    const repeated = chain.seen.get(key) ?? 0;
    if (action === 'echoKill' && repeated === 0) chain.echoKills++;
    if (repeated >= 2 || chain.recent[0] === action || chain.seen.size >= 24) return;
    chain.seen.set(key, repeated + 1);
    chain.recent.unshift(action);
    chain.recent.length = Math.min(4, chain.recent.length);
    chain.distinct.add(action);
    chain.multi = Math.min(4, 1 + (chain.distinct.size - 1) * .5);
    chain.score += Math.round(SIGNATURE.actions[action] * chain.multi * (repeated ? .34 : 1));
    chain.tier = SIGNATURE.thresholds.filter(value => chain.score >= value).length;
    chain.peak = Math.max(chain.peak, chain.score);
    chain.bestTier = Math.max(chain.bestTier, chain.tier);
    chain.bestMulti = Math.max(chain.bestMulti, chain.multi);
    chain.last = now;
    chain.serial++;
  }

  finish(chain, now) {
    if (chain.score === 0) return;
    chain.done = chain.score;
    chain.doneAt = now;
    chain.score = chain.tier = 0;
    chain.multi = 1;
    chain.recent.length = 0;
    chain.seen.clear();
    chain.distinct.clear();
  }

  step(now) {
    for (const echo of this.echoes.values()) {
      if (!echo.owner.hp || echo.owner.vehicle >= 0 || echo.owner.turret >= 0) this.clearEcho(echo.owner, 'exit');
      else if (now >= echo.expires) this.clearEcho(echo.owner, 'fade');
    }
    for (const chain of this.chains.values()) if (now - chain.last >= SIGNATURE.chainWindow) this.finish(chain, now);
  }

  snapshot(now) {
    if (!this.echoEnabled && !this.momentumEnabled) return undefined;
    return {
      echo: this.echoEnabled, momentum: this.momentumEnabled, window: SIGNATURE.chainWindow, lifetime: SIGNATURE.echoLifetime,
      durability: SIGNATURE.echoHealth,
      echoes: [...this.echoes.values()].map(e => [e.owner.id, e.serial, e.x, e.y, e.z, e.yaw, e.team, e.slot, e.character, e.weapon,
        e.crouch, Math.max(0, e.expires - now), e.hp, ...(e.frame ? [e.frame] : [])]),
      chains: [...this.chains].map(([id, c]) => [id, c.score, c.multi, c.tier, Math.max(0, SIGNATURE.chainWindow - (now - c.last)),
        c.peak, c.bestTier, c.bestMulti, c.echoKills, c.serial, c.recent,
        now - c.doneAt < 1.25 && c.done >= SIGNATURE.thresholds[1] ? c.done : 0]),
    };
  }
}
