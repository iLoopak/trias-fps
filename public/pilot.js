import * as THREE from '/three.module.js';

const clamp = THREE.MathUtils.clamp;
const mix = THREE.MathUtils.lerp;
const TAU = Math.PI * 2;
const down = new THREE.Vector3(0, -1, 0);
const aim = new THREE.Vector3();
const bend = new THREE.Vector3();
const joint = new THREE.Vector3();
const along = new THREE.Vector3();
const oval = new THREE.SphereGeometry(0.5, 14, 10);
const barrel = new THREE.CylinderGeometry(0.5, 0.5, 1, 10);
const taper = new THREE.CylinderGeometry(0.28, 0.5, 1, 8);
const outline = new THREE.Shape();
outline.moveTo(-0.38, -0.44);
outline.lineTo(0.38, -0.44);
outline.lineTo(0.44, -0.38);
outline.lineTo(0.44, 0.38);
outline.lineTo(0.38, 0.44);
outline.lineTo(-0.38, 0.44);
outline.lineTo(-0.44, 0.38);
outline.lineTo(-0.44, -0.38);
outline.closePath();
const bevel = new THREE.ExtrudeGeometry(outline, { depth: 0.88, bevelThickness: 0.06, bevelSize: 0.06, bevelSegments: 1 });
bevel.center();

const material = (color, roughness = 0.63, metalness = 0.24) => new THREE.MeshStandardMaterial({ color, roughness, metalness });
const rubber = material('#19202c', 0.88, 0.12);
const alloy = material('#545965', 0.42, 0.52);
const carbon = material('#272c36', 0.50, 0.46);
const visor = material('#101e2d', 0.24, 0.45);
const webbing = material('#846746', 0.86, 0.04);
const buckle = material('#b6a889', 0.43, 0.50);
const kits = {
  vector:  { shell: material('#d6d9cc'), under: material('#47526a'), hips: 0.16, chest: 0.43, shoulder: 0.24, leg: 0.145, bob: 0.023, cadence: 1.00, runTilt: 0.25, stride: 1.00 },
  bulwark: { shell: material('#737c80'), under: material('#343b41'), hips: 0.18, chest: 0.56, shoulder: 0.29, leg: 0.185, bob: 0.041, cadence: 0.82, runTilt: 0.20, stride: 1.12 },
  wraith:  { shell: material('#343e55'), under: material('#292939'), hips: 0.13, chest: 0.36, shoulder: 0.21, leg: 0.123, bob: 0.015, cadence: 1.12, runTilt: 0.32, stride: 0.96 },
  kestrel: { shell: material('#b68a4f'), under: material('#494938'), hips: 0.15, chest: 0.41, shoulder: 0.23, leg: 0.14, bob: 0.029, cadence: 1.04, runTilt: 0.29, stride: 1.02 },
};

export function pilotShell(id) {
  return (kits[id] ?? kits.vector).shell;
}

function part(parent, skin, dimensions, position, geometry = bevel) {
  const mesh = new THREE.Mesh(geometry, skin);
  mesh.scale.set(...dimensions);
  mesh.position.set(...position);
  parent.add(mesh);
  return mesh;
}

function group(parent, x = 0, y = 0, z = 0) {
  const result = new THREE.Group();
  result.position.set(x, y, z);
  parent.add(result);
  return result;
}

function headDetails(head, id, kit, { armor, detail }) {
  const { shell } = kit;
  if (id === 'bulwark') {
    part(head, shell, [0.36, 0.30, 0.33], [0, 0.13, 0.012]).castShadow = true;
    part(head, carbon, [0.32, 0.105, 0.07], [0, 0.16, -0.167]);
    part(head, armor, [0.35, 0.052, 0.11], [0, 0.22, -0.146]);
    part(head, detail, [0.245, 0.018, 0.017], [0, 0.156, -0.207]);
    for (const side of [-1, 1]) {
      part(head, shell, [0.10, 0.159, 0.18], [side * 0.151, 0.06, -0.087]).rotation.z = side * 0.17;
      part(head, alloy, [0.06, 0.116, 0.12], [side * 0.173, 0.109, 0.081], barrel).rotation.z = Math.PI / 2;
    }
  } else if (id === 'wraith') {
    part(head, shell, [0.292, 0.351, 0.35], [0, 0.124, 0.019], oval).castShadow = true;
    part(head, carbon, [0.206, 0.236, 0.116], [0, 0.126, -0.141]).rotation.x = 0.23;
    part(head, detail, [0.024, 0.137, 0.018], [0, 0.148, -0.207]).rotation.x = 0.23;
    for (const side of [-1, 1]) {
      part(head, armor, [0.051, 0.195, 0.22], [side * 0.113, 0.135, 0.00]).rotation.z = -side * 0.20;
      part(head, shell, [0.039, 0.22, 0.067], [side * 0.124, 0.23, 0.08], taper).rotation.x = 0.24;
    }
  } else if (id === 'kestrel') {
    part(head, shell, [0.32, 0.31, 0.365], [0, 0.125, 0.02], oval).castShadow = true;
    part(head, armor, [0.32, 0.063, 0.26], [0, 0.223, -0.065]).rotation.x = 0.11;
    for (const side of [-1, 1]) {
      part(head, carbon, [0.12, 0.082, 0.116], [side * 0.071, 0.172, -0.137]).rotation.y = -side * 0.32;
      part(head, detail, [0.072, 0.027, 0.033], [side * 0.072, 0.167, -0.196]).rotation.y = -side * 0.31;
    }
    part(head, shell, [0.144, 0.09, 0.21], [0, 0.084, -0.115]).rotation.x = -0.43;
    part(head, carbon, [0.018, 0.25, 0.018], [0.136, 0.245, 0.076], barrel).rotation.z = -0.20;
    part(head, detail, [0.025, 0.036, 0.025], [0.159, 0.374, 0.076], oval);
  } else {
    part(head, shell, [0.348, 0.314, 0.364], [0, 0.138, 0.016], oval).castShadow = true;
    part(head, visor, [0.322, 0.141, 0.18], [0, 0.155, -0.099], oval);
    part(head, detail, [0.231, 0.016, 0.079], [0, 0.15, -0.161], oval);
    part(head, armor, [0.054, 0.265, 0.339], [0, 0.183, 0.019], oval);
    part(head, shell, [0.251, 0.066, 0.129], [0, 0.056, -0.095]);
    for (const side of [-1, 1]) part(head, carbon, [0.038, 0.122, 0.122], [side * 0.158, 0.128, 0.022], oval);
  }
}

function kitDetails(torso, id, kit, { armor, detail }) {
  const { shell } = kit;
  if (id === 'bulwark') {
    part(torso, shell, [0.47, 0.23, 0.16], [0, 0.40, 0.20]);
    for (const side of [-1, 1]) {
      part(torso, carbon, [0.125, 0.316, 0.125], [side * 0.162, 0.40, 0.28], barrel);
      part(torso, detail, [0.072, 0.043, 0.034], [side * 0.163, 0.34, 0.348]);
      part(torso, buckle, [0.14, 0.049, 0.028], [side * 0.12, 0.40, -0.217]).rotation.z = -side * 0.25;
    }
  } else if (id === 'wraith') {
    for (const side of [-1, 1]) {
      part(torso, shell, [0.092, 0.54, 0.19], [side * 0.13, 0.35, 0.168], taper).rotation.z = -side * 0.25;
      part(torso, detail, [0.032, 0.33, 0.025], [side * 0.111, 0.38, 0.257]).rotation.z = -side * 0.23;
    }
    part(torso, armor, [0.15, 0.17, 0.07], [0, 0.374, -0.17], oval);
  } else if (id === 'kestrel') {
    part(torso, carbon, [0.30, 0.35, 0.155], [0.027, 0.34, 0.18]);
    for (const side of [-1, 1]) {
      part(torso, armor, [0.078, 0.34, 0.127], [side * 0.132, 0.365, 0.252], taper).rotation.z = -side * 0.34;
      part(torso, detail, [0.051, 0.13, 0.014], [side * 0.118, 0.33, 0.324]).rotation.z = -side * 0.29;
    }
    part(torso, webbing, [0.059, 0.346, 0.024], [0.045, 0.31, -0.212]).rotation.z = -0.47;
    part(torso, buckle, [0.064, 0.044, 0.037], [0.085, 0.389, -0.221]).rotation.z = -0.47;
  } else {
    part(torso, carbon, [0.30, 0.30, 0.13], [0, 0.39, 0.178]);
    part(torso, shell, [0.235, 0.208, 0.08], [0, 0.41, 0.24]);
    for (const side of [-1, 1]) {
      part(torso, detail, [0.038, 0.145, 0.025], [side * 0.095, 0.397, 0.286]).rotation.z = side * 0.16;
    }
  }
}

export function buildPilot(paint, ally, id) {
  const kit = kits[id] ?? kits.vector;
  const { armor, detail } = paint;
  const root = new THREE.Group();
  const body = group(root);
  const torso = group(body, 0, 0.82);
  part(torso, kit.under, [kit.chest, 0.46, 0.33], [0, 0.295, 0.005]).castShadow = true;
  part(torso, rubber, [kit.hips * 2.1, 0.175, 0.292], [0, 0.07, 0.015]);
  part(torso, alloy, [kit.hips * 2.2, 0.093, 0.083], [0, 0.05, -0.136]);
  part(torso, buckle, [0.09, 0.072, 0.038], [0.006, 0.048, -0.184]);
  for (const side of [-1, 1]) {
    const chest = part(torso, armor, [kit.chest * 0.54, 0.244, 0.15], [side * kit.chest * 0.235, 0.388, -0.145]);
    chest.rotation.z = -side * 0.16;
    part(torso, kit.shell, [kit.chest * 0.44, 0.08, 0.113], [side * kit.chest * 0.25, 0.504, -0.139]).rotation.z = -side * 0.16;
    part(torso, detail, [kit.chest * 0.30, 0.023, 0.016], [side * kit.chest * 0.23, 0.437, -0.226]).rotation.z = -side * 0.16;
    part(torso, carbon, [0.098, 0.114, 0.114], [side * kit.hips, 0.074, -0.022]);
  }
  kitDetails(torso, id, kit, paint);
  part(torso, rubber, [0.168, 0.115, 0.18], [0, 0.564, 0.00], barrel);
  const head = group(torso, 0, 0.61, 0.0);
  headDetails(head, id, kit, paint);

  const weapon = group(torso, 0.12, 0.34, -0.23);
  weapon.scale.setScalar(0.92);
  const limbs = [-1, 1].map(side => {
    const upper = group(body);
    const lower = group(body);
    const foot = group(body);
    const width = kit.leg;
    part(upper, kit.under, [width * 1.10, 0.435, width * 1.18], [0, -0.188, 0]);
    part(upper, armor, [width * 0.66, 0.24, 0.077], [side * width * 0.18, -0.13, -0.064]).rotation.z = -side * 0.12;
    part(lower, rubber, [width * 0.96, 0.403, width], [0, -0.16, 0], barrel);
    part(lower, kit.shell, [width * 1.05, 0.136, 0.075], [0, 0.005, -0.043]);
    part(lower, kit.shell, [width * 0.96, 0.204, 0.088], [0, -0.18, -0.031]);
    part(lower, detail, [width * 0.37, 0.077, 0.022], [0, -0.14, -0.083]);
    part(lower, rubber, [width * 0.84, 0.128, width * 0.87], [0, -0.352, 0], barrel);
    part(foot, rubber, [width * 1.14, 0.13, 0.247], [0, -0.024, -0.037]);
    part(foot, carbon, [width, 0.048, 0.153], [0, -0.011, -0.094]);
    part(foot, alloy, [width * 1.23, 0.032, 0.258], [0, -0.074, -0.038]);
    return { upper, lower, foot, side, hip: new THREE.Vector3(), target: new THREE.Vector3(), pole: new THREE.Vector3(0, 0, -1) };
  });

  const arms = [-1, 1].map(side => {
    const upper = group(torso);
    const lower = group(torso);
    const width = id === 'bulwark' ? 0.162 : 0.121;
    part(upper, kit.under, [width, 0.30, width * 1.15], [0, -0.135, 0]);
    const pauldron = part(upper, armor, [width * (id === 'bulwark' ? 1.81 : 1.38), id === 'wraith' ? 0.132 : 0.180, width * 1.53],
      [side * width * 0.18, -0.041, 0.005], id === 'vector' ? oval : bevel);
    pauldron.rotation.z = -side * 0.25;
    pauldron.castShadow = true;
    part(upper, kit.shell, [width * 0.84, 0.08, width * 1.40], [side * width * 0.30, 0.01, -0.01]).rotation.z = -side * 0.27;
    part(upper, detail, [width * 0.30, 0.08, 0.028], [side * width * 0.45, -0.034, -width * 0.76]).rotation.z = -side * 0.25;
    part(lower, carbon, [width, 0.235, width], [0, -0.105, 0]);
    part(lower, kit.shell, [width * 0.95, 0.135, 0.05], [0, -0.096, -width * 0.39]);
    part(lower, rubber, [width * 0.83, 0.107, width * 0.86], [0, -0.244, -0.005], oval);
    return { upper, lower, side, hip: new THREE.Vector3(side * kit.shoulder, 0.463, 0.035),
      target: new THREE.Vector3(), pole: new THREE.Vector3(side * 0.55, -1, 0.24) };
  });

  let marker;
  if (ally) {
    const chevron = new THREE.Shape();
    chevron.moveTo(-0.14, 0.02);
    chevron.lineTo(0, 0.105);
    chevron.lineTo(0.14, 0.02);
    chevron.lineTo(0.14, -0.038);
    chevron.lineTo(0, 0.048);
    chevron.lineTo(-0.14, -0.038);
    chevron.closePath();
    marker = new THREE.Mesh(new THREE.ShapeGeometry(chevron), detail);
    root.add(marker);
  }
  return { root, body, torso, head, weapon, limbs, arms, kit, marker, phase: 0, gait: 0, crouch: 0, pace: 0, shotAt: -1000, actionAt: -1000 };
}

function poseLimb(limb, upperLength, lowerLength) {
  const { hip, target, pole, upper, lower } = limb;
  aim.subVectors(target, hip);
  const distance = clamp(aim.length(), Math.abs(upperLength - lowerLength) + 0.001, upperLength + lowerLength - 0.001);
  aim.normalize();
  bend.copy(pole).addScaledVector(aim, -pole.dot(aim)).normalize();
  const reach = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2 * distance);
  joint.copy(hip).addScaledVector(aim, reach).addScaledVector(bend, Math.sqrt(Math.max(0, upperLength * upperLength - reach * reach)));
  along.subVectors(joint, hip).normalize();
  upper.position.copy(hip);
  upper.quaternion.setFromUnitVectors(down, along);
  along.copy(hip).addScaledVector(aim, distance).sub(joint).normalize();
  lower.position.copy(joint);
  lower.quaternion.setFromUnitVectors(down, along);
}

export function posePilot(model, state, dt, time, terrain) {
  const { root, torso, head, weapon, kit, limbs, arms } = model;
  const vx = state.vx ?? 0;
  const vz = state.vz ?? 0;
  const speed = Math.hypot(vx, vz);
  const blend = 1 - Math.exp(-dt * 15);
  const moving = state.grounded && speed > 0.22 && (state.turret ?? -1) < 0;
  model.pace = mix(model.pace, moving ? speed : 0, blend);
  model.gait = mix(model.gait, moving ? Math.min(1, speed * 0.35) : 0, blend);
  model.crouch = mix(model.crouch, state.crouch ? 1 : 0, blend);
  const crouch = model.crouch;
  const gait = model.gait;
  const run = state.sprint ? clamp((model.pace - 4.5) * 0.22, 0, 1) * (1 - crouch) : 0;
  const cos = Math.cos(root.rotation.y);
  const sin = Math.sin(root.rotation.y);
  const strideX = speed > 0.18 ? (vx * cos - vz * sin) / speed : 0;
  const strideZ = speed > 0.18 ? (vx * sin + vz * cos) / speed : -1;
  const cycleSpeed = Math.min(2.16 + run * 0.43, model.pace * 0.34) * kit.cadence;
  model.phase = (model.phase + dt * cycleSpeed) % 1;
  const breath = Math.sin(time * 0.0034 + model.phase * TAU) * 0.006 * (1 - gait);
  const landingAge = time - model.actionAt;
  const landing = model.action === 'land' && landingAge < 255 ? Math.sin(Math.PI * clamp(landingAge / 255, 0, 1)) : 0;
  const recoil = Math.exp(-Math.max(0, time - model.shotAt) / 83);
  const doubleJump = model.action === 'double' && landingAge < 215 ? 1 - clamp(landingAge / 215, 0, 1) : 0;
  const dodging = (state.dodgeTime ?? 0) > 0;
  const air = !state.grounded ? 1 : 0;
  const sway = Math.cos(model.phase * TAU) * gait;
  const hipsY = 0.783 - crouch * 0.518 + Math.abs(sway) * kit.bob + breath - landing * 0.096 - air * 0.05;
  torso.position.set(sway * 0.030, hipsY, crouch * 0.012);
  torso.rotation.set(-kit.runTilt * run - crouch * 0.30 + strideZ * gait * 0.055 - recoil * 0.05,
    sway * (0.084 + run * 0.12) + strideX * gait * 0.14, -strideX * (dodging ? 0.23 : 0.10) + sway * 0.034);
  head.rotation.set((state.pitch ?? 0) * 0.58 - torso.rotation.x * 0.30, -torso.rotation.y * 0.78, -torso.rotation.z * 0.52);

  const reload = state.reload ? Math.sin(time * 0.008) * 0.13 + 0.25 : 0;
  const low = run * (1 - recoil) * (1 - Number(Boolean(state.reload)));
  weapon.position.set(0.116 - low * 0.055, 0.324 - low * 0.142 - reload * 0.20 + sway * 0.019, -0.229 + recoil * 0.06);
  weapon.rotation.set((state.pitch ?? 0) * 0.60 - low * 0.30 + recoil * 0.14 - reload * 0.42,
    -low * 0.19 + sway * 0.057, -strideX * gait * 0.09 + reload * 0.54);
  weapon.updateMatrix();
  for (const arm of arms) {
    if (arm.side > 0) arm.target.set(0.014, -0.027, 0.07);
    else arm.target.set(-0.077 - reload * 0.31, -0.053 - reload * 0.35, -0.23 + reload * 0.62);
    arm.target.applyMatrix4(weapon.matrix);
    poseLimb(arm, 0.291, 0.284);
  }

  for (const [i, limb] of limbs.entries()) {
    const phase = (model.phase + i * 0.50) % 1;
    const contact = mix(0.66, 0.54, run);
    const swing = clamp((phase - contact) / (1 - contact), 0, 1);
    const lift = Math.sin(swing * Math.PI);
    const reach = (phase < contact ? mix(1, -1, phase / contact) : -Math.cos(swing * Math.PI));
    const travel = 0.36 * kit.stride * (1 + run * 0.21 - crouch * 0.47) * gait * reach * (1 - air);
    const lateral = limb.side * (kit.hips + crouch * 0.027);
    limb.hip.set(limb.side * kit.hips + torso.position.x, hipsY + limb.side * sway * 0.014, torso.position.z);
    limb.target.set(lateral + strideX * travel + (dodging ? strideX * 0.17 : 0),
      0.095 + lift * (0.155 + run * 0.09 - crouch * 0.065) * gait * (1 - air) + air * ((i ? 0.23 : 0.12) + doubleJump * 0.09),
      strideZ * travel + crouch * (0.070 + limb.side * 0.032) + air * (i ? 0.18 : -0.26));
    if (terrain && state.grounded) {
      const { x, z } = limb.target;
      const elevation = terrain(root.position.x + x * cos + z * sin, root.position.z - x * sin + z * cos);
      limb.target.y += clamp(elevation - root.position.y, -0.15, 0.16);
    }
    limb.pole.set(limb.side * (0.18 + crouch * 0.27), 0, -1);
    poseLimb(limb, 0.420, 0.425);
    limb.foot.position.copy(limb.target);
    limb.foot.rotation.set(lift * gait * -0.25 - air * 0.31, -strideX * 0.24 - limb.side * 0.095, 0);
  }
  if (model.marker) model.marker.position.y = 2.025 - crouch * 0.43;
}
