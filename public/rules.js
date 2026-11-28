import { ISLAND_RADIUS, ISLAND_SCALE, islandFord, islandHeight, islandTerrainDistance, riverCenter } from './island-terrain.js';
export { ISLAND_CROSSINGS, creekCenter, islandFord, riverCenter } from './island-terrain.js';

export const TICK = 1 / 30;
export const TEAM_SIZE = 4;
export const CAPACITY = 12;
export const MAP_SIZE = 34;
export const BODY_RADIUS = 0.43;
export const EYE = 1.58;
export const PILOT_HP = 210;
export const HEADSHOT_MULTIPLIER = 1.5;
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
  pulse: { label: 'Pulse', color: '#89fff0', magazine: 24, reserve: Infinity, interval: 0.145, reload: 1.3, range: 60, damage: 23, pattern: [0],
    alt: { label: 'Trojimpulz', cost: 3, interval: 0.38, range: 48, damage: 17, pattern: [0, -0.015, 0.015] } },
  scatter: { label: 'Scatter', color: '#ffad72', magazine: 6, reserve: 24, interval: 0.66, reload: 1.55, range: 22, damage: 14,
    pattern: [0, -0.06, 0.06, -0.09, 0.09, -0.025, 0.025],
    alt: { label: 'Slug', cost: 2, interval: 0.91, range: 47, damage: 56, pattern: [0] } },
  lance: { label: 'Lance', color: '#d8b4ff', magazine: 4, reserve: 12, interval: 0.85, reload: 1.7, range: 85, damage: 68, pattern: [0],
    alt: { label: 'Průstřel', cost: 2, interval: 1.05, range: 75, damage: 70, pattern: [0], penetration: 0.54 } },
  arc: { label: 'Arc', color: '#ffed87', magazine: 36, reserve: 108, interval: 0.08, reload: 1.6, range: 43, damage: 13, pattern: [0],
    alt: { label: 'Obloukový vějíř', cost: 8, interval: 0.53, range: 25, damage: 15,
      pattern: [0, -0.034, 0.034, -0.069, 0.069, -0.108, 0.108], horizontal: true } },
  rift: { label: 'Rift', color: '#ff79b9', magazine: 4, reserve: 12, interval: 0.72, reload: 1.62, range: 55,
    damage: 72, projectile: { speed: 22, radius: 3.2, splash: 54 },
    alt: { label: 'Těžká koule', cost: 2, interval: 1.05, range: 39, damage: 78,
      projectile: { speed: 14, radius: 4.65, splash: 68 } } },
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
  bulwark: { label: 'Bašta', weapon: 'scatter', description: 'Brokovnice pro boj zblízka.' },
  wraith: { label: 'Přízrak', weapon: 'lance', description: 'Přesná puška pro boj na dálku.' },
  kestrel: { label: 'Poštolka', weapon: 'arc', description: 'Rychlopalná zbraň pro krytí postupu.' },
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
  ...TEAMS.map((team,sector) => ({
    ...radial(team.angle+Math.PI/3,29.4,-6),w: 3.2,d: 2.2,h: 1.65,
    angle: team.angle+Math.PI/3,sector,profile:'airlock',
  })),
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

const foundryShell = [
  ...[-1,1].flatMap(side => [
    routeBox(side*21.7,0,32.6,76,-3.95,0,'bank'),
    routeBox(0,side*34.1,10.8,7.8,-3.95,0,'bank'),
  ]),
  routeBox(0,0,10.8,61,-4.1,-3.3,'channel'),
];
const foundryCrossings = [-10,10].map(z => routeBox(0,z,30.4,5.2,2.55,3.3,'bridge'));
const foundryRoutes = [-1,1].flatMap(side => [
  ...stairFlight({x:0,z:side*21.55},side>0 ? 0 : Math.PI,4.5,-3.3,0),
  ...foundryCrossings.flatMap(({z}) => stairFlight({x:side*23.65,z},side>0 ? -Math.PI/2 : Math.PI/2,4.65,0,3.3)),
  ...foundryCrossings.flatMap(({z}) => [-1,1].map(edge =>
    routeBox(side*7.65,z+edge*2.51,14.3,.18,3.3,4.15,'rail'))),
]);
const foundryPickups = [
  { x:0, y:3.3, z:-10, type:'lance' }, { x:0, y:3.3, z:10, type:'rift' },
  { x:-3.2, y:-3.3, z:-.7, type:'surge' },
  ...[-1,1].flatMap(side => [
    { x:side*17.2,y:0,z:0,type:'med' }, { x:side*24.5,y:0,z:0,type:'shield' },
    { x:side*13,y:0,z:side*21,type:'scatter' },
  ]),
];
const foundryCover = packCover([
  ...foundryShell, ...foundryCrossings, ...foundryRoutes,
  ...utilityBuilding(-27.6,19.5,0,0,'quench-wall'),
  ...utilityBuilding(27.6,-19.5,0,Math.PI,'quench-wall'),
  ...TEAMS.flatMap((team, sector) => [
    { ...radial(team.angle, 20.1, 7.4), w: 2.65, d: 4.4, h: 3.3, angle: team.angle, sector, profile: 'pylon' },
    { ...radial(team.angle, 16.8, 11.35), w: 3.4, d: 3.7, bottom: 1.27, h: 1.66, angle: team.angle, sector, duct: true },
    { ...radial(team.angle, 14.5, 4.9), w: 1.05, d: 5.5, h: 2.4, angle: team.angle + 0.20, sector, profile: 'baffle' },
    { ...radial(team.angle + Math.PI / 3, 26, 7.2), w: 5.3, d: 2.75, h: 3.4, angle: team.angle + Math.PI / 3, sector: -1, profile: 'pylon' },
    { ...radial(team.angle + Math.PI / 3, 12.8, 6.6), w: 1.85, d: 4.6, h: 1.85, angle: team.angle + Math.PI / 3, sector: -1, profile: 'baffle' },
    { ...radial(team.angle + Math.PI / 3, 4.1), w: 2.15, d: 2.3, h: 1.35, angle: team.angle + Math.PI / 3, sector, terrace: true },
  ]),
  ...TEAMS.flatMap((team,sector) => [
    {...radial(team.angle+Math.PI/3,31,-4.1),w: 4.1,d: 4.3,h: 3.4,angle: team.angle+Math.PI/3,sector,profile:'kiln'},
    {...radial(team.angle+Math.PI/3,33,5.5),w: 3.4,d: 2.35,h: 1.8,angle: team.angle+Math.PI/3,sector,profile:'slag'},
  ]),
  ...[-1,1].map(side => routeBox(side*5.76,0,1.2,5.8,0,6.45,'support')),
  ...[-1,1].map(side => routeBox(side*3.3,side*5.7,1.65,2.25,-3.3,-1.95,'baffle')),
]);

const foundryBases = TEAMS.map(team => radial(team.angle, 30.84));
const foundrySpawns = TEAMS.map(team =>
  [-3.6,-1.2,1.2,3.6].map(offset => radial(team.angle,31.8,offset))
);
const foundryTurrets = TEAMS.map((team, sector) => {
  const position = radial(team.angle, 17.25, 8.4);
  return { ...position, y: 0, yaw: Math.atan2(position.x, position.z), sector };
});

const FOUNDRY_SIZE = 38;
const BASIN_SIZE = 64;
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
  { ...radial(team.angle, 47.8, -8.2), heading: team.angle + Math.PI / 2, kind: 'skimmer', team: sector },
  { ...radial(team.angle, 47.7, 9.2), heading: team.angle - Math.PI / 2, kind: 'bastion', team: sector },
]);
const basinRepairs = TEAMS.map(team => ({ ...radial(team.angle + Math.PI / 3, 42.5), radius: 3.65 }));
const basinFootZones = [...basinPoints.map(p => ({ ...p, radius: 3.95 })), ...basinBases.map(p => ({ ...p, radius: 3.35 }))];
// Three conveyor galleries cross the excavation without narrowing the vehicle loop.
const basinGalleries = TEAMS.map(team => deckSection(team.angle,24.725,16.4,5.6,10.01,2.67,3.3,'catwalk'));
const basinRoutes = TEAMS.flatMap(team => [
  ...stairFlight(radial(team.angle,38.1,16.4),team.angle+Math.PI,5.65,0,3.3),
  ...stairFlight(radial(team.angle,11.4,16.4),team.angle,5.65,0,3.3),
  ...[-1,1].map(edge => deckSection(team.angle,24.725,16.4+edge*2.7,.2,8.0,3.3,4.19,'rail')),
  ...[20.3,29.1].map(along => deckSection(team.angle,along,18.6,1.0,1.5,0,2.67,'support')),
]);
basinPickups.push(...basinGalleries.map((gallery,i) => ({x:gallery.x,z:gallery.z,y:gallery.h,type:i===0?'surge':'shield'})));
const basinCover = packCover([
  ...basinGantries, ...basinGalleries, ...basinRoutes,
  ...TEAMS.flatMap(team=> {
    const at = radial(team.angle,55,-12);
    return utilityBuilding(at.x,at.z,0,team.angle,'survey-wall');
  }),
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
  ...TEAMS.flatMap((team,sector) => [
    {...radial(team.angle+Math.PI/3,55.4,-7.2),w:5.7,d:3.3,h:3.75,angle:team.angle+.52,sector,profile:'ore'},
    {...radial(team.angle+Math.PI/3,55.2,6.6),w:3.2,d:5.1,h:2.1,angle:team.angle+1.31,sector,profile:'ore'},
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

const onIsland = ({ x: localX, z: localZ, y = 0, ...props }) => {
  const x = localX * ISLAND_SCALE, z = localZ * ISLAND_SCALE;
  return { ...props, x, z, y: islandHeight(x, z) + y };
};
// Visible top of each Kordilera rock form, sampled from the Unity kit meshes (TRIAS_ART_ROCKS in the
// art-review probe): 16 rows across the rock's depth, 16 columns across its width, each digit the stone's
// height in tenths of its span above the buried foot ('-' = no stone). Rock collision is built from it as
// stepped boxes that never stand above the visible stone, so pilots do not stand on air over a receding
// crown; the full rock box stays in `scenery` for presentation only.
const ROCK_TOPS = {
  RockShelf: '----2222222222--/--333333444432--/--3445566655422-/--3456777776532-/-235678888765321/-345688887765421/-345789887765421/-345799988765422/3455799988775432/3455799988766432/3455899988766432/3456899988866432/3446899888876532/-245788888765530/--2355578875532-/-----111334442--',
  RockSplit: '-0001122222220--/122212233333332-/2222223444455430/2222224556665543/2222234666666654/2222235666777776/2222245667899887/1222345678889987/0112345678889987/-012446678889987/--22446778899986/--2344567889998-/--2345567899996-/--3445578899993-/--344567899996--/---35555544444--',
  RockErased: '--346666332-----/-235788775432---/-2468888765432--/-3469888865433--/046899888655431-/1468998887654320/1468998888654320/1468998888755420/2468998888765420/2569998888865430/3579998888876530/3589988888865430/2689998888655420/169999988554431-/-7887754444331--/-544433322000---',
  RockOutcrop: '---12111----00--/-11234322111222-/1112445544353332/1123455676655332/1123456677665432/1133556777776532/1233566778876432/1234567788865332/1244567789855321/0244567789765310/-34456778986531-/-34557788986532-/-24567789987532-/--455778998652--/--245777663221--/---444333220----',
};

// Same pick as the Unity client's EnvironmentVariant, so the collider and the drawn form agree.
function rockStone(box) {
  const x = Math.round(box.x * 17), z = Math.round(box.z * 23);
  return box.strata ? 'RockOutcrop' : ['RockShelf', 'RockSplit', 'RockErased'][Math.abs((x ^ z) % 12) % 3];
}

function rockSteps(rock) {
  const rows = ROCK_TOPS[rock.stone].split('/').map(row => [...row].map(c => c === '-' ? -1 : +c));
  const n = rows.length, foot = Math.max(0, rock.ground - .6), span = rock.h - foot;
  // Largest rectangle of cells whose stone reaches `need` tenths (a 5 % span tolerance).
  const largest = need => {
    let best = null;
    for (let j0 = 0; j0 < n; j0++) for (let i0 = 0; i0 < n; i0++)
      for (let j1 = j0; j1 < n && rows[j1][i0] >= need; j1++)
        for (let i1 = i0; i1 < n; i1++) {
          let ok = true;
          for (let j = j0; j <= j1 && ok; j++) ok = rows[j][i1] >= need;
          if (!ok) break;
          const area = (j1 - j0 + 1) * (i1 - i0 + 1);
          if (!best || area > best.area) best = { i0, i1, j0, j1, area };
        }
    return best;
  };
  const needAt = height => Math.max(1, Math.ceil(((height - foot) / span - .05) * 10));
  let top = 9;
  while (top > 1 && (largest(top)?.area ?? 0) < 8) top--;
  const base = Math.min(needAt(rock.ground + .5), top), middle = Math.round((base + top) / 2);
  return [...new Set([base, middle, top])].map(need => {
    const r = largest(need);
    const u = ((r.i0 + r.i1 + 1) / 2 / n - .5) * rock.w, v = ((r.j0 + r.j1 + 1) / 2 / n - .5) * rock.d;
    return {
      x: rock.x + rock.cos * u + rock.sin * v, z: rock.z - rock.sin * u + rock.cos * v, angle: rock.angle,
      w: (r.i1 - r.i0 + 1) / n * rock.w, d: (r.j1 - r.j0 + 1) / n * rock.d, bottom: rock.bottom, ground: rock.ground,
      h: Math.min(rock.h, foot + (need / 10 + .05) * span), sector: rock.sector, collider: true,
    };
  });
}

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
  [-24,7], [-19.5,11.6], [-17.2,13.3], [-15.4,16.9], [-9.8,21.5], [-8.7,25.9],
  [-3.6,21], [3.7,23.2], [9.5,24.3], [12.4,20.8], [18.3,17.4], [19.3,12.4],
  [17.6,-17.4],[5.6,-25.2],[-5.7,-23.9],[-11.4,-24.4],
];
const islandSites = [
  ...islandRocks,
  ...islandTreeSites.map(([x, z], i) => ({ x, z, w: i % 4 ? 0.64 : 0.87, d: 0.59, h: 3.62, nature: 'tree' })),
  { x: 3.5, z: -1.5, w: 3.12, d: 2.18, h: 1.9, angle: 0.44, nature: 'rock' },
  { x: -25.6, z: -12.3, w: 2.5, d: 2.0, h: 3.05, angle: 0.26, nature: 'rock', strata: true },
  { x: 23.6, z: -12.3, w: 4.1, d: 2.4, h: 2.45, angle: 0.28, nature: 'rock', strata: true },
  { x: 8.1, z: -17.1, w: 3.8, d: 2.5, h: 2.4, angle: -0.38, nature: 'rock', strata: true },
  { x: -16.3, z: 15.4, w: 4.1, d: 2.6, h: 2.65, angle: 0.5, nature: 'rock', strata: true },
  { x: 22.2, z: -5.05, w: 2.75, d: 1.85, h: 2.6, angle: -0.30, nature: 'rock', strata: true },
].map(box => {
  const x = box.x * ISLAND_SCALE, z = box.z * ISLAND_SCALE;
  const ground = islandHeight(x, z);
  const spread = box.nature === 'tree' ? 1 : 1.15;
  const site = { ...box, x, z, w: box.w * spread, d: box.d * spread, ground, bottom: Math.max(0, ground - 1.3), h: ground + box.h };
  return site.nature === 'rock' ? { ...site, stone: rockStone(site) } : site;
});
const islandScenery = packCover(islandSites.filter(site => site.nature === 'rock'));
const islandRockSteps = islandScenery.map(rock => packCover(rockSteps(rock)));
const islandCover = packCover([
  ...islandSites.filter(site => site.nature !== 'rock'),
  ...islandRockSteps.flat(),
]);
// Each team's weapon rests on the centre of its rock's highest step that stands clear of the slope and
// of nearby structures: the stone's real crown, which on a split rock lies off the rock's centre.
const islandRockPerch = (index, layout) => {
  const steps = [...islandRockSteps[index]].sort((a, b) => b.h - a.h);
  for (const step of steps) {
    const spots = [[0, 0], [-.3, 0], [.3, 0], [0, -.3], [0, .3], [-.3, -.3], [.3, .3], [-.3, .3], [.3, -.3]].map(([u, v]) => ({
      x: step.x + step.cos * u * step.w + step.sin * v * step.d, z: step.z - step.sin * u * step.w + step.cos * v * step.d,
    }));
    const spot = spots.find(({ x, z }) => islandHeight(x, z) < step.h - .05 && !blocked(x, z, BODY_RADIUS, step.h, layout));
    if (spot) return { ...spot, y: step.h };
  }
  throw new Error(`No clear perch on Kordilera rock ${index}`);
};
const islandPickups = layout => [
  ...TEAMS.flatMap((team, i) => [
    { ...islandRockPerch(4 * i, layout), type: ARMORY[i + 1] },
    onIsland({ ...radial(team.angle, 18.8, 2.3), y: 0, type: 'med' }),
    onIsland({ ...radial(team.angle, 11.5, -4.15), y: 0, type: 'shield' }),
  ]),
  ...[
    { x: 8.1, z: -14.2, type: 'rift' },
    { x: -3.6, z: 3.25, y: 0, type: 'surge' },
    { x: 20.2, z: 16.2, type: 'shield' }, { x: -18.3, z: 14.8, type: 'med' },
    { x: -9.1, z: -16.3, type: 'lance' },
  ].map(onIsland),
];

export const RELAY_SERVICE = {
  id: 'relay', length: MAP_SIZE, label: 'Relay Station', description: 'Servisní paluba reaktoru.',
  cover: COVER, launchPads: LAUNCH_PADS, gantries: GANTRIES, pickups: PICKUPS, bases: BASES, points: POINTS, spawns: SPAWNS, turrets: [],
};

const RELAY_MID = 3.3;
const RELAY_HIGH = 6.6;
const relayPlatforms = [];
const relaySteps = [];
function deckSection(angle, along, lateral, w, d, bottom, h, structure) {
  return { ...radial(angle, along, lateral), angle, w, d, bottom, h, structure, sector: -1 };
}
function routeBox(x, z, w, d, bottom, h, structure, angle = 0) {
  return { x, z, w, d, bottom, h, angle, structure, sector: -1 };
}
function utilityBuilding(x, z, y, angle, style, footing = 0) {
  const section = (lx, lz, w, d, bottom, top, part) => routeBox(
    x + Math.cos(angle)*lx + Math.sin(angle)*lz, z - Math.sin(angle)*lx + Math.cos(angle)*lz,
    w, d, y+bottom, y+top, part, angle);
  return [
    section(0,0,9.3,6.6,2.89,3.35,'roof'),
    section(-4.4,0,.54,6.5,-footing,2.89,style),
    section(4.4,1.73,.54,3.05,-footing,2.89,style),
    section(0,3.0,8.3,.52,-footing,2.89,style),
    section(-2.87,-3,2.55,.52,-footing,2.89,style),
    section(2.87,-3,2.55,.52,-footing,2.89,style),
  ];
}
function stairFlight(start, angle, width, bottom, top) {
  return Array.from({ length:10 }, (_,i) => routeBox(
    start.x + Math.sin(angle)*i*.94, start.z + Math.cos(angle)*i*.94,
    width, .96, bottom, bottom + (top-bottom)*(i+1)/10, 'stair', angle));
}

for (let quarter = 0; quarter < 4; quarter++) {
  const angle = quarter * Math.PI / 2;
  // Four slabs leave a square shaft open from the roof down.
  relayPlatforms.push(deckSection(angle, 23.5, 0, quarter % 2 ? 26 : 68, 21, -.84, 0, 'service'));
  relayPlatforms.push(deckSection(angle, 17, 0, quarter % 2 ? 27 : 41, 7, 2.67, RELAY_MID, 'gallery'));
  relayPlatforms.push(deckSection(angle, 11.93, 0, 5.6, 16.9, 5.88, RELAY_HIGH, 'bridge'));
  relayPlatforms.push(deckSection(angle, 2.7, 0, quarter % 2 ? 4.2 : 7, 1.6, 5.88, RELAY_HIGH, 'hub'));
  for (const side of [-1, 1]) {
    relayPlatforms.push(deckSection(angle, 16.3, side * 10.4, 2.4, 1.05, RELAY_MID, 4.45, 'baffle'));
    relayPlatforms.push(deckSection(angle, 31.25, side * 11.5, 2.64, 3.25, 0, 2.65, 'pylon'));
  }
  relayPlatforms.push(deckSection(angle, 6.75, -2.70, .20, 6.3, RELAY_HIGH, 7.47, 'rail'));
  relayPlatforms.push(deckSection(angle, 17.20, -2.70, .20, 6.2, RELAY_HIGH, 7.47, 'rail'));
  relayPlatforms.push(deckSection(angle, 15.17, 2.70, .20, 10.1, RELAY_HIGH, 7.47, 'rail'));
  relayPlatforms.push(deckSection(angle, 5.49, 2.70, .20, 3.95, RELAY_HIGH, 7.47, 'rail'));
  relayPlatforms.push(deckSection(angle, 8.45, 4.91, 4.64, 1.4, 6.1, RELAY_HIGH, 'arrival'));
  // Both flights gain 3.3 m without requiring jump. The second starts near the inner gallery rim.
  for (let tread = 0; tread < 10; tread++) {
    const rise = (tread+1) * .33;
    relaySteps.push(deckSection(angle, 29.55 - tread * .94, 0, 4.55, .95, 0, rise, 'stair'));
    relaySteps.push(deckSection(angle, 17.8 - tread * .94, 5.2, 4.55, .95, RELAY_MID, RELAY_MID + rise, 'stair'));
  }
}
relayPlatforms.push({ x:0,z:0,angle:0,w:3.7,d:3.7,bottom:-16,h:14.8,structure:'spine',sector:-1 });

const relayShaft = {
  ...RELAY_SERVICE, revision: 'relay-shaft-3', fallY: -14,
  description: 'Servisní paluba, přestupní galerie a čtyři lávky nad šachtou reaktoru.',
  cover: packCover([
    ...relayPlatforms, ...relaySteps,
    ...utilityBuilding(-23,-24,0,.5,'relay-wall'),
    ...utilityBuilding(26,28,0,Math.PI+.5,'relay-wall'),
  ]),
  gantries: [],
  bases:TEAMS.map(team=>radial(team.angle,23.5,4.9)),
  spawns: TEAMS.map(team => [-7.2,-4.7,4.7,7.2].map(offset=>radial(team.angle,29.2,offset))),
  launchPads: Array.from({ length:4 }, (_,i) => {
    const angle=i*Math.PI/2;
    return { ...radial(angle,15.9,-4.7), y:RELAY_MID, angle, sector:-1, radius:.86,
      vx:-Math.sin(angle)*5.9+Math.cos(angle)*3.5, vz:-Math.cos(angle)*5.9-Math.sin(angle)*3.5 };
  }),
  points: [
    { x:-8, z:-17, y:RELAY_MID, label:'A' },
    { x:17, z:8, y:RELAY_MID, label:'B' },
    { x:-17, z:8, y:RELAY_MID, label:'C' },
  ],
  pickups: [
    ...TEAMS.flatMap((team,i)=>[
      { ...radial(team.angle,25.5,5.2), y:0, type:'med' },
      { ...radial(i*Math.PI/2,17,-8.7), y:RELAY_MID, type:ARMORY[i+1] },
    ]),
    { x:0,z:8.5,y:RELAY_HIGH,type:'rift' },
    { x:0,z:-8.5,y:RELAY_HIGH,type:'surge' },
    { x:8.6,z:0,y:RELAY_HIGH,type:'shield' },
    { x:-8.6,z:0,y:RELAY_HIGH,type:'shield' },
  ],
};

// Dry cliff-to-cliff path, separate from the low fords. The upper end runs into the hillside.
const cordilleraBridgeX = 14;
const cordilleraRiverZ = riverCenter(cordilleraBridgeX);
const cordilleraRoutes = [
  ...utilityBuilding(18,-5,2.23,Math.PI,'refuge-wall',.48),
  ...utilityBuilding(-21,11,5.75,-.4,'refuge-wall',1.9),
  routeBox(cordilleraBridgeX,cordilleraRiverZ,4.8,17,2.5,3.15,'timber'),
  ...stairFlight({ x:cordilleraBridgeX,z:cordilleraRiverZ-17 },0,4.4,0,3.15),
  ...[-1,1].map(side => routeBox(cordilleraBridgeX+side*2.32,cordilleraRiverZ,.16,16,3.15,4.03,'rail')),
  ...[-1,1].map(side => routeBox(cordilleraBridgeX,cordilleraRiverZ+side*6.7,4.35,2.1,.35,2.5,'support')),
];

export const RELAY_HORDE = {
  ...relayShaft, revision:'relay-defense-2',
  description:'Obrana převodního jádra na jižní galerii se dvěma schodišti.',
  cover:[...relayShaft.cover, ...packCover(stairFlight({x:11.2,z:-29.55},0,4.25,0,RELAY_MID))],
  bases:[{x:6.6,y:RELAY_MID,z:-17.7}, ...relayShaft.bases.slice(1)],
  spawns:[[-10,-7.6,4,9].map(x=>({x,y:RELAY_MID,z:-18.9})), ...relayShaft.spawns.slice(1)],
  construction:[
    {x:-.1,y:RELAY_MID,z:-18.9,angle:0}, {x:11.2,y:RELAY_MID,z:-18.9,angle:0},
    {x:7.6,y:RELAY_MID,z:-14.8,angle:Math.PI/2},
    {x:-7.7,y:RELAY_MID,z:-15.3,angle:Math.PI/2},
    {x:14.8,y:RELAY_MID,z:-18,angle:Math.PI/2},
  ],
};

export const LAYOUT_SET = 'linked-decks-2';
export const DEFAULT_ARENA = 'relay';
export const ARENAS = {
  relay: relayShaft,
  foundry: {
    id: 'foundry', length: FOUNDRY_SIZE, revision:'foundry-channel-2', fallY:-13, label: 'Vanta Foundry', description: 'Dvě výrobní paluby, spodní chladicí kanál a mosty nad licí linkou.',
    hazard: { x:0, z:0, y:-3.3, w:3.3, d:33, damage:22, interval:.25 },
    cover: foundryCover, launchPads: [], gantries: [], pickups: foundryPickups,
    bases: foundryBases, points: [
      {x:0,z:-10,y:3.3,label:"A"}, {x:3.1,z:.7,y:-3.3,label:"B"}, {x:0,z:10,y:3.3,label:"C"},
    ], spawns: foundrySpawns, turrets: foundryTurrets,
  },
  basin: {
    id: 'basin', length: BASIN_SIZE, revision:'basin-conveyor-2', walkways:true, label: 'Meridian Basin',
    description: 'Těžební údolí s okruhem pro vozidla a zvýšenými dopravníky pro pěchotu.',
    cover: basinCover, launchPads: basinPads, gantries: basinGantries, pickups: basinPickups, bases: basinBases,
    points: basinPoints, spawns: basinSpawns, turrets: [], vehicles: basinVehicles, repairs: basinRepairs,
    footZones: basinFootZones, roadRadius: 47.8, siege: BASIN_SIEGE,
  },
  island: {
    id: 'island', length: 50, revision:'kordilera-span-2', walkways:true, edgeRadius: ISLAND_RADIUS, islandScale: ISLAND_SCALE, terrain: islandHeight, label: 'Kordilera',
    description: 'Horský říční zářez s brody, vodopády a lávkou zavázanou do skalních svahů.',
    cover: [...islandCover, ...packCover(cordilleraRoutes)], scenery: islandScenery, launchPads: [], gantries: [],
    pickups: [...islandPickups({ edgeRadius: ISLAND_RADIUS, cover: [...islandCover, ...packCover(cordilleraRoutes)] }),{x:cordilleraBridgeX,y:3.15,z:cordilleraRiverZ,type:"shield"}], bases: islandBases,
    points: islandPoints, spawns: islandSpawns, turrets: [],
  },
};

export function touchesHazard(hazard, x, y, z, clearance = 0) {
  return hazard && y >= hazard.y - .04 && y <= hazard.y + .22
    && Math.abs(x - hazard.x) < hazard.w / 2 + clearance
    && Math.abs(z - hazard.z) < hazard.d / 2 + clearance;
}

function coversPoint(box, x, z, radius) {
  if (box.structure === 'stair') radius = Math.min(radius,.16);
  const dx = x - box.x;
  const dz = z - box.z;
  return Math.abs(dx * box.cos - dz * box.sin) < box.w / 2 + radius
    && Math.abs(dx * box.sin + dz * box.cos) < box.d / 2 + radius;
}

export function blocked(x, z, radius = BODY_RADIUS, y = 0, layout = RELAY_SERVICE, height = BODY_HEIGHT) {
  if (layout.edgeRadius ? x * x + z * z > (layout.edgeRadius - radius) ** 2
    : Math.abs(x) > layout.length - radius || Math.abs(z) > layout.length - radius) return true;
  return layout.cover.some(box => y < box.h - 0.0001 && y + height > box.bottom + 0.0001
    && coversPoint(box, x, z, radius));
}

export function floorBelow(x, z, y, radius = 0, layout = RELAY_SERVICE) {
  let floor = layout.terrain?.(x, z) ?? (layout.fallY === undefined ? 0 : -Infinity);
  for (const box of layout.cover) {
    if (y + 0.0001 >= box.h && box.h > floor && coversPoint(box, x, z, radius)) floor = box.h;
  }
  return floor;
}

export function canDodge(player) {
  return !player.crouch && (player.dodgeCooldown ?? 0) <= 0 && (player.grounded || player.airDodge);
}

export function movePlayer(player, input, dt = TICK, layout = RELAY_SERVICE) {
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
    const alignment = layout.fallY === undefined ? 0 : 2.47;
    player.vx = pad.vx + (pad.x - player.x) * alignment;
    player.vz = pad.vz + (pad.z - player.z) * alignment;
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
  // The gallery catapult keeps its authored heading until the pilot clears the bridge lip.
  const releaseLane = layout.fallY !== undefined && player.padCooldown > 0 && player.vy > 0;
  if (!pad && !releaseLane && !player.dodgeTime) {
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

  let stepHeight = currentY;
  const moveY = Math.max(currentY, nextY);
  const walkingTerrain = layout.terrain && player.grounded;
  const mayStep = (layout.walkways || layout.fallY !== undefined) && player.grounded && player.vy <= 0;
  const stairAt = (x, z) => {
    if (!mayStep) return stepHeight;
    let top = stepHeight;
    for (const box of layout.cover) {
      if (box.structure !== 'stair' || box.h <= top || box.h > currentY + .35 || !coversPoint(box,x,z,BODY_RADIUS)) continue;
      top = box.h;
    }
    return top;
  };
  const canMove = (x, z) => {
    const ground = layout.terrain?.(x, z) ?? (layout.fallY === undefined ? 0 : -Infinity);
    if (layout.terrain && !walkingTerrain && ground > moveY + 0.0001) return false;
    const step = stairAt(x,z);
    if (blocked(x, z, BODY_RADIUS, Math.max(moveY, ground, step), layout, height)) return false;
    stepHeight = step;
    return true;
  };
  const x = player.x + player.vx * dt;
  const z = player.z + player.vz * dt;
  if (!canMove(x, player.z)) player.vx = 0;
  else player.x = x;
  if (!canMove(player.x, z)) player.vz = 0;
  else player.z = z;

  const floor = player.vy <= 0 ? floorBelow(player.x, player.z, stepHeight, BODY_RADIUS, layout) : -Infinity;
  const followsSlope = (walkingTerrain || mayStep) && floor >= currentY - 0.35;
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

export function wallDistance(origin, direction, limit = 65, layout = RELAY_SERVICE) {
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
  if (direction.y < 0 && layout.fallY === undefined) distance = Math.min(distance, -origin.y / direction.y);
  return distance;
}

export function distanceSquared(a, b) {
  return (a.x - b.x) ** 2 + (a.z - b.z) ** 2;
}

export function roomCode(value) {
  return typeof value === 'string' && /^[a-z0-9-]{3,24}$/i.test(value) ? value.toUpperCase() : null;
}
