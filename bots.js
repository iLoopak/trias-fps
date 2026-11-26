import { PILOT_HP, WEAPONS, blocked, canDodge, distanceSquared, eyeHeight, radial, wallDistance } from './public/rules.js';

import { boardingVehicle, vehicleCockpit, VEHICLES, wrapAngle } from './public/vehicles.js';
const navigation = new WeakMap();

function navigationFor(layout) {
  if (navigation.has(layout)) return navigation.get(layout);
  const width = Math.max(20, Math.ceil(layout.length / 1.55));
  const step = layout.length * 2 / width;
  const cells = Array.from({ length: width ** 2 }, (_, i) => ({
    x: (i % width + 0.5) * step - layout.length,
    z: (Math.floor(i / width) + 0.5) * step - layout.length,
  }));
  const open = cell => !blocked(cell.x, cell.z, 0.65, layout.terrain?.(cell.x, cell.z) ?? 0, layout);
  const edges = cells.map((cell, i) => {
    if (!open(cell)) return [];
    return [i % width ? i - 1 : -1, i % width < width - 1 ? i + 1 : -1, i - width, i + width]
      .filter(next => cells[next] && open(cells[next])
        && open({ x: (cell.x + cells[next].x) / 2, z: (cell.z + cells[next].z) / 2 }));
  });
  const grid = { cells, edges };
  navigation.set(layout, grid);
  return grid;
}

function nearestCell(position, { cells, edges }) {
  let best = Infinity;
  let index = 0;
  for (let i = 0; i < cells.length; i++) {
    if (!edges[i].length) continue;
    const distance = distanceSquared(position, cells[i]);
    if (distance >= best) continue;
    best = distance;
    index = i;
  }
  return index;
}

export function route(from, to, layout) {
  const grid = navigationFor(layout);
  const { cells, edges } = grid;
  const start = nearestCell(from, grid);
  const end = nearestCell(to, grid);
  if (start === end) return [];
  const parents = new Int16Array(cells.length).fill(-1);
  const queue = [start];
  parents[start] = start;
  for (let head = 0; head < queue.length && parents[end] === -1; head++) {
    for (const neighbor of edges[queue[head]]) {
      if (parents[neighbor] !== -1) continue;
      queue.push(neighbor);
      parents[neighbor] = queue[head];
    }
  }
  if (parents[end] === -1) return [];
  const path = [];
  for (let node = end; node !== start; node = parents[node]) path.push(cells[node]);
  return path.reverse();
}

export function visible(from, to, layout) {
  const dy = to.y + eyeHeight(to) - from.y - eyeHeight(from);
  const distance = Math.hypot(to.x - from.x, dy, to.z - from.z);
  if (distance < 0.01) return true;
  return wallDistance({ x: from.x, y: from.y + eyeHeight(from), z: from.z }, {
    x: (to.x - from.x) / distance, y: dy / distance, z: (to.z - from.z) / distance,
  }, distance + 1, layout) >= distance;
}

function driveBot(room, player, enemies, now) {
  const hull = room.fleet.hulls[player.vehicle];
  const spec = VEHICLES[hull.kind];
  const angle = Math.atan2(hull.x, hull.z);
  const elapsed = now - hull.mountedAt;
  if (elapsed > 23 || hull.hp < spec.hp * 0.23) {
    return { x: 0, z: 0, brake: true, use: Math.abs(hull.speed) < 3.5 };
  }
  if (!hull.patrolDirection) {
    const clockwise = radial(angle + 0.26, room.layout.roadRadius);
    const toward = Math.atan2(hull.x - clockwise.x, hull.z - clockwise.z);
    hull.patrolDirection = Math.abs(wrapAngle(toward - hull.heading)) < Math.PI / 2 ? 1 : -1;
  }
  const next = radial(angle + hull.patrolDirection * 0.31, room.layout.roadRadius);
  const desired = Math.atan2(hull.x - next.x, hull.z - next.z);
  const turn = wrapAngle(desired - hull.heading);
  if (Math.abs(hull.speed) < 1.05 && elapsed > 1.5 && now > (hull.recoverAfter ?? 0)) {
    hull.reverseUntil = now + 0.72;
    hull.recoverAfter = now + 2.0;
  }
  const reversing = now < (hull.reverseUntil ?? 0);
  const core = room.siege?.cores.find(item => item.opened & (1 << player.team));
  const baseInSight = core && distanceSquared(hull, core) < 35 ** 2 && visible(player, { ...core, y: 0 }, room.layout);
  const target = (baseInSight && { ...core, y: 0, vx: 0, vz: 0 }) ||
    enemies.find(enemy => distanceSquared(hull, enemy) < 34 ** 2 && visible(player, enemy, room.layout));
  let yaw = desired;
  let pitch = 0;
  if (target) {
    const flight = spec.gun.projectile ? Math.sqrt(distanceSquared(target, hull)) / spec.gun.projectile.speed : 0;
    const x = target.x + target.vx * flight * 0.58;
    const z = target.z + target.vz * flight * 0.58;
    yaw = Math.atan2(hull.x - x, hull.z - z);
    pitch = Math.atan2(target.y + eyeHeight(target) * 0.68 - vehicleCockpit(hull), Math.hypot(x - hull.x, z - hull.z));
  }
  return {
    x: Math.max(-1, Math.min(1, -turn * 1.9)) * (reversing ? -1 : 1),
    z: reversing ? -0.76 : Math.abs(turn) > 0.84 ? 0.49 : 0.94,
    sprint: Math.abs(turn) < 0.21 && elapsed > 2 && hull.boost > 0.27,
    yaw, pitch, fire: Boolean(target && elapsed > 0.75),
  };
}

export function botInput(room, player, now) {
  const { bases, points, pickups, launchPads, gantries } = room.layout;
  const enemies = [...room.players.values()].filter(other =>
    other !== player && other.hp > 0 && (room.mode === 'ffa' || other.team !== player.team));
  enemies.sort((a, b) => distanceSquared(a, player) - distanceSquared(b, player));
  if (player.vehicle >= 0) return driveBot(room, player, enemies, now);
  const spec = WEAPONS[player.weapon];
  const sightRange = player.weapon === 'lance' ? 39 : player.weapon === 'scatter' ? 20 : 27;
  const enemy = enemies.find(other => distanceSquared(player, other) < sightRange ** 2 && visible(player, other, room.layout));
  if (enemy !== player.target) {
    player.target = enemy;
    player.reactAt = now + 0.22 + Math.random() * 0.33;
  }

  let objective = enemies[0] ?? points[player.team];
  const siegeGoal = room.siege?.botGoal(player);
  if (siegeGoal) objective = siegeGoal.target;
  if (room.mode === 'dom') {
    const station = (player.team + player.slot) % points.length;
    objective = points[room.points[station].owner === player.team ? (station + 1) % points.length : station];
  }
  if (room.mode === 'ctf') {
    const own = room.flags[player.team];
    const enemyTeam = (player.team + 1 + player.slot % 2) % bases.length;
    const flag = room.flags[enemyTeam];
    objective = room.flags.some(item => item.carrier === player.id) ? bases[player.team]
      : (own.status === 'dropped' && player.slot < 2 ? own : room.players.get(flag.carrier) ?? flag);
  }

  const supply = pickups.find((item, i) => Math.abs(item.y - (room.layout.terrain?.(item.x, item.z) ?? 0)) < 0.05 && room.pickups[i] <= now
    && distanceSquared(player, item) < 7 ** 2
    && (item.type === 'med' && player.hp < PILOT_HP * 0.64 || item.type === 'shield' && player.armor < 14));
  const carrying = room.mode === 'ctf' && room.flags.some(flag => flag.carrier === player.id);
  if (supply && !enemy && !carrying) objective = supply;
  const pad = launchPads[player.team];
  const arenaMode = room.mode === 'tdm' || room.mode === 'ffa';
  const wantsHeight = pad && arenaMode && !enemy && !supply && !player.usedLaunch
    && player.slot % 2 === 0 && distanceSquared(player, pad) < 8.5 ** 2;
  if (wantsHeight) objective = pad;
  const kind = (player.team + player.deaths) % 2 ? 'bastion' : 'skimmer';
  const chassis = player.slot === 3 && !carrying && !enemy && now > player.vehiclePlanAt
    ? room.fleet.hulls.find(hull => hull.team === player.team && hull.kind === kind && !hull.driver && hull.hp > 0
      && distanceSquared(player, hull) < 17 ** 2)
    : null;
  if (chassis) objective = chassis;

  if (now >= player.routeAt || !player.path) {
    player.routeAt = now + 0.5 + Math.random() * 0.55;
    player.path = route(player, objective, room.layout);
  }
  while (player.path.length && distanceSquared(player, player.path[0]) < 1.1 ** 2) player.path.shift();
  let destination = player.path[0] ?? objective;
  if (!player.path.length && distanceSquared(player, objective) < 1.5) destination = player;
  if (wantsHeight && distanceSquared(player, pad) < 3.6 ** 2) destination = pad;
  if (chassis && distanceSquared(player, chassis) < 5.2 ** 2) destination = chassis;
  const flightTime = now - player.launchAt;
  const inner = gantries[player.team * 2 + 1];
  const crossing = pad && inner && arenaMode && player.slot === 0 && !player.grounded && flightTime > 0.25 && flightTime < 1.65
    && distanceSquared(player, pad) < 14 ** 2;
  if (crossing) destination = inner;
  const flight = enemy && spec.projectile ? Math.sqrt(distanceSquared(player, enemy)) / spec.projectile.speed : 0;
  const lead = flight ? { x: enemy.x + enemy.vx * flight * 0.6, z: enemy.z + enemy.vz * flight * 0.6 } : null;
  const core = siegeGoal?.core;
  const baseInSight = core && distanceSquared(player, core) < spec.range ** 2 && visible(player, { ...core, y: 0 }, room.layout);
  const hostileEcho = !enemy && !baseInSight && room.signature.echoEnabled && now >= (player.reactAt ?? 0)
    ? [...room.signature.echoes.values()].find(echo => room.signature.hostile(player, echo)
      && distanceSquared(player, echo) < 14 ** 2 && visible(player, echo, room.layout)) : null;
  const aim = lead ?? enemy ?? hostileEcho ?? (baseInSight ? core : destination);
  const aimDistance = distanceSquared(player, aim);
  const personAim = enemy ?? hostileEcho;
  const sway = Math.sin(now * 11 + player.slot) * 0.095;
  const yaw = aimDistance > 0.01 ? Math.atan2(player.x - aim.x, player.z - aim.z) + (enemy ? sway : 0) : player.yaw;
  const travelling = destination !== player;
  const movementYaw = Math.atan2(player.x - destination.x, player.z - destination.z);
  const relative = movementYaw - yaw;
  const close = enemy && distanceSquared(player, enemy) < 12 ** 2;
  const closingWithScatter = enemy && player.weapon === 'scatter' && aimDistance > 6 ** 2 && !carrying && !siegeGoal;
  const steadyingLance = enemy && player.weapon === 'lance' && aimDistance > 15 ** 2 && !carrying && !siegeGoal
    && (room.mode === 'tdm' || room.mode === 'ffa');
  const showDodge = close && canDodge(player) && now >= (player.dodgeDecisionAt ?? 0)
    && player.hp < PILOT_HP * 0.76 && player.grounded && !carrying;
  if (showDodge) player.dodgeDecisionAt = now + 1.15 + Math.random() * 0.85;
  const drift = !crossing && !player.grounded && player.padCooldown > 0;
  const isStrafing = close && !crossing;
  return {
    x: drift ? 0 : closingWithScatter ? Math.sin(now * 5 + player.slot) * 0.32
      : steadyingLance ? Math.sin(now * 2.3 + player.slot) * 0.21
        : isStrafing ? Math.sin(now * 3 + player.slot) * 0.76 : -Number(travelling) * Math.sin(relative),
    z: drift ? 0 : closingWithScatter ? 0.94 : steadyingLance ? 0 : isStrafing ? 0.08 : Number(travelling) * Math.cos(relative),
    yaw, pitch: personAim ? Math.atan2(personAim.y + eyeHeight(personAim) - player.y - eyeHeight(player) - .35,
      Math.sqrt(aimDistance)) + sway * .52
      : baseInSight ? Math.atan2(core.y - player.y - eyeHeight(player), Math.sqrt(aimDistance)) : 0,
    sprint: crossing || closingWithScatter || !enemy,
    crouch: Boolean(steadyingLance),
    use: Boolean(chassis && boardingVehicle(player, room.fleet.hulls) === chassis),
    jump: crossing && player.airJump && flightTime > 0.52 && player.vy < 1.5,
    dodge: Boolean(showDodge && Math.random() < 0.55),
    fire: Boolean(enemy && player.reactAt <= now && aimDistance < (player.weapon === 'scatter' ? 11 : spec.range) ** 2 || !enemy && baseInSight || hostileEcho),
    echo: Boolean(enemy && player.reactAt <= now && room.signature.echoes.has(player.id) && Math.random() < .045),
    reload: !enemy && player.loadout[player.weapon].ammo < spec.magazine / 3,
  };
}
