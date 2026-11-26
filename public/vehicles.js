import { aimVector, blocked, distanceSquared, rayBox, TICK, wallDistance } from './rules.js';

export const VEHICLES = {
  skimmer: {
    label: 'Skimmer', color: '#70f2e4', hp: 155, radius: 1.17, halfWidth: 0.93, halfLength: 1.5, height: 1.22, gunHeight: 1.08,
    speed: 20.7, boost: 29.3, reverse: 8.9, acceleration: 22.5, braking: 31, turn: 2.48, respawn: 16,
    gun: { key: 'arc', interval: 0.13, range: 53, damage: 18, pattern: [0] },
  },
  bastion: {
    label: 'Bastion', color: '#ffcb8c', hp: 310, radius: 1.81, halfWidth: 1.53, halfLength: 1.98, height: 1.82, gunHeight: 1.43,
    speed: 12.7, boost: 12.7, reverse: 5.5, acceleration: 13.7, braking: 22, turn: 1.54, respawn: 22,
    gun: { key: 'rift', interval: 1.12, range: 63, damage: 80, projectile: { speed: 25, radius: 3.45, splash: 56 } },
  },
};

export function wrapAngle(angle) {
  return Math.atan2(Math.sin(angle), Math.cos(angle));
}

export function vehicleBlocked(x, z, kind, layout, others = [], id = null) {
  const spec = VEHICLES[kind];
  if (blocked(x, z, spec.radius, 0, layout, spec.height)) return true;
  const position = { x, z };
  if (layout.footZones?.some(zone => distanceSquared(zone, position) < (zone.radius + spec.radius * 0.52) ** 2)) return true;
  return others.some(other => other.id !== id && other.hp > 0
    && distanceSquared(other, position) < (spec.radius + VEHICLES[other.kind].radius) ** 2);
}

export function moveVehicle(vehicle, input, layout, others, dt = TICK) {
  const spec = VEHICLES[vehicle.kind];
  const throttle = input.z ?? 0;
  const steering = input.x ?? 0;
  const boosting = input.sprint && !input.brake && throttle > 0.05 && vehicle.boost > 0.04 && vehicle.kind === 'skimmer';
  vehicle.boost = Math.max(0, Math.min(1, vehicle.boost + dt * (boosting ? -0.43 : 0.205)));
  const target = input.brake ? 0 : throttle * (throttle < 0 ? spec.reverse : boosting ? spec.boost : spec.speed);
  const speedChange = (input.brake || Math.sign(target) !== Math.sign(vehicle.speed) ? spec.braking : spec.acceleration) * dt;
  vehicle.speed += Math.max(-speedChange, Math.min(speedChange, target - vehicle.speed));
  const steeringFactor = Math.max(vehicle.kind === 'bastion' ? 0.55 : 0.31, 1 - Math.abs(vehicle.speed) / spec.boost * 0.64);
  vehicle.heading = wrapAngle(vehicle.heading - steering * spec.turn * steeringFactor * dt * (vehicle.speed < -0.7 ? -1 : 1));
  const dx = -Math.sin(vehicle.heading) * vehicle.speed * dt;
  const dz = -Math.cos(vehicle.heading) * vehicle.speed * dt;
  const wasBlocked = vehicleBlocked(vehicle.x + dx, vehicle.z + dz, vehicle.kind, layout, others, vehicle.id);
  if (!wasBlocked) {
    vehicle.x += dx;
    vehicle.z += dz;
  } else {
    if (!vehicleBlocked(vehicle.x + dx, vehicle.z, vehicle.kind, layout, others, vehicle.id)) vehicle.x += dx;
    if (!vehicleBlocked(vehicle.x, vehicle.z + dz, vehicle.kind, layout, others, vehicle.id)) vehicle.z += dz;
    vehicle.speed *= 0.38;
  }
  vehicle.boosting = Boolean(boosting && !wasBlocked);
}

export function hullDistance(vehicle, origin, direction, padding = 0) {
  const spec = VEHICLES[vehicle.kind];
  const cos = Math.cos(vehicle.heading);
  const sin = Math.sin(vehicle.heading);
  const x = origin.x - vehicle.x;
  const z = origin.z - vehicle.z;
  return rayBox(
    { x: x * cos - z * sin, y: origin.y, z: x * sin + z * cos },
    { x: direction.x * cos - direction.z * sin, y: direction.y, z: direction.x * sin + direction.z * cos },
    { x: -spec.halfWidth - padding, y: 0.03, z: -spec.halfLength - padding },
    { x: spec.halfWidth + padding, y: spec.height + padding, z: spec.halfLength + padding },
  );
}

export function boardingVehicle(player, vehicles) {
  if (!player.grounded || player.y > 0.55) return null;
  let nearest = null;
  let distance = Infinity;
  for (const vehicle of vehicles) {
    if (vehicle.hp <= 0 || vehicle.driver || Math.abs(vehicle.speed) > 1.75) continue;
    const gap = distanceSquared(player, vehicle);
    if (gap < (VEHICLES[vehicle.kind].radius + 1.03) ** 2 && gap < distance) {
      nearest = vehicle;
      distance = gap;
    }
  }
  return nearest;
}

export function vehicleCockpit(vehicle) {
  return VEHICLES[vehicle.kind].gunHeight;
}

export function vehicleAimFrame(vehicle, yaw, pitch, layout) {
  const gun = { x: vehicle.x, y: vehicleCockpit(vehicle), z: vehicle.z };
  const aim = aimVector(yaw, pitch);
  const length = vehicle.kind === 'bastion' ? 7.1 : 6;
  const height = vehicle.kind === 'bastion' ? 2.82 : 2.27;
  const distance = Math.hypot(length, height);
  const back = { x: Math.sin(yaw) * length / distance, y: height / distance, z: Math.cos(yaw) * length / distance };
  const stop = Math.max(0.1, wallDistance(gun, back, distance, layout) - 0.13);
  const camera = { x: gun.x + back.x * stop, y: gun.y + back.y * stop, z: gun.z + back.z * stop };
  const focus = { x: gun.x + aim.x * 31, y: gun.y + aim.y * 31, z: gun.z + aim.z * 31 };
  const delta = { x: focus.x - camera.x, y: focus.y - camera.y, z: focus.z - camera.z };
  const size = Math.hypot(delta.x, delta.y, delta.z);
  const sight = { x: delta.x / size, y: delta.y / size, z: delta.z / size };
  return { aim, gun, camera, focus, sight, stop };
}
