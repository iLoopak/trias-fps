import { ISLAND_RADIUS, islandFord, islandHeight, islandTerrainDistance } from './island-terrain.js';
export { ISLAND_CROSSINGS, creekCenter, islandFord, riverCenter } from './island-terrain.js';

export const TICK = 1 / 30;
export const TEAM_SIZE = 4;
export const CAPACITY = 12;
export const MAP_SIZE = 30;
export const BODY_RADIUS = 0.43;
export const EYE = 1.58;
export const PILOT_HP = 210;
export const MED_HEAL = 65;
export const SHIELD_ARMOR = 50;
const BODY_HEIGHT = 1.78;
const CROUCH_HEIGHT = 1.12;
export const GRENADE = { label: 'Impulsní granát', color: '#ffb863', splash: 72, radius: 4, fuse: 1.65 };
export const TURRET = { turn: 1.0, interval: 0.10, dismountRadius: 1.65, damage: 18, range: 49, pattern: [0] };

export function eyeHeight(player) {
  return player.crouch ? 0.92 : EYE;
}

export function hitHeight(player) {
  return player.crouch ? CROUCH_HEIGHT : BODY_HEIGHT;
}

export function turretYaw(yaw, facing) {
  const delta = yaw - facing;
  return facing + Math.max(-TURRET.turn, Math.min(TURRET.turn, Math.atan2(Math.sin(delta), Math.cos(delta))));
}
export const SPRINT_SPEED = 10.1;
export const DODGE_COOLDOWN = 0.96;
const DODGE_DURATION = 0.22;
const GRAVITY = 19;
export const DEFAULT_WEAPON = 'pulse';
export const WEAPONS = {
  pulse: { label: 'Pulse', color: '#89fff0', magazine: 24, reserve: Infinity, interval: 0.145, reload: 1.3, range: 60, damage: 23, pattern: [0] },
  scatter: { label: 'Scatter', color: '#ffad72', magazine: 6, reserve: 24, interval: 0.66, reload: 1.55, range: 22, damage: 14,
    pattern: [0, -0.06, 0.06, -0.09, 0.09, -0.025, 0.025],
    alt: { cost: 2, interval: 0.91, range: 47, damage: 56, pattern: [0] } },
  lance: { label: 'Lance', color: '#d8b4ff', magazine: 4, reserve: 12, interval: 0.85, reload: 1.7, range: 85, damage: 68, pattern: [0] },
  arc: { label: 'Arc', color: '#ffed87', magazine: 36, reserve: 108, interval: 0.08, reload: 1.6, range: 43, damage: 13, pattern: [0] },
  rift: { label: 'Rift', color: '#ff79b9', magazine: 4, reserve: 12, interval: 0.72, reload: 1.62, range: 55,
    damage: 72, projectile: { speed: 22, radius: 3.2, splash: 54 } },
};
export const ARMORY = Object.keys(WEAPONS);
export const POWERUPS = {
  shield: { label: 'Štít', color: '#69b7ff', respawn: 28 },
  surge: { label: 'Overcharge', color: '#ff769b', respawn: 38 },
  med: { label: 'Léčení', color: '#9cff8c', respawn: 18 },
};
export const RESPAWN = 5;
export const HORDE_RESPAWN = 7;
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
  siege: { label: 'Obléhání', short: 'SIEGE', time: 720, arena: 'basin', description: 'Pěšáci propojují relé. Dovedou-li spojení k soupeřově základně, otevřou její jádro palbě.' },
  ctf: { label: 'Capture the Flag', short: 'CTF', goal: 5, time: 480, description: 'Doneste soupeřovu vlajku na vlastní základnu, když je vaše vlajka doma.' },
};
export const DEFAULT_MODE = 'tdm';
export const CHARACTERS = {
  vector: { label: 'Vektor', weapon: 'pulse', description: 'Univerzální karabina pro střední vzdálenost.' },
  bulwark: { label: 'Bašta', weapon: 'scatter', rounds: 4, description: 'Brokovnice pro boj zblízka.' },
  wraith: { label: 'Přízrak', weapon: 'lance', rounds: 2, description: 'Přesná puška pro boj na dálku.' },
  kestrel: { label: 'Poštolka', weapon: 'arc', rounds: 24, description: 'Rychlopalná zbraň pro krytí postupu.' },
};
export const DEFAULT_CHARACTER = 'vector';

export function radial(angle, distance, offset = 0) {
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
export const GANTRIES = TEAMS.flatMap((team, sector) => [
  { ...radial(team.angle, 14.65), w: 9.15, d: 3.55, bottom: 2.77, h: 3.3, angle: team.angle, sector, gantry: true },
  { ...radial(team.angle, 7.15), w: 4.1, d: 3.9, bottom: 3.48, h: 3.96, angle: team.angle, sector, gantry: true },
]);
export const LAUNCH_PADS = TEAMS.map((team, sector) => ({
  ...radial(team.angle, 19.9), y: 0, sector, angle: team.angle, radius: 0.98,
  vx: -Math.sin(team.angle) * 5.35, vz: -Math.cos(team.angle) * 5.35,
}));

export const PICKUPS = [
  ...TEAMS.flatMap((team, i) => [
    { ...TERRACES[i * 3 + 2], y: 3.4, type: ARMORY[i + 1] },
    { ...radial(team.angle, 16, -3.7), y: 0, type: 'med' },
    { ...radial(team.angle, 13, 4.25), y: 0, type: 'shield' },
  ]),
  { x: GANTRIES[3].x, y: GANTRIES[3].h, z: GANTRIES[3].z, type: 'rift' },
  { x: 0, y: 4.5, z: 0, type: 'surge' },
];
const relayCover = [
  ...GANTRIES,
  ...TERRACES,
  ...TEAMS.map((team, sector) => ({
    ...radial(team.angle, 3.32), w: 2.05, d: 2.05, h: 1.67, sector, terrace: true,
  })),
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 19, -4.9), w: 2.85, d: 2.8, h: 3.5, sector, angle: team.angle, profile: 'pylon' },
    { ...radial(team.angle, 18.5, 4.9), w: 2.0, d: 4.05, h: 2.35, sector, angle: team.angle, profile: 'baffle' },
    { ...radial(team.angle, 10.9, -5.5), w: 1.2, d: 3.8, h: 1.6, sector, angle: team.angle + 0.40, profile: 'baffle' },
    { ...radial(team.angle, 10.5, 5.0), w: 2.7, d: 1.68, h: 2.2, sector, angle: team.angle - 0.23, profile: 'pylon' },
    { ...radial(team.angle + Math.PI / 3, 21.5), w: 4.05, d: 3.75, h: 3.85, sector: -1, profile: 'pylon' },
    { ...radial(team.angle + Math.PI / 3, 7.8), w: 2.7, d: 2.7, h: 2.6, sector: -1, terrace: true },
    { ...radial(team.angle + Math.PI / 3, 4.65), w: 2.3, d: 2.75, h: 2.38, sector, angle: team.angle + Math.PI / 3, terrace: true },
    { ...radial(team.angle + Math.PI / 3, 26.5, 3), w: 2.1, d: 3.3, h: 1.8, sector, angle: team.angle + Math.PI / 3, profile: 'baffle' },
  ]),
  { x: 0, z: 0, w: 2.25, d: 2.25, h: 4.5, sector: -1, relay: true },
];

export function packCover(cover) {
  return cover.map(box => ({
    bottom: 0, ...box, cos: Math.cos(box.angle ?? 0), sin: Math.sin(box.angle ?? 0),
    radius: Math.hypot(box.w, box.d) / 2,
    min: { x: -box.w / 2, y: box.bottom ?? 0, z: -box.d / 2 },
    max: { x: box.w / 2, y: box.h, z: box.d / 2 },
  }));
}

export const COVER = packCover(relayCover);

const foundryGantries = TEAMS.flatMap((team, sector) => [
  { ...radial(team.angle, 14.8, -5.4), w: 3.4, d: 8.4, bottom: 2.34, h: 2.85, angle: team.angle, sector, gantry: true },
  { ...radial(team.angle, 6.8, -4.05), w: 3.8, d: 3.0, bottom: 3.12, h: 3.65, angle: team.angle, sector, gantry: true },
]);
const foundryPads = LAUNCH_PADS.map((pad, i) => ({ ...pad, ...radial(TEAMS[i].angle, 21, -5.4) }));
const foundryPickups = [
  ...TEAMS.flatMap((team, i) => {
    const deck = foundryGantries[i * 2];
    return [
      { ...radial(team.angle, 13.2, -5.4), y: deck.h, type: ARMORY[i + 1] },
      { ...radial(team.angle, 18.2, 4.15), y: 0, type: 'med' },
      { ...radial(team.angle + Math.PI / 3, 18.2), y: 0, type: 'shield' },
    ];
  }),
  { ...foundryGantries[1], y: foundryGantries[1].h, type: 'rift' },
  { x: 0, y: 3.2, z: 0, type: 'surge' },
];
const foundryCover = packCover([
  ...foundryGantries,
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 20.1, 7.4), w: 2.65, d: 4.4, h: 3.3, angle: team.angle, sector, profile: 'pylon' },
    { ...radial(team.angle, 16.8), w: 3.4, d: 3.7, bottom: 1.27, h: 1.66, angle: team.angle, sector, duct: true },
    { ...radial(team.angle, 14.5, 4.9), w: 1.05, d: 5.5, h: 2.4, angle: team.angle + 0.20, sector, profile: 'baffle' },
    { ...radial(team.angle + Math.PI / 3, 22), w: 5.3, d: 2.75, h: 3.4, angle: team.angle + Math.PI / 3, sector: -1, profile: 'pylon' },
    { ...radial(team.angle + Math.PI / 3, 8.75), w: 1.85, d: 4.6, h: 1.85, angle: team.angle + Math.PI / 3, sector: -1, profile: 'baffle' },
    { ...radial(team.angle + Math.PI / 3, 4.1), w: 2.15, d: 2.3, h: 1.35, angle: team.angle + Math.PI / 3, sector, terrace: true },
  ]),
  { x: 0, z: 0, w: 3.3, d: 3.3, h: 3.2, sector: -1, relay: true },
]);

const foundryTurrets = TEAMS.map((team, sector) => {
  const position = radial(team.angle, 17.25, 8.4);
  return { ...position, y: 0, yaw: Math.atan2(position.x, position.z), sector };
});

const BASIN_SIZE = 56;
const basinBases = TEAMS.map(team => radial(team.angle, 34));
const basinPoints = TEAMS.map((team, i) => ({ ...radial(team.angle, 23.6), label: 'ABC'[i] }));
const basinSpawns = TEAMS.map(team =>
  [-4.2, -1.45, 1.45, 4.2].map(offset => radial(team.angle, 39.3, offset))
);
const basinGantries = TEAMS.flatMap((team, sector) => [
  { ...radial(team.angle, 21.7, -6.5), angle: team.angle, w: 4.8, d: 7.6, bottom: 2.54, h: 3.08, sector, gantry: true },
  { ...radial(team.angle, 13.8, -5.6), angle: team.angle, w: 5.3, d: 4.65, bottom: 3.18, h: 3.62, sector, gantry: true },
]);
const basinPads = LAUNCH_PADS.map((pad, i) => ({ ...pad, ...radial(TEAMS[i].angle, 28, -6.5) }));
const basinPickups = [
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 21.5, -6.5), y: basinGantries[sector * 2].h, type: ARMORY[sector + 1] },
    { ...radial(team.angle, 18.1, 2.6), y: 0, type: 'med' },
    { ...radial(team.angle, 31.1, 3.8), y: 0, type: 'shield' },
  ]),
  ...[1, 3, 5].map(i => ({ ...basinGantries[i], y: basinGantries[i].h, type: 'rift' })),
  { x: 0, y: 3.6, z: 0, type: 'surge' },
];
const basinVehicles = TEAMS.flatMap((team, sector) => [
  { ...radial(team.angle, 42.8, -8.2), heading: team.angle + Math.PI / 2, kind: 'skimmer', team: sector },
  { ...radial(team.angle, 42.7, 9.2), heading: team.angle - Math.PI / 2, kind: 'bastion', team: sector },
]);
const basinRepairs = TEAMS.map(team => ({ ...radial(team.angle + Math.PI / 3, 37.5), radius: 3.65 }));
const basinFootZones = [...basinPoints.map(p => ({ ...p, radius: 3.95 })), ...basinBases.map(p => ({ ...p, radius: 3.35 }))];
const basinCover = packCover([
  ...basinGantries,
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 33.7, 6.3), w: 2.2, d: 6.5, h: 2.85, angle: team.angle, sector, profile: 'baffle' },
    { ...radial(team.angle, 32.7, -5.6), w: 1.9, d: 5.8, h: 2.6, angle: team.angle + 0.42, sector, profile: 'baffle' },
    { ...radial(team.angle, 23.8, 5.2), w: 2.8, d: 6.9, h: 3.0, angle: team.angle, sector, profile: 'pylon' },
    { ...radial(team.angle, 21.1), w: 4.1, d: 1.1, bottom: 1.26, h: 1.72, angle: team.angle, sector, duct: true },
    { ...radial(team.angle, 14.5, 6.7), w: 2.65, d: 5.1, h: 3.05, angle: team.angle - 0.36, sector, profile: 'baffle' },
    { ...radial(team.angle + Math.PI / 3, 28.4), w: 7.8, d: 3.35, h: 3.75, angle: team.angle + Math.PI / 3, sector: -1, profile: 'pylon' },
    { ...radial(team.angle + Math.PI / 3, 15.2), w: 3.6, d: 4.9, h: 2.2, angle: team.angle + Math.PI / 3, sector: -1, profile: 'baffle' },
    { ...radial(team.angle, 6.4), w: 2.5, d: 2.5, h: 1.56, angle: team.angle, sector, terrace: true },
  ]),
  { x: 0, z: 0, w: 5.8, d: 5.8, h: 3.6, sector: -1, relay: true },
]);

export const SIEGE_CORE_HP = 720;
const siegeNodes = [
  ...basinPoints.map((point, team) => ({ ...point, y: 0, radius: 3.95, home: team })),
  ...TEAMS.map((team, i) => ({ ...radial(team.angle - Math.PI / 3, 21.65), y: 0, label: 'DEF'[i], radius: 3.9, home: -1 })),
];
const siegeCores = basinBases.map((base, team) => ({ ...base, y: 1.15, radius: 1.08, height: 0.90, team }));
const siegeLinks = TEAMS.flatMap((_, i) => [[i, i + 3], [i + 3, (i + 1) % TEAMS.length]]);
export const BASIN_SIEGE = { nodes: siegeNodes, cores: siegeCores, links: siegeLinks };

const onIsland = ({ x, z, y = 0, ...props }) => ({ ...props, x, z, y: islandHeight(x, z) + y });
const islandBases = TEAMS.map(team => onIsland(radial(team.angle, 22.2)));
const islandPoints = TEAMS.map((team, i) => onIsland({ ...radial(team.angle, 10.7), label: 'ABC'[i] }));
const islandSpawns = TEAMS.map(team => [-3.0, -1.0, 1.0, 3.0].map(offset => onIsland(radial(team.angle, 25.7, offset))));
const islandRocks = TEAMS.flatMap((team, sector) => [
  { ...radial(team.angle, 15.1, -5.2), w: 3.2, d: 2.8, h: 1.36, angle: team.angle, sector, nature: 'rock' },
  { ...radial(team.angle, 15.5, 5.0), w: 3.3, d: 2.2, h: 1.73, angle: team.angle + 0.4, sector, nature: 'rock' },
  { ...radial(team.angle + Math.PI / 3, 19.5), w: 3.9, d: 2.9, h: 2.06, angle: team.angle, sector, nature: 'rock' },
  { ...radial(team.angle, 7.0, 4.3), w: 2.14, d: 2.10, h: 1.17, angle: team.angle - 0.2, sector, nature: 'rock' },
]);
const islandTreeSites = [
  [-19, -7.1], [-21, -16], [-12, -18], [5, -20], [15, -18], [19, -12], [25.8, -13], [24, 2],
  [21, 8], [14, 19], [6, 22], [-6, 23], [-12, 19], [-19, 10], [-23, 2],
  [-10, -6], [8, -8], [6, 14], [-5, 9],
];
const islandCover = packCover([
  ...islandRocks,
  ...islandTreeSites.map(([x, z], i) => ({ x, z, w: i % 4 ? 0.64 : 0.87, d: 0.59, h: 3.62, nature: 'tree' })),
  { x: 3.5, z: -1.5, w: 3.12, d: 2.18, h: 1.9, angle: 0.44, nature: 'rock' },
  { x: -25.6, z: -12.3, w: 2.5, d: 2.0, h: 3.05, angle: 0.26, nature: 'rock', strata: true },
  { x: 23.6, z: -12.3, w: 4.1, d: 2.4, h: 2.45, angle: 0.28, nature: 'rock', strata: true },
  { x: 8.1, z: -17.1, w: 3.8, d: 2.5, h: 2.4, angle: -0.38, nature: 'rock', strata: true },
  { x: -16.3, z: 15.4, w: 4.1, d: 2.6, h: 2.65, angle: 0.5, nature: 'rock', strata: true },
  { x: 22.2, z: -5.05, w: 2.75, d: 1.85, h: 2.6, angle: -0.30, nature: 'rock', strata: true },
].map(box => {
  const ground = islandHeight(box.x, box.z);
  return { ...box, ground, bottom: Math.max(0, ground - 1.3), h: ground + box.h };
}));
const islandPickups = [
  ...TEAMS.flatMap((team, i) => [
    { ...radial(team.angle, 15.1, -5.2), y: islandRocks[4 * i].h, type: ARMORY[i + 1] },
    { ...radial(team.angle, 18.8, 2.3), y: 0, type: 'med' },
    { ...radial(team.angle, 11.5, -4.15), y: 0, type: 'shield' },
  ]),
  { x: 8.1, z: -14.2, type: 'rift' },
  { x: -3.6, z: 3.25, y: 0, type: 'surge' },
].map(onIsland);

export const DEFAULT_ARENA = 'relay';
export const ARENAS = {
  relay: {
    id: 'relay', length: MAP_SIZE, label: 'Relay Station', description: 'Prasklý reaktor a zavěšené lávky.',
    cover: COVER, launchPads: LAUNCH_PADS, gantries: GANTRIES, pickups: PICKUPS, bases: BASES, points: POINTS, spawns: SPAWNS, turrets: [],
  },
  foundry: {
    id: 'foundry', length: MAP_SIZE, label: 'Vanta Foundry', description: 'Otevřená slévárna s dlouhými tavicími kanály a bočními mosty.',
    cover: foundryCover, launchPads: foundryPads, gantries: foundryGantries, pickups: foundryPickups,
    bases: BASES, points: POINTS, spawns: SPAWNS, turrets: foundryTurrets,
  },
  basin: {
    id: 'basin', length: BASIN_SIZE, label: 'Meridian Basin',
    description: 'Těžební údolí s vozidly, širokým okruhem a krytými cestami pro pěchotu.',
    cover: basinCover, launchPads: basinPads, gantries: basinGantries, pickups: basinPickups, bases: basinBases,
    points: basinPoints, spawns: basinSpawns, turrets: [], vehicles: basinVehicles, repairs: basinRepairs,
    footZones: basinFootZones, roadRadius: 41.8, siege: BASIN_SIEGE,
  },
  island: {
    id: 'island', length: 32, edgeRadius: ISLAND_RADIUS, terrain: islandHeight, label: 'Mechový ostrov',
    description: 'Zelená miniplaneta s horským hřebenem a dvěma vodopády.',
    cover: islandCover, launchPads: [], gantries: [], pickups: islandPickups, bases: islandBases,
    points: islandPoints, spawns: islandSpawns, turrets: [],
  },
};

function coversPoint(box, x, z, radius) {
  const dx = x - box.x;
  const dz = z - box.z;
  return Math.abs(dx * box.cos - dz * box.sin) < box.w / 2 + radius
    && Math.abs(dx * box.sin + dz * box.cos) < box.d / 2 + radius;
}

export function blocked(x, z, radius = BODY_RADIUS, y = 0, layout = ARENAS.relay, height = BODY_HEIGHT) {
  if (layout.edgeRadius ? x * x + z * z > (layout.edgeRadius - radius) ** 2
    : Math.abs(x) > layout.length - radius || Math.abs(z) > layout.length - radius) return true;
  return layout.cover.some(box => y < box.h - 0.0001 && y + height > box.bottom + 0.0001
    && coversPoint(box, x, z, radius));
}

export function floorBelow(x, z, y, radius = 0, layout = ARENAS.relay) {
  let floor = layout.terrain?.(x, z) ?? 0;
  for (const box of layout.cover) {
    if (y + 0.0001 >= box.h && box.h > floor && coversPoint(box, x, z, radius)) floor = box.h;
  }
  return floor;
}

export function canDodge(player) {
  return !player.crouch && (player.dodgeCooldown ?? 0) <= 0 && (player.grounded || player.airDodge);
}

export function movePlayer(player, input, dt = TICK, layout = ARENAS.relay) {
  let signal;
  player.dodgeCooldown = Math.max(0, (player.dodgeCooldown ?? 0) - dt);
  player.dodgeTime = Math.max(0, (player.dodgeTime ?? 0) - dt);
  player.padCooldown = Math.max(0, (player.padCooldown ?? 0) - dt);

  const wasGrounded = player.grounded;
  player.crouch = Boolean(input.crouch || player.crouch && blocked(player.x, player.z, BODY_RADIUS, player.y, layout));
  const height = hitHeight(player);
  const currentY = player.y ?? 0;
  const yaw = input.yaw;
  const cos = Math.cos(yaw);
  const sin = Math.sin(yaw);
  const length = Math.hypot(input.x, input.z);
  const magnitude = Math.max(1, length);
  const wishX = (input.x * cos - input.z * sin) / magnitude;
  const wishZ = (-input.x * sin - input.z * cos) / magnitude;
  const pad = wasGrounded && !player.crouch && player.padCooldown <= 0
    ? layout.launchPads.find(item => Math.abs(item.y - currentY) < 0.04 && distanceSquared(item, player) < item.radius ** 2)
    : null;

  if (pad) {
    player.vx = pad.vx;
    player.vz = pad.vz;
    player.vy = 12.35;
    player.grounded = false;
    player.airJump = player.airDodge = true;
    player.padCooldown = 0.45;
    signal = 'launch';
  } else if (input.jump && (wasGrounded || player.airJump)) {
    player.vy = (wasGrounded ? 7.65 : 8.75) * (player.crouch ? 0.69 : 1);
    player.grounded = false;
    player.airJump = wasGrounded;
    signal = wasGrounded ? 'jump' : 'double';
  }
  if (!pad && input.dodge && canDodge(player)) {
    const speed = 18.3 / (length || 1);
    player.vx = length ? wishX * speed * magnitude : -sin * speed;
    player.vz = length ? wishZ * speed * magnitude : -cos * speed;
    player.dodgeCooldown = DODGE_COOLDOWN;
    player.dodgeTime = DODGE_DURATION;
    if (player.grounded) player.vy = 3.25;
    else player.vy = Math.max(player.vy ?? 0, 1.9);
    player.grounded = false;
    player.airDodge = false;
    signal = 'dodge';
  }

  player.vx ??= 0;
  player.vz ??= 0;
  if (!pad && !player.dodgeTime) {
    const grounded = player.grounded;
    const shallow = layout.id === 'island' && grounded && islandFord(player.x, player.z);
    const speed = (player.crouch ? 4.25 : input.sprint ? SPRINT_SPEED : 7.55) * (shallow ? 0.72 : 1);
    const deltaX = wishX * speed - player.vx;
    const deltaZ = wishZ * speed - player.vz;
    const acceleration = grounded ? (length ? 70 : 64) : (length ? 20 : 4.2);
    const blend = Math.min(1, acceleration * dt / (Math.hypot(deltaX, deltaZ) || 1));
    player.vx += deltaX * blend;
    player.vz += deltaZ * blend;
  }

  player.vy = (player.vy ?? 0) - GRAVITY * dt;
  const fallSpeed = player.vy;
  let nextY = currentY + player.vy * dt;
  if (player.vy > 0) {
    const head = currentY + height;
    for (const box of layout.cover) {
      if (head <= box.bottom + 0.0001 && nextY + height > box.bottom
        && coversPoint(box, player.x, player.z, BODY_RADIUS)) {
        nextY = box.bottom - height;
        player.vy = 0;
      }
    }
  }

  const moveY = Math.max(currentY, nextY);
  const walkingTerrain = layout.terrain && player.grounded;
  const canMove = (x, z) => {
    const ground = layout.terrain?.(x, z) ?? 0;
    if (layout.terrain && !walkingTerrain && ground > moveY + 0.0001) return false;
    return !blocked(x, z, BODY_RADIUS, Math.max(moveY, ground), layout, height);
  };
  const x = player.x + player.vx * dt;
  const z = player.z + player.vz * dt;
  if (!canMove(x, player.z)) player.vx = 0;
  else player.x = x;
  if (!canMove(player.x, z)) player.vz = 0;
  else player.z = z;

  const floor = player.vy <= 0 ? floorBelow(player.x, player.z, currentY, BODY_RADIUS, layout) : -1;
  const followsSlope = walkingTerrain && floor >= currentY - 0.35;
  player.grounded = nextY <= floor || Boolean(followsSlope);
  player.y = player.grounded ? floor : nextY;
  if (player.grounded) {
    player.vy = 0;
    player.airJump = player.airDodge = true;
    if (!signal && !wasGrounded && fallSpeed < -3.65) signal = 'land';
  }
  return signal;
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

export function wallDistance(origin, direction, limit = 65, layout = ARENAS.relay) {
  let distance = limit;
  const horizontal = Math.hypot(direction.x, direction.z);
  const localOrigin = { x: 0, y: origin.y, z: 0 };
  const localDirection = { x: 0, y: direction.y, z: 0 };
  for (const box of layout.cover) {
    const x = origin.x - box.x;
    const z = origin.z - box.z;
    const radius = box.radius * horizontal;
    const along = -(x * direction.x + z * direction.z);
    if (along < -radius || along > distance * horizontal * horizontal + radius
      || Math.abs(x * direction.z - z * direction.x) > radius) continue;
    localOrigin.x = x * box.cos - z * box.sin;
    localOrigin.z = x * box.sin + z * box.cos;
    localDirection.x = direction.x * box.cos - direction.z * box.sin;
    localDirection.z = direction.x * box.sin + direction.z * box.cos;
    distance = Math.min(distance, rayBox(localOrigin, localDirection, box.min, box.max));
  }
  if (layout.edgeRadius && horizontal > 0.000001) {
    const projection = origin.x * direction.x + origin.z * direction.z;
    const rest = origin.x * origin.x + origin.z * origin.z - layout.edgeRadius ** 2;
    const horizontalSquare = horizontal * horizontal;
    const roots = projection * projection - horizontalSquare * rest;
    if (roots >= 0) {
      const exit = (-projection + Math.sqrt(roots)) / horizontalSquare;
      if (exit >= 0) distance = Math.min(distance, exit);
    }
  } else if (!layout.edgeRadius) {
    for (const axis of ['x', 'z']) {
      if (Math.abs(direction[axis]) > 0.000001) {
        const edge = layout.length * Math.sign(direction[axis]);
        distance = Math.min(distance, (edge - origin[axis]) / direction[axis]);
      }
    }
  }
  if (layout.terrain) return islandTerrainDistance(origin, direction, distance);
  if (direction.y < 0) distance = Math.min(distance, -origin.y / direction.y);
  return distance;
}

export function distanceSquared(a, b) {
  return (a.x - b.x) ** 2 + (a.z - b.z) ** 2;
}

export function roomCode(value) {
  return typeof value === 'string' && /^[a-z0-9-]{3,24}$/i.test(value) ? value.toUpperCase() : null;
}
