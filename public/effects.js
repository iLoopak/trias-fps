import * as THREE from '/three.module.js';
import { WEAPONS } from './rules.js';

export const SHOT_STYLE = {
  pulse: { life: 112, width: 0.14, flash: 0.27, recoil: 0.072, recover: 100 },
  scatter: { life: 66, width: 0.10, flash: 0.46, recoil: 0.124, recover: 205 },
  lance: { life: 230, width: 0.28, flash: 0.43, recoil: 0.103, recover: 175 },
  arc: { life: 72, width: 0.09, flash: 0.21, recoil: 0.036, recover: 69 },
};
const worldUp = new THREE.Vector3(0, 1, 0);
const colors = Object.fromEntries(Object.entries(WEAPONS).map(([key, spec]) => [key, new THREE.Color(spec.color)]));
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
    this.flashLight = new THREE.PointLight('#fff', 0, 4, 2);
    muzzle.add(this.flashLight);
  }

  shot(event, muzzle, local, time) {
    const style = SHOT_STYLE[event.weapon];
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
      if (!impact || (impact === 'wall' && Math.abs(y) > 5 && Math.max(Math.abs(x), Math.abs(z)) >= 29.9)) continue;
      const ray = serverOrigin.clone().sub(end).normalize();
      const start = end.clone().addScaledVector(ray, 0.045);
      const hitColor = impact === 'shield' ? shieldSpark : impact === 'pilot' ? pilotSpark : metalSpark;
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

  render(time) {
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
    const strength = Math.max(0, 1 - flashAge / 58);
    this.flash.visible = strength > 0;
    if (strength) {
      this.flash.scale.setScalar(this.lastFlash.style.flash * (1 - strength * 0.35));
      this.flash.material.opacity = strength;
    }
    this.flashLight.intensity = strength * 3.5;
  }
}
