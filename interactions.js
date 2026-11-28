import { blocked, BODY_RADIUS, distanceSquared, eyeHeight, floorBelow, hitHeight, wallDistance } from './shared/rules.js';
import { VEHICLES } from './shared/vehicles.js';

export function interactionFlags(env) {
  return {
    intercept: env.TRIAS_GRENADE_INTERCEPT === '1',
    recall: env.TRIAS_ECHO_RECALL === '1',
    brace: env.TRIAS_SHIELD_BRACE === '1',
    traversal: env.TRIAS_TRAVERSAL_STYLE === '1',
  };
}

export function grenadeOnRay(room, shooter, origin, aim, limit, now) {
  let grenade;
  let distance = limit;
  for (const projectile of room.projectiles) {
    if (projectile.weapon !== 'grenade' || projectile.fuseAt - now <= .10 ||
        room.players.get(projectile.owner.id) !== projectile.owner ||
        projectile.owner === shooter || (room.horde ? Boolean(shooter.hordeType) === Boolean(projectile.owner.hordeType)
          : room.mode !== 'ffa' && projectile.owner.team === shooter.team)) continue;

    const x = projectile.position.x - origin.x, y = projectile.position.y - origin.y, z = projectile.position.z - origin.z;
    const along = x * aim.x + y * aim.y + z * aim.z;
    if (along <= 0 || along > distance + .27) continue;
    const overlap = .27 ** 2 - (x*x + y*y + z*z - along*along);
    if (overlap < 0) continue;
    const hit = along - Math.sqrt(overlap);
    if (hit <= .04 || hit >= distance) continue;
    grenade = projectile;
    distance = hit;
  }
  return grenade ? { grenade, distance } : null;
}

export function explosionImpulse(player, direction, strength, projectile, shielded, enabled) {
  const horizontal = Math.hypot(direction.x, direction.z) || 1;
  let push = (projectile.direction ? 4.8 : 7.3) * strength;
  const brace = enabled && shielded;
  if (brace) push *= .48;
  const incomingSpeed = Math.hypot(player.vx, player.vz);
  player.vx += direction.x / horizontal * push;
  player.vz += direction.z / horizontal * push;
  if (brace) {
    const cap = Math.max(15, incomingSpeed);
    const speed = Math.hypot(player.vx, player.vz);
    if (speed > cap) {
      player.vx *= cap / speed;
      player.vz *= cap / speed;
    }
  }
  player.vy = Math.max(player.vy, (brace ? 1.25 : 2.8) * strength);
  player.grounded = false;
}

export function recallBlocker(room, player, echo) {
  if (!player.grounded || !echo.grounded || player.vehicle >= 0 || player.turret >= 0 || player.dodgeTime > 0) return 'ground';
  if (room.mode === 'ctf' && room.flags.some(flag => flag.carrier === player.id)) return 'objective';
  const layout = room.combatLayout;
  const radius = BODY_RADIUS;
  const height = hitHeight(player);
  const x = echo.x - player.x, y = echo.y - player.y, z = echo.z - player.z;
  const distance = Math.hypot(x, y, z);
  if (distance < .55 || distance > 10 || Math.abs(y) > .5) return 'range';

  const zones = room.mode === 'dom' ? room.layout.points : room.mode === 'ctf' ? [...room.layout.bases, ...room.flags]
    : room.siege ? [...room.siege.layout.nodes, ...room.siege.cores] : room.horde ? [room.horde.core] : [];
  const lengthSq = x*x + z*z;
  const crossesZone = zones.some(site => {
    const t = Math.max(0, Math.min(1, ((site.x-player.x)*x + (site.z-player.z)*z) / lengthSq));
    return distanceSquared(site, { x: player.x + x*t, z: player.z + z*t }) < ((site.radius ?? 3.05) + radius) ** 2;
  });
  if (crossesZone) return 'objective';

  if (layout.launchPads.some(pad => Math.abs(echo.y - pad.y) < .12 &&
      distanceSquared(echo, pad) < (pad.radius + radius) ** 2)) return 'blocked';
  const floor = floorBelow(echo.x, echo.z, echo.y + .065, radius, layout);
  if (Math.abs(floor - echo.y) > .07) return 'blocked';
  const step = Math.ceil(distance / .34);
  for (let i = 1; i <= step; i++) {
    const t = i / step;
    const px = player.x + t*x, py = player.y + t*y, pz = player.z + t*z;
    if ((layout.terrain?.(px, pz) ?? 0) > py + .065 || blocked(px, pz, radius, py + .01, layout, height)) return 'blocked';
  }
  const eye = { x: player.x, y: player.y + eyeHeight(player), z: player.z };
  if (wallDistance(eye, { x: x/distance, y: y/distance, z: z/distance }, distance, layout) < distance - .03) return 'blocked';
  for (const other of room.players.values()) {
    if (other !== player && other.hp > 0 && other.vehicle < 0 && echo.y < other.y + hitHeight(other)
        && echo.y + height > other.y && distanceSquared(echo, other) < (radius * 2) ** 2) return 'occupied';
  }
  if (room.fleet.hulls.some(hull => {
    const spec = VEHICLES[hull.kind];
    return hull.hp > 0 && echo.y < spec.height && distanceSquared(echo, hull) < (Math.hypot(spec.halfLength, spec.halfWidth) + radius) ** 2;
  })) return 'occupied';
  return null;
}
