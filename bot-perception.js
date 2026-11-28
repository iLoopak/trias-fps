import { distanceSquared, eyeHeight, wallDistance } from './shared/rules.js';
import { wrapAngle } from './shared/vehicles.js';
import { roll } from './bot-profiles.js';

export function visible(from, to, layout) {
  const x = to.x - from.x, z = to.z - from.z;
  const y = (to.y ?? 0) + eyeHeight(to) - (from.y ?? 0) - eyeHeight(from);
  const distance = Math.hypot(x, y, z);
  return distance < .01 || wallDistance(
    { x: from.x, y: (from.y ?? 0) + eyeHeight(from), z: from.z },
    { x: x / distance, y: y / distance, z: z / distance }, distance + .01, layout) >= distance;
}

export function hostile(room, observer, other) {
  if (observer === other) return false;
  return room.horde ? Boolean(observer.hordeType) !== Boolean(other.hordeType)
    : room.mode === 'ffa' || observer.team !== other.team;
}

export function inView(player, target, yaw = player.yaw) {
  const horizontal = Math.sqrt(distanceSquared(player,target));
  const vertical = (target.y ?? 0) + eyeHeight(target) - (player.y ?? 0) - eyeHeight(player);
  return Math.abs(wrapAngle(bearing(player,target) - yaw)) < 1.08
    && Math.abs(Math.atan2(vertical,horizontal) - (player.pitch ?? 0)) < .83;
}

export function noteBotHit(player,id,damage,now) {
  const brain = player.bot && player.brain;
  if (!brain || brain.target?.id !== id || !damage) return;
  const previous = brain.damageNote;
  brain.damageNote = { id, amount: damage + (previous?.id === id && now - previous.at < 4 ? previous.amount : 0), at: now };
}

export function bearing(from, to) {
  return Math.atan2(from.x - to.x, from.z - to.z);
}

export function botSound(room, player, at, now, radius) {
  if (!room.bots && !room.horde) return;
  const signal = { x: at.x, y: at.y, z: at.z, at: now, radius, team: player.team,
    horde: Boolean(player.hordeType), id: player.id };
  room.botSoundIndex = ((room.botSoundIndex ?? -1) + 1) % 32;
  (room.botSound ??= [])[room.botSoundIndex] = signal;
}

function audible(room, player, now, layout) {
  let best = null, strength = 0;
  for (const cue of room.botSound ?? []) {
    if (now - cue.at > .55 || cue.id === player.id ||
        (room.horde ? cue.horde === Boolean(player.hordeType) : room.mode !== 'ffa' && cue.team === player.team)) continue;
    const distance = distanceSquared(player, cue);
    if (distance > cue.radius ** 2) continue;
    const weight = (1 - (now - cue.at) / .64) * (1 - Math.sqrt(distance) / cue.radius);
    if (weight <= strength) continue;
    best = cue;
    strength = weight;
  }
  if (!best || !visible(player, best, layout) && distanceSquared(player, best) > (best.radius * .46) ** 2) return null;
  return { x: Math.round(best.x / 3) * 3, z: Math.round(best.z / 3) * 3, heardAt: best.at };
}

export function senseBots(room, player, brain, now) {
  if (now < brain.senseAt) return;
  brain.senseAt = now + brain.tuning.sense * (.88 + roll(brain) * .24);
  const layout = room.combatLayout ?? room.layout;
  const radius = player.weapon === 'lance' ? 45 : 35;
  const candidates = [];
  for (const other of room.players.values()) {
    if (other.hp <= 0 || !hostile(room, player, other) || !inView(player, other)) continue;
    const distance = distanceSquared(player, other);
    if (distance <= radius ** 2) candidates.push({ other, distance });
  }
  candidates.sort((a, b) => a.distance - b.distance);
  const seen = brain.sensed;
  seen.length = 0;
  for (let i = 0; i < Math.min(candidates.length, 5); i++) {
    const { other, distance } = candidates[i];
    if (!visible(player, other, layout)) continue;
    seen.push({ id: other.id, x: other.x, y: other.y, z: other.z, vx: other.vx, vz: other.vz,
      crouch: other.crouch, distance: Math.sqrt(distance),
      weakened: brain.damageNote?.id === other.id && brain.damageNote.amount >= 65 && now - brain.damageNote.at < 3.8 });
  }
  let target = null, score = -Infinity;
  for (const item of seen) {
    const isolated = brain.kind === 'hunter' && !seen.some(other => other !== item && distanceSquared(other, item) < 7 ** 2);
    const value = -item.distance * .62 - Math.abs(item.distance - brain.traits.range) * .22
      + (item.id === brain.target?.id ? 7 : 0) + (isolated ? 8 : 0)
      + (item.weakened && brain.kind === 'opportunist' ? 11 : 0);
    if (value <= score) continue;
    target = item;
    score = value;
  }
  if (target && target.id !== brain.target?.id)
    brain.reactionAt = now + brain.tuning.reaction * brain.variation * (.78 + roll(brain) * .44);
  brain.target = target;
  if (target) brain.memory = { x: target.x, y: target.y, z: target.z, vx: target.vx, vz: target.vz, at: now };
  else if (brain.memory && now - brain.memory.at > 2.45) brain.memory = null;
  if (!target) brain.hearing = audible(room, player, now, layout) ??
    (brain.hearing && now - brain.hearing.heardAt < 1.85 ? brain.hearing : null);
  brain.hostileEcho = null;
  if (target || !room.signature?.echoEnabled || player.hordeType) return;
  let checks = 0;
  for (const echo of room.signature.echoes.values()) {
    if (!room.signature.hostile(player,echo) || echo.expires - now < .4
      || distanceSquared(player,echo) > 16 ** 2 || !inView(player,echo)) continue;
    if (++checks > 3) break;
    if (!visible(player,echo,layout)) continue;
    brain.hostileEcho = echo;
    if (brain.echoSeen !== echo.serial) brain.echoReactAt = now + brain.tuning.reaction;
    brain.echoSeen = echo.serial;
    break;
  }
}
