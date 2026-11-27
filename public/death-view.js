import { eyeHeight, wallDistance } from './rules.js';

export const KILL_CAM_MS = 1050;

export function nextAlly(players, own, mode, current) {
  if (mode === 'ffa') return null;
  const allies = players.filter(player => player.hp > 0 && player.team === own.team && player.id !== own.id)
    .sort((a, b) => Number(a.bot) - Number(b.bot) || a.slot - b.slot);
  if (!allies.length) return null;
  return allies[(allies.findIndex(player => player.id === current) + 1) % allies.length];
}

export function deathFrame(event, progress, reducedMotion) {
  const body = event.body;
  const focus = event.impact ?? [body.x, body.y + (body.crouch ? 0.64 : 1.03), body.z];
  const source = body.from;
  const dx = source[0] - focus[0];
  const dz = source[2] - focus[2];
  const bearing = Math.hypot(dx, dz) > 0.12 ? Math.atan2(dx, dz) : body.yaw;
  const t = reducedMotion ? 0.48 : progress;
  const angle = bearing + Math.PI * (0.73 - t * 0.28);
  return {
    anchor: { x: focus[0], y: focus[1] + 0.18, z: focus[2] },
    camera: { x: Math.sin(angle) * (3.45 + t), y: 0.96 + t * 0.50, z: Math.cos(angle) * (3.45 + t) },
    focus: { x: focus[0] + dx * 0.09, y: focus[1] + 0.16, z: focus[2] + dz * 0.09 },
  };
}

export function followFrame(player, pose = player) {
  const anchor = { x: pose.x, y: pose.y + eyeHeight(player) * 0.84, z: pose.z };
  const sin = Math.sin(pose.yaw);
  const cos = Math.cos(pose.yaw);
  return {
    anchor,
    camera: { x: sin * 3.7 + cos * 0.67, y: 1.05, z: cos * 3.7 - sin * 0.67 },
    focus: { x: anchor.x - sin * 3, y: anchor.y + Math.sin(player.pitch) * 1.4 - 0.13, z: anchor.z - cos * 3 },
  };
}

export function cameraInCover(frame, layout) {
  const { anchor, camera } = frame;
  const length = Math.hypot(camera.x, camera.y, camera.z);
  const direction = { x: camera.x / length, y: camera.y / length, z: camera.z / length };
  const clearance = wallDistance(anchor, direction, length, layout);
  const distance = Math.max(0.04, Math.min(length, clearance - 0.17));
  return { x: anchor.x + direction.x * distance, y: anchor.y + direction.y * distance, z: anchor.z + direction.z * distance };
}
