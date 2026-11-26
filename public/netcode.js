// Version 2 snapshots are self-contained. Positional rows keep field names off the wire.
export const NETWORK_VERSION = 2;
export const SNAPSHOT_INTERVAL = 1000 / 30;

const PLAYER_FIELDS = [
  'team', 'slot', 'character', 'hp', 'armor', 'surge', 'weapon', 'loadout', 'kills', 'deaths',
  'spawn', 'ack', 'turret', 'vehicle', 'grenades',
];
const PLAYER_MOTION = ['x', 'y', 'z', 'vx', 'vy', 'vz', 'yaw', 'pitch', 'dodgeCooldown', 'dodgeTime', 'padCooldown', 'respawn', 'reloadRemaining'];
const PLAYER_FLAGS = ['bot', 'grounded', 'airJump', 'airDodge', 'crouch', 'sprint', 'reload', 'shield'];
const VEHICLE_FIELDS = ['id', 'index', 'kind', 'team', 'driver', 'hp', 'boosting'];
const VEHICLE_MOTION = ['x', 'z', 'heading', 'yaw', 'pitch', 'speed', 'boost', 'respawn'];
const INPUT_FLAGS = ['crouch', 'sprint', 'brake', 'fire', 'reload', 'jump', 'dodge', 'alt', 'grenade', 'use'];

const round = value => Math.round(value * 1000) / 1000;

function flags(object, keys) {
  let bits = 0;
  for (let i = 0; i < keys.length; i++) if (object[keys[i]]) bits |= 1 << i;
  return bits;
}

function readFlags(object, bits, keys) {
  for (let i = 0; i < keys.length; i++) object[keys[i]] = Boolean(bits & (1 << i));
}

export function packInput(input) {
  return [input.seq, input.x, input.z, round(input.yaw), round(input.pitch), flags(input, INPUT_FLAGS), input.weapon];
}

export function unpackInput(row) {
  if (!Array.isArray(row) || row.length !== 7 || !Number.isInteger(row[5]) || row[5] < 0 || row[5] >= (1 << INPUT_FLAGS.length)) return null;
  const [seq, x, z, yaw, pitch, bits, weapon] = row;
  const input = { seq, x, z, yaw, pitch, weapon };
  readFlags(input, bits, INPUT_FLAGS);
  return input;
}

function unpackRow(object, row, keys, offset) {
  for (let i = 0; i < keys.length; i++) object[keys[i]] = row[offset + i];
}

export function packSnapshot(state, frame, time) {
  const { players, vehicles, projectiles, ...world } = state;
  return {
    ...world, tick: frame, at: Math.round(time * 1000), v: NETWORK_VERSION,
    players: players.map(p => [
      p.id, p.name, flags(p, PLAYER_FLAGS), ...PLAYER_FIELDS.map(key => p[key]), ...PLAYER_MOTION.map(key => round(p[key])),
      ...(p.hordeType ? [p.hordeType] : []),
    ]),
    vehicles: vehicles.map(hull => [
      ...VEHICLE_FIELDS.map(key => hull[key]), ...VEHICLE_MOTION.map(key => round(hull[key])),
    ]),
    projectiles: projectiles.map(shot => [shot.id, shot.weapon, round(shot.x), round(shot.y), round(shot.z), shot.fuse == null ? null : round(shot.fuse)]),
  };
}

export function unpackSnapshot(packet) {
  if (packet.v !== NETWORK_VERSION) return packet;
  const players = packet.players.map(row => {
    const player = { id: row[0], name: row[1] };
    readFlags(player, row[2], PLAYER_FLAGS);
    unpackRow(player, row, PLAYER_FIELDS, 3);
    unpackRow(player, row, PLAYER_MOTION, 3 + PLAYER_FIELDS.length);
    return player;
  });
  const vehicles = packet.vehicles.map(row => {
    const hull = {};
    unpackRow(hull, row, VEHICLE_FIELDS, 0);
    unpackRow(hull, row, VEHICLE_MOTION, VEHICLE_FIELDS.length);
    return hull;
  });
  const projectiles = packet.projectiles.map(([id, weapon, x, y, z, fuse]) =>
    ({ id, weapon, x, y, z, fuse: fuse ?? undefined }));
  return { ...packet, players, vehicles, projectiles };
}
