import * as THREE from '/three.module.js';
import { SIEGE_CORE_HP, TEAMS } from './rules.js';

const base = new THREE.MeshStandardMaterial({ color: '#263342', roughness: 0.62, metalness: 0.37 });
const coreShape = new THREE.OctahedronGeometry(0.93, 0);
const pillar = new THREE.CylinderGeometry(0.72, 0.94, 0.43, 8);
const relayHead = new THREE.CylinderGeometry(0.20, 0.30, 0.45, 6);
const glass = new THREE.SphereGeometry(1.25, 22, 14);
const wire = new THREE.IcosahedronGeometry(1.268, 2);
const neutral = new THREE.Color('#657385');
const palette = TEAMS.map(team => new THREE.Color(team.color));
const activeColor = new THREE.Color('#f5faf9');

function add(parent, geometry, material, x, y, z) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.set(x, y, z);
  parent.add(mesh);
  return mesh;
}
function seal(shade, opacity) {
  return new THREE.MeshBasicMaterial({
    color: shade, transparent: true, opacity, blending: THREE.AdditiveBlending,
    depthWrite: false, toneMapped: false,
  });
}

export class SiegeView {
  constructor(scene, spec, glyph) {
    this.root = new THREE.Group();
    this.root.visible = false;
    scene.add(this.root);
    this.nodes = spec.nodes.map(node => {
      const root = new THREE.Group();
      root.position.set(node.x, node.y, node.z);
      this.root.add(root);
      const paint = new THREE.MeshBasicMaterial({ color: neutral, toneMapped: false, side: THREE.DoubleSide });
      const ring = add(root, new THREE.RingGeometry(node.radius - 0.10, node.radius, 48), paint, 0, 0.034, 0);
      ring.rotation.x = -Math.PI / 2;
      const inner = add(root, new THREE.RingGeometry(node.radius - 0.42, node.radius - 0.38, 48), paint, 0, 0.035, 0);
      inner.rotation.x = -Math.PI / 2;
      add(root, relayHead, paint, 0, 0.29, 0);
      const cap = add(root, new THREE.OctahedronGeometry(0.19), paint, 0, 0.79, 0);
      const label = glyph(node.label);
      label.position.y = 2.35;
      root.add(label);
      return { paint, cap, label, lit: false };
    });
    this.links = spec.links.map(([a, b]) => {
      const start = spec.nodes[a];
      const end = spec.nodes[b];
      const dx = end.x - start.x;
      const dz = end.z - start.z;
      const length = Math.hypot(dx, dz);
      const paint = seal(neutral, 0.3);
      const center = add(this.root, new THREE.PlaneGeometry(0.16, length - 6.6), paint,
        (start.x + end.x) / 2, 0.061, (start.z + end.z) / 2);
      center.rotation.set(-Math.PI / 2, 0, -Math.atan2(dx, dz), 'YXZ');
      paint.side = THREE.DoubleSide;
      return { a, b, paint, lit: false };
    });
    this.cores = spec.cores.map(core => {
      const root = new THREE.Group();
      root.position.set(core.x, 0, core.z);
      this.root.add(root);
      const color = palette[core.team];
      add(root, pillar, base, 0, 0.23, 0);
      const collar = add(root, new THREE.TorusGeometry(0.63, 0.082, 5, 19),
        new THREE.MeshBasicMaterial({ color, toneMapped: false }), 0, 0.72, 0);
      collar.rotation.x = Math.PI / 2;
      const shard = add(root, coreShape, new THREE.MeshStandardMaterial({
        color, roughness: 0.28, metalness: 0.30, emissive: color, emissiveIntensity: 0.56,
      }), 0, core.y, 0);
      shard.scale.set(0.69, 0.89, 0.69);
      const bubble = add(root, glass, seal(color, 0.10), 0, core.y, 0);
      const lines = seal(color, 0.23);
      lines.wireframe = true;
      const shield = add(root, wire, lines, 0, core.y, 0);
      const marker = glyph(TEAMS[core.team].name);
      marker.position.y = 3.3;
      marker.scale.setScalar(2.2);
      marker.material.color.copy(color);
      root.add(marker);
      return { root, collar, shard, bubble, shield, marker, open: false, hp: SIEGE_CORE_HP };
    });
  }

  update(state, team) {
    this.root.visible = Boolean(state);
    if (!state) return;
    this.nodes.forEach((model, i) => {
      const node = state.nodes[i];
      model.lit = node.linked || node.progress > 0;
      const color = palette[node.capture >= 0 ? node.capture : node.owner] ?? neutral;
      model.paint.color.copy(color).multiplyScalar(model.lit ? 1 : 0.48);
      model.label.material.color.copy(node.contested ? activeColor : color);
    });
    this.links.forEach(link => {
      const a = state.nodes[link.a];
      const b = state.nodes[link.b];
      link.lit = a.linked && b.linked && a.owner === b.owner;
      link.paint.color.copy(link.lit ? palette[a.owner] : neutral);
    });
    this.cores.forEach((model, i) => {
      const core = state.cores[i];
      model.open = Boolean(core.opened && (i === team || core.opened & (1 << team)));
      model.hp = core.hp;
      model.shard.visible = model.marker.visible = core.hp > 0;
      model.shield.visible = model.bubble.visible = core.hp > 0 && !model.open;
    });
  }

  render(time) {
    if (!this.root.visible) return;
    for (const node of this.nodes) {
      node.cap.rotation.y = time * 0.0012;
      node.cap.position.y = 0.76 + (node.lit ? Math.sin(time * 0.005) * 0.14 : 0);
    }
    for (const link of this.links) link.paint.opacity = link.lit ? 0.64 + Math.sin(time * 0.003) * 0.18 : 0.18;
    for (const core of this.cores) {
      core.shard.rotation.y = time * 0.0011;
      core.shield.rotation.y = -time * 0.00027;
      core.shard.material.emissiveIntensity = (core.open ? 0.72 : 0.36) + Math.sin(time * 0.007) * 0.13;
      core.collar.material.color.set(core.hp > 0 ? core.shard.material.color : neutral);
    }
  }
}
