export const TICK = 1 / 30;
export const TEAM_SIZE = 4;
export const CAPACITY = 12;
export const MAP_SIZE = 30;
export const BODY_RADIUS = 0.43;
export const EYE = 1.58;
export const DEFAULT_WEAPON = 'pulse';
export const WEAPONS = {
  pulse: { label: 'Pulse', color: '#89fff0', magazine: 24, reserve: Infinity, interval: 0.145, reload: 1.3, range: 60, damage: 27, pattern: [0] },
  scatter: { label: 'Scatter', color: '#ffad72', magazine: 6, reserve: 24, interval: 0.66, reload: 1.55, range: 22, damage: 18, pattern: [0, -0.06, 0.06, -0.09, 0.09, -0.025, 0.025] },
  lance: { label: 'Lance', color: '#d8b4ff', magazine: 4, reserve: 12, interval: 0.85, reload: 1.7, range: 85, damage: 76, pattern: [0] },
  arc: { label: 'Arc', color: '#ffed87', magazine: 36, reserve: 108, interval: 0.08, reload: 1.6, range: 43, damage: 15, pattern: [0] },
};
export const ARMORY = Object.keys(WEAPONS);
export const POWERUPS = {
  shield: { label: 'Štít', color: '#69b7ff', respawn: 28 },
  surge: { label: 'Overcharge', color: '#ff769b', respawn: 38 },
  med: { label: 'Léčení', color: '#9cff8c', respawn: 18 },
};
export const RESPAWN = 2.8;
export const TEAMS = [
  { name: 'ION', color: '#1be8ed', angle: Math.PI },
  { name: 'EMBER', color: '#ff833b', angle: Math.PI / 3 },
  { name: 'VIOLET', color: '#b58aff', angle: -Math.PI / 3 },
];
const PILOT_COLORS = [
  '#2df0f1', '#fc8242', '#bd83ff', '#c9f665',
  '#ff679e', '#7da1ff', '#ffe066', '#f4f7ff',
  '#66ffa9', '#ff6059', '#58b7ee', '#e27bdf',
];

export function pilotColor(player, mode) {
  return mode === 'ffa' ? PILOT_COLORS[player.team * TEAM_SIZE + player.slot] : TEAMS[player.team].color;
}

export const MODES = {
  tdm: { label: 'Team Deathmatch', short: 'TDM', goal: 35, time: 360, description: 'Tři týmy. Každé vyřazení přidá bod vašemu týmu.' },
  ffa: { label: 'Deathmatch', short: 'FFA', goal: 20, time: 300, description: 'Každý za sebe. Vyhrává hráč s nejvíc vyřazeními.' },
  dom: { label: 'Dominance', short: 'DOM', goal: 150, time: 420, description: 'Obsazujte tři stanice. Každá držená stanice průběžně přidává body.' },
  ctf: { label: 'Capture the Flag', short: 'CTF', goal: 5, time: 480, description: 'Doneste soupeřovu vlajku na vlastní základnu, když je vaše vlajka doma.' },
};
export const DEFAULT_MODE = 'tdm';

function radial(angle, distance, offset = 0) {
  return { x: Math.sin(angle) * distance + Math.cos(angle) * offset, z: Math.cos(angle) * distance - Math.sin(angle) * offset };
}

export const BASES = TEAMS.map(team => radial(team.angle, 23));
export const POINTS = TEAMS.map((team, i) => ({ ...radial(team.angle, 12), label: 'ABC'[i] }));
export const SPAWNS = TEAMS.map(team =>
  [-3.6, -1.2, 1.2, 3.6].map((offset, i) => radial(team.angle, 25 + (i % 3 ? 0 : 0.55), offset))
);
export const TERRACES = TEAMS.flatMap((team, sector) =>
  [1.05, 2.25, 3.4].map((h, i) => ({
    ...radial(team.angle + Math.PI / 3, [11.2, 13.8, 16.6][i]),
    w: [2.75, 3.0, 3.5][i], d: [2.75, 3.0, 3.5][i], h, sector, terrace: true,
  }))
);
export const PICKUPS = [
  ...TEAMS.flatMap((team, i) => [
    { ...TERRACES[i * 3 + 2], y: 3.4, type: ARMORY[i + 1] },
    { ...radial(team.angle, 16, -3.7), y: 0, type: 'med' },
    { ...radial(team.angle, 13, 4.25), y: 0, type: 'shield' },
  ]),
  { x: 0, y: 4.5, z: 0, type: 'surge' },
];
export const COVER = [
  ...TERRACES,
  ...TEAMS.map((team, sector) => ({
    ...radial(team.angle, 3.32), w: 2.05, d: 2.05, h: 1.67, sector, terrace: true,
  })),
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 19, -4.5), w: 3.0, d: 2.8, h: 2.85, sector },
    { ...radial(team.angle, 19, 4.5), w: 3.0, d: 2.8, h: 2.85, sector },
    { ...radial(team.angle, 11, -5.1), w: 3.15, d: 2.1, h: 1.95, sector },
    { ...radial(team.angle, 11, 5.1), w: 3.15, d: 2.1, h: 1.95, sector },
    { ...radial(team.angle + Math.PI / 3, 21), w: 3.7, d: 4.0, h: 3.6, sector: -1 },
    { ...radial(team.angle + Math.PI / 3, 7.8), w: 2.7, d: 2.7, h: 2.6, sector: -1 },
    { ...radial(team.angle + Math.PI / 3, 26.5, 3), w: 2.4, d: 3.0, h: 1.8, sector: -1 },
  ]),
  { x: 0, z: 0, w: 2.25, d: 2.25, h: 4.5, sector: -1, relay: true },
];

export function blocked(x, z, radius = BODY_RADIUS, y = 0) {
  if (Math.abs(x) > MAP_SIZE - radius || Math.abs(z) > MAP_SIZE - radius) return true;
  return COVER.some(b => y < b.h - 0.0001 && Math.abs(x - b.x) < b.w / 2 + radius && Math.abs(z - b.z) < b.d / 2 + radius);
}

export function movePlayer(player, input, dt = TICK) {
  if (input.jump) {
    if (player.grounded) {
      player.vy = 7.25;
      player.grounded = false;
      player.airJump = true;
    } else if (player.airJump) {
      player.vy = 8.5;
      player.airJump = false;
    }
  }
  player.vy = (player.vy ?? 0) - 18 * dt;
  const currentY = player.y ?? 0;
  let nextY = currentY + player.vy * dt;
  let floor = 0;
  if (player.vy <= 0) {
    for (const obstacle of COVER) {
      if (currentY + 0.0001 >= obstacle.h
        && Math.abs(player.x - obstacle.x) < obstacle.w / 2 + BODY_RADIUS * 0.9
        && Math.abs(player.z - obstacle.z) < obstacle.d / 2 + BODY_RADIUS * 0.9) floor = Math.max(floor, obstacle.h);
    }
  }
  player.grounded = nextY <= floor;
  if (player.grounded) {
    nextY = floor;
    player.vy = 0;
    player.airJump = true;
  }
  player.y = nextY;

  const length = Math.hypot(input.x, input.z);
  if (!length) return;
  const speed = (input.sprint ? 8.7 : 6.2) * dt / Math.max(1, length);
  const cos = Math.cos(input.yaw);
  const sin = Math.sin(input.yaw);
  const dx = (input.x * cos - input.z * sin) * speed;
  const dz = (-input.x * sin - input.z * cos) * speed;
  if (!blocked(player.x + dx, player.z, BODY_RADIUS, nextY)) player.x += dx;
  if (!blocked(player.x, player.z + dz, BODY_RADIUS, nextY)) player.z += dz;
}

export function aimVector(yaw, pitch) {
  const horizontal = Math.cos(pitch);
  return { x: -Math.sin(yaw) * horizontal, y: Math.sin(pitch), z: -Math.cos(yaw) * horizontal };
}

export function rayBox(origin, direction, min, max) {
  let entry = 0;
  let exit = Infinity;
  for (const axis of ['x', 'y', 'z']) {
    if (Math.abs(direction[axis]) < 0.000001) {
      if (origin[axis] < min[axis] || origin[axis] > max[axis]) return Infinity;
      continue;
    }
    const first = (min[axis] - origin[axis]) / direction[axis];
    const second = (max[axis] - origin[axis]) / direction[axis];
    entry = Math.max(entry, Math.min(first, second));
    exit = Math.min(exit, Math.max(first, second));
    if (entry > exit) return Infinity;
  }
  return entry;
}

export function wallDistance(origin, direction, limit = 65) {
  let distance = limit;
  for (const box of COVER) {
    distance = Math.min(distance, rayBox(origin, direction,
      { x: box.x - box.w / 2, y: 0, z: box.z - box.d / 2 },
      { x: box.x + box.w / 2, y: box.h, z: box.z + box.d / 2 }));
  }
  for (const axis of ['x', 'z']) {
    if (Math.abs(direction[axis]) > 0.000001) {
      const edge = MAP_SIZE * Math.sign(direction[axis]);
      distance = Math.min(distance, (edge - origin[axis]) / direction[axis]);
    }
  }
  if (direction.y < 0) distance = Math.min(distance, -origin.y / direction.y);
  return distance;
}

export function distanceSquared(a, b) {
  return (a.x - b.x) ** 2 + (a.z - b.z) ** 2;
}

export function roomCode(value) {
  return typeof value === 'string' && /^[a-z0-9-]{3,24}$/i.test(value) ? value.toUpperCase() : null;
}
