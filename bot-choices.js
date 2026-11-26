import { PILOT_HP, WEAPONS, blocked, distanceSquared } from './public/rules.js';
import { WEAPON_ROLES } from './bot-profiles.js';
import { visible } from './bot-perception.js';

export function chooseWeapon(player, brain, enemy) {
  const range = enemy?.distance ?? brain.traits.range;
  let key = player.weapon, best = -Infinity;
  for (const [weapon, inventory] of Object.entries(player.loadout)) {
    if (!inventory.ammo && !inventory.reserve) continue;
    const role = WEAPON_ROLES[weapon];
    const suitability = 1 - Math.abs(range - role.distance) / role.tolerance;
    const value = suitability + (weapon === brain.traits.weapon ? .21 : 0)
      + (weapon === player.weapon ? .20 : 0) - (!inventory.ammo ? .64 : 0);
    if (value > best) { best = value; key = weapon; }
  }
  return key;
}

function objectiveForMode(room, player, brain) {
  const { points, bases } = room.layout;
  if (room.siege) {
    const goal = room.siege.botGoal(player);
    return { point: goal.target, core: goal.core, intent: goal.core ? 'siege-core' : 'siege-link' };
  }
  if (room.horde) {
    const core = room.horde.core;
    const angle = -Math.PI * .5 + player.slot * Math.PI / 3;
    return { point: { x: core.x + Math.sin(angle)*5.5, y: 0, z: core.z + Math.cos(angle)*5.5 }, intent: 'defend-core' };
  }
  if (room.mode === 'dom') {
    const anchor = (player.team + player.slot) % points.length;
    let station = anchor, best = -Infinity;
    for (let i = 0; i < points.length; i++) {
      const owned = room.points[i].owner === player.team;
      const assigned = [...room.players.values()].filter(ally =>
        ally !== player && ally.team === player.team && ally.hp > 0 && ally.brain?.objective?.point === points[i]).length;
      const weight = (owned ? brain.traits.team > .76 ? 13 : -8 : 13)
        + (room.points[i].contested ? 8 * brain.traits.team : 0)
        + (anchor === i ? 9 : 0) - assigned * 10 - Math.sqrt(distanceSquared(player, points[i])) * .28;
      if (weight > best) { station = i; best = weight; }
    }
    return { point: points[station], intent: room.points[station].owner === player.team ? 'guard-point' : 'capture-point' };
  }
  if (room.mode === 'ctf') {
    const own = room.flags[player.team];
    if (room.flags.some(flag => flag.carrier === player.id))
      return { point: bases[player.team], intent: 'return-flag' };
    if (own.status === 'dropped') return { point: own, intent: 'recover-flag' };
    if (own.status === 'carried' && brain.traits.team >= .50)
      return { point: { x: own.x, y: own.y, z: own.z }, intent: 'intercept' };
    const carrier = room.flags.map(flag => room.players.get(flag.carrier))
      .find(ally => ally !== player && ally?.team === player.team && ally.hp > 0);
    if (carrier && brain.traits.team > .55)
      return { point: { x: carrier.x, y: carrier.y, z: carrier.z }, intent: 'escort' };
    if (brain.kind === 'sentinel') {
      const base = bases[player.team];
      const out = points[player.team];
      return { point: { x: (base.x * .72 + out.x * .28), y: base.y ?? 0, z: (base.z * .72 + out.z * .28) }, intent: 'guard-base' };
    }
    const target = room.flags[(player.team + 1 + player.slot % 2) % bases.length];
    return { point: { x: target.x, y: target.y, z: target.z }, intent: 'attack-flag' };
  }
  return null;
}

function supplyFor(room, player, brain, now) {
  let best = null, score = 0;
  const radius = 6 + brain.traits.supply * 9;
  for (const [i, pickup] of room.layout.pickups.entries()) {
    if (room.pickups[i] > now || room.horde && !room.horde.available(pickup)
      || Math.abs(pickup.y - (room.layout.terrain?.(pickup.x, pickup.z) ?? 0)) > .1) continue;
    const dist = distanceSquared(player, pickup);
    if (dist > radius * radius) continue;
    const kind = pickup.type;
    const inventory = player.loadout[kind];
    const value = kind === 'med' && player.hp < PILOT_HP * .74 ? 37 * (1 - player.hp / PILOT_HP)
      : kind === 'shield' && player.armor < 24 ? 15
        : kind === 'surge' ? 13 * brain.traits.supply
          : WEAPONS[kind] && kind !== 'pulse' && (!inventory || inventory.ammo + inventory.reserve < 3)
            ? brain.traits.supply * (kind === brain.traits.weapon ? 25 : 16) : 0;
    const priority = value - Math.sqrt(dist) * .67;
    if (priority > score) { best = pickup; score = priority; }
  }
  return best;
}

export function retreatPoint(player, threat, brain, layout) {
  const distance = Math.sqrt(distanceSquared(player, threat)) || 1;
  const heading = Math.atan2((player.x - threat.x) / distance, (player.z - threat.z) / distance);
  let nearest = null, score = -Infinity;
  for (const offset of [0, .66, -.66, 1.24, -1.24]) {
    const x = player.x + Math.sin(heading + offset) * 5.2;
    const z = player.z + Math.cos(heading + offset) * 5.2;
    const target = { x, y: layout.terrain?.(x, z) ?? 0, z };
    if (blocked(x, z, .62, target.y, layout)) continue;
    const hidden = !visible(target, threat, layout);
    const value = (hidden ? 8 : 0) + (visible(player, target, layout) ? 4 : 0) - Math.abs(offset) + Math.sign(offset) * brain.strafe * .2;
    if (value > score) { nearest = target; score = value; }
  }
  return nearest;
}

export function chooseObjective(room, player, brain, now) {
  const layout = room.combatLayout ?? room.layout;
  const target = brain.target;
  const modeGoal = objectiveForMode(room, player, brain);
  if (modeGoal?.intent === 'return-flag' || modeGoal?.intent === 'recover-flag') return modeGoal;
  const supply = supplyFor(room, player, brain, now);
  const retreat = target && player.hp + player.armor * .55 < PILOT_HP * brain.traits.retreat * brain.variation;
  if (retreat) {
    const cover = retreatPoint(player, target, brain, layout);
    if (cover) return { point: cover, intent: 'retreat' };
  }
  if (supply && (!target || player.hp < 60 && brain.traits.supply > .55))
    return { point: supply, intent: 'resupply' };
  if (modeGoal) return modeGoal;
  if (target) {
    const distance = target.distance;
    if (brain.kind === 'marksman' && distance >= 13 && now - (brain.lastDischargeAt ?? -10) < .57) {
      const cover = retreatPoint(player,target,brain,layout);
      if (cover) return { point: cover, intent: 'reposition' };
    }
    if (distance > brain.traits.range * 1.15 && brain.traits.flank > .5) {
      const side = bearingOffset(player,target,brain.strafe);
      if (!blocked(side.x,side.z,.65,layout.terrain?.(side.x,side.z) ?? 0,layout))
        return { point: side, intent: 'flank' };
    }
    return { point: target, intent: 'engage' };
  }
  if (brain.memory) {
    const memory = brain.memory;
    return { point: { x: memory.x + (memory.vx ?? 0)*.15, z: memory.z + (memory.vz ?? 0)*.15 }, intent: 'investigate' };
  }
  if (brain.hearing) return { point: brain.hearing, intent: 'listen' };
  if (room.mode !== 'ffa' && brain.traits.team > .63) {
    const ally = [...room.players.values()].find(other => other !== player && other.team === player.team
      && other.hp > 0 && distanceSquared(player, other) < 15 ** 2 && other.brain?.target && visible(player, other, layout));
    if (ally) return { point: { x: ally.x - brain.strafe * 2.8, z: ally.z + 2.2 }, intent: 'support' };
  }
  const pad = room.layout.launchPads[player.team];
  if (pad && !player.usedLaunch && (brain.traits.mobility > .57 || brain.kind === 'marksman') && distanceSquared(player,pad) < 13 ** 2)
    return { point: pad, intent: 'high-ground' };
  const kind = (player.team + player.deaths) % 2 ? 'bastion' : 'skimmer';
  if (now > player.vehiclePlanAt && (player.slot === 3 || brain.kind === 'opportunist')) {
    const hull = room.fleet.hulls.find(item => item.team === player.team && item.kind === kind && !item.driver && item.hp > 0
      && distanceSquared(player,item) < 17 ** 2);
    if (hull) return { point: hull, vehicle: hull, intent: 'board' };
  }
  if (now > player.vehiclePlanAt && (brain.kind === 'sentinel' || brain.kind === 'opportunist')) {
    const turret = room.turrets.find(item => !item.user && distanceSquared(player,item) < 11 ** 2);
    if (turret) return { point: turret, turret, intent: 'turret' };
  }
  const points = room.layout.points;
  if (brain.patrol == null || distanceSquared(player,points[brain.patrol]) < 3.2 ** 2 || now > (brain.patrolAt ?? 0)) {
    brain.patrol = (brain.patrol == null ? player.team + player.slot : brain.patrol + (brain.strafe > 0 ? 1 : 2)) % points.length;
    brain.patrolAt = now + 5.8;
  }
  return { point: points[brain.patrol], intent: 'patrol' };
}

function bearingOffset(player, target, side) {
  const angle = Math.atan2(player.z - target.z, player.x - target.x) + side * .60;
  const distance = Math.max(7, target.distance * .62);
  return { x: target.x + Math.cos(angle) * distance, z: target.z + Math.sin(angle) * distance };
}
