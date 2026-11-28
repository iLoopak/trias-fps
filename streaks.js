import { aimVector, distanceSquared, eyeHeight, floorBelow, PILOT_HP, wallDistance, WEAPONS } from './shared/rules.js';

const PERKS = ['scan', 'module', 'strike', 'overdrive'];
const GOALS = [3, 5, 7, 9];

export class Streaks {
  constructor(room) { this.room = room; }

  reset() {
    this.scans = new Map();
    this.strikes = [];
    this.nextStrike = 0;
  }

  scope(pilot) { return this.room.mode === 'ffa' ? pilot.id : String(pilot.team); }

  kill(pilot, victim, direct) {
    victim.chain = victim.perks = 0;
    if (!direct || pilot.bot || pilot.hp <= 0 || this.room.players.get(pilot.id) !== pilot) return;
    pilot.chain++;
    const index = GOALS.indexOf(pilot.chain);
    if (index < 0) return;
    pilot.perks |= 1 << index;
    this.room.events.push({ type: 'perk-ready', id: pilot.id, perk: PERKS[index] });
  }

  activate(pilot, request, now) {
    const index = PERKS.indexOf(request.perk);
    if (index < 0 || !pilot || pilot.hp <= 0 || this.room.restartAt || !(pilot.perks & (1 << index)))
      return 'Tahle odměna teď není připravená.';
    let position;
    if (request.perk === 'strike') {
      position = this.aim(pilot, request);
      if (!position) return 'Pro úder namiř na zem v dosahu.';
      this.strikes.push({ ...position, id: ++this.nextStrike, pilot, at: now + 2.15, radius: 5.3 });
    } else if (request.perk === 'scan') {
      this.scans.set(this.scope(pilot), now + 8);
    } else if (request.perk === 'module') {
      this.module(pilot);
    } else {
      pilot.armor = Math.max(pilot.armor, 45);
      pilot.surgeUntil = Math.max(pilot.surgeUntil, now + 12);
    }
    pilot.perks &= ~(1 << index);
    this.room.practice?.perk(pilot, request.perk);
    this.room.events.push({ type: 'perk-used', id: pilot.id, perk: request.perk, position: [pilot.x, pilot.y, pilot.z] });
    return null;
  }

  aim(pilot, { yaw, pitch }) {
    if (!Number.isFinite(yaw) || !Number.isFinite(pitch) || pitch >= -.10 || pitch < -1.49) return null;
    const layout = this.room.combatLayout;
    const origin = { x: pilot.x, y: pilot.y + eyeHeight(pilot), z: pilot.z };
    const direction = aimVector(yaw % (Math.PI * 2), pitch);
    const hit = wallDistance(origin, direction, 28, layout);
    if (hit < 1.4 || hit >= 28) return null;
    const x = origin.x + direction.x * (hit - .05), z = origin.z + direction.z * (hit - .05);
    const y = floorBelow(x, z, origin.y + direction.y * hit + .17, 0, layout);
    return { x, y, z };
  }

  module(pilot) {
    const allies = [...this.room.players.values()].filter(ally => ally.hp > 0 && (ally === pilot || this.room.mode !== 'ffa'
      && ally.team === pilot.team && !ally.hordeType && distanceSquared(ally, pilot) < 7 ** 2));
    for (const ally of allies) {
      switch (pilot.character) {
        case 'bulwark':
          ally.armor = Math.max(ally.armor, 40);
          break;
        case 'wraith': {
          const ammo = ally.loadout[ally.weapon];
          const magazine = WEAPONS[ally.weapon].magazine;
          ammo.ammo = magazine;
          ammo.reserve = Math.min(WEAPONS[ally.weapon].reserve, ammo.reserve + magazine);
          break;
        }
        case 'kestrel':
          ally.grenades = Math.min(2, ally.grenades + 1);
          ally.armor = Math.max(ally.armor, 20);
          break;
        default:
          ally.hp = Math.min(PILOT_HP, ally.hp + 42);
      }
    }
  }

  step(now) {
    for (const [scope, expires] of this.scans) if (expires <= now) this.scans.delete(scope);
    for (let i = this.strikes.length - 1; i >= 0; i--) {
      const strike = this.strikes[i];
      if (strike.at > now) continue;
      this.strikes.splice(i, 1);
      const origin = { x: strike.x, y: strike.y + .27, z: strike.z };
      const hits = [];
      this.room.events.push({ type: 'orbital-impact', id: strike.pilot.id, from: [origin.x, origin.y, origin.z], hits });
      for (const target of this.room.players.values()) {
        if (!this.room.hostile(strike.pilot, target) || target.safeUntil > now) continue;
        const dx = target.x - strike.x, dy = target.y + .75 - origin.y, dz = target.z - strike.z;
        const distance = Math.hypot(dx, dy, dz);
        if (distance >= strike.radius) continue;
        if (distance > .01 && wallDistance(origin, { x: dx / distance, y: dy / distance, z: dz / distance },
          distance, this.room.combatLayout) < distance - .08) continue;
        hits.push(this.room.damage(strike.pilot, target, 115 * (1 - .62 * distance / strike.radius), now, origin, 'rift', undefined, false));
      }
    }
  }

  snapshot(now) {
    return {
      charges: [...this.room.players.values()].filter(pilot => !pilot.bot && pilot.chain > 0)
        .map(pilot => [pilot.id, pilot.chain, pilot.perks]),
      scans: [...this.scans].map(([scope, expires]) => [scope, Math.max(0, expires - now)]),
      strikes: this.strikes.map(({ id, x, y, z, at, radius }) => [id, x, y, z, Math.max(0, at - now), radius]),
    };
  }
}
