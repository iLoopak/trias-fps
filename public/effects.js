import * as THREE from '/three.module.js';
import { GRENADE, WEAPONS } from './rules.js';

const SHOT_STYLE = {
  pulse: { life: 105, width: 0.14, flash: 0.30, duration: 52, shape: 1.15 },
  scatter: { life: 72, width: 0.098, flash: 0.52, duration: 83, shape: 1.65 },
  lance: { life: 238, width: 0.28, flash: 0.47, duration: 106, shape: 0.62 },
  arc: { life: 63, width: 0.091, flash: 0.22, duration: 43, shape: 1.32 },
  rift: { life: 135, width: 0.24, flash: 0.48, duration: 127, shape: 1.0 },
};
const worldUp = new THREE.Vector3(0, 1, 0);
const colors = Object.fromEntries(Object.entries(WEAPONS).map(([key, spec]) => [key, new THREE.Color(spec.color)]));
colors.grenade = new THREE.Color(GRENADE.color);
const coreSpark = new THREE.Color('#f6d092');
const metalSpark = new THREE.Color('#ffc07d');
const shieldSpark = new THREE.Color('#6fbaff');
const pilotSpark = new THREE.Color('#ff8c71');

function lightMap(line = false) {
  const size = 64;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext('2d');
  const gradient = line
    ? ctx.createLinearGradient(0, 0, size, 0)
    : ctx.createRadialGradient(32, 32, 0, 32, 32, 31);
  gradient.addColorStop(0, line ? '#fff0' : '#fff');
  gradient.addColorStop(0.30, '#fff4');
  gradient.addColorStop(0.46, line ? '#fffd' : '#fff2');
  gradient.addColorStop(0.50, line ? '#fff' : '#fff1');
  if (line) {
    gradient.addColorStop(0.54, '#fffd');
    gradient.addColorStop(0.70, '#fff4');
  }
  gradient.addColorStop(1, '#fff0');
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, size, size);
  return new THREE.CanvasTexture(canvas);
}

function batch(scene, geometry, capacity, map = null) {
  const material = new THREE.MeshBasicMaterial({
    map, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false,
    toneMapped: false, fog: false, side: THREE.DoubleSide,
  });
  const mesh = new THREE.InstancedMesh(geometry, material, capacity);
  mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
  mesh.frustumCulled = false;
  mesh.count = 0;
  mesh.renderOrder = 1;
  scene.add(mesh);
  return mesh;
}

function commit(mesh, count) {
  mesh.count = count;
  if (!count) return;
  mesh.instanceMatrix.needsUpdate = true;
  mesh.instanceColor.needsUpdate = true;
}

export class ArenaEffects {
  constructor(scene, camera, muzzle) {
    this.camera = camera;
    this.projectileLayer = new THREE.Group();
    scene.add(this.projectileLayer);
    this.projectiles = new Map();
    this.orbGeometry = new THREE.IcosahedronGeometry(0.20, 1);
    this.orbitGeometry = new THREE.TorusGeometry(0.29, 0.022, 4, 15);
    this.orbMaterial = new THREE.MeshBasicMaterial({ color: '#fff1fb', toneMapped: false });
    this.orbitMaterial = new THREE.MeshBasicMaterial({ color: colors.rift, toneMapped: false });
    this.grenadeGeometry = new THREE.IcosahedronGeometry(0.145, 0);
    this.grenadeShell = new THREE.MeshStandardMaterial({ color: '#555b60', roughness: 0.52, metalness: 0.44 });
    this.grenadeRing = new THREE.MeshBasicMaterial({ color: colors.grenade, toneMapped: false });
    this.beamMesh = batch(scene, new THREE.PlaneGeometry(), 80, lightMap(true));
    this.sparkMesh = batch(scene, new THREE.PlaneGeometry(), 240, lightMap());
    this.ringMesh = batch(scene, new THREE.RingGeometry(0.91, 1, 40).rotateX(-Math.PI / 2), 24);
    this.beams = [];
    this.sparks = [];
    this.rings = [];
    this.pose = new THREE.Object3D();
    this.normal = new THREE.Vector3();
    this.side = new THREE.Vector3();
    this.basis = new THREE.Matrix4();
    this.tint = new THREE.Color();
    this.flash = new THREE.Sprite(new THREE.SpriteMaterial({
      map: this.sparkMesh.material.map, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
    }));
    this.flash.visible = false;
    muzzle.add(this.flash);
    this.flashCore = new THREE.Sprite(this.flash.material.clone());
    this.flashCore.material.color.set('#fff2cd');
    this.flashCore.visible = false;
    muzzle.add(this.flashCore);
    this.flashLight = new THREE.PointLight('#fff', 0, 4, 2);
    muzzle.add(this.flashLight);
  }

  reset() {
    this.beams.length = this.sparks.length = this.rings.length = 0;
    this.lastFlash = undefined;
    this.setProjectiles([]);
  }

  shot(event, muzzle, local, time) {
    const style = event.alt ? { ...SHOT_STYLE[event.weapon], life: 144, width: 0.19, shape: 0.98 } : SHOT_STYLE[event.weapon];
    const shade = colors[event.weapon];
    if (local) {
      this.lastFlash = { time, style };
      this.flash.material.color.copy(shade);
      this.flashLight.color.copy(shade);
    }

    const origin = local ? muzzle.getWorldPosition(new THREE.Vector3()) : new THREE.Vector3(...event.from);
    const serverOrigin = new THREE.Vector3(...event.from);
    for (const [x, y, z, impact] of event.ends) {
      const end = new THREE.Vector3(x, y, z);
      const direction = end.clone().sub(origin);
      const length = direction.length();
      if (length >= 0.19 && (!local || end.distanceToSquared(serverOrigin) > 0.9)) {
        if (this.beams.length >= 80) this.beams.shift();
        this.beams.push({ from: origin, end, direction: direction.normalize(), length, shade, time, ...style });
      }
      if (!impact) continue;
      const ray = serverOrigin.clone().sub(end).normalize();
      const start = end.clone().addScaledVector(ray, 0.045);
      const hitColor = impact === 'shield' ? shieldSpark : impact === 'pilot' ? pilotSpark : impact === 'core' ? coreSpark : metalSpark;
      this.burst(start, ray, hitColor, time, event.weapon === 'scatter' ? 3 : 8);
    }
  }

  burst(origin, direction, shade, time, count) {
    for (let i = 0; i < count; i++) {
      if (this.sparks.length >= 240) this.sparks.shift();
      const velocity = direction.clone().multiplyScalar(1.5 + Math.random() * 2.4);
      velocity.x += (Math.random() - 0.5) * 4.5;
      velocity.y += 0.3 + Math.random() * 2.9;
      velocity.z += (Math.random() - 0.5) * 4.5;
      this.sparks.push({ origin, velocity, shade, time, life: 100 + Math.random() * 170, size: 0.07 + Math.random() * 0.1 });
    }
  }

  pulse(position, shade, time, pickup = false) {
    if (this.rings.length >= 24) this.rings.shift();
    const origin = new THREE.Vector3(...position);
    this.rings.push({ origin, shade, time, life: pickup ? 480 : 650, radius: pickup ? 1.22 : 1.7 });
    if (pickup) this.burst(origin.clone().addScaledVector(worldUp, 0.36), worldUp, shade, time, 12);
  }

  setProjectiles(items) {
    const seen = new Set();
    for (const shot of items) {
      seen.add(shot.id);
      let orb = this.projectiles.get(shot.id);
      if (!orb) {
        const root = new THREE.Group();
        root.position.set(shot.x, shot.y, shot.z);
        const grenade = shot.weapon === 'grenade';
        const core = new THREE.Mesh(grenade ? this.grenadeGeometry : this.orbGeometry, grenade ? this.grenadeShell : this.orbMaterial);
        const orbit = new THREE.Mesh(this.orbitGeometry, grenade ? this.grenadeRing : this.orbitMaterial);
        if (grenade) orbit.scale.setScalar(0.59);
        const halo = new THREE.Sprite(new THREE.SpriteMaterial({
          map: this.sparkMesh.material.map, color: colors[shot.weapon],
          transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
        }));
        halo.scale.setScalar(1.78);
        root.add(core, orbit, halo);
        if (shot.alt) root.scale.setScalar(1.48);
        this.projectileLayer.add(root);
        orb = { root, core, orbit, halo, target: new THREE.Vector3(), lastTrail: 0, shade: colors[shot.weapon], grenade };
        this.projectiles.set(shot.id, orb);
      }
      orb.target.set(shot.x, shot.y, shot.z);
      orb.fuse = shot.fuse;
    }
    for (const [id, orb] of this.projectiles) {
      if (seen.has(id)) continue;
      orb.root.removeFromParent();
      orb.halo.material.dispose();
      this.projectiles.delete(id);
    }
  }

  projectileBurst(position, time, weapon, alternate = false) {
    const tint = colors[weapon];
    const origin = new THREE.Vector3(...position);
    if (this.rings.length >= 24) this.rings.shift();
    this.rings.push({ origin, shade: tint, time, life: 580, radius: weapon === 'grenade' ? 4 : alternate ? 4.65 : 3.28 });
    this.burst(origin, worldUp, tint, time, 36);
    this.burst(origin, worldUp, this.orbMaterial.color, time, 10);
  }

  movement(position, action, accent, time) {
    if (action === 'jump') return;
    if (action === 'land') {
      this.burst(new THREE.Vector3(...position), worldUp, metalSpark, time, 4);
      return;
    }
    this.pulse(position, accent, time, action === 'launch' || action === 'double');
  }

  render(time, dt) {
    for (const orb of this.projectiles.values()) {
      orb.root.position.lerp(orb.target, 1 - Math.exp(-dt * 37));
      orb.core.rotation.set(time * 0.006, time * 0.01, 0);
      orb.orbit.rotation.set(time * 0.007, 0.89, time * 0.012);
      if (orb.grenade) {
        orb.halo.scale.setScalar(0.34 + Math.max(0, Math.sin(time * (orb.fuse < 0.5 ? 0.062 : 0.024))) * 0.36);
      } else {
        orb.halo.scale.setScalar(1.57 + Math.sin(time * 0.023) * 0.19);
      }
      if (!orb.grenade && time - orb.lastTrail >= 37) {
        orb.lastTrail = time;
        this.burst(orb.root.position.clone(), worldUp, orb.shade, time, 2);
      }
    }
    this.beams = this.beams.filter(beam => time - beam.time < beam.life);
    this.sparks = this.sparks.filter(spark => time - spark.time < spark.life);
    this.rings = this.rings.filter(ring => time - ring.time < ring.life);
    const { pose, tint } = this;
    for (const [i, beam] of this.beams.entries()) {
      const fade = Math.max(0, 1 - (time - beam.time) / beam.life);
      pose.position.copy(beam.from).lerp(beam.end, 0.5);
      this.normal.copy(this.camera.position).sub(pose.position);
      this.side.crossVectors(beam.direction, this.normal);
      if (this.side.lengthSq() < 0.000001) this.side.crossVectors(beam.direction, worldUp);
      if (this.side.lengthSq() < 0.000001) this.side.set(1, 0, 0);
      this.side.normalize();
      this.normal.crossVectors(this.side, beam.direction);
      this.basis.makeBasis(this.side, beam.direction, this.normal);
      pose.quaternion.setFromRotationMatrix(this.basis);
      pose.scale.set(beam.width * (0.6 + fade * 0.4), beam.length, 1);
      pose.updateMatrix();
      this.beamMesh.setMatrixAt(i, pose.matrix);
      this.beamMesh.setColorAt(i, tint.copy(beam.shade).multiplyScalar(fade * fade * 1.45));
    }
    for (const [i, spark] of this.sparks.entries()) {
      const age = Math.max(0, (time - spark.time) / 1000);
      const fade = Math.max(0, 1 - age * 1000 / spark.life);
      pose.position.copy(spark.origin).addScaledVector(spark.velocity, age);
      pose.position.y -= age * age * 4;
      pose.quaternion.copy(this.camera.quaternion);
      pose.scale.setScalar(spark.size * (0.5 + fade));
      pose.updateMatrix();
      this.sparkMesh.setMatrixAt(i, pose.matrix);
      this.sparkMesh.setColorAt(i, tint.copy(spark.shade).multiplyScalar(fade * 1.7));
    }
    for (const [i, ring] of this.rings.entries()) {
      const phase = Math.max(0, (time - ring.time) / ring.life);
      pose.position.copy(ring.origin);
      pose.position.y += 0.035 + phase * 0.12;
      pose.quaternion.identity();
      pose.scale.setScalar(0.17 + phase * ring.radius);
      pose.updateMatrix();
      this.ringMesh.setMatrixAt(i, pose.matrix);
      this.ringMesh.setColorAt(i, tint.copy(ring.shade).multiplyScalar((1 - phase) ** 2 * 0.9));
    }
    commit(this.beamMesh, this.beams.length);
    commit(this.sparkMesh, this.sparks.length);
    commit(this.ringMesh, this.rings.length);

    const flashAge = Math.max(0, time - (this.lastFlash?.time ?? -1000));
    const style = this.lastFlash?.style;
    const strength = style ? Math.max(0, 1 - flashAge / style.duration) : 0;
    this.flashCore.visible = this.flash.visible = strength > 0;
    if (strength) {
      const spread = style.flash * (1 - strength * 0.25);
      this.flash.scale.set(spread * style.shape, spread / Math.sqrt(style.shape), spread);
      this.flash.material.opacity = strength;
      this.flashCore.scale.set(spread * 0.58, spread * 0.30 / style.shape, spread);
      this.flashCore.material.opacity = strength ** 2.5;
    }
    this.flashLight.intensity = strength * 3.5;
  }
}
