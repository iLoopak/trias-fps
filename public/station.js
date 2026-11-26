import * as THREE from '/three.module.js';
import { TEAMS, radial } from './rules.js';

const TAU = Math.PI * 2;
const box = new THREE.BoxGeometry();
const joint = new THREE.CylinderGeometry(0.5, 0.62, 1, 6);
const shell = new THREE.MeshStandardMaterial({ color: '#273341', roughness: 0.54, metalness: 0.28 });
const frame = new THREE.MeshStandardMaterial({ color: '#616d7d', roughness: 0.48, metalness: 0.30 });
const casing = new THREE.MeshStandardMaterial({ color: '#c2b6a6', roughness: 0.54, metalness: 0.21 });
const graphite = new THREE.MeshStandardMaterial({ color: '#121c29', roughness: 0.7 });
const shared = new THREE.MeshBasicMaterial({ color: '#ff779e', toneMapped: false });
const plasma = new THREE.MeshBasicMaterial({ color: '#fff0da', toneMapped: false });

function mesh(parent, geometry, material, position, scale) {
  const object = new THREE.Mesh(geometry, material);
  object.position.set(...position);
  if (scale) object.scale.set(...scale);
  parent.add(object);
  return object;
}

function strut(parent, material, start, end, thickness, depth = thickness) {
  const a = new THREE.Vector3(...start);
  const d = new THREE.Vector3(...end).sub(a);
  const beam = mesh(parent, box, material, a.addScaledVector(d, 0.5).toArray(), [thickness, d.length(), depth]);
  beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize());
  return beam;
}

function arc(parent, material, radius, width, start, end, height) {
  const part = mesh(parent, new THREE.RingGeometry(radius - width, radius, 48, 1, start, end - start),
    material, [0, height, 0]);
  part.rotation.x = -Math.PI / 2;
  return part;
}

function deckCircuit(scene, layout) {
  const steelPaint = new THREE.MeshBasicMaterial({ color: '#526078', toneMapped: false });
  for (const [i, team] of TEAMS.entries()) {
    const glow = new THREE.MeshBasicMaterial({ color: new THREE.Color(team.color).multiplyScalar(0.57), toneMapped: false });
    const paint = new THREE.MeshBasicMaterial({ color: new THREE.Color(team.color).multiplyScalar(0.097), toneMapped: false });
    const lanes = new THREE.Group();
    lanes.rotation.y = team.angle;
    scene.add(lanes);
    for (let radius = 6; radius <= 24; radius += 3) {
      for (const side of [-1, 1]) {
        const panel = mesh(lanes, box, paint, [side * 2.75, 0.014, radius], [0.56, 0.006, 1.7]);
        panel.rotation.y = side * (i === 2 ? 0.58 : 0.30);
        mesh(lanes, box, glow, [side * 1.7, 0.021, radius], [0.065, 0.008, 1.67]);
      }
    }
    const t = team.angle - Math.PI / 2;
    arc(scene, paint, 12.4, 2.04, t - 0.42, t + 0.42, 0.016);
    arc(scene, glow, 12.4, 0.07, t - 0.42, t + 0.42, 0.020);
    arc(scene, steelPaint, 7.14, 0.10, t - 0.47, t + 0.47, 0.023);
    const base = layout.bases[i];
    const baseCircuit = new THREE.Group();
    baseCircuit.position.set(base.x, 0, base.z);
    baseCircuit.rotation.y = team.angle;
    scene.add(baseCircuit);
    arc(baseCircuit, paint, 3.58, 0.84, 0, TAU, 0.012);
    for (let spokes = 0; spokes < 6; spokes++) {
      const angle = spokes * TAU / 6;
      arc(baseCircuit, glow, 3.48, 0.09, angle + 0.11, angle + 0.66, 0.02);
    }
  }
  for (let i = 0; i < 24; i++) {
    const angle = i * TAU / 24;
    const length = i % 3 === 0 ? 0.26 : 0.13;
    const plate = mesh(scene, box, i % 3 === 0 ? shared : steelPaint,
      [Math.sin(angle) * 7.36, 0.019, Math.cos(angle) * 7.36], [0.09, 0.008, length]);
    plate.rotation.y = angle;
  }
}

function reactor(scene) {
  const group = new THREE.Group();
  group.position.y = 13.1;
  scene.add(group);
  const shellGeometry = new THREE.IcosahedronGeometry(2.7, 1);
  const core = mesh(group, new THREE.IcosahedronGeometry(1.36, 2), plasma, [0, 0, 0]);
  const cage = new THREE.Group();
  cage.rotation.z = -0.39;
  group.add(cage);
  mesh(cage, shellGeometry, new THREE.MeshBasicMaterial({ color: '#ff97bd', wireframe: true,
    transparent: true, opacity: 0.31, toneMapped: false }), [0, 0, 0], [0.86, 0.86, 0.86]);
  const outerRings = [];
  for (const [radius, tilt, spin, metal] of [[3.75, 0.46, 0.08, shell], [4.35, -0.64, -0.05, frame], [3.31, 1.03, -0.12, graphite]]) {
    const ring = new THREE.Group();
    ring.rotation.set(tilt, 0.10, tilt * 0.45);
    group.add(ring);
    const track = new THREE.Group();
    ring.add(track);
    const segments = 15;
    for (let i = 0; i < segments; i++) {
      if (i === 1 || i === 2 || (radius > 4 && i === 3)) continue;
      const angle = i / segments * TAU;
      const sector = TAU / segments * 0.94;
      mesh(track, new THREE.TorusGeometry(radius, radius > 4 ? 0.31 : 0.24, 4, 4, sector), metal, [0, 0, 0]).rotation.z = angle;
      const rail = mesh(track, new THREE.TorusGeometry(radius, 0.075, 4, 5, sector * 0.86), shared, [0, 0, 0.24]);
      rail.rotation.z = angle;
    }
    outerRings.push({ track, spin });
  }
  for (let i = 0; i < 8; i++) {
    const angle = i * TAU / 8;
    const shard = mesh(cage, new THREE.IcosahedronGeometry(0.65, 0), i % 2 ? shell : casing,
      [Math.cos(angle) * 3.01, Math.sin(angle * 3) * 1.6, Math.sin(angle) * 3.01], [0.64, 1.78, 0.74]);
    shard.rotation.set(angle, angle * 3.7, angle / 3);
  }
  const beam = mesh(scene, new THREE.CylinderGeometry(0.22, 0.48, 5.85, 12, 1, true), new THREE.MeshBasicMaterial({
    color: '#ff90bf', blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    transparent: true, opacity: 0.14, depthWrite: false, toneMapped: false,
  }), [0, 8.45, 0]);
  mesh(scene, joint, shell, [0, 4.25, 0], [1.25, 0.35, 1.25]);
  return { core, cage, outerRings, beam };
}

function sectors(scene, layout) {
  const animated = [];
  for (const [i, team] of TEAMS.entries()) {
    const lightColor = new THREE.Color(team.color);
    const marker = new THREE.MeshBasicMaterial({ color: lightColor, toneMapped: false });
    const painted = new THREE.MeshStandardMaterial({
      color: lightColor.clone().multiplyScalar(0.50), emissive: lightColor, emissiveIntensity: 0.16, roughness: 0.56,
    });
    const root = new THREE.Group();
    root.rotation.y = team.angle;
    scene.add(root);
    const boundary = layout.length / Math.max(Math.abs(Math.sin(team.angle)), Math.abs(Math.cos(team.angle)));
    const z = boundary + 4.5;
    const fillPos = radial(team.angle, 20);
    const light = new THREE.PointLight(team.color, 33, 19, 2);
    light.position.set(fillPos.x, 4.4, fillPos.z);
    scene.add(light);
    for (const side of [-1, 1]) {
      mesh(root, box, graphite, [side * 9, 1.0, z], [3.4, 5.5, 3.1]);
      mesh(root, box, painted, [side * 8.08, 4.17, z - 0.22], [0.43, 4.0, 1.7]);
      strut(root, frame, [side * 10.0, 2.8, z], [side * 6.3, 8.0, z], 0.28, 0.33);
      strut(root, marker, [side * 9.85, 3.02, z - 0.25], [side * 6.55, 7.69, z - 0.25], 0.08, 0.10);
    }
    if (i === 0) {
      const rotor = new THREE.Group();
      rotor.position.set(0, 7.9, z + 2.1);
      root.add(rotor);
      mesh(rotor, new THREE.TorusGeometry(5.65, 0.48, 5, 40), graphite, [0, 0, 0]);
      mesh(rotor, new THREE.TorusGeometry(5.46, 0.12, 5, 40), marker, [0, 0, -0.39]);
      const fan = new THREE.Group();
      fan.userData.moving = true;
      rotor.add(fan);
      mesh(fan, new THREE.CylinderGeometry(0.86, 1.06, 1.2, 10).rotateX(Math.PI / 2), casing, [0, 0, 0]);
      for (let j = 0; j < 7; j++) {
        const spin = new THREE.Group();
        spin.rotation.z = j / 7 * TAU;
        fan.add(spin);
        mesh(spin, box, painted, [-0.42, 2.82, -0.03], [0.84, 3.82, 0.51]).rotation.z = 0.29;
      }
      animated.push(fan);
    } else if (i === 1) {
      for (const x of [-4.9, 0, 4.9]) {
        const top = x ? 9.0 : 13.1;
        mesh(root, box, graphite, [x, top / 2, z + 1.0], [3.1, top, 3.05]);
        mesh(root, box, marker, [x, top - 0.2, z - 0.65], [2.87, 0.11, 0.16]);
        for (let y = 3; y < top; y += 0.77) {
          mesh(root, box, painted, [x, y, z - 0.64], [2.84, 0.28, 0.42]).rotation.x = -0.41;
        }
      }
    } else {
      for (const [x, top] of [[-5, 13], [0, 16], [5, 10]]) {
        const tower = mesh(root, joint, graphite, [x, top / 2, z + 0.87], [2.15, top, 2.15]);
        tower.rotation.y = Math.PI / 6;
        strut(root, marker, [x, top * 0.35, z - 0.17], [x, top + 1.8, z + 0.81], 0.11, 0.12);
        mesh(root, box, painted, [x, top * 0.68, z + 0.86], [2.88, 0.32, 2.89]).rotation.y = Math.PI / 4;
        mesh(root, new THREE.OctahedronGeometry(0.53, 0), marker, [x, top + 2.0, z + 0.87]);
      }
    }
  }
  return animated;
}

function flightDecks(scene, layout) {
  const deckMetal = new THREE.MeshStandardMaterial({ color: '#566373', metalness: 0.48, roughness: 0.46 });
  const hazard = new THREE.MeshBasicMaterial({ color: '#ffd191', toneMapped: false });
  for (const [i, deck] of [...layout.gantries, ...layout.cover.filter(item => item.duct)].entries()) {
    const root = new THREE.Group();
    root.position.set(deck.x, deck.h, deck.z);
    root.rotation.y = deck.angle;
    scene.add(root);
    const accent = new THREE.MeshBasicMaterial({ color: TEAMS[deck.sector].color, toneMapped: false });
    const thickness = deck.h - deck.bottom;
    mesh(root, box, shell, [0, -thickness * 0.53, 0], [deck.w - 0.04, thickness * 0.94, deck.d - 0.05]);
    mesh(root, box, deckMetal, [0, -0.05, 0], [deck.w, 0.10, deck.d]);
    for (const side of [-1, 1]) {
      mesh(root, box, graphite, [0, -0.28, side * (deck.d / 2 - 0.06)], [deck.w - 0.06, 0.25, 0.11]);
      mesh(root, box, accent, [0, -0.19, side * (deck.d / 2 - 0.026)], [deck.w - 0.8, 0.045, 0.047]);
      mesh(root, box, accent, [side * (deck.w / 2 - 0.09), 0.012, 0], [0.066, 0.022, deck.d - 0.31]);
      for (let x = -deck.w / 2 + 0.42; x < deck.w / 2 - 0.35; x += 0.40) {
        mesh(root, box, i % 2 ? hazard : graphite, [x, 0.012, side * (deck.d / 2 - 0.13)], [0.19, 0.022, 0.095])
          .rotation.y = Math.PI / 7;
      }
      for (const offset of [-0.44, 0.44]) {
        const x = deck.w * offset;
        mesh(root, box, frame, [x, -0.225, side * (deck.d / 2 - 0.054)], [0.25, 0.43, 0.10]);
      }
    }
    if (!deck.duct && i % 2 === 0) {
      const suspension = new THREE.MeshBasicMaterial({
        color: accent.color, transparent: true, opacity: 0.25, blending: THREE.AdditiveBlending,
        depthWrite: false, toneMapped: false,
      });
      for (const side of [-1, 1]) {
        const offset = side * (deck.w / 2 - 0.26);
        mesh(root, joint, deckMetal, [offset, -0.04, 0], [0.55, 0.18, 0.55]);
        mesh(root, joint, graphite, [offset, 3.29, 0], [0.82, 0.36, 0.82]);
        mesh(root, joint, accent, [offset, 3.14, 0], [0.52, 0.068, 0.52]);
        mesh(root, new THREE.CylinderGeometry(0.086, 0.21, 3.17, 7, 1, true), suspension, [offset, 1.58, 0]);
      }
    }
    if (!deck.duct && i % 2 === 0) {
      const wash = new THREE.PointLight(TEAMS[deck.sector].color, 10.5, 6.6, 2);
      wash.position.set(deck.x, deck.bottom - 0.55, deck.z);
      scene.add(wash);
    }
  }

  return layout.launchPads.map(pad => {
    const root = new THREE.Group();
    root.position.set(pad.x, pad.y, pad.z);
    root.rotation.y = pad.angle;
    scene.add(root);
    const rim = new THREE.CylinderGeometry(1.02, 1.13, 0.12, 8);
    mesh(root, rim, graphite, [0, 0.065, 0]);
    mesh(root, new THREE.CylinderGeometry(0.865, 0.925, 0.08, 8), deckMetal, [0, 0.12, 0]);
    arc(root, hazard, 1.01, 0.071, 0, TAU, 0.151);
    const luminous = new THREE.MeshBasicMaterial({
      color: '#ffbb75', blending: THREE.AdditiveBlending, transparent: true, opacity: 0.45, depthWrite: false, toneMapped: false,
    });
    const air = new THREE.Group();
    root.add(air);
    const marker = new THREE.Shape();
    marker.moveTo(-0.43, 0);
    marker.lineTo(0, 0.28);
    marker.lineTo(0.43, 0);
    marker.lineTo(0.43, -0.13);
    marker.lineTo(0, 0.13);
    marker.lineTo(-0.43, -0.13);
    marker.closePath();
    const arrow = new THREE.ShapeGeometry(marker);
    for (const z of [-0.42, 0, 0.42]) {
      mesh(root, arrow, hazard, [0, 0.168, z]).rotation.x = -Math.PI / 2;
    }
    for (const x of [-0.48, 0.48]) {
      const flow = mesh(air, arrow, luminous, [x, 0.88, 0.28]);
      flow.rotation.y = x < 0 ? -0.48 : 0.48;
    }
    return { air, luminous };
  });
}

function batchArchitecture(root) {
  root.updateWorldMatrix(true, true);
  const groups = new Map();
  const collect = object => {
    if (object.userData.moving) return;
    if (object.isMesh && object.geometry === box) {
      if (!groups.has(object.material)) groups.set(object.material, []);
      groups.get(object.material).push(object);
    }
    for (const child of object.children) collect(child);
  };
  collect(root);
  for (const [material, items] of groups) {
    if (items.length < 3) continue;
    const merged = new THREE.InstancedMesh(box, material, items.length);
    items.forEach((item, i) => {
      merged.setMatrixAt(i, item.matrixWorld);
      item.removeFromParent();
    });
    root.add(merged);
  }
}

function smelter(scene, layout) {
  const heat = new THREE.MeshBasicMaterial({ color: '#ff8b36', toneMapped: false });
  const whiteHeat = new THREE.MeshBasicMaterial({ color: '#ffda92', toneMapped: false });
  const oxidized = new THREE.MeshStandardMaterial({ color: '#87523b', metalness: 0.42, roughness: 0.69 });
  const vat = new THREE.CylinderGeometry(3.1, 2.55, 3.3, 9);
  mesh(scene, vat, oxidized, [0, 7.3, 0]);
  mesh(scene, new THREE.CylinderGeometry(2.1, 2.6, 1.5, 9), graphite, [0, 9.5, 0]);
  mesh(scene, new THREE.TorusGeometry(2.80, 0.17, 5, 28), heat, [0, 5.86, 0]).rotation.x = Math.PI / 2;
  const flow = mesh(scene, new THREE.CylinderGeometry(0.67, 0.40, 2.34, 11), new THREE.MeshBasicMaterial({
    color: '#ff9237', transparent: true, opacity: 0.30, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
  }), [0, 4.41, 0]);
  const spill = new THREE.PointLight('#ff7737', 40, 14, 2);
  spill.position.set(0, 4.6, 0);
  scene.add(spill);
  const coals = [];
  for (const [i, team] of TEAMS.entries()) {
    const root = new THREE.Group();
    root.rotation.y = team.angle;
    scene.add(root);
    const accents = new THREE.MeshBasicMaterial({ color: team.color, toneMapped: false });
    for (const side of [-1, 1]) {
      const offset = side * (layout.length + 4.3);
      const vents = new THREE.CylinderGeometry(0.92, 1.28, 10.5 + i, 7);
      mesh(root, vents, graphite, [offset, 5.4, 8.3 + side * 3]);
      for (const y of [2.75, 5.3, 8.6]) {
        mesh(root, new THREE.TorusGeometry(1.02, 0.13, 4, 15), oxidized, [offset, y, 8.3 + side * 3]).rotation.x = Math.PI / 2;
      }
      mesh(root, box, accents, [offset - side * 0.87, 7.6, 8.1 + side * 3], [0.05, 0.45, 0.40]);
    }
    for (let j = 0; j < 11; j++) {
      const material = j % 3 === 0 ? whiteHeat : heat;
      const coal = mesh(root, box, material, [4.65, 0.021, 7.6 + j * 1.65], [0.10, 0.014, 0.40]);
      coal.rotation.y = -0.62;
    }
    for (const side of [-1, 1]) {
      strut(root, oxidized, [side * 2.16, 7.7, 1.47], [side * 17.2, 11.65, 29.5], 0.34, 0.46);
      strut(root, frame, [side * 1.90, 8.05, 1.15], [side * 13.6, 12.65, 31.3], 0.18, 0.26);
    }
    const flares = new THREE.OctahedronGeometry(0.043, 0);
    for (let j = 0; j < 8; j++) {
      const particle = mesh(scene, flares, j % 2 ? heat : whiteHeat, [0, 3.8, 0]);
      coals.push({ particle, angle: j * Math.PI / 4 + i * 1.44, phase: (j * 0.371 + i * 0.11) % 1 });
    }
  }
  return seconds => {
    flow.material.opacity = 0.21 + Math.sin(seconds * 4.1) * 0.08;
    spill.intensity = 37 + Math.sin(seconds * 3.1) * 6;
    for (const { particle, angle, phase } of coals) {
      const age = (seconds * 0.52 + phase) % 1;
      const spread = 0.29 + age * 1.4;
      particle.position.set(Math.cos(angle) * spread, 3.48 + age * (1.7 + phase * 3), Math.sin(angle) * spread);
      particle.scale.setScalar(1 - age * 0.65);
    }
  };
}

function basinWorks(scene, layout) {
  const road = new THREE.MeshStandardMaterial({ color: '#202721', roughness: 0.91, metalness: 0.07 });
  const shade = new THREE.MeshStandardMaterial({ color: '#393338', roughness: 0.94 });
  const stripe = new THREE.MeshBasicMaterial({ color: '#aaafa1', toneMapped: false });
  const yellow = new THREE.MeshBasicMaterial({ color: '#ffcc82', toneMapped: false });
  const green = new THREE.MeshBasicMaterial({ color: '#93fdae', toneMapped: false });
  const drill = new THREE.MeshStandardMaterial({ color: '#9b6350', roughness: 0.65, metalness: 0.34 });
  const gold = new THREE.MeshBasicMaterial({ color: '#ff9e6e', toneMapped: false });

  const roadRadius = layout.roadRadius;
  arc(scene, road, roadRadius + 4.15, 8.3, 0, TAU, 0.009);
  arc(scene, stripe, roadRadius + 3.8, 0.067, 0, TAU, 0.016);
  arc(scene, stripe, roadRadius - 3.8, 0.067, 0, TAU, 0.016);
  for (let i = 0; i < 63; i++) {
    const start = i / 63 * TAU;
    arc(scene, yellow, roadRadius, 0.087, start, start + 0.052, 0.018);
  }

  const drillHead = new THREE.Group();
  drillHead.position.y = 10.7;
  scene.add(drillHead);
  mesh(drillHead, new THREE.CylinderGeometry(2.78, 4.5, 3.2, 8), graphite, [0, 1.32, 0]);
  mesh(drillHead, new THREE.CylinderGeometry(3.72, 2.86, 2.41, 8), drill, [0, -0.86, 0]);
  mesh(drillHead, new THREE.TorusGeometry(3.07, 0.13, 6, 26), gold, [0, -1.94, 0]).rotation.x = Math.PI / 2;
  const prism = mesh(scene, new THREE.IcosahedronGeometry(1.78, 0), gold, [0, 17.2, 0], [0.61, 1.46, 0.61]);
  const extraction = mesh(scene, new THREE.CylinderGeometry(0.56, 0.87, 5.1, 9, 1, true), new THREE.MeshBasicMaterial({
    color: '#ffae7a', transparent: true, opacity: 0.24, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
  }), [0, 6.22, 0]);
  const stationLight = new THREE.PointLight('#ff9860', 68, 23.0, 2);
  stationLight.position.set(0, 6, 0);
  scene.add(stationLight);

  for (const [index, team] of TEAMS.entries()) {
    const root = new THREE.Group();
    root.rotation.y = team.angle;
    scene.add(root);
    const marker = new THREE.MeshBasicMaterial({ color: team.color, toneMapped: false });
    const canopyGround = new THREE.MeshStandardMaterial({
      color: new THREE.Color(team.color).multiplyScalar(0.16), roughness: 0.88,
    });
    const turn = new THREE.Group();
    turn.rotation.y = -0.53;
    root.add(turn);
    mesh(turn, box, road, [0, 0.012, 29], [6.95, 0.014, 25.4]);
    for (const border of [-3.3, 3.3]) mesh(turn, box, stripe, [border, 0.024, 29], [0.07, 0.014, 25.0]);
    for (let j = 0; j < 7; j++) mesh(turn, box, yellow, [0, 0.024, 22 + j * 2.85], [0.08, 0.015, 1.26]);

    const yard = new THREE.Group();
    yard.position.set(0, 0, 23.6);
    root.add(yard);
    arc(yard, canopyGround, 4.12, 3.9, 0, TAU, 0.012);
    arc(yard, marker, 4.11, 0.063, 0, TAU, 0.018);
    const relay = new THREE.Group();
    relay.position.set(-3.28, 0, 1.09);
    relay.rotation.y = 0.52;
    yard.add(relay);
    mesh(relay, joint, graphite, [0, 0.12, 0], [0.73, 0.25, 0.68]);
    mesh(relay, box, frame, [0, 1.12, 0], [0.18, 2.02, 0.22]);
    mesh(relay, box, marker, [-0.07, 1.35, -0.12], [0.062, 1.05, 0.03]);
    const dish = mesh(relay, new THREE.SphereGeometry(0.73, 14, 7, 0, TAU, 0.60, 0.97),
      new THREE.MeshStandardMaterial({ color: '#8c919b', metalness: 0.27, roughness: 0.66, side: THREE.DoubleSide }),
      [0.04, 2.48, 0], [1.23, 1, 1.02]);
    dish.rotation.set(-0.19, 0.16, -0.43);
    mesh(relay, new THREE.OctahedronGeometry(0.13, 0), marker, [0.15, 2.80, 0.06]);
    const footOnly = new THREE.Group();
    footOnly.position.z = 34;
    root.add(footOnly);
    for (let i = 0; i < 8; i++) {
      const angle = i * TAU / 8;
      arc(footOnly, marker, 3.43, 0.054, angle + 0.12, angle + 0.57, 0.021);
    }

    for (let j = 0; j < 12; j++) {
      const angle = j * TAU / 12 + 0.27;
      mesh(yard, box, casing, [Math.cos(angle) * 4.03, 0.025, Math.sin(angle) * 4.03], [0.17, 0.05, 0.35])
        .rotation.y = -angle;
    }
    mesh(root, box, canopyGround, [-6.5, 0.018, 30.6], [3.0, 0.012, 14.3]);
    for (const offset of [-7.95, -5.05]) mesh(root, box, marker, [offset, 0.022, 30.6], [0.060, 0.011, 14.1]);
    for (const side of [-1, 1]) {
      const hangar = side === -1 ? -8.2 : 9.2;
      mesh(root, box, shade, [hangar, 0.019, 43], [5.32, 0.012, 5.3]);
      mesh(root, box, marker, [hangar, 0.041, 45.5], [4.7, 0.034, 0.12]);
      mesh(root, box, marker, [hangar, 0.041, 40.5], [4.7, 0.034, 0.12]);
    }
    const center = layout.length + 6.6;
    for (const side of [-1, 1]) {
      const rail = side * (13 + index * 1.2);
      mesh(root, box, graphite, [rail, 4.0, center], [2.15, 13.0, 2.4]);
      const head = [side * 19.5, 13.7, 23.3];
      strut(root, drill, [rail, 10.3, center], head, 0.44, 0.63);
      strut(root, frame, [rail, 5.4, center], [side * 17.3, 12.3, 36.7], 0.29, 0.36);
      strut(root, marker, [rail, 10.12, center - 0.31], [side * 19.5, 13.35, 23.3], 0.079);
      mesh(root, box, marker, [rail, 10.55, center - 1.27], [0.5, 0.18, 0.16]);
      mesh(root, joint, graphite, head, [1.32, 0.65, 1.32]);
      strut(root, frame, head, [side * 19.5, 10.0, 23.3], 0.075);
      mesh(root, joint, graphite, [side * 19.5, 9.79, 23.3], [0.62, 0.43, 0.64]);
      mesh(root, box, yellow, [side * 19.5, 9.63, 23.3], [0.38, 0.062, 0.36]);
    }
  }

  for (const pad of layout.repairs) {
    const shop = new THREE.Group();
    shop.position.set(pad.x, 0, pad.z);
    scene.add(shop);
    arc(shop, shade, pad.radius, pad.radius - 0.05, 0, TAU, 0.018);
    for (let i = 0; i < 6; i++) {
      const angle = i * TAU / 6;
      arc(shop, green, pad.radius - 0.17, 0.13, angle + 0.11, angle + 0.92, 0.031);
    }
    mesh(shop, box, green, [0, 0.035, 0], [0.34, 0.029, 2.38]);
    mesh(shop, box, green, [0, 0.035, 0], [2.38, 0.029, 0.34]);
  }

  const strata = ['#4f414d', '#3e3848', '#666070', '#262532']
    .map(color => new THREE.MeshStandardMaterial({ color, roughness: 0.98 }));
  const cliff = new THREE.IcosahedronGeometry(1, 0);
  for (let i = 0; i < 55; i++) {
    const angle = i * 2.399963;
    const radius = 93 + (i * 23 % 31);
    const plate = mesh(scene, cliff, strata[i % strata.length],
      [Math.cos(angle) * radius, -7.8 + (i % 9) * 0.81, Math.sin(angle) * radius],
      [8.2 + (i * 3 % 13), 10.2 + (i * 17 % 23), 8.1 + (i * 11 % 9)]);
    plate.rotation.set(i * 0.35, angle, 0.17 * Math.cos(i));
  }

  return seconds => {
    drillHead.rotation.y = seconds * -0.22;
    prism.rotation.set(seconds * 0.16, seconds * 0.34, 0.21);
    extraction.material.opacity = 0.22 + Math.sin(seconds * 3.1) * 0.065;
  };
}

export function installStation(scene, layout) {
  const architecture = new THREE.Group();
  scene.add(architecture);
  deckCircuit(architecture, layout);
  const lifts = flightDecks(architecture, layout);
  const isFoundry = layout.id === 'foundry';
  const isBasin = layout.id === 'basin';
  const core = isFoundry || isBasin ? null : reactor(scene);
  const updateSmelter = isFoundry ? smelter(architecture, layout) : null;
  const updateBasin = isBasin ? basinWorks(architecture, layout) : null;
  const sectorMotion = isFoundry || isBasin ? [] : sectors(architecture, layout);
  batchArchitecture(architecture);
  return time => {
    const seconds = time / 1000;
    if (core) {
      core.core.rotation.set(seconds * 0.31, seconds * -0.27, seconds * 0.16);
      core.core.scale.setScalar(1 + Math.sin(seconds * 2.7) * 0.045);
      core.cage.rotation.y = seconds * 0.23;
      for (const { track, spin } of core.outerRings) track.rotation.z = seconds * spin;
      core.beam.material.opacity = 0.14 + Math.sin(seconds * 3.1) * 0.043;
    }
    updateSmelter?.(seconds);
    updateBasin?.(seconds);
    for (const fan of sectorMotion) fan.rotation.z = seconds * 0.62;
    lifts.forEach(({ air, luminous }, i) => {
      const phase = (seconds * 1.25 + i * 0.31) % 1;
      air.position.y = phase * 1.75;
      luminous.opacity = Math.sin(phase * Math.PI) * 0.76;
    });
  };
}
