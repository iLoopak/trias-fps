import * as THREE from '/three.module.js';
import { ArenaEffects, SHOT_STYLE } from './effects.js';
import { ARMORY, BASES, COVER, EYE, MAP_SIZE, PICKUPS, POINTS, POWERUPS, TEAMS, WEAPONS } from './rules.js';

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
const helmetShell = new THREE.IcosahedronGeometry(0.65, 1);
const capsule = new THREE.CapsuleGeometry(0.5, 0.52, 4, 10);
const barrel = new THREE.CylinderGeometry(0.5, 0.5, 1, 10, 1).rotateX(Math.PI / 2);
const rim = new THREE.TorusGeometry(0.39, 0.11, 6, 14);
const plate = new THREE.MeshStandardMaterial({ color: '#a6afb2', roughness: 0.42, metalness: 0.32 });
const graphite = new THREE.MeshStandardMaterial({ color: '#171d25', roughness: 0.58, metalness: 0.50 });
const brass = new THREE.MeshStandardMaterial({ color: '#8f6647', roughness: 0.45, metalness: 0.51 });
const ceramic = new THREE.MeshStandardMaterial({ color: '#d8ddcf', roughness: 0.48, metalness: 0.20 });
const glass = new THREE.MeshStandardMaterial({ color: '#172e3b', roughness: 0.18, metalness: 0.48 });
const yellow = new THREE.MeshStandardMaterial({ color: '#9c7b37', roughness: 0.42, metalness: 0.34 });
const dark = new THREE.MeshStandardMaterial({ color: '#263037', roughness: 0.77, metalness: 0.28 });
const steel = new THREE.MeshStandardMaterial({ color: '#46555e', roughness: 0.65, metalness: 0.45 });
const black = new THREE.MeshStandardMaterial({ color: '#11191e', roughness: 0.84 });
const trim = new THREE.MeshStandardMaterial({ color: '#99adb3', roughness: 0.59, metalness: 0.43 });
const glow = value => new THREE.MeshBasicMaterial({ color: value });
const teamGlow = TEAMS.map(team => glow(team.color));
const whiteGlow = glow('#91c7d3');
const orange = glow('#ffac65');
const enemyGlow = glow('#ff735f');

function block(parent, material, w, h, d, x, y, z, geometry = box) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.scale.set(w, h, d);
  mesh.position.set(x, y, z);
  parent.add(mesh);
  return mesh;
}

function floorTexture(renderer) {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 256;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = '#2e393d';
  ctx.fillRect(0, 0, 256, 256);
  ctx.fillStyle = '#222c30';
  ctx.fillRect(0, 0, 256, 3);
  ctx.fillRect(0, 0, 3, 256);
  ctx.fillStyle = '#374348';
  ctx.fillRect(6, 6, 245, 1);
  ctx.fillRect(6, 6, 1, 245);
  ctx.strokeStyle = '#54616652';
  ctx.beginPath();
  ctx.moveTo(18, 20);
  ctx.lineTo(18, 224);
  ctx.lineTo(32, 240);
  ctx.lineTo(218, 240);
  ctx.lineTo(238, 220);
  ctx.lineTo(238, 33);
  ctx.stroke();
  ctx.fillStyle = '#171e20aa';
  for (let x = 25; x < 95; x += 8) ctx.fillRect(x, 19, 4, 3);
  for (let x = 165; x < 238; x += 8) ctx.fillRect(x, 233, 4, 3);
  ctx.fillStyle = '#424c50';
  for (const x of [13, 241]) {
    for (const y of [13, 241]) ctx.fillRect(x - 2, y - 2, 4, 4);
  }
  let seed = 269;
  for (let i = 0; i < 9000; i++) {
    seed = (Math.imul(1664525, seed) + 1013904223) >>> 0;
    ctx.fillStyle = seed & 65536 ? '#ffffff09' : '#00000014';
    ctx.fillRect(seed & 255, (seed >>> 8) & 255, 1 + (seed >>> 25) % 4, 1);
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(15, 15);
  texture.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
  return texture;
}

function orbit(scene) {
  const atmosphere = new THREE.ShaderMaterial({
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
        vec3 light = vec3(.5,.78,.97);
        gl_FragColor = vec4(ocean + clouds * sunlight * vec3(.11,.16,.22) + rim * light * (.17 + .33 * sunlight), 1.);
        #include <colorspace_fragment>
      }`,
  });
  const horizon = new THREE.Mesh(new THREE.SphereGeometry(114, 64, 40), atmosphere);
  horizon.position.set(-54, -97, 170);
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

function buildMap(scene, renderer) {
  const deck = floorTexture(renderer);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(MAP_SIZE * 2, MAP_SIZE * 2),
    new THREE.MeshStandardMaterial({ map: deck, bumpMap: deck, bumpScale: 0.028, roughness: 0.77, metalness: 0.16 }));
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);

  const panelMap = new THREE.TextureLoader().load('/bulkhead.jpg');
  panelMap.colorSpace = THREE.SRGBColorSpace;
  panelMap.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
  const panel = new THREE.MeshStandardMaterial({
    map: panelMap, color: '#d1d8df', bumpMap: panelMap, bumpScale: 0.047, roughness: 0.63, metalness: 0.32, emissive: '#25313b', emissiveIntensity: 0.36,
  });
  const batches = new Map();
  const add = (material, w, h, d, x, y, z, angle = 0) => {
    const matrix = new THREE.Matrix4().compose(new THREE.Vector3(x, y, z),
      new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), angle), new THREE.Vector3(w, h, d));
    if (!batches.has(material)) batches.set(material, []);
    batches.get(material).push(matrix);
  };
  for (const c of COVER) {
    const { x, z, h, w, d } = c;
    const light = teamGlow[c.sector] ?? whiteGlow;
    add(dark, w, h - 0.16, d, x, (h - 0.16) / 2, z);
    add(steel, w + 0.08, 0.16, d + 0.08, x, h - 0.08, z);
    add(black, w + 0.1, 0.25, d + 0.1, x, 0.126, z);
    for (const face of [-1, 1]) {
      add(panel, w * 0.87, h * 0.67, 0.046, x, h * 0.49, z + face * d / 2);
      add(panel, 0.046, h * 0.67, d * 0.87, x + face * w / 2, h * 0.49, z);
      add(light, w * 0.52, 0.045, 0.07, x, h * 0.75, z + face * d / 2);
      add(light, 0.06, 0.055, d * 0.53, x + face * w / 2, h * 0.75, z);
      for (const stripe of [-0.42, 0.42]) {
        add(steel, 0.13, h * 0.82, 0.08, x + w * stripe, h * 0.49, z + face * d / 2);
        add(steel, 0.08, h * 0.82, 0.13, x + face * w / 2, h * 0.49, z + d * stripe);
      }
    }
    if (c.terrace || c.relay) {
      const rail = c.relay ? whiteGlow : teamGlow[c.sector];
      for (const side of [-1, 1]) {
        add(rail, 0.048, 0.035, d - 0.15, x + side * (w / 2 - 0.12), h + 0.02, z);
        add(rail, w - 0.25, 0.035, 0.04, x, h + 0.021, z + side * (d / 2 - 0.12));
        add(steel, 0.11, h * 0.65, 0.13, x + w * 0.43, h * 0.38, z + side * d / 2);
        if (!c.relay) add(steel, 0.14, h * 0.65, 0.13, x - w * 0.43, h * 0.38, z + side * d / 2);
      }
    }
    if (c.relay) {
      for (const x of [-0.87, 0.87]) {
        add(whiteGlow, 0.07, 0.68, 0.07, x, h + 0.34, -0.82);
        add(steel, 0.15, 0.10, 0.15, x, h + 0.70, -0.82);
      }
    }
  }

  const hullMetal = new THREE.MeshStandardMaterial({ color: '#253543', roughness: 0.8, metalness: 0.16 });
  for (let side = 0; side < 4; side++) {
    const angle = side * Math.PI / 2;
    const edge = (material, w, h, d, x, y, z) =>
      add(material, w, h, d, x * Math.cos(angle) + z * Math.sin(angle), y, -x * Math.sin(angle) + z * Math.cos(angle), angle);
    edge(dark, 60, 0.87, 0.65, 0, 0.435, -30.31);
    edge(steel, 60, 0.18, 0.76, 0, 0.91, -30.31);
    edge(whiteGlow, 60, 0.025, 0.03, 0, 0.77, -29.98);
    edge(black, 60, 0.015, 0.38, 0, 0.012, -28.86);
    edge(hullMetal, 60, 1.3, 2.1, 0, -1.15, -30.9);
    edge(hullMetal, 54, 0.58, 1.4, 0, 5.8, -36.2);
    edge(steel, 53.5, 0.1, 0.92, 0, 5.48, -35.92);
    for (const post of [-26, -8.5, 8.5, 26]) {
      edge(hullMetal, 1.32, 8.3, 1.6, post, 1.7, -36.2);
      edge(steel, 1.7, 0.68, 1.62, post, 4.81, -36.2);
      edge(whiteGlow, 0.66, 0.053, 0.05, post, 4.82, -35.37);
      edge(orange, 0.23, 0.18, 0.23, post, 6.23, -36.28);
    }
    for (let i = -27; i <= 27; i += 6) {
      edge(steel, 0.17, 2.6, 0.32, i, 1.3, -30.08);
      edge(orange, 0.105, 0.155, 0.065, i, 2.54, -29.89);
      edge(black, 3.3, 0.18, 0.49, i, 0.49, -30);
      for (let j = -1.2; j <= 1.2; j += 0.6) edge(orange, 0.13, 0.018, 0.3, i + j, 0.019, -28.86);
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

function glyph(text) {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 128;
  const ctx = canvas.getContext('2d');
  ctx.translate(64, 64);
  ctx.rotate(Math.PI / 4);
  ctx.fillStyle = '#10191cf2';
  ctx.fillRect(-33, -33, 66, 66);
  ctx.strokeStyle = '#dfeff4';
  ctx.lineWidth = 3;
  ctx.strokeRect(-33, -33, 66, 66);
  ctx.rotate(-Math.PI / 4);
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 43px monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 0, 2);
  const map = new THREE.CanvasTexture(canvas);
  map.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map, depthTest: false, fog: false }));
  sprite.scale.set(1.52, 1.52, 1.52);
  sprite.renderOrder = 3;
  return sprite;
}

function buildObjectives(scene) {
  return TEAMS.map((team, i) => {
    const base = new THREE.Group();
    base.position.set(BASES[i].x, 0.025, BASES[i].z);
    base.add(ring(2.38, 0.055, teamGlow[i]), ring(2.13, 0.02, teamGlow[i]));
    scene.add(base);
    sectorDecal(scene, team, BASES[i]);

    const point = new THREE.Group();
    const indicator = new THREE.MeshBasicMaterial({ color: '#b0c0cb' });
    point.position.set(POINTS[i].x, 0.028, POINTS[i].z);
    point.add(ring(3.05, 0.075, indicator), ring(2.88, 0.025, indicator));
    const label = glyph(POINTS[i].label);
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

function buildPickups(scene) {
  return PICKUPS.map(item => {
    const spec = WEAPONS[item.type] ?? POWERUPS[item.type];
    const material = glow(spec.color);
    const pad = new THREE.Group();
    pad.position.set(item.x, item.y + 0.035, item.z);
    pad.add(ring(0.92, 0.047, material), ring(0.76, 0.026, material));
    const highlight = new THREE.MeshBasicMaterial({
      color: spec.color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.1,
    });
    const halo = ring(0.63, 0.27, highlight);
    halo.position.y = 0.004;
    pad.add(halo);
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
    scene.add(pad);
    return { pad, object, highlight };
  });
}

function pilot(material) {
  const root = new THREE.Group();
  const body = new THREE.Group();
  root.add(body);
  const torso = block(body, dark, 0.56, 0.49, 0.365, 0, 1.12, 0, beveled);
  block(body, steel, 0.49, 0.282, 0.165, 0, 1.169, -0.135, beveled);
  block(body, plate, 0.38, 0.074, 0.065, 0, 1.263, -0.208, beveled);
  block(body, graphite, 0.275, 0.122, 0.062, 0, 1.054, -0.189, beveled);
  for (const side of [-1, 1]) {
    block(body, plate, 0.11, 0.17, 0.082, side * 0.165, 1.129, -0.177, beveled).rotation.z = -side * 0.42;
    block(body, steel, 0.095, 0.32, 0.14, side * 0.147, 1.135, 0.169, beveled).rotation.z = side * 0.11;
  }
  block(body, black, 0.42, 0.26, 0.19, 0, 1.16, 0.168, beveled);
  block(body, material, 0.24, 0.035, 0.027, 0, 1.22, -0.215, beveled);
  block(body, material, 0.08, 0.16, 0.03, 0, 1.17, 0.264, beveled);
  block(body, black, 0.41, 0.15, 0.31, 0, 0.89, 0.015, beveled);
  const helmetGroup = new THREE.Group();
  helmetGroup.position.set(0, 1.57, 0.016);
  body.add(helmetGroup);
  const helmet = block(helmetGroup, plate, 0.34, 0.303, 0.33, 0, 0, 0, helmetShell);
  block(helmetGroup, graphite, 0.15, 0.059, 0.27, 0, 0.133, -0.042, beveled);
  for (const side of [-1, 1]) {
    block(helmetGroup, black, 0.068, 0.16, 0.177, side * 0.185, -0.022, 0.012, beveled);
    block(helmetGroup, material, 0.025, 0.055, 0.084, side * 0.223, -0.004, 0.005, beveled);
  }
  block(helmetGroup, glass, 0.33, 0.145, 0.14, 0, -0.004, -0.15, beveled);
  block(helmetGroup, material, 0.27, 0.07, 0.029, 0, 0.012, -0.222, beveled);
  block(helmetGroup, dark, 0.18, 0.066, 0.116, 0, -0.083, -0.162, beveled);
  const limbs = [];
  for (const side of [-1, 1]) {
    const shoulder = block(body, plate, 0.24, 0.175, 0.31, side * 0.34, 1.30, 0, beveled);
    shoulder.rotation.z = -side * 0.23;
    block(body, material, 0.11, 0.033, 0.21, side * 0.371, 1.383, 0, beveled);
    const arm = block(body, dark, 0.169, 0.239, 0.172, side * 0.365, 1.139, -0.06, capsule);
    arm.rotation.x = 0.43;
    const forearm = block(body, steel, 0.17, 0.16, 0.245, side * 0.31, 1.067, -0.224, beveled);
    forearm.rotation.y = -side * 0.28;
    const leg = new THREE.Group();
    leg.position.set(side * 0.151, 0.85, 0);
    body.add(leg);
    block(leg, dark, 0.21, 0.25, 0.22, 0, -0.18, 0, capsule);
    block(leg, graphite, 0.184, 0.16, 0.198, 0, -0.363, -0.016, beveled);
    block(leg, plate, 0.13, 0.094, 0.044, 0, -0.351, -0.126, beveled);
    block(leg, steel, 0.19, 0.27, 0.204, 0, -0.55, 0.02, beveled);
    block(leg, material, 0.06, 0.14, 0.026, 0, -0.51, -0.09, beveled);
    block(leg, dark, 0.222, 0.15, 0.34, 0, -0.76, -0.049, beveled);
    limbs.push(leg);
  }
  const weapon = new THREE.Group();
  weapon.position.set(0.23, 1.065, -0.39);
  weapon.scale.setScalar(0.92);
  body.add(weapon);
  torso.castShadow = helmet.castShadow = true;
  return { root, body, helmetGroup, weapon, limbs, phase: 0 };
}

const weaponGlow = Object.fromEntries(ARMORY.map(key => [key, glow(WEAPONS[key].color)]));

const MUZZLES = { pulse: -0.61, scatter: -0.52, lance: -0.78, arc: -0.49 };
const weaponModels = new Map();

function buildWeapon(parent, key) {
  if (weaponModels.has(key)) {
    parent.add(weaponModels.get(key).clone());
    return;
  }
  const assembly = new THREE.Group();
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

  if (key === 'pulse') {
    part(graphite, 0.18, 0.153, 0.55, 0, 0.015, -0.047);
    part(ceramic, 0.176, 0.084, 0.29, 0, 0.084, -0.075);
    part(black, 0.11, 0.104, 0.32, 0, 0.032, -0.364, barrel);
    muzzle(0, 0.034, -0.607, 0.10);
    part(steel, 0.144, 0.094, 0.156, 0, 0.017, -0.426);
    part(light, 0.049, 0.023, 0.03, 0, 0.143, -0.183);
    part(dark, 0.093, 0.186, 0.14, 0, -0.092, -0.115).rotation.x = -0.18;
    for (const side of [-1, 1]) {
      part(black, 0.019, 0.086, 0.255, side * 0.091, 0.024, -0.148);
      for (let i = 0; i < 4; i++) {
        part(light, 0.02, 0.041, 0.024, side * 0.102, 0.03, -0.08 - i * 0.042).rotation.x = 0.31;
      }
    }
  } else if (key === 'scatter') {
    part(graphite, 0.245, 0.172, 0.46, 0, 0.012, -0.022);
    part(brass, 0.225, 0.099, 0.25, 0, 0.093, -0.066);
    part(steel, 0.223, 0.109, 0.225, 0, -0.051, -0.265);
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
    for (const side of [-1, 1]) {
      part(steel, 0.035, 0.053, 0.42, side * 0.067, 0.02, -0.462);
      part(light, 0.014, 0.02, 0.26, side * 0.09, 0.022, -0.434);
    }
  } else {
    part(graphite, 0.22, 0.174, 0.41, 0, 0.014, -0.062);
    part(yellow, 0.241, 0.11, 0.27, 0, 0.094, -0.081);
    part(steel, 0.19, 0.155, 0.094, 0, 0.035, -0.328);
    muzzle(0, 0.034, -0.478, 0.123);
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
    model.visible = false;
    root.add(model);
    return model;
  });
  for (const arm of [
    { x: 0.065, y: -0.234, z: 0.24, yaw: -0.20 },
    { x: -0.163, y: -0.177, z: -0.02, yaw: 0.35 },
  ]) {
    const forearm = new THREE.Group();
    forearm.position.set(arm.x, arm.y, arm.z);
    forearm.rotation.set(0.28, arm.yaw, arm.x > 0 ? -0.20 : 0.21);
    root.add(forearm);
    block(forearm, graphite, 0.19, 0.155, 0.46, 0, 0, 0.055, beveled);
    block(forearm, steel, 0.132, 0.06, 0.19, 0, 0.073, 0.105, beveled);
    block(forearm, plate, 0.129, 0.045, 0.074, 0, 0.063, -0.047, beveled);
    block(forearm, black, 0.134, 0.11, 0.17, 0, 0.031, -0.186, beveled);
  }
  camera.add(root);
  return { root, equipment, muzzle, lastShot: -1000 };
}

export class Arena {
  constructor(canvas) {
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 1.6));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.38;
    this.canvas = canvas;
    this.scene = new THREE.Scene();
    this.scene.background = color('#070d18');
    this.scene.fog = new THREE.FogExp2('#0a1321', 0.017);
    this.camera = new THREE.PerspectiveCamera(76, 1, 0.034, 300);
    this.camera.rotation.order = 'YXZ';
    this.scene.add(this.camera);
    const ambient = new THREE.HemisphereLight('#c7e8ff', '#51616a', 2.1);
    const bounce = new THREE.DirectionalLight('#7e9dd0', 0.85);
    bounce.position.set(15, 12, -21);
    const sun = new THREE.DirectionalLight('#cce5ff', 2.4);
    sun.position.set(-16, 29, 12);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, { left: -42, right: 42, top: 42, bottom: -42, near: 1, far: 77 });
    sun.shadow.camera.updateProjectionMatrix();
    sun.shadow.normalBias = 0.055;
    this.scene.add(ambient, bounce, sun);
    orbit(this.scene);
    buildMap(this.scene, this.renderer);
    this.objectives = buildObjectives(this.scene);
    this.pickups = buildPickups(this.scene);
    this.weapon = viewModel(this.camera);
    this.effects = new ArenaEffects(this.scene, this.camera, this.weapon.muzzle);
    this.pilots = new Map();
    this.previous = performance.now();
  }

  snapshot(state, own, mode) {
    const active = new Set();
    for (const player of state.players) {
      if (player.id === own.id) continue;
      active.add(player.id);
      const material = mode === 'ffa' ? enemyGlow : teamGlow[player.team];
      if (!this.pilots.has(player.id)) {
        const model = pilot(material);
        this.pilots.set(player.id, model);
        this.scene.add(model.root);
      }
      const model = this.pilots.get(player.id);
      if (model.spawn !== player.spawn) {
        model.root.position.set(player.x, player.y, player.z);
        model.root.rotation.y = player.yaw;
        if (model.spawn !== undefined) this.effects.pulse([player.x, player.y, player.z], material.color, performance.now());
        model.spawn = player.spawn;
      }
      if (model.activeWeapon !== player.weapon) {
        model.weapon.clear();
        buildWeapon(model.weapon, player.weapon);
        model.activeWeapon = player.weapon;
      }
      model.target = player;
      model.root.visible = player.hp > 0;
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
      if (flag) objective.flag.position.set(flag.x, flag.status === 'carried' ? flag.y + 1.05 : 0.03, flag.z);
    });
    this.pickups.forEach((item, i) => { item.pad.visible = state.pickups[i]; });
  }

  shot(event, local) {
    const time = performance.now();
    if (local) {
      this.weapon.lastShot = time;
      this.weapon.muzzle.position.set(0, 0.035, MUZZLES[event.weapon]);
    }
    this.effects.shot(event, this.weapon.muzzle, local, time);
  }

  collect(event) {
    const spec = WEAPONS[event.item] ?? POWERUPS[event.item];
    this.effects.pulse(event.position, color(spec.color), performance.now(), true);
  }

  render(time, view, own, moving) {
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
    const dt = Math.min(0.1, (time - this.previous) / 1000);
    const blend = 1 - Math.exp(-dt * 17);
    this.previous = time;
    this.camera.position.set(view.x, view.y + (own.hp ? EYE + (moving && own.grounded ? Math.sin(time * 0.014) * 0.026 : 0) : 0.77), view.z);
    this.camera.rotation.set(own.hp ? view.pitch : 0.14, view.yaw, 0, 'YXZ');
    const elapsed = time - this.weapon.lastShot;
    const shot = SHOT_STYLE[own.weapon];
    const recoil = Math.max(0, 1 - elapsed / shot.recover) ** 2;
    this.weapon.lowered = THREE.MathUtils.lerp(this.weapon.lowered ?? 0, own.reload ? 1 : 0, 1 - Math.exp(-dt * 14));
    this.weapon.root.visible = own.hp > 0;
    this.weapon.root.position.set(0.33 + (moving ? Math.sin(time * 0.008) * 0.019 : 0),
      -0.262 - this.weapon.lowered * 0.14 + Math.sin(time * (moving ? 0.016 : 0.003)) * 0.009, -0.49 + recoil * shot.recoil);
    this.weapon.root.rotation.set(-0.50 * this.weapon.lowered + recoil * 0.11, -0.048, -0.19 * this.weapon.lowered);
    this.weapon.equipment.forEach((mesh, i) => { mesh.visible = ARMORY[i] === own.weapon; });
    for (const model of this.pilots.values()) {
      const { root, target } = model;
      if (!root.visible) continue;
      const distance = Math.hypot(root.position.x - target.x, root.position.z - target.z);
      root.position.x += (target.x - root.position.x) * blend;
      root.position.y += (target.y - root.position.y) * blend;
      root.position.z += (target.z - root.position.z) * blend;
      const angle = target.yaw - root.rotation.y;
      root.rotation.y += Math.atan2(Math.sin(angle), Math.cos(angle)) * blend;
      model.phase += dt * 15;
      model.helmetGroup.rotation.x = target.pitch * 0.45;
      model.weapon.rotation.x = target.pitch * 0.58;
      model.body.position.y = target.grounded ? Math.abs(Math.sin(model.phase)) * Math.min(0.035, distance * 0.09) : 0;
      model.body.rotation.x = target.grounded ? 0 : 0.07;
      model.limbs.forEach((leg, i) => { leg.rotation.x = target.grounded ? Math.sin(model.phase + i * Math.PI) * Math.min(0.66, distance * 2.8) : (i ? -0.39 : 0.23); });
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
    this.effects.render(time);
    this.renderer.render(this.scene, this.camera);
  }
}
