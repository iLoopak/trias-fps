import * as THREE from '/three.module.js';
import { TEAMS } from './rules.js';
import { VEHICLES } from './vehicles.js';

const box = new THREE.BoxGeometry();
const roller = new THREE.CylinderGeometry(0.5, 0.5, 1, 12).rotateZ(Math.PI / 2);
const bore = new THREE.CylinderGeometry(0.52, 0.5, 1, 10).rotateX(Math.PI / 2);
const coil = new THREE.TorusGeometry(0.41, 0.096, 5, 16);
const chassis = new THREE.MeshStandardMaterial({ color: '#52616a', metalness: 0.43, roughness: 0.56 });
const tread = new THREE.MeshStandardMaterial({ color: '#20232a', metalness: 0.34, roughness: 0.84 });
const canopy = new THREE.MeshStandardMaterial({ color: '#163746', metalness: 0.51, roughness: 0.19 });
const nose = new THREE.MeshStandardMaterial({ color: '#b1baba', metalness: 0.47, roughness: 0.47 });
const heat = new THREE.MeshBasicMaterial({ color: '#ff9874', toneMapped: false });

const footprint = new THREE.Shape();
footprint.moveTo(-0.44, -0.23);
footprint.lineTo(-0.28, -0.48);
footprint.lineTo(0.28, -0.48);
footprint.lineTo(0.44, -0.23);
footprint.lineTo(0.44, 0.3);
footprint.lineTo(0.30, 0.43);
footprint.lineTo(-0.30, 0.43);
footprint.lineTo(-0.44, 0.3);
footprint.closePath();
const plating = new THREE.ExtrudeGeometry(footprint, {
  depth: 0.82, bevelEnabled: true, bevelThickness: 0.075, bevelSize: 0.045, bevelSegments: 2, curveSegments: 1,
}).center().rotateX(Math.PI / 2);

function piece(parent, material, at, scale, geometry = box) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.set(...at);
  mesh.scale.set(...scale);
  parent.add(mesh);
  return mesh;
}

function skimmer(body, armor, lights, driverGun) {
  const tub = piece(body, armor, [0, 0.60, -0.07], [1.26, 0.42, 2.54], plating);
  tub.castShadow = true;
  piece(body, canopy, [0, 0.90, 0.07], [0.69, 0.28, 1.15], plating);
  piece(body, nose, [0, 0.70, -1.06], [0.32, 0.26, 0.66], plating);
  for (const side of [-1, 1]) {
    const arm = piece(body, chassis, [side * 0.64, 0.58, -0.32], [0.68, 0.17, 0.54], plating);
    arm.rotation.y = side * 0.49;
    piece(body, armor, [side * 0.81, 0.47, -0.05], [0.48, 0.40, 2.32], plating)
      .rotation.y = -0.05 * side;
    piece(body, tread, [side * 0.79, 0.47, 1.01], [0.36, 0.36, 0.49], bore);
    piece(body, lights, [side * 0.79, 0.47, 1.24], [0.33, 0.31, 0.11], coil);
    piece(body, lights, [side * 0.79, 0.41, -0.84], [0.31, 0.046, 0.35], plating);
    piece(driverGun, tread, [side * 0.12, 0, -0.57], [0.12, 0.13, 1.02], bore);
    piece(driverGun, lights, [side * 0.12, 0, -0.97], [0.15, 0.15, 0.07], coil);
  }
  driverGun.position.y = 1.03;
  return [];
}

function bastion(body, armor, lights, driverGun) {
  const tub = piece(body, chassis, [0, 0.65, 0.05], [2.91, 0.60, 3.32], plating);
  tub.castShadow = true;
  piece(body, armor, [0, 1.0, -0.02], [2.53, 0.46, 2.94], plating);
  piece(body, nose, [0, 0.79, -1.59], [1.84, 0.25, 0.51], plating);
  const rollers = [];
  for (const side of [-1, 1]) {
    piece(body, tread, [side * 1.29, 0.37, 0.02], [0.46, 0.64, 3.62], plating);
    piece(body, chassis, [side * 1.40, 0.67, 0.02], [0.26, 0.21, 3.42], plating);
    piece(body, lights, [side * 1.49, 0.68, 0.33], [0.025, 0.049, 1.6]);
    for (let i = -2; i <= 2; i++) {
      const wheel = piece(body, chassis, [side * 1.54, 0.36, i * 0.66], [0.14, 0.60, 0.60], roller);
      rollers.push(wheel);
      piece(wheel, tread, [side * 0.47, 0, 0], [0.61, 0.39, 0.39], roller);
      piece(wheel, nose, [side * 0.57, 0.12, 0], [0.22, 0.27, 0.13]);
    }
    piece(body, heat, [side * 0.84, 0.80, 1.57], [0.21, 0.08, 0.026]);
  }
  driverGun.position.y = 1.39;
  piece(driverGun, armor, [0, 0.04, 0.07], [1.43, 0.43, 1.40], plating);
  piece(driverGun, chassis, [0, 0.03, -0.89], [0.30, 0.32, 1.58], bore);
  piece(driverGun, nose, [0, 0.025, -1.40], [0.49, 0.40, 0.36], plating);
  piece(driverGun, lights, [0, 0.035, -1.60], [0.28, 0.28, 0.079], coil);
  return rollers;
}

export function buildGarageView(scene, layout, glyph) {
  return (layout.vehicles ?? []).map(({ kind, x, z, heading, team }) => {
    const spec = VEHICLES[kind];
    const root = new THREE.Group();
    root.position.set(x, 0, z);
    root.rotation.y = heading;
    scene.add(root);
    const hull = new THREE.Group();
    root.add(hull);
    const armor = new THREE.MeshStandardMaterial({
      color: TEAMS[team].color, metalness: 0.43, roughness: 0.53,
      emissive: TEAMS[team].color, emissiveIntensity: 0.11,
    });
    const lights = new THREE.MeshBasicMaterial({ color: spec.color, toneMapped: false });
    const gun = new THREE.Group();
    hull.add(gun);
    const wheels = (kind === 'skimmer' ? skimmer : bastion)(hull, armor, lights, gun);
    const marker = glyph('E');
    marker.position.y = spec.height + 0.65;
    marker.scale.setScalar(0.80);
    marker.material.depthTest = true;
    root.add(marker);
    return { kind, root, hull, gun, armor, lights, wheels, marker, team, target: null, lastTrail: -1000 };
  });
}
