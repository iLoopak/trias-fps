import * as THREE from '/three.module.js';
import { installIsland } from './island.js';
import { cameraInCover, deathFrame, followFrame } from './death-view.js';
import { buildPilot, pilotShell, posePilot } from './pilot.js';
import { recoilPose, reloadPose } from './weapon-motion.js';
import { presentedPose, rememberPose, SnapshotClock } from './snapshot-clock.js';
import { SiegeView } from './siege-view.js';
import { buildGarageView } from './vehicle-view.js';
import { vehicleAimFrame, VEHICLES, wrapAngle } from './vehicles.js';
import { installStation } from './station.js';
import { ArenaEffects } from './effects.js';
import { ARENAS, ARMORY, GRENADE, POWERUPS, SPRINT_SPEED, TEAMS, WEAPONS,
  blocked, eyeHeight, floorBelow, pilotColor } from './rules.js';

const color = value => new THREE.Color(value);
const box = new THREE.BoxGeometry(1, 1, 1);
const armorShape = new THREE.Shape();
for (const [i, [x, y]] of [[-.42, -.30], [-.29, -.42], [.29, -.42], [.42, -.30], [.42, .29], [.28, .42], [-.28, .42], [-.42, .29]].entries()) {
  if (i === 0) armorShape.moveTo(x, y);
  else armorShape.lineTo(x, y);
}
armorShape.closePath();
const beveled = new THREE.ExtrudeGeometry(armorShape,
  { depth: 0.84, bevelEnabled: true, bevelThickness: 0.08, bevelSize: 0.08, bevelSegments: 2, curveSegments: 1 });
beveled.center();
const barrel = new THREE.CylinderGeometry(0.5, 0.5, 1, 10, 1).rotateX(Math.PI / 2);
const rim = new THREE.TorusGeometry(0.39, 0.11, 6, 14);
const plate = new THREE.MeshStandardMaterial({ color: '#a6afb2', roughness: 0.42, metalness: 0.32 });
const graphite = new THREE.MeshStandardMaterial({ color: '#171d25', roughness: 0.58, metalness: 0.50 });
const brass = new THREE.MeshStandardMaterial({ color: '#8f6647', roughness: 0.45, metalness: 0.51 });
let ceramicGrain;
const ceramic = new THREE.MeshStandardMaterial({ color: '#d8ddcf', roughness: 0.48, metalness: 0.20 });
const glass = new THREE.MeshStandardMaterial({ color: '#172e3b', roughness: 0.18, metalness: 0.48 });
const yellow = new THREE.MeshStandardMaterial({ color: '#9c7b37', roughness: 0.42, metalness: 0.34 });
const dark = new THREE.MeshStandardMaterial({ color: '#263037', roughness: 0.77, metalness: 0.28 });
const steel = new THREE.MeshStandardMaterial({ color: '#46555e', roughness: 0.65, metalness: 0.45 });
const black = new THREE.MeshStandardMaterial({ color: '#11191e', roughness: 0.84 });
const rubberGrip = new THREE.MeshStandardMaterial({ color: '#2d2623', roughness: 0.94 });
const trim = new THREE.MeshStandardMaterial({ color: '#99adb3', roughness: 0.59, metalness: 0.43 });
const glow = value => new THREE.MeshBasicMaterial({ color: value });
const teamGlow = TEAMS.map(team => glow(team.color));
const whiteGlow = glow('#91c7d3');
const orange = glow('#ffac65');
const pinkReactor = glow('#ff67a0');
const armorMaterials = new Map();
function uniform(accent) {
  if (!armorMaterials.has(accent)) {
    armorMaterials.set(accent, {
      detail: new THREE.MeshBasicMaterial({ color: accent, toneMapped: false, fog: true }),
      armor: new THREE.MeshStandardMaterial({
        color: accent, map: ceramicGrain, emissive: accent, emissiveIntensity: 0.34,
        bumpMap: ceramicGrain, bumpScale: 0.025, roughness: 0.46, metalness: 0.23,
      }),
    });
  }
  return armorMaterials.get(accent);
}

function block(parent, material, w, h, d, x, y, z, geometry = box) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.scale.set(w, h, d);
  mesh.position.set(x, y, z);
  parent.add(mesh);
  return mesh;
}

function floorTexture(renderer, layout) {
  const url = { relay: '/deck.jpg', foundry: '/foundry-deck.jpg', basin: '/basin.jpg' }[layout.id];
  const texture = new THREE.TextureLoader().load(url);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.setScalar(layout.id === 'basin' ? 25 : 18);
  texture.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
  return texture;
}

function orbit(scene, layout) {
  const atmosphere = new THREE.ShaderMaterial({
    uniforms: { warm: { value: layout.id === 'foundry' ? 1 : layout.id === 'basin' ? 0.54 : 0 } },
    vertexShader: `
      varying vec3 surface;
      varying vec3 normalInView;
      varying vec3 eye;
      void main() {
        surface = normalize(position);
        normalInView = normalize(normalMatrix * normal);
        vec4 viewPosition = modelViewMatrix * vec4(position, 1.);
        eye = -viewPosition.xyz;
        gl_Position = projectionMatrix * viewPosition;
      }`,
    fragmentShader: `
      varying vec3 surface;
      varying vec3 normalInView;
      varying vec3 eye;
      uniform float warm;
      float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
      float noise(vec3 p) {
        vec3 i = floor(p);
        vec3 f = fract(p);
        vec3 u = f * f * (3. - 2. * f);
        return mix(mix(mix(hash(i), hash(i+vec3(1,0,0)),u.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),u.x),u.y),
                   mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),u.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),u.x),u.y),u.z);
      }
      void main() {
        vec3 p = normalize(surface);
        float shear = noise(p * 5. + vec3(1., 4., 0.));
        float clouds = noise(vec3(p.x * 7., p.y * 34. + shear * 3., p.z * 8.));
        clouds = smoothstep(.31, .77, clouds + noise(p * 48.) * .23);
        float sunlight = smoothstep(-.35, .48, dot(p, normalize(vec3(-.75,.45,-.42))));
        float rim = pow(1. - max(0., dot(normalize(normalInView), normalize(eye))), 4.);
        vec3 ocean = mix(vec3(.010,.017,.032), vec3(.036,.084,.13), sunlight);
        vec3 light = mix(vec3(.5,.78,.97), vec3(.94,.58,.34), warm);
        ocean = mix(ocean, ocean.bgr * vec3(1.52,.79,.62), warm);
        gl_FragColor = vec4(ocean + clouds * sunlight * vec3(.11,.16,.22) + rim * light * (.17 + .33 * sunlight), 1.);
        #include <colorspace_fragment>
      }`,
  });
  const horizon = new THREE.Mesh(new THREE.SphereGeometry(114, 64, 40), atmosphere);
  horizon.position.set(layout.id === 'basin' ? -90 : layout.id === 'foundry' ? -32 : -83, layout.id === 'basin' ? -37 : -61, 167);
  scene.add(horizon);
  const moon = new THREE.Mesh(new THREE.SphereGeometry(18, 26, 16), atmosphere);
  moon.position.set(153, 57, -111);
  scene.add(moon);
  const stars = [];
  let seed = 10731;
  const random = () => ((seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0) / 4294967296);
  for (let i = 0; i < 920; i++) {
    const a = random() * Math.PI * 2;
    const y = random() * 230 - 45;
    stars.push(Math.cos(a) * 235, y, Math.sin(a) * 235);
  }
  const geometry = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(stars, 3));
  scene.add(new THREE.Points(geometry, new THREE.PointsMaterial({ color: '#91abc8', size: 0.29, fog: false })));
  const brightStars = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(stars.slice(0, 270), 3));
  scene.add(new THREE.Points(brightStars, new THREE.PointsMaterial({ color: '#dfecff', size: 0.56, fog: false })));
}

function buildMap(scene, renderer, layout) {
  const deck = floorTexture(renderer, layout);
  const size = layout.length;
  const foundry = layout.id === 'foundry';
  const basin = layout.id === 'basin';
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(size * 2, size * 2),
    new THREE.MeshStandardMaterial({ map: deck, bumpMap: deck, bumpScale: 0.016, roughness: 0.78, metalness: 0.19, color: foundry ? '#d2c8bc' : basin ? '#c4acad' : '#cbd8e2' }));
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);
  if (basin) {
    const outside = new THREE.Mesh(new THREE.PlaneGeometry(315, 315), new THREE.MeshStandardMaterial({ color: '#3a333e', roughness: 0.98 }));
    outside.position.y = -0.13;
    outside.rotation.x = -Math.PI / 2;
    scene.add(outside);
  }

  const panelMap = new THREE.TextureLoader().load('/bulkhead.jpg');
  panelMap.colorSpace = THREE.SRGBColorSpace;
  panelMap.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
  const panel = new THREE.MeshStandardMaterial({
    map: panelMap, color: foundry ? '#a38c72' : basin ? '#b8a29e' : '#d1d8df', bumpMap: panelMap, bumpScale: 0.047, roughness: 0.63, metalness: 0.32, emissive: '#25313b', emissiveIntensity: 0.36,
  });
  const batches = new Map();
  const add = (material, w, h, d, x, y, z, angle = 0) => {
    const matrix = new THREE.Matrix4().compose(new THREE.Vector3(x, y, z),
      new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), angle), new THREE.Vector3(w, h, d));
    if (!batches.has(material)) batches.set(material, []);
    batches.get(material).push(matrix);
  };
  const porcelain = new THREE.MeshStandardMaterial({ color: '#938d82', roughness: 0.61, metalness: 0.24 });
  for (const c of layout.cover) {
    if (c.gantry || c.duct) continue;
    const { x, z, h, w, d } = c;
    const angle = c.angle ?? 0;
    const light = c.relay ? pinkReactor : teamGlow[c.sector] ?? whiteGlow;
    const facade = c.sector >= 0 ? uniform(TEAMS[c.sector].color).armor : porcelain;
    const tile = (material, width, height, depth, lx, y, lz, rotation = 0) =>
      add(material, width, height, depth, x + lx * c.cos + lz * c.sin, y, z - lx * c.sin + lz * c.cos, angle + rotation);

    tile(dark, w - 0.07, h - 0.14, d - 0.07, 0, (h - 0.14) / 2, 0);
    tile(steel, w, 0.13, d, 0, h - 0.064, 0);
    tile(black, w, 0.18, d, 0, 0.089, 0);
    for (const direction of [-1, 1]) {
      const sides = [
        [w, d, (material, width, height, depth, offset, y, outward) =>
          tile(material, width, height, depth, offset, y, outward * direction)],
        [d, w, (material, width, height, depth, offset, y, outward) =>
          tile(material, width, height, depth, outward * direction, y, -offset, Math.PI / 2)],
      ];
      for (const [length, thickness, face] of sides) {
        face(panel, length * 0.91, h * 0.71, 0.05, 0, h * 0.48, thickness / 2 - 0.024);
        face(facade, length * (c.terrace ? 0.83 : 0.69), 0.14, 0.056, 0, h * 0.74, thickness / 2 - 0.019);
        face(light, length * 0.50, 0.043, 0.032, 0, h * 0.75, thickness / 2 + 0.003);
        const spokes = c.profile === 'baffle' ? [-0.31, -0.13, 0.13, 0.31] : [-0.42, 0.42];
        for (const stripe of spokes) {
          face(c.profile === 'baffle' ? black : facade, c.profile === 'baffle' ? 0.11 : 0.165,
            c.profile === 'baffle' ? h * 0.51 : h * 0.81, 0.09, length * stripe, h * 0.48, thickness / 2 - 0.035);
        }
      }
      if (c.terrace || c.relay) {
        tile(light, 0.046, 0.022, d - 0.17, direction * (w / 2 - 0.11), h + 0.015, 0);
        tile(light, w - 0.24, 0.022, 0.042, 0, h + 0.016, direction * (d / 2 - 0.11));
      }
    }
    if (c.relay) {
      for (const offset of [-0.87, 0.87]) {
        tile(light, 0.09, 0.69, 0.09, offset, h + 0.34, -0.82);
        tile(steel, 0.16, 0.1, 0.16, offset, h + 0.70, -0.82);
      }
    }
  }

  const hullMetal = new THREE.MeshStandardMaterial({ color: '#253543', roughness: 0.8, metalness: 0.16 });
  for (let side = 0; side < 4; side++) {
    const angle = side * Math.PI / 2;
    const edge = (material, w, h, d, x, y, z) =>
      add(material, w, h, d, x * Math.cos(angle) + z * Math.sin(angle), y, -x * Math.sin(angle) + z * Math.cos(angle), angle);
    edge(dark, size * 2, 0.87, 0.65, 0, 0.435, -size - 0.31);
    edge(steel, size * 2, 0.18, 0.76, 0, 0.91, -size - 0.31);
    edge(whiteGlow, size * 2, 0.025, 0.03, 0, 0.77, -size + 0.02);
    edge(black, size * 2, 0.015, 0.38, 0, 0.012, -size + 1.14);
    edge(hullMetal, size * 2, 1.3, 2.1, 0, -1.15, -size - 0.90);
    if (!basin) {
      edge(hullMetal, size * 2 - 6, 0.58, 1.4, 0, 5.8, -size - 6.2);
      edge(steel, size * 2 - 6.5, 0.1, 0.92, 0, 5.48, -size - 5.92);
      for (const post of [-26, -8.5, 8.5, 26]) {
        edge(hullMetal, 1.32, 8.3, 1.6, post, 1.7, -size - 6.20);
        edge(steel, 1.7, 0.68, 1.62, post, 4.81, -size - 6.20);
        edge(whiteGlow, 0.66, 0.053, 0.05, post, 4.82, -size - 5.37);
        edge(orange, 0.23, 0.18, 0.23, post, 6.23, -size - 6.28);
      }
    }
    for (let i = 3 - size; i <= size - 3; i += basin ? 8 : 6) {
      edge(steel, 0.17, 2.6, 0.32, i, 1.3, -size - 0.08);
      edge(orange, 0.105, 0.155, 0.065, i, 2.54, -size + 0.11);
      edge(black, 3.3, 0.18, 0.49, i, 0.49, -size);
      for (let j = -1.2; j <= 1.2; j += 0.6) edge(orange, 0.13, 0.018, 0.3, i + j, 0.019, -size + 1.14);
    }
  }
  for (const [i, team] of TEAMS.entries()) {
    const lane = (material, w, d, offset, radius) => {
      const angle = team.angle;
      add(material, w, 0.009, d, offset * Math.cos(angle) + radius * Math.sin(angle),
        0.013, -offset * Math.sin(angle) + radius * Math.cos(angle), angle);
    };
    for (const offset of [-2.35, 2.35]) {
      lane(black, 0.27, 22, offset, 14);
      lane(teamGlow[i], 0.035, 21, offset, 14);
    }
    for (let radius = 5; radius <= 25; radius += 2) {
      lane(steel, 0.08, 0.56, 0, radius);
    }
  }

  for (const [material, transforms] of batches) {
    const mesh = new THREE.InstancedMesh(box, material, transforms.length);
    for (const [i, matrix] of transforms.entries()) mesh.setMatrixAt(i, matrix);
    mesh.castShadow = !material.isMeshBasicMaterial;
    mesh.receiveShadow = true;
    scene.add(mesh);
  }
}

function sectorDecal(scene, team, base) {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 256;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = team.color;
  ctx.strokeStyle = team.color;
  ctx.lineWidth = 3;
  ctx.globalAlpha = 0.58;
  ctx.strokeRect(38, 48, 436, 156);
  ctx.fillRect(38, 48, 30, 7);
  ctx.fillRect(444, 197, 30, 7);
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.font = 'bold 80px monospace';
  ctx.fillText(team.name, 256, 117);
  ctx.font = 'bold 21px monospace';
  ctx.fillText('RELAY / SECTOR', 256, 172);
  const map = new THREE.CanvasTexture(canvas);
  map.colorSpace = THREE.SRGBColorSpace;
  const decal = new THREE.Mesh(new THREE.PlaneGeometry(3.5, 1.75), new THREE.MeshBasicMaterial({
    map, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2,
  }));
  decal.rotation.set(-Math.PI / 2, 0, team.angle, 'YXZ');
  decal.position.set(base.x + Math.sin(team.angle) * 1.1, 0.024, base.z + Math.cos(team.angle) * 1.1);
  scene.add(decal);
}

function ring(radius, width, material) {
  const mesh = new THREE.Mesh(new THREE.RingGeometry(radius - width, radius, 64), material);
  mesh.rotation.x = -Math.PI / 2;
  return mesh;
}

function terrainRing(radius, width, material, spot, terrain) {
  const marker = ring(radius, width, material);
  if (terrain) {
    const vertices = marker.geometry.attributes.position;
    for (let i = 0; i < vertices.count; i++) {
      vertices.setZ(i, terrain(spot.x + vertices.getX(i), spot.z - vertices.getY(i)) - spot.y);
    }
    marker.geometry.computeBoundingSphere();
  }
  return marker;
}

function glyph(text) {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 128;
  const ctx = canvas.getContext('2d');
  const map = new THREE.CanvasTexture(canvas);
  map.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map, depthTest: false, fog: false }));
  sprite.scale.set(1.52, 1.52, 1.52);
  sprite.renderOrder = 3;
  sprite.userData.label = value => {
    ctx.resetTransform();
    ctx.clearRect(0, 0, 128, 128);
    ctx.translate(64, 64);
    ctx.rotate(Math.PI / 4);
    ctx.fillStyle = '#10191cf2';
    ctx.fillRect(-33, -33, 66, 66);
    ctx.strokeStyle = '#dfeff4';
    ctx.lineWidth = 3;
    ctx.strokeRect(-33, -33, 66, 66);
    ctx.rotate(-Math.PI / 4);
    ctx.fillStyle = '#ffffff';
    ctx.font = value.length > 1 ? 'bold 26px monospace' : 'bold 43px monospace';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(value, 0, 2, 69);
    map.needsUpdate = true;
  };
  sprite.userData.label(text);
  return sprite;
}

function buildObjectives(scene, layout) {
  return TEAMS.map((team, i) => {
    const home = layout.bases[i];
    const station = layout.points[i];
    const floorRing = (radius, width, material, spot) => terrainRing(radius, width, material, spot, layout.terrain);
    const base = new THREE.Group();
    base.position.set(home.x, (home.y ?? 0) + 0.045, home.z);
    base.add(floorRing(2.38, 0.055, teamGlow[i], home), floorRing(2.13, 0.02, teamGlow[i], home));
    scene.add(base);
    if (!layout.terrain) sectorDecal(scene, team, home);

    const point = new THREE.Group();
    const indicator = new THREE.MeshBasicMaterial({ color: '#b0c0cb' });
    point.position.set(station.x, (station.y ?? 0) + 0.055, station.z);
    point.add(floorRing(3.05, 0.075, indicator, station), floorRing(2.88, 0.025, indicator, station));
    const label = glyph(station.label);
    label.position.y = 2.35;
    point.add(label);
    scene.add(point);

    const flag = new THREE.Group();
    block(flag, trim, 0.046, 1.65, 0.046, 0, 0.83, 0);
    const bannerGeometry = new THREE.PlaneGeometry(0.86, 0.49, 10, 4);
    bannerGeometry.translate(0.43, 0, 0);
    const banner = new THREE.Mesh(bannerGeometry, new THREE.MeshStandardMaterial({
      color: team.color, emissive: team.color, emissiveIntensity: 0.31, roughness: 0.84, side: THREE.DoubleSide,
    }));
    banner.position.y = 1.37;
    flag.add(banner);
    block(flag, whiteGlow, 0.09, 0.05, 0.09, 0, 1.675, 0, beveled);
    scene.add(flag);
    point.visible = flag.visible = false;
    return { point, indicator, label, flag, banner };
  });
}

function buildPickups(scene, layout) {
  return layout.pickups.map(item => {
    const spec = WEAPONS[item.type] ?? POWERUPS[item.type];
    const material = glow(spec.color);
    const pad = new THREE.Group();
    pad.position.set(item.x, item.y + 0.035, item.z);
    const activeRings = new THREE.Group();
    activeRings.add(ring(0.92, 0.047, material), ring(0.76, 0.026, material));
    pad.add(activeRings);
    const highlight = new THREE.MeshBasicMaterial({
      color: spec.color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.1,
    });
    const halo = ring(0.63, 0.27, highlight);
    halo.position.y = 0.004;
    activeRings.add(halo);
    block(pad, black, 0.94, 0.07, 0.94, 0, -0.002, 0, beveled);
    const object = new THREE.Group();
    pad.add(object);
    if (WEAPONS[item.type]) {
      buildWeapon(object, item.type);
      object.scale.setScalar(1.32);
      object.rotation.z = 0.2;
    } else if (item.type === 'med') {
      block(object, material, 0.52, 0.16, 0.19, 0, 0, 0, beveled);
      block(object, material, 0.16, 0.52, 0.19, 0, 0, 0, beveled);
    } else {
      const core = new THREE.Mesh(new THREE.OctahedronGeometry(item.type === 'surge' ? 0.34 : 0.27), material);
      const outer = new THREE.Mesh(new THREE.TorusGeometry(0.43, 0.018, 5, 19), material);
      outer.rotation.x = 0.89;
      object.add(core, outer);
    }
    const label = glyph(WEAPONS[item.type] ? String(ARMORY.indexOf(item.type) + 1)
      : { med: '+', shield: 'S', surge: 'O' }[item.type]);
    label.position.y = 1.42;
    label.scale.setScalar(0.78);
    label.material.depthTest = true;
    label.material.color.set(spec.color);
    pad.add(label);
    const progress = glyph('·');
    progress.position.copy(label.position);
    progress.scale.setScalar(0.66);
    progress.material.depthTest = true;
    progress.material.color.set('#a8b7c8');
    progress.visible = false;
    pad.add(progress);
    scene.add(pad);
    return { pad, object, highlight, activeRings, label, progress, spec, remaining: 0 };
  });
}

const weaponTint = Object.fromEntries(ARMORY.map(key => [key, color(WEAPONS[key].color)]));
const weaponGlow = Object.fromEntries(ARMORY.map(key => [key, new THREE.MeshBasicMaterial({ color: weaponTint[key], toneMapped: false })]));

const MUZZLES = { pulse: -0.61, scatter: -0.52, lance: -0.78, arc: -0.49, rift: -0.66 };
const FEEDS = {
  pulse: [0.097, 0.156, 0.132, -0.124, -0.125], scatter: [0.166, 0.102, 0.15, -0.094, -0.118],
  lance: [0.083, 0.133, 0.13, -0.101, -0.177], arc: [0.174, 0.098, 0.19, -0.102, -0.071],
  rift: [0.133, 0.138, 0.137, -0.108, -0.106],
};
const weaponModels = new Map();

function buildWeapon(parent, key) {
  if (weaponModels.has(key)) {
    parent.add(weaponModels.get(key).clone());
    return;
  }
  const assembly = new THREE.Group();
  const action = new THREE.Group();
  action.name = 'action';
  const feed = new THREE.Group();
  feed.name = 'feed';
  assembly.add(action, feed);
  const light = weaponGlow[key];
  const part = (material, w, h, d, x, y, z, geometry = beveled) =>
    block(assembly, material, w, h, d, x, y, z, geometry);
  const muzzle = (x, y, z, width) => {
    part(black, width, width, 0.12, x, y, z + 0.05, barrel);
    part(trim, width * 1.08, width * 1.08, width * 0.40, x, y, z, rim);
  };
  part(graphite, 0.107, 0.20, 0.14, 0, -0.112, 0.061).rotation.x = 0.17;
  part(black, 0.13, 0.061, 0.19, 0, -0.035, 0.110);
  for (const side of [-1, 1]) {
    part(trim, 0.039, 0.039, 0.023, side * 0.064, -0.026, 0.13, barrel);
  }
  const [cellWidth, cellHeight, cellDepth, cellY, cellZ] = FEEDS[key];
  block(feed, dark, cellWidth, cellHeight, cellDepth, 0, cellY, cellZ, beveled);
  block(feed, trim, cellWidth * 1.12, 0.022, cellDepth * 1.04, 0, cellY - cellHeight * 0.43, cellZ, beveled);
  for (const side of [-1, 1]) {
    block(feed, light, 0.011, 0.015, cellDepth * 0.66, side * cellWidth * 0.51, cellY - cellHeight * 0.23, cellZ);
  }

  if (key === 'pulse') {
    part(graphite, 0.18, 0.153, 0.55, 0, 0.015, -0.047);
    part(ceramic, 0.176, 0.084, 0.29, 0, 0.084, -0.075);
    part(black, 0.11, 0.104, 0.32, 0, 0.032, -0.364, barrel);
    muzzle(0, 0.034, -0.607, 0.10);
    part(steel, 0.144, 0.094, 0.156, 0, 0.017, -0.426);
    part(light, 0.049, 0.023, 0.03, 0, 0.143, -0.183);
    block(action, trim, 0.045, 0.020, 0.102, -0.101, 0.070, -0.02, beveled);
    part(steel, 0.066, 0.032, 0.21, 0, 0.129, -0.064);
    part(trim, 0.147, 0.051, 0.062, 0, 0.02, -0.508);
    for (const side of [-1, 1]) {
      part(black, 0.019, 0.086, 0.255, side * 0.091, 0.024, -0.148);
      for (let i = 0; i < 4; i++) {
        part(light, 0.02, 0.041, 0.024, side * 0.102, 0.03, -0.08 - i * 0.042).rotation.x = 0.31;
      }
    }
  } else if (key === 'scatter') {
    part(graphite, 0.245, 0.172, 0.46, 0, 0.012, -0.022);
    part(brass, 0.225, 0.099, 0.25, 0, 0.093, -0.066);
    block(action, steel, 0.223, 0.109, 0.194, 0, -0.050, -0.307, beveled);
    for (let i = 0; i < 4; i++) block(action, rubberGrip, 0.228, 0.087, 0.014, 0, -0.067, -0.23 - i * 0.041, beveled);
    part(graphite, 0.215, 0.038, 0.022, 0, 0.132, -0.261);
    part(orange, 0.026, 0.024, 0.016, 0, 0.153, -0.274);
    for (const side of [-1, 1]) {
      part(black, 0.10, 0.105, 0.32, side * 0.067, 0.049, -0.316, barrel);
      muzzle(side * 0.067, 0.049, -0.517, 0.104);
      part(light, 0.026, 0.036, 0.12, side * 0.125, 0.021, -0.158);
      for (let i = 0; i < 3; i++) part(dark, 0.032, 0.07, 0.023, side * 0.104, -0.057, -0.20 - i * 0.048);
    }
  } else if (key === 'lance') {
    part(graphite, 0.157, 0.151, 0.73, 0, 0.012, -0.125);
    part(ceramic, 0.15, 0.062, 0.51, 0, 0.085, -0.225);
    part(black, 0.081, 0.083, 0.46, 0, 0.015, -0.5, barrel);
    muzzle(0, 0.015, -0.77, 0.085);
    part(graphite, 0.065, 0.088, 0.19, 0, 0.124, -0.135);
    part(dark, 0.113, 0.113, 0.22, 0, 0.172, -0.127, barrel);
    part(glass, 0.102, 0.102, 0.021, 0, 0.172, -0.243, barrel);
    part(light, 0.061, 0.061, 0.016, 0, 0.172, -0.255, rim);
    block(action, trim, 0.108, 0.029, 0.079, 0, 0.115, -0.267, beveled);
    part(black, 0.176, 0.048, 0.112, 0, 0.021, -0.702);
    for (const side of [-1, 1]) part(plate, 0.022, 0.032, 0.074, side * 0.085, 0.144, -0.160);
    for (const side of [-1, 1]) {
      part(steel, 0.035, 0.053, 0.42, side * 0.067, 0.02, -0.462);
      part(light, 0.014, 0.02, 0.26, side * 0.09, 0.022, -0.434);
    }
  } else if (key === 'rift') {
    part(graphite, 0.242, 0.181, 0.45, 0, 0.003, -0.056);
    part(ceramic, 0.212, 0.065, 0.23, 0, 0.104, -0.077);
    part(black, 0.136, 0.128, 0.32, 0, 0.017, -0.306, barrel);
    part(light, 0.112, 0.112, 0.028, 0, 0.024, -0.644, rim);
    part(graphite, 0.192, 0.155, 0.146, 0, 0.023, -0.477, barrel);
    block(action, light, 0.125, 0.125, 0.024, 0, 0.024, -0.591, rim);
    block(action, brass, 0.178, 0.164, 0.038, 0, 0.029, -0.542, rim);
    for (const side of [-1, 1]) {
      part(steel, 0.048, 0.089, 0.36, side * 0.106, 0.002, -0.396);
      part(black, 0.087, 0.125, 0.243, side * 0.124, 0.013, -0.446, barrel);
      part(light, 0.040, 0.039, 0.199, side * 0.168, 0.025, -0.440);
      part(plate, 0.071, 0.099, 0.087, side * 0.106, 0.045, -0.606);
    }
  } else {
    part(graphite, 0.22, 0.174, 0.41, 0, 0.014, -0.062);
    part(yellow, 0.241, 0.11, 0.27, 0, 0.094, -0.081);
    part(steel, 0.19, 0.155, 0.094, 0, 0.035, -0.328);
    muzzle(0, 0.034, -0.478, 0.123);
    block(action, light, 0.16, 0.114, 0.034, 0, 0.032, -0.401, rim);
    part(graphite, 0.089, 0.035, 0.22, 0, 0.154, -0.084);
    for (let i = 0; i < 4; i++) part(brass, 0.086, 0.018, 0.012, 0, 0.171, -0.039 - i * 0.04);
    for (const side of [-1, 1]) {
      part(black, 0.088, 0.109, 0.22, side * 0.094, -0.049, -0.141, barrel);
      part(light, 0.029, 0.071, 0.208, side * 0.137, -0.047, -0.143);
      for (let i = 0; i < 3; i++) {
        part(steel, 0.029, 0.085, 0.023, side * 0.153, -0.045, -0.08 - i * 0.064);
      }
    }
  }
  weaponModels.set(key, assembly);
  parent.add(assembly.clone());
}

function viewModel(camera) {
  const root = new THREE.Group();
  const muzzle = new THREE.Object3D();
  root.add(muzzle);
  const equipment = ARMORY.map(key => {
    const model = new THREE.Group();
    buildWeapon(model, key);
    const panel = block(model, black, 0.098, 0.016, 0.148, -0.078, 0.112, -0.02, beveled);
    panel.rotation.x = 0.23;
    panel.rotation.z = -0.35;
    const status = new THREE.MeshBasicMaterial({ color: weaponTint[key], toneMapped: false });
    const cells = [];
    for (let i = 0; i < 6; i++) {
      const light = block(model, status, 0.044, 0.013, 0.009, -0.088, 0.124 + i * 0.003, 0.036 - i * 0.018);
      light.rotation.z = -0.35;
      cells.push(light);
    }
    model.userData.mechanism = { action: model.getObjectByName('action'), feed: model.getObjectByName('feed'), cells, status };
    if (key === 'arc' || key === 'rift') {
      const coil = new THREE.Group();
      coil.position.set(0, 0.028, MUZZLES[key] + 0.085);
      const coilSkin = new THREE.MeshBasicMaterial({
        color: weaponTint[key], transparent: true, opacity: 0.54, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
      });
      for (const [i, radius] of [0.095, 0.13].entries()) {
        const stream = new THREE.Mesh(new THREE.TorusGeometry(radius, 0.005, 4, 14, Math.PI * 1.50), coilSkin);
        stream.rotation.x = i ? -0.35 : 0.29;
        stream.rotation.z = i * 1.9;
        coil.add(stream);
      }
      model.add(coil);
      model.userData.flow = coil;
      model.userData.coilSkin = coilSkin;
    }
    model.visible = false;
    root.add(model);
    return model;
  });
  let support;
  const cuffs = [];
  for (const arm of [
    { x: 0.065, y: -0.234, z: 0.24, yaw: -0.20 },
    { x: -0.163, y: -0.177, z: -0.02, yaw: 0.35 },
  ]) {
    const forearm = new THREE.Group();
    forearm.position.set(arm.x, arm.y, arm.z);
    forearm.rotation.set(0.28, arm.yaw, arm.x > 0 ? -0.20 : 0.21);
    root.add(forearm);
    if (arm.x < 0) support = forearm;
    block(forearm, graphite, 0.19, 0.155, 0.46, 0, 0, 0.055, beveled);
    block(forearm, steel, 0.132, 0.06, 0.19, 0, 0.073, 0.105, beveled);
    cuffs.push(block(forearm, plate, 0.129, 0.045, 0.117, 0, 0.073, -0.047, beveled));
    block(forearm, black, 0.134, 0.11, 0.17, 0, 0.031, -0.186, beveled);
  }
  camera.add(root);
  return { root, equipment, muzzle, support, cuffs, lastShot: -1000 };
}

function buildTurrets(scene, layout) {
  return layout.turrets.map(spec => {
    const root = new THREE.Group();
    root.position.set(spec.x, spec.y, spec.z);
    root.rotation.y = spec.yaw;
    scene.add(root);
    block(root, graphite, 1.36, 0.11, 1.26, 0, 0.055, -0.38, beveled);
    block(root, steel, 0.19, 0.89, 0.22, 0, 0.51, -0.75, beveled).rotation.x = -0.16;
    for (const side of [-1, 1]) {
      block(root, plate, 0.36, 0.074, 0.48, side * 0.44, 0.115, -0.38, beveled);
      block(root, orange, 0.22, 0.012, 0.064, side * 0.44, 0.157, -0.27);
    }
    const pivot = new THREE.Group();
    pivot.position.set(0, 1.22, -0.53);
    root.add(pivot);
    buildWeapon(pivot, 'arc');
    pivot.scale.setScalar(1.52);
    const label = glyph('E');
    label.position.set(0, 1.78, -0.57);
    label.scale.setScalar(0.60);
    label.material.depthTest = true;
    root.add(label);
    return { root, pivot, label, spec };
  });
}

export class Arena {
  constructor(canvas, layout = ARENAS.relay) {
    this.layout = layout;
    const foundry = layout.id === 'foundry';
    const basin = layout.id === 'basin';
    const island = layout.id === 'island';
    if (!ceramicGrain) {
      ceramicGrain = new THREE.TextureLoader().load('/armor.jpg');
      ceramicGrain.wrapS = ceramicGrain.wrapT = THREE.RepeatWrapping;
      ceramicGrain.colorSpace = THREE.SRGBColorSpace;
      ceramic.map = ceramicGrain;
      ceramic.bumpMap = ceramicGrain;
      ceramic.bumpScale = 0.038;
      graphite.bumpMap = ceramicGrain;
      graphite.bumpScale = 0.025;
    }
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    this.maxResolution = Math.min(devicePixelRatio, 1.6);
    this.resolution = this.maxResolution;
    this.renderer.setPixelRatio(this.resolution);
    this.frameCost = 16.7;
    this.resolutionCheck = performance.now() + 2600;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.38;
    this.canvas = canvas;
    this.scene = new THREE.Scene();
    this.scene.background = color(island ? '#a8dddb' : foundry ? '#20121b' : basin ? '#292238' : '#070d18');
    this.scene.fog = new THREE.FogExp2(island ? '#b9e3d8' : foundry ? '#382531' : basin ? '#4c3d59' : '#0a1321', island ? 0.0085 : foundry ? 0.019 : basin ? 0.0096 : 0.017);
    this.camera = new THREE.PerspectiveCamera(76, 1, 0.034, 300);
    this.camera.rotation.order = 'YXZ';
    this.scene.add(this.camera);
    const ambient = new THREE.HemisphereLight(island ? '#fcf3cb' : foundry ? '#ffe0be' : basin ? '#dcd6ff' : '#c7e8ff', island ? '#719665' : foundry ? '#64443d' : basin ? '#6e5160' : '#51616a', island ? 2.4 : 2.1);
    const bounce = new THREE.DirectionalLight('#7e9dd0', 0.85);
    bounce.position.set(15, 12, -21);
    const sun = new THREE.DirectionalLight(island ? '#fff2bc' : foundry ? '#ffcda6' : basin ? '#ffe3bb' : '#cce5ff', island ? 2.05 : 2.4);
    sun.position.set(-16, 29, 12);
    sun.castShadow = true;
    sun.shadow.mapSize.set(basin ? 1536 : 1024, basin ? 1536 : 1024);
    const shadowSize = basin ? 67 : 42;
    Object.assign(sun.shadow.camera, { left: -shadowSize, right: shadowSize, top: shadowSize, bottom: -shadowSize, near: 1, far: 100 });
    sun.shadow.camera.updateProjectionMatrix();
    sun.shadow.normalBias = 0.055;
    this.scene.add(ambient, bounce, sun);
    if (island) this.animateStation = installIsland(this.scene, layout);
    else {
      orbit(this.scene, layout);
      buildMap(this.scene, this.renderer, layout);
      this.animateStation = installStation(this.scene, layout);
    }
    this.objectives = buildObjectives(this.scene, layout);
    this.siegeView = layout.siege ? new SiegeView(this.scene, layout.siege, glyph) : null;
    this.pickups = buildPickups(this.scene, layout);
    this.turrets = buildTurrets(this.scene, layout);
    this.garage = buildGarageView(this.scene, layout, glyph);
    this.dustTint = color('#a77c73');
    this.weapon = viewModel(this.camera);
    this.effects = new ArenaEffects(this.scene, this.camera, this.weapon.muzzle);
    this.pilots = new Map();
    this.snapshotClock = new SnapshotClock();
    this.corpses = [];
    this.previous = performance.now();
  }

  dispose() {
    this.reset();
    const geometry = new Set();
    const materials = new Set();
    this.scene.traverse(object => {
      if (object.geometry) geometry.add(object.geometry);
      if (object.material) materials.add(object.material);
    });
    for (const item of geometry) item.dispose();
    for (const item of materials) {
      item.map?.dispose();
      item.dispose();
    }
    this.renderer.dispose();
  }

  reset() {
    this.followTarget = null;
    this.snapshotClock = new SnapshotClock();
    for (const corpse of this.corpses) corpse.model.root.removeFromParent();
    this.corpses.length = 0;
    for (const model of this.pilots.values()) this.scene.remove(model.root);
    this.pilots.clear();
    this.weapon.lastShot = this.weapon.intent = -1000;
    this.motion = null;
    this.eye = undefined;
    this.weapon.lastWeapon = undefined;
    this.weapon.reloading = null;
    this.weapon.sprint = 0;
    this.lookYaw = this.lookPitch = undefined;
    for (const model of this.garage) model.target = null;
    this.sideSway = this.verticalSway = this.stride = 0;
    this.effects.reset();
  }

  snapshot(state, own, mode) {
    const gap = this.snapshotClock.receive(state.at, performance.now());
    if (this.weapon.pilot !== own.character) {
      this.weapon.pilot = own.character;
      for (const cuff of this.weapon.cuffs) cuff.material = pilotShell(own.character);
    }
    const active = new Set();
    for (const player of state.players) {
      if (player.id === own.id) continue;
      active.add(player.id);
      const accent = pilotColor(player, mode);
      if (!this.pilots.has(player.id)) {
        const model = buildPilot(uniform(accent), mode !== 'ffa' && player.team === own.team, player.character);
        model.history = [];
        model.presentation = {};
        this.pilots.set(player.id, model);
        this.scene.add(model.root);
      }
      const model = this.pilots.get(player.id);
      if (gap || model.spawn !== player.spawn || model.target?.vehicle !== player.vehicle || !model.target?.hp) {
        model.history.length = 0;
      }
      rememberPose(model.history, state.at, player);
      if (model.spawn !== player.spawn) {
        model.root.position.set(player.x, player.y, player.z);
        model.root.rotation.y = player.yaw;
        if (model.spawn !== undefined) this.respawn(player, mode);
        model.spawn = player.spawn;
        model.phase = ((player.team * 4 + player.slot) * 0.618) % 1;
        model.gait = model.pace = 0;
        model.crouch = player.crouch ? 1 : 0;
        model.shotAt = model.actionAt = -1000;
      }
      if (model.activeWeapon !== player.weapon) {
        model.weapon.clear();
        buildWeapon(model.weapon, player.weapon);
        model.activeWeapon = player.weapon;
      }
      model.target = player;
      model.root.visible = player.hp > 0 && (player.vehicle ?? -1) < 0;
    }
    for (const [id, model] of this.pilots) {
      if (active.has(id)) continue;
      this.scene.remove(model.root);
      this.pilots.delete(id);
    }
    this.objectives.forEach((objective, i) => {
      const point = state.points?.[i];
      objective.point.visible = Boolean(point);
      if (point) {
        const team = TEAMS[point.capture >= 0 ? point.capture : point.owner];
        objective.indicator.color.set(team?.color ?? '#9caebb');
        objective.label.material.color.set(team?.color ?? '#ffffff');
      }
      const flag = state.flags?.[i];
      objective.flag.visible = Boolean(flag);
      if (flag) objective.flag.position.set(flag.x, flag.y + (flag.status === 'carried' ? 1.05 : 0.03), flag.z);
    });
    this.siegeView?.update(state.siege, own.team);
    this.effects.setProjectiles(state.projectiles);
    this.garage.forEach((model, i) => {
      const car = state.vehicles?.[i];
      if (car?.hp > 0 && !(model.target?.hp > 0)) model.root.position.set(car.x, 0, car.z);
      model.target = car;
    });
    this.turrets.forEach((turret, i) => {
      const station = state.turrets[i];
      const gunner = state.players.find(item => item.id === station.user);
      turret.pivot.rotation.y = gunner ? gunner.yaw - turret.spec.yaw : 0;
      turret.pivot.rotation.x = gunner ? gunner.pitch : 0;
      turret.label.visible = !gunner;
    });
    this.pickups.forEach((item, i) => {
      const remaining = state.pickups[i];
      if (item.remaining === remaining) return;
      const { object, activeRings, label, progress, spec } = item;
      if (!remaining && item.remaining > 0) {
        const { x, y, z } = this.layout.pickups[i];
        this.effects.pulse([x, y, z], color(spec.color), performance.now(), true);
      }
      item.remaining = remaining;
      object.visible = activeRings.visible = label.visible = remaining === 0;
      progress.visible = remaining > 0;
      if (remaining > 0) {
        progress.userData.label(remaining < 10 ? String(remaining) : '·');
        const total = spec.respawn ?? 22;
        const charge = 1 - remaining / total;
        progress.material.color.set('#536779').lerp(color(spec.color), charge);
        progress.scale.setScalar(0.42 + charge * 0.31);
      }
    });
  }

  shot(event, local) {
    const time = performance.now();
    const handheld = !event.mounted && !event.vehicle;
    if (local && handheld) {
      this.weapon.lastShot = time;
      this.weapon.lastWeapon = event.weapon;
      this.weapon.alternate = event.alt;
      const scatterSide = event.weapon === 'scatter' && !event.alt ? (this.weapon.scatterSide ?? 1) : 0;
      this.weapon.scatterSide = scatterSide ? -scatterSide : this.weapon.scatterSide;
      this.weapon.muzzle.position.set(scatterSide * 0.067, 0.035, MUZZLES[event.weapon]);
    }
    const pilot = this.pilots.get(event.id);
    if (pilot && !event.mounted && !event.vehicle) pilot.shotAt = time;
    this.effects.shot(event, this.weapon.muzzle, local && handheld, time);
  }

  collect(event) {
    const spec = WEAPONS[event.item] ?? POWERUPS[event.item];
    this.effects.pulse(event.position, color(spec.color), performance.now(), true);
  }

  burst(event) {
    this.effects.projectileBurst(event.from, performance.now(), event.weapon, event.alt);
  }

  move(event, local, accent) {
    const time = performance.now();
    if (local) this.motion = { action: event.action, time };
    const pilot = this.pilots.get(event.id);
    if (pilot && event.action !== 'step') {
      pilot.action = event.action;
      pilot.actionAt = time;
    }
    this.effects.movement(event.position, event.action, color(accent), time);
  }

  anticipate() {
    this.weapon.intent = performance.now();
  }

  eliminate(event, local, mode) {
    const dead = event.body;
    if (!dead) return;
    const time = performance.now();
    if (!local) this.effects.elimination(event, time, color(pilotColor(dead, mode)));
    const model = buildPilot(uniform(pilotColor(dead, mode)), false, dead.character);
    model.crouch = dead.crouch ? 1 : 0;
    model.root.rotation.y = dead.yaw;
    posePilot(model, { ...dead, grounded: false }, 0, performance.now());
    buildWeapon(model.weapon, dead.weapon);
    this.scene.add(model.root);
    const dx = dead.x - dead.from[0];
    const dz = dead.z - dead.from[2];
    const gap = Math.hypot(dx, dz) || 1;
    this.corpses.push({
      model, x: dead.x, y: dead.y, z: dead.z, yaw: dead.yaw,
      vx: dead.vx * 0.31 + dx / gap * 2.45, vy: Math.max(-8, dead.vy), vz: dead.vz * 0.31 + dz / gap * 2.45,
      tilt: 0, settledAt: null, startAt: time,
    });
  }

  animateCorpses(time, dt) {
    for (let i = this.corpses.length - 1; i >= 0; i--) {
      const corpse = this.corpses[i];
      const { model } = corpse;
      if (corpse.settledAt === null) {
        corpse.vy -= 16 * dt;
        const y = corpse.y + corpse.vy * dt;
        const x = corpse.x + corpse.vx * dt;
        const z = corpse.z + corpse.vz * dt;
        if (!blocked(x, corpse.z, 0.16, corpse.y, this.layout, 0.34)) corpse.x = x;
        if (!blocked(corpse.x, z, 0.16, corpse.y, this.layout, 0.34)) corpse.z = z;
        const floor = floorBelow(corpse.x, corpse.z, corpse.y, 0.15, this.layout);
        corpse.y = Math.max(y, floor);
        if (y <= floor) corpse.settledAt = time;
      } else if (time - corpse.settledAt > 1000) {
        model.root.removeFromParent();
        this.corpses.splice(i, 1);
        continue;
      }
      const phase = THREE.MathUtils.smoothstep(time - corpse.startAt, 0, 390);
      corpse.tilt = Math.max(phase * 1.1, corpse.settledAt === null ? 0
        : THREE.MathUtils.smoothstep(time - corpse.settledAt, 0, 330) * 1.57);
      model.body.position.y = -0.82;
      model.body.rotation.z = (1 - phase) * 0.13;
      model.root.position.set(corpse.x, corpse.y + THREE.MathUtils.lerp(0.82, 0.23, corpse.tilt / 1.57), corpse.z);
      model.root.rotation.set(-corpse.tilt, corpse.yaw, phase * 0.13, 'YXZ');

    }
  }

  wreck(event) {
    const time = performance.now();
    if (event.type === 'core-wreck') this.effects.coreCollapse(event.position, time, color(TEAMS[event.team].color));
    else this.effects.projectileBurst(event.position, time, 'grenade');
  }

  respawn(player, mode) {
    this.effects.respawn([player.x, player.y, player.z], performance.now(), color(pilotColor(player, mode)));
  }

  observe(view, own, time, dt, reducedMotion) {
    if (view?.event) {
      this.followTarget = null;
      const frame = deathFrame(view.event, view.progress, reducedMotion);
      if (view.progress >= 0.18 && !view.played) {
        view.played = true;
        this.effects.replay(view.event, time, color(TEAMS[own.team].color));
      }
      const position = cameraInCover(frame, this.layout);
      this.camera.position.set(position.x, position.y, position.z);
      this.camera.lookAt(frame.focus.x, frame.focus.y, frame.focus.z);
      return;
    }
    const target = view?.target ?? own;
    const model = this.pilots.get(target.id);
    const pose = model?.root.visible ? {
      x: model.root.position.x, y: model.root.position.y, z: model.root.position.z, yaw: model.root.rotation.y,
    } : { ...target };
    if (this.followTarget === target.id && !reducedMotion) {
      this.followYaw += wrapAngle(pose.yaw - this.followYaw) * (1 - Math.exp(-dt * 12));
    } else {
      this.followTarget = target.id;
      this.followYaw = pose.yaw;
    }
    pose.yaw = this.followYaw;
    const frame = followFrame(target, pose);
    const position = cameraInCover(frame, this.layout);
    this.camera.position.set(position.x, position.y, position.z);
    this.camera.lookAt(frame.focus.x, frame.focus.y, frame.focus.z);
  }

  vehicleCamera(vehicle, view) {
    const { camera, focus } = vehicleAimFrame({ ...vehicle, x: view.x, z: view.z }, view.yaw, view.pitch, this.layout);
    this.camera.position.set(camera.x, camera.y, camera.z);
    this.camera.lookAt(focus.x, focus.y, focus.z);
  }

  animateGarage(time, view, dt, blend, current) {
    for (const model of this.garage) {
      const target = current?.index === model.target?.index ? current : model.target;
      model.root.visible = target?.hp > 0;
      if (!model.root.visible) continue;
      const self = current === target;
      if (self) model.root.position.set(view.x, 0, view.z);
      else {
        model.root.position.x += (target.x - model.root.position.x) * blend;
        model.root.position.z += (target.z - model.root.position.z) * blend;
      }
      model.root.rotation.y += wrapAngle(target.heading - model.root.rotation.y) * blend;
      model.gun.rotation.y = wrapAngle((self ? view.yaw : target.yaw) - model.root.rotation.y);
      model.gun.rotation.x = self ? view.pitch : target.pitch;
      if (model.team !== target.team) {
        model.team = target.team;
        model.armor.color.set(TEAMS[model.team].color);
        model.armor.emissive.copy(model.armor.color);
      }
      model.hull.position.y = model.kind === 'skimmer' ? Math.sin(time * 0.007 + target.index * 2) * 0.055 + 0.035 : 0;
      model.lights.color.set(VEHICLES[model.kind].color).multiplyScalar(target.boosting ? 1.42 : 0.88);
      model.marker.visible = !target.driver && Math.hypot(target.x - view.x, target.z - view.z) < 13;
      model.wheels.forEach(wheel => { wheel.rotation.x += target.speed * dt * 2.4; });
      if (Math.abs(target.speed) > 6 && time - model.lastTrail > 100) {
        model.lastTrail = time;
        const direction = new THREE.Vector3(Math.sin(model.root.rotation.y), 0.09, Math.cos(model.root.rotation.y));
        const origin = model.root.position.clone().addScaledVector(direction, 1.43);
        origin.y = 0.24;
        this.effects.burst(origin, direction, target.boosting ? model.lights.color : this.dustTint, time, target.boosting ? 4 : 2);
      }
    }
  }

  render(time, view, own, motion, reducedMotion, vehicle = null, observation = null) {
    const width = this.canvas.clientWidth;
    const height = this.canvas.clientHeight;
    if (!width || !height) return;
    if (width !== this.width || height !== this.height) {
      this.width = width;
      this.height = height;
      this.renderer.setSize(width, height, false);
      this.camera.aspect = width / height;
      this.camera.updateProjectionMatrix();
    }
    const frameMs = time - this.previous;
    const dt = Math.max(0, Math.min(0.1, frameMs / 1000));
    const blend = 1 - Math.exp(-dt * 17);
    this.previous = time;
    if (frameMs > 0 && frameMs < 130) this.frameCost += (frameMs - this.frameCost) * 0.035;
    if (time >= this.resolutionCheck) {
      const floor = Math.min(this.maxResolution, 0.78);
      const step = this.frameCost > 22 ? -0.16 : this.frameCost < 17.2 ? 0.10 : 0;
      const next = THREE.MathUtils.clamp(this.resolution + step, floor, this.maxResolution);
      if (next !== this.resolution) {
        this.resolution = next;
        this.renderer.setPixelRatio(next);
      }
      this.resolutionCheck = time + (step > 0 ? 5900 : 2200);
    }
    const speed = Math.hypot(motion.vx ?? 0, motion.vz ?? 0);
    const lateral = (motion.vx ?? 0) * Math.cos(view.yaw) - (motion.vz ?? 0) * Math.sin(view.yaw);
    const active = own.hp > 0;
    const motionAge = Math.max(0, time - (this.motion?.time ?? -1000));
    const landing = this.motion?.action === 'land' ? Math.sin(Math.min(motionAge / 170, 1) * Math.PI) * 0.07 : 0;
    const running = active && motion.grounded;
    this.stride = (this.stride ?? 0) + (running ? speed * dt * 1.65 : 0);
    const stride = running && !reducedMotion ? Math.sin(this.stride * 2) * Math.min(0.024, speed * 0.003) : 0;
    const roll = active && !reducedMotion ? THREE.MathUtils.clamp(-lateral * 0.0024, -0.039, 0.039) : 0;
    this.eye = THREE.MathUtils.lerp(this.eye ?? eyeHeight(motion), eyeHeight(motion), blend);
    this.camera.position.set(view.x, view.y + (active ? this.eye + stride - (reducedMotion ? 0 : landing) : 0.77), view.z);
    this.camera.rotation.set(active ? view.pitch : 0.14, view.yaw, roll, 'YXZ');
    if (vehicle) this.vehicleCamera(vehicle, view);
    const wantedFov = vehicle ? 75 + (!reducedMotion && vehicle.boosting ? 6 : 0)
      : active && !reducedMotion ? 76 + Math.max(0, speed - 7.6) / SPRINT_SPEED * 5.4 + (motion.dodgeTime > 0 ? 3.5 : 0)
        : observation?.event ? 64 : 76;
    this.camera.fov = THREE.MathUtils.lerp(this.camera.fov, wantedFov, blend);
    this.camera.updateProjectionMatrix();
    const yawChange = this.lookYaw === undefined ? 0 : view.yaw - this.lookYaw;
    const pitchChange = this.lookPitch === undefined ? 0 : view.pitch - this.lookPitch;
    this.lookYaw = view.yaw;
    this.lookPitch = view.pitch;
    const targetSide = THREE.MathUtils.clamp(Math.atan2(Math.sin(yawChange), Math.cos(yawChange)) * -0.72, -0.054, 0.054);
    const targetVertical = THREE.MathUtils.clamp(pitchChange * -0.79, -0.055, 0.055);
    this.sideSway = THREE.MathUtils.lerp(this.sideSway ?? 0, targetSide, blend);
    this.verticalSway = THREE.MathUtils.lerp(this.verticalSway ?? 0, targetVertical, blend);
    const shotAge = this.weapon.lastWeapon === own.weapon ? time - this.weapon.lastShot : Infinity;
    const recoil = recoilPose(own.weapon, shotAge, this.weapon.alternate);
    const intent = shotAge < 75 ? 0 : Math.max(0, 1 - (time - (this.weapon.intent ?? -1000)) / 65);
    if (own.reload && this.weapon.reloading !== own.weapon) {
      const duration = WEAPONS[own.weapon].reload * 1000;
      this.weapon.reloadStarted = time - duration + (own.reloadRemaining ?? duration / 1000) * 1000;
    }
    this.weapon.reloading = own.reload ? own.weapon : null;
    const reloadProgress = own.reload ? (time - this.weapon.reloadStarted) / (WEAPONS[own.weapon].reload * 1000) : null;
    const reload = reloadPose(own.weapon, reloadProgress);
    const sprint = !reducedMotion && motion.grounded ? THREE.MathUtils.clamp((speed - 7.25) / 3.25, 0, 1) : 0;
    this.weapon.sprint = THREE.MathUtils.lerp(this.weapon.sprint ?? 0, sprint * (1 - recoil.flash), blend);
    const lowReady = this.weapon.sprint * (1 - 0.6 * reload.reach);
    const multiplier = reducedMotion ? 0.42 : 1;
    this.weapon.root.visible = active && own.turret < 0 && !vehicle;
    this.weapon.root.position.set(0.33 - 0.043 * lowReady + (reducedMotion ? 0 : this.sideSway + Math.sin(this.stride) * speed * 0.002),
      -0.262 - reload.pitch * 0.23 - lowReady * 0.091 + (reducedMotion ? 0 : this.verticalSway + stride * 0.55 - landing * 0.55),
      -0.49 + recoil.travel * multiplier + intent * 0.013);
    this.weapon.root.rotation.set(-reload.pitch - lowReady * 0.24 + recoil.pitch * multiplier, -0.048 + lowReady * 0.20,
      reload.roll - roll * 1.1 + recoil.roll * multiplier);
    this.weapon.support.position.set(-0.163 - reload.reach * 0.09, -0.177 - reload.reach * 0.118, -0.02 + reload.reach * 0.112);
    this.weapon.support.rotation.z = 0.21 + reload.reach * 0.36;
    this.weapon.equipment.forEach((mesh, i) => {
      const key = ARMORY[i];
      mesh.visible = key === own.weapon;
      const activity = 0.75 + Math.sin(time * 0.008 + i) * 0.18 + (mesh.visible ? recoil.flash * 0.65 : 0);
      weaponGlow[key].color.copy(weaponTint[key]).multiplyScalar(activity);
      if (!mesh.visible) return;
      const { action, feed, cells, status } = mesh.userData.mechanism;
      action.position.z = recoil.cycle;
      feed.position.set(reload.tray * -0.025, reload.tray * -0.145, reload.tray * 0.055);
      feed.rotation.x = reload.tray * -0.37;
      const rounds = own.loadout[i]?.[0] ?? 0;
      const lit = Math.ceil(cells.length * rounds / WEAPONS[key].magazine);
      cells.forEach((cell, j) => { cell.visible = j < lit || j === 0; });
      status.color.set(rounds ? weaponTint[key] : '#ff4b3d');
      if (mesh.userData.flow) {
        mesh.userData.flow.rotation.z = time * (key === 'rift' ? 0.008 : 0.013) + recoil.cycle * 7;
        mesh.userData.coilSkin.opacity = 0.42 + recoil.flash * 0.46;
      }
    });
    const remoteTime = this.snapshotClock.time(time);
    for (const model of this.pilots.values()) {
      const { root, target } = model;
      if (!root.visible) continue;
      if (presentedPose(model.history, remoteTime, model.presentation)) {
        const pose = model.presentation;
        root.position.set(pose.x, pose.y, pose.z);
        root.rotation.y = pose.yaw;
        if (model.marker) model.marker.rotation.y = view.yaw - root.rotation.y;
        posePilot(model, pose, dt, time, this.layout.terrain);
        continue;
      }
      root.position.x += (target.x - root.position.x) * blend;
      root.position.y += (target.y - root.position.y) * blend;
      root.position.z += (target.z - root.position.z) * blend;
      const angle = target.yaw - root.rotation.y;
      root.rotation.y += Math.atan2(Math.sin(angle), Math.cos(angle)) * blend;
      if (model.marker) model.marker.rotation.y = view.yaw - root.rotation.y;
      posePilot(model, target, dt, time, this.layout.terrain);
    }
    for (const { flag, banner } of this.objectives) {
      if (!flag.visible) continue;
      flag.rotation.y = 0.5 + Math.sin(time * 0.0018) * 0.35;
      const vertices = banner.geometry.attributes.position;
      for (let i = 0; i < vertices.count; i++) {
        const x = vertices.getX(i);
        vertices.setZ(i, Math.sin(x * 11 - time * 0.005 + flag.position.x * 0.1) * x * 0.15);
      }
      vertices.needsUpdate = true;
      banner.geometry.computeVertexNormals();
    }
    this.pickups.forEach(({ pad, object, highlight }, i) => {
      if (!pad.visible) return;
      highlight.opacity = 0.13 + Math.sin(time * 0.004 + i) * 0.06;
      object.position.y = 0.64 + Math.sin(time * 0.003 + i) * 0.095;
      object.rotation.y = time * 0.0016 + i;
    });
    this.animateGarage(time, view, dt, blend, vehicle);
    this.animateCorpses(time, dt);
    this.animateStation(time);
    this.siegeView?.render(time);
    if (!active && observation) this.observe(observation, own, time, dt, reducedMotion);
    else this.followTarget = null;
    this.effects.render(time, dt);
    const hideOpponents = !active && observation && !observation.target;
    const hidden = [];
    if (hideOpponents) {
      for (const model of [...this.pilots.values(), ...this.garage]) {
        if (!model.root.visible || (observation.team !== null && model.target?.team === observation.team)) continue;
        model.root.visible = false;
        hidden.push(model.root);
      }
    }
    this.renderer.render(this.scene, this.camera);
    for (const root of hidden) root.visible = true;
  }
}
