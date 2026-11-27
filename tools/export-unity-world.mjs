import { readFileSync, writeFileSync } from 'node:fs';
import { ARENAS, WEAPONS } from '../public/rules.js';
import {
  ISLAND_CREEK_END, ISLAND_CREEK_START, ISLAND_CROSSINGS, ISLAND_GRID,
  ISLAND_TRAIL, creekCenter, creekLevel, islandGridHeight, riverCenter,
} from '../public/island-terrain.js';

const target = new URL('../unity-vfx-preview/Assets/Resources/TRIAS/World.json',import.meta.url);
const existing = readFileSync(target,'utf8');
const catalog = JSON.parse(existing);
const clean = value => JSON.parse(JSON.stringify(value,(key,v) => typeof v === 'number' ? +v.toFixed(5) : v));
const cover = ({min, max, cos, sin, radius, ...shape}) => shape;
const coordinates = ['length','label','description','edgeRadius','islandScale','cover','launchPads','pickups','bases','points',
  'turrets','vehicles','repairs','roadRadius','siege'];

for (const map of catalog.maps) {
  const arena = ARENAS[map.id];
  for (const key of coordinates)
    if (arena[key] !== undefined) map[key] = clean(key === 'cover' ? arena.cover.map(cover) : arena[key]);
  if (map.id !== 'island') continue;
  const {start,step,count} = ISLAND_GRID;
  map.terrain = {start,step,count,heights: Array.from({length:count*count},(_,i) =>
    +islandGridHeight(i%count,Math.floor(i/count)).toFixed(5))};
  map.crossings = ISLAND_CROSSINGS.map(x => ({x,z:riverCenter(x)}));
  function riverMouth(sign) {
    let low = 0, high = map.edgeRadius;
    for (let i = 0; i < 28; i++) {
      const x = (low + high) / 2;
      if (Math.hypot(x, riverCenter(sign * x)) < map.edgeRadius) low = x;
      else high = x;
    }
    return sign * low;
  }
  const west = riverMouth(-1), east = riverMouth(1);
  const stations = Array.from({length:145},(_,i) => west + (east - west) * i / 144);
  stations.push(...ISLAND_CROSSINGS);
  map.river = clean([...new Set(stations)].sort((a,b)=>a-b).map(x => ({x,z:riverCenter(x)})));
  map.creek = clean(Array.from({length:43},(_,i) => {
    const z = ISLAND_CREEK_START + (ISLAND_CREEK_END-ISLAND_CREEK_START)*i/42;
    return {x:creekCenter(z),z,y:creekLevel(z)};
  }));
  map.trail = clean(ISLAND_TRAIL.map(([x,z])=>({x,z})));
}
catalog.weapons = catalog.weapons.map(choice => {
  const { magazine, reload, alt, label, color, reserve, ...shot } = WEAPONS[choice.id];
  return { id: choice.id, label, color, magazine, reload, shot, alt };
});
const generated = JSON.stringify(catalog);
if (process.argv.includes('--check')) {
  if (existing !== generated) {
    console.error('World.json differs from the authoritative arenas. Run node tools/export-unity-world.mjs.');
    process.exitCode = 1;
  }
} else writeFileSync(target,generated);
