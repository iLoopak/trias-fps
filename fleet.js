import { blocked, BODY_RADIUS, distanceSquared, TICK, wallDistance } from './shared/rules.js';
import { boardingVehicle, moveVehicle, VEHICLES, vehicleBlocked } from './shared/vehicles.js';

const BRAKE = { x: 0, z: 0, brake: true };

export class Fleet {
  constructor(match) {
    this.match = match;
  }

  reset(now) {
    this.hulls = (this.match.layout.vehicles ?? []).map((spawn, index) => this.build(spawn, index, now));
  }

  build(spawn, index, now) {
    return {
      ...spawn, index, id: 'hull-' + index, spawn, hp: VEHICLES[spawn.kind].hp,
      speed: 0, boost: 1, boosting: false, driver: null, yaw: spawn.heading,
      pitch: 0, fireAt: now + 0.50, parkedAt: now, respawnAt: 0, protectedUntil: now + 1.9,
    };
  }

  release(player, now) {
    if (player.vehicle < 0) return;
    const hull = this.hulls[player.vehicle];
    if (hull?.driver === player.id) {
      hull.driver = null;
      hull.parkedAt = now;
      hull.boosting = false;
    }
    player.vehicle = -1;
    player.vehiclePlanAt = now + 12;
  }

  use(player, now) {
    if (player.vehicle >= 0) {
      const hull = this.hulls[player.vehicle];
      if (Math.abs(hull.speed) > 5.5) return true;
      const spec = VEHICLES[hull.kind];
      for (const offset of [Math.PI / 2, -Math.PI / 2, Math.PI, 0, 0.75, -0.75, 2.35, -2.35]) {
        const angle = hull.heading + offset;
        const gap = spec.radius + BODY_RADIUS + 0.64;
        const exit = { x: hull.x - Math.sin(angle) * gap, z: hull.z - Math.cos(angle) * gap };
        if (blocked(exit.x, exit.z, BODY_RADIUS, 0, this.match.layout) || this.hulls.some(other =>
          other !== hull && other.hp > 0 && distanceSquared(exit, other) < (VEHICLES[other.kind].radius + BODY_RADIUS) ** 2)) continue;
        Object.assign(player, exit, { y: 0, vx: 0, vy: 0, vz: 0, grounded: true, fireAt: now + 0.16 });
        this.release(player, now);
        return true;
      }
      return true;
    }
    if (player.turret >= 0 || this.match.flags.some(flag => flag.carrier === player.id)) return false;
    const hull = boardingVehicle(player, this.hulls);
    if (!hull) return false;
    const dx = hull.x - player.x;
    const dz = hull.z - player.z;
    const gap = Math.hypot(dx, dz);
    if (gap > 0.005 && wallDistance({ x: player.x, y: 0.88, z: player.z },
      { x: dx / gap, y: 0, z: dz / gap }, gap, this.match.layout) < gap - 0.04) return false;
    hull.driver = player.id;
    hull.mountedAt = now;
    hull.team = player.team;
    hull.yaw = hull.heading;
    hull.pitch = 0;
    hull.fireAt = Math.max(hull.fireAt, now + 0.30);
    hull.protectedUntil = now;
    Object.assign(player, {
      x: hull.x, y: 0, z: hull.z, vx: 0, vy: 0, vz: 0, yaw: hull.yaw, pitch: 0,
      vehicle: hull.index, crouch: false, safeUntil: now, stride: 0,
    });
    return true;
  }

  hostile(attacker, hull) {
    return hull.hp > 0 && hull.driver !== attacker.id && (this.match.mode === 'ffa' || hull.team !== attacker.team);
  }

  damage(attacker, hull, amount, now, origin, weapon, impact) {
    if (hull.protectedUntil > now) return null;
    const lost = Math.min(hull.hp, Math.round(amount));
    hull.hp -= lost;
    const driver = this.match.players.get(hull.driver);
    const hit = { id: driver?.id ?? hull.id, health: 0, absorbed: lost, eliminated: false };
    if (hull.hp > 0) return hit;
    this.match.events.push({ type: 'wreck', id: hull.id, position: [hull.x, 0.62, hull.z], kind: hull.kind, team: hull.team });
    hull.respawnAt = now + VEHICLES[hull.kind].respawn;
    hull.speed = 0;
    if (driver?.hp > 0) return this.match.damage(attacker, driver, driver.hp + driver.armor, now, origin, weapon, impact);
    hull.driver = null;
    return hit;
  }

  drive(player, input, now) {
    const hull = this.hulls[player.vehicle];
    const spec = VEHICLES[hull.kind];
    hull.yaw = input.yaw ?? hull.yaw;
    hull.pitch = Math.max(-0.66, Math.min(0.67, input.pitch ?? hull.pitch));
    moveVehicle(hull, input, this.match.layout, this.hulls);
    Object.assign(player, {
      x: hull.x, y: 0, z: hull.z, yaw: hull.yaw, pitch: hull.pitch,
      vx: -Math.sin(hull.heading) * hull.speed, vy: 0, vz: -Math.cos(hull.heading) * hull.speed,
    });
    if ((input.fire || player.fireRequest) && now >= hull.fireAt) {
      hull.fireAt = now + spec.gun.interval;
      this.match.shoot(player, now, false, false, hull);
    }

    if (Math.abs(hull.speed) < 9) return;
    for (const target of this.match.players.values()) {
      if (!this.match.hostile(player, target) || target.safeUntil > now || target.impactAt > now
        || target.y > spec.height || distanceSquared(hull, target) > (spec.radius + BODY_RADIUS * 0.8) ** 2) continue;
      target.impactAt = now + 0.85;
      const dx = target.x - hull.x;
      const dz = target.z - hull.z;
      const length = Math.hypot(dx, dz) || 1;
      target.vx += dx / length * 8.6;
      target.vz += dz / length * 8.6;
      target.vy = 4.2;
      target.grounded = false;
      const hits = [];
      this.match.events.push({ type: 'collision', id: player.id, from: [hull.x, spec.height * 0.6, hull.z], hits });
      hits.push(this.match.damage(player, target, Math.min(52, Math.abs(hull.speed) * 2.1), now, { x: hull.x, y: 0.8, z: hull.z }, 'collision'));
      hull.speed *= 0.82;
    }
  }

  tick(now) {
    for (const hull of this.hulls) {
      if (hull.hp > 0 && hull.driver) {
        if (Math.abs(hull.speed) < 2 && this.match.layout.repairs.some(pad => distanceSquared(pad, hull) < pad.radius ** 2)) {
          hull.hp = Math.min(VEHICLES[hull.kind].hp, hull.hp + TICK * 21);
        }
        continue;
      }
      if (hull.hp > 0 && Math.abs(hull.speed) > 0.01) moveVehicle(hull, BRAKE, this.match.layout, this.hulls);
      const abandoned = hull.hp > 0 && now - hull.parkedAt > 40 && distanceSquared(hull, hull.spawn) > 8 ** 2;
      if (!abandoned && !(hull.hp <= 0 && now >= hull.respawnAt)) continue;
      const { spawn } = hull;
      if (!vehicleBlocked(spawn.x, spawn.z, hull.kind, this.match.layout, this.hulls, hull.id)) {
        this.hulls[hull.index] = this.build(spawn, hull.index, now);
      }
    }
  }

  snapshot(now) {
    return this.hulls.map(hull => ({
      id: hull.id, index: hull.index, kind: hull.kind, team: hull.team, driver: hull.driver, x: hull.x, z: hull.z,
      heading: hull.heading, yaw: hull.yaw, pitch: hull.pitch, hp: Math.ceil(hull.hp), speed: hull.speed,
      boost: hull.boost, boosting: hull.boosting, respawn: Math.max(0, hull.respawnAt - now),
    }));
  }
}
