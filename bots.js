import { BASES, EYE, MAP_SIZE, PICKUPS, POINTS, WEAPONS, blocked, distanceSquared, wallDistance } from './public/rules.js';

const GRID = 20;
const STEP = MAP_SIZE * 2 / GRID;
const cells = Array.from({ length: GRID ** 2 }, (_, i) => ({
  x: (i % GRID + 0.5) * STEP - MAP_SIZE,
  z: (Math.floor(i / GRID) + 0.5) * STEP - MAP_SIZE,
}));
const edges = cells.map((cell, i) => {
  if (blocked(cell.x, cell.z, 0.65)) return [];
  return [i % GRID ? i - 1 : -1, i % GRID < GRID - 1 ? i + 1 : -1, i - GRID, i + GRID]
    .filter(next => cells[next] && !blocked(cells[next].x, cells[next].z, 0.65)
      && !blocked((cell.x + cells[next].x) / 2, (cell.z + cells[next].z) / 2, 0.65));
});

function nearestCell(position) {
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

function route(from, to) {
  const start = nearestCell(from);
  const end = nearestCell(to);
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

function visible(from, to) {
  const dy = to.y - from.y;
  const distance = Math.hypot(to.x - from.x, dy, to.z - from.z);
  if (distance < 0.01) return true;
  return wallDistance({ x: from.x, y: from.y + EYE, z: from.z }, {
    x: (to.x - from.x) / distance, y: dy / distance, z: (to.z - from.z) / distance,
  }, distance + 1) >= distance;
}

export function botInput(room, player, now) {
  const enemies = [...room.players.values()].filter(other =>
    other !== player && other.hp > 0 && (room.mode === 'ffa' || other.team !== player.team));
  enemies.sort((a, b) => distanceSquared(a, player) - distanceSquared(b, player));
  const enemy = enemies.find(other => distanceSquared(player, other) < 26 ** 2 && visible(player, other));
  if (enemy !== player.target) {
    player.target = enemy;
    player.reactAt = now + 0.22 + Math.random() * 0.33;
  }

  let objective = enemies[0] ?? POINTS[player.team];
  if (room.mode === 'dom') {
    const station = (player.team + player.slot) % POINTS.length;
    objective = POINTS[room.points[station].owner === player.team ? (station + 1) % POINTS.length : station];
  }
  if (room.mode === 'ctf') {
    const own = room.flags[player.team];
    const enemyTeam = (player.team + 1 + player.slot % 2) % BASES.length;
    const flag = room.flags[enemyTeam];
    objective = room.flags.some(item => item.carrier === player.id) ? BASES[player.team]
      : (own.status === 'dropped' && player.slot < 2 ? own : room.players.get(flag.carrier) ?? flag);
  }

  const supply = PICKUPS.find((item, i) => item.y === 0 && room.pickups[i] <= now
    && distanceSquared(player, item) < 7 ** 2
    && (item.type === 'med' && player.hp < 65 || item.type === 'shield' && player.armor < 14));
  if (supply && !enemy && !room.flags.some(flag => flag.carrier === player.id)) objective = supply;

  if (now >= player.routeAt || !player.path) {
    player.routeAt = now + 0.5 + Math.random() * 0.55;
    player.path = route(player, objective);
  }
  while (player.path.length && distanceSquared(player, player.path[0]) < 1.1 ** 2) player.path.shift();
  let destination = player.path[0] ?? objective;
  if (!player.path.length && distanceSquared(player, objective) < 1.5) destination = player;
  const aim = enemy ?? destination;
  const aimDistance = distanceSquared(player, aim);
  const sway = Math.sin(now * 11 + player.slot) * 0.095;
  const yaw = aimDistance > 0.01 ? Math.atan2(player.x - aim.x, player.z - aim.z) + (enemy ? sway : 0) : player.yaw;
  const travelling = destination !== player;
  const movementYaw = Math.atan2(player.x - destination.x, player.z - destination.z);
  const relative = movementYaw - yaw;
  const close = enemy && distanceSquared(player, enemy) < 12 ** 2;
  return {
    x: close ? Math.sin(now * 3 + player.slot) * 0.72 : -Number(travelling) * Math.sin(relative),
    z: close ? 0.08 : Number(travelling) * Math.cos(relative),
    yaw, pitch: enemy ? Math.atan2(enemy.y - player.y - 0.35, Math.sqrt(aimDistance)) + sway * 0.52 : 0,
    sprint: !enemy,
    fire: Boolean(enemy && player.reactAt <= now && aimDistance < WEAPONS[player.weapon].range ** 2),
    reload: !enemy && player.loadout[player.weapon].ammo < WEAPONS[player.weapon].magazine / 3,
  };
}
