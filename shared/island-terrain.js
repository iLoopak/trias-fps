export const ISLAND_SCALE = 1.56;
export const ISLAND_RADIUS = 30.65 * ISLAND_SCALE;
const crossings = [-16, 0, 15];
export const ISLAND_CROSSINGS = crossings.map(x => x * ISLAND_SCALE);
export const ISLAND_CREEK_START = -19 * ISLAND_SCALE;
export const ISLAND_CREEK_END = -2.6 * ISLAND_SCALE;
export const ISLAND_TRAIL = [
  [-26.9, -9.5], [-23.8, -9.5], [-21.5, -13.1], [-17.6, -14.8], [-13.7, -13.6], [-10.5, -11.3],
  [-6.5, -10.5], [-2.8, -12.8], [4.9, -11.9], [8.1, -14.2], [12.9, -12.2],
  [14.1, -8.3], [16.5, -6.6], [20.6, -7.0], [23.9, -7.7], [25.7, -8.5],
].map(([x,z]) => [x * ISLAND_SCALE,z * ISLAND_SCALE]);
export const ISLAND_WATER_LEVEL = 0.035;
export const ISLAND_GRID = { start: -48.75, step: .65, count: 151 };

function riverAt(x) {
  return 1.35 + Math.sin(x * 0.15) * 4.72 + Math.sin(x * 0.33) * 0.48;
}

function creekAt(z) {
  return -6.7 + Math.sin((z + 19) * 0.32) * 1.24;
}

export const riverCenter = x => ISLAND_SCALE * riverAt(x / ISLAND_SCALE);
export const creekCenter = z => ISLAND_SCALE * creekAt(z / ISLAND_SCALE);

export function islandFord(x, z) {
  x /= ISLAND_SCALE;
  z /= ISLAND_SCALE;
  if (z >= -19 && z <= -2.2 && Math.abs(x - creekAt(z)) < 0.53) return true;
  return Math.abs(z - riverAt(x)) < 1.74
    && !crossings.some(crossing => Math.abs(x - crossing) <= 1.35);
}

function smooth(from, to, value) {
  const t = Math.max(0, Math.min(1, (value - from) / (to - from)));
  return t * t * (3 - 2 * t);
}

export function islandTrailDistance(x, z) {
  let distance = Infinity;
  for (let i = 1; i < ISLAND_TRAIL.length; i++) {
    const [ax, az] = ISLAND_TRAIL[i - 1];
    const [bx, bz] = ISLAND_TRAIL[i];
    const dx = bx - ax;
    const dz = bz - az;
    const t = Math.max(0, Math.min(1, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)));
    distance = Math.min(distance, Math.hypot(x - ax - t * dx, z - az - t * dz));
  }
  return distance;
}

const hills = [
  [-23.6, -9.6, 8.5, 7.6, 7.4], [-16.8, -14.7, 4.8, 8.4, 6.8],
  [-3.7, -15.7, 3.8, 11.6, 8.5], [9.7, -13.1, 5.2, 8.6, 8.3],
  [23.7, -8.1, 9.4, 8.4, 8.8], [-14.3, 10.8, 5.5, 8, 8.8],
  [11.3, 16.9, 5.7, 10.4, 8.8], [23.1, 11.7, 2.9, 7, 7.2],
  [-23.8, 11.6, 3.4, 5.8, 6.4], [2.4, 22.7, 4.1, 6.5, 6.1], [1.4,-23.9,2.1,7.1,5.6],
];

export function creekLevel(z) {
  return 1.66 * (1 - smooth(-19, -3.3, z / ISLAND_SCALE));
}

function landscape(x, z) {
  const creekWater = creekLevel(z);
  x /= ISLAND_SCALE;
  z /= ISLAND_SCALE;
  const radius = Math.hypot(x, z);
  const limit = ISLAND_RADIUS / ISLAND_SCALE;
  const crown = 1.85 * (1 - (Math.min(radius, limit) / limit) ** 2);
  let elevation = crown;
  for (const [hx, hz, peak, width, depth] of hills) {
    const distance = ((x - hx) / width) ** 2 + ((z - hz) / depth) ** 2;
    elevation += peak * Math.exp(-distance * 1.62);
  }
  elevation += 0.24 * Math.sin(x * 0.69 + z * 0.23) * Math.cos(z * 0.39 - x * 0.13);
  elevation += .17 * Math.sin(x * 1.18 + z * .61) * Math.cos(z * .91 + x * .25);
  elevation *= (1 - .24 * smooth(26, limit + .35, radius)) * 1.19;

  const inlet = smooth(-20.2, -18.8, z) * (1 - smooth(-3.7, -1.9, z));
  const creek = inlet * (1 - smooth(0.95, 4.7, Math.abs(x - creekAt(z))));
  elevation += (creekWater - elevation) * creek;

  const river = Math.abs(z - riverAt(x));
  const bridge = crossings.some(crossing => Math.abs(x - crossing) < 1.65);
  return Math.max(0, elevation * smooth(bridge ? 2.6 : 2.13, 6.5, river));
}

const { start, step, count } = ISLAND_GRID;
const heights = Float32Array.from({ length: count * count }, (_, index) =>
  landscape(start + index % count * step, start + Math.floor(index / count) * step));

export function islandGridHeight(x, z) {
  return heights[x + z * count];
}

export function islandHeight(x, z) {
  const gx = Math.max(0, Math.min(count - 1, (x - start) / step));
  const gz = Math.max(0, Math.min(count - 1, (z - start) / step));
  const ix = Math.min(count - 2, Math.floor(gx));
  const iz = Math.min(count - 2, Math.floor(gz));
  const fx = gx - ix;
  const fz = gz - iz;
  const a = islandGridHeight(ix, iz);
  const b = islandGridHeight(ix + 1, iz);
  const c = islandGridHeight(ix, iz + 1);
  const d = islandGridHeight(ix + 1, iz + 1);
  if (fx + fz <= 1) return a + (b - a) * fx + (c - a) * fz;
  return d + (c - d) * (1 - fx) + (b - d) * (1 - fz);
}

export function islandTerrainDistance(origin, direction, limit) {
  const { x, y, z } = origin;
  const height = islandHeight(x, z);
  if (y < height - 0.00001) return 0;
  if (Math.abs(direction.x) + Math.abs(direction.z) < 0.000001) {
    return direction.y < 0 ? Math.min(limit, (height - y) / direction.y) : limit;
  }

  // Each triangular face is a plane. Test the ray at the grid lines and at the diagonal of each cell.
  const nextBorder = (position, velocity) => {
    if (!velocity) return Infinity;
    const index = (position - start) / step;
    const next = velocity > 0 ? Math.floor(index + 1e-9) + 1 : Math.ceil(index - 1e-9) - 1;
    return (start + next * step - position) / velocity;
  };
  let borderX = nextBorder(x, direction.x);
  let borderZ = nextBorder(z, direction.z);
  const strideX = Math.abs(step / direction.x);
  const strideZ = Math.abs(step / direction.z);
  const diagonalSpeed = direction.x + direction.z;
  let previous = 0;
  let clearance = Math.max(0, y - height);
  while (previous < limit) {
    const border = Math.min(limit, borderX, borderZ);
    const middle = (previous + border) * 0.5;
    const cellX = Math.floor((x + direction.x * middle - start) / step);
    const cellZ = Math.floor((z + direction.z * middle - start) / step);
    const diagonal = (2 * start + (cellX + cellZ + 1) * step - x - z) / diagonalSpeed;
    const end = diagonal > previous + 1e-8 && diagonal < border - 1e-8 ? diagonal : border;
    const nextClearance = y + direction.y * end - islandHeight(x + direction.x * end, z + direction.z * end);
    if (nextClearance < 0 && clearance >= 0) {
      return previous + (end - previous) * clearance / (clearance - nextClearance);
    }
    clearance = nextClearance;
    previous = end;
    if (end === border) {
      if (borderX <= border + 1e-8) borderX += strideX;
      if (borderZ <= border + 1e-8) borderZ += strideZ;
    }
  }
  return limit;
}
