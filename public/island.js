import * as THREE from '/three.module.js';
import {
  ISLAND_CROSSINGS, ISLAND_GRID, ISLAND_TRAIL, ISLAND_WATER_LEVEL,
  creekCenter, creekLevel, islandFord, islandGridHeight, islandHeight, islandTrailDistance, riverCenter,
} from './island-terrain.js';

const cube = new THREE.BoxGeometry();
const round = new THREE.SphereGeometry(1, 11, 8);
const rock = new THREE.DodecahedronGeometry(1, 0);
const leafy = new THREE.IcosahedronGeometry(1, 1);
const bark = new THREE.CylinderGeometry(0.39, 0.59, 1, 7);
const clay = new THREE.MeshStandardMaterial({ color: '#7b5436', roughness: 0.95 });
const rootWood = new THREE.MeshStandardMaterial({ color: '#513a2b', roughness: 1 });
const creekStone = new THREE.MeshStandardMaterial({ color: '#84958b', roughness: 0.95 });
const sunnyStone = new THREE.MeshStandardMaterial({ color: '#c4ab86', roughness: 0.95 });
const deepStone = new THREE.MeshStandardMaterial({ color: '#73756e', roughness: 1 });
const moss = new THREE.MeshStandardMaterial({ color: '#7ea746', roughness: 0.95 });
const plank = new THREE.MeshStandardMaterial({ color: '#a7744f', roughness: 0.93 });
const white = new THREE.MeshStandardMaterial({ color: '#f9e5cf', roughness: 0.95 });
const ear = new THREE.MeshStandardMaterial({ color: '#cc977d', roughness: 0.95 });
const black = new THREE.MeshBasicMaterial({ color: '#1e2828' });
const cloudSkin = new THREE.MeshBasicMaterial({ color: '#e9f7ef', transparent: true, opacity: 0.9, fog: true });

function mesh(parent, geometry, material, position, scale) {
  const model = new THREE.Mesh(geometry, material);
  model.position.set(...position);
  if (scale) model.scale.set(...scale);
  parent.add(model);
  return model;
}

function randomFrom(seed) {
  return () => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    return seed / 4294967296;
  };
}

function ground(scene, radius) {
  const meadow = new THREE.TextureLoader().load('/meadow.jpg');
  meadow.colorSpace = THREE.SRGBColorSpace;
  meadow.wrapS = meadow.wrapT = THREE.RepeatWrapping;
  meadow.anisotropy = 8;
  const grass = new THREE.MeshStandardMaterial({ roughness: 0.98, vertexColors: true });
  grass.onBeforeCompile = shader => {
    shader.uniforms.meadow = { value: meadow };
    shader.vertexShader = 'attribute float turf; varying float meadowWeight; varying vec2 meadowUV;\n' + shader.vertexShader;
    shader.vertexShader = shader.vertexShader.replace('#include <begin_vertex>', `
      #include <begin_vertex>
      meadowUV = position.xz * 0.14;
      meadowWeight = turf;
    `);
    shader.fragmentShader = 'uniform sampler2D meadow; varying float meadowWeight; varying vec2 meadowUV;\n' + shader.fragmentShader;
    shader.fragmentShader = shader.fragmentShader.replace('#include <color_fragment>', `
      #include <color_fragment>
      vec3 grassGrain = texture2D(meadow, meadowUV).rgb;
      float grain = sin(meadowUV.x * 159.0) * sin(meadowUV.y * 231.0) * .025;
      diffuseColor.rgb *= mix(vec3(.83 + grain), grassGrain * 1.36 + vec3(.28), meadowWeight);
    `);
  };
  const geometry = new THREE.BufferGeometry();
  const { start, step, count } = ISLAND_GRID;
  const positions = [];
  const colors = [];
  const turf = [];
  const indices = [];
  const meadowGreen = new THREE.Color('#86ad63');
  const meadowGold = new THREE.Color('#a0ac6d');
  const slate = new THREE.Color('#838179');
  const sandstone = new THREE.Color('#c0a27b');
  const tint = new THREE.Color();
  for (let iz = 0; iz < count; iz++) {
    for (let ix = 0; ix < count; ix++) {
      const rawX = start + ix * step;
      const rawZ = start + iz * step;
      const clipping = Math.min(1, radius / (Math.hypot(rawX, rawZ) || 1));
      const x = rawX * clipping;
      const z = rawZ * clipping;
      const y = clipping === 1 ? islandGridHeight(ix, iz) : islandHeight(x, z);
      positions.push(x, y, z);

      const slope = Math.hypot(islandHeight(x + step, z) - islandHeight(x - step, z),
        islandHeight(x, z + step) - islandHeight(x, z - step)) / (2 * step);
      const flecks = 0.5 + Math.sin(x * 1.87 + z * 0.25) * Math.cos(z * 2.53 - x * 0.61) * 0.5;
      const stone = THREE.MathUtils.smoothstep(slope + (flecks - 0.5) * 0.23, 0.63, 1.37);
      const trail = 1 - THREE.MathUtils.smoothstep(islandTrailDistance(x, z) + (flecks - 0.5) * 0.36, 0.5, 1.3);
      const bank = 1 - THREE.MathUtils.smoothstep(Math.abs(z - riverCenter(x)), 1.66, 2.82);
      tint.copy(meadowGreen).lerp(meadowGold, 0.5 + Math.sin(x * 0.44 - z * 0.54) * 0.35)
        .lerp(slate, stone).lerp(sandstone, Math.max(trail, bank * 0.82));
      colors.push(tint.r, tint.g, tint.b);
      turf.push((1 - stone) * (1 - Math.max(trail, bank)));
      if (ix + 1 < count && iz + 1 < count && Math.hypot(rawX + step / 2, rawZ + step / 2) < radius + step) {
        const i = iz * count + ix;
        indices.push(i, i + count, i + 1, i + 1, i + count, i + count + 1);
      }
    }
  }
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geometry.setAttribute('turf', new THREE.Float32BufferAttribute(turf, 1));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  const surface = mesh(scene, geometry, grass, [0, 0, 0]);
  surface.castShadow = surface.receiveShadow = true;

  const rim = new THREE.BufferGeometry();
  const rimPoints = [];
  const rimColors = [];
  const rimFaces = [];
  const strata = [
    [1, 0.04, '#78985d'], [1.015, -1.2, '#697958'], [0.98, -3.4, '#8e8066'],
    [0.925, -6.7, '#686f67'], [0.83, -10.6, '#9c866c'], [0.70, -15.5, '#626c66'],
    [0.49, -20.1, '#817165'], [0.23, -23.2, '#5c6663'], [0, -24.5, '#62645d'],
  ];
  const divisions = 160;
  for (const [layer, [width, depth, shade]] of strata.entries()) {
    for (let i = 0; i <= divisions; i++) {
      const angle = i * Math.PI * 2 / divisions;
      const scallop = Math.sin(angle * 15 + layer * 0.35) * 0.51 + Math.cos(angle * 27 - layer * 0.61) * 0.27;
      const distance = radius * width + (layer && width ? scallop : 0);
      const x = Math.cos(angle) * distance;
      const z = Math.sin(angle) * distance;
      const top = islandHeight(Math.cos(angle) * radius, Math.sin(angle) * radius);
      rimPoints.push(x, top * width + depth + (layer && width ? scallop * 1.14 : 0), z);
      tint.set(shade).multiplyScalar(1 + scallop * 0.15);
      rimColors.push(tint.r, tint.g, tint.b);
      if (layer > 0 && i < divisions) {
        const current = layer * (divisions + 1) + i;
        const above = current - divisions - 1;
        rimFaces.push(above, current, above + 1, above + 1, current, current + 1);
      }
    }
  }
  rim.setAttribute('position', new THREE.Float32BufferAttribute(rimPoints, 3));
  rim.setAttribute('color', new THREE.Float32BufferAttribute(rimColors, 3));
  rim.setIndex(rimFaces);
  rim.computeVertexNormals();
  mesh(scene, rim, new THREE.MeshStandardMaterial({
    vertexColors: true, side: THREE.DoubleSide, roughness: 1, flatShading: true,
  }), [0, 0, 0]).receiveShadow = true;

  const random = randomFrom(293);
  for (let i = 0; i < 32; i++) {
    const angle = i * Math.PI * 2 / 32 + random() * 0.1;
    const distance = 31.3 + random() * 1.3;
    const x = Math.cos(angle) * distance;
    const z = Math.sin(angle) * distance;
    if (Math.abs(z - riverCenter(x)) < 3.2) continue;
    const height = islandHeight(x, z);
    const boulder = mesh(scene, rock, i % 3 ? creekStone : sunnyStone, [x, height - 3.6 - random() * 2.5, z],
      [0.8 + random() * 1.9, 1.4 + random() * 2.5, 0.9 + random() * 1.65]);
    boulder.rotation.set(random() * 0.42, angle, random() * 0.6);
    boulder.castShadow = true;
  }
}

function lookouts(scene) {
  for (const [i, [x, z]] of ISLAND_TRAIL.entries()) {
    if (i !== 0 && i !== ISLAND_TRAIL.length - 1 && i % 3 !== 0) continue;
    const end = i === 0 || i === ISLAND_TRAIL.length - 1;
    const side = i === 0 ? -1 : 1;
    const postX = x + (end ? side * 1.75 : 0.38);
    const postZ = z + (end ? 0.25 : -1.42);
    const y = islandHeight(postX, postZ);
    mesh(scene, cube, rootWood, [postX, y + 0.64, postZ], [0.13, 1.28, 0.15]).castShadow = true;
    const board = mesh(scene, cube, plank, [postX, y + 1.21, postZ], [end ? 0.79 : 0.38, 0.27, 0.11]);
    board.rotation.y = end ? -side * 0.65 : 0.12;
    mesh(board, cube, white, [0, 0, 0.52], [0.47, 0.18, 0.16]);
    if (!end) continue;
    for (const [dx, dz] of [[side * 1.7, 1.35], [side * 0.5, 2.2], [side * 2.0, -1.2]]) {
      const bx = x + dx;
      const bz = z + dz;
      const base = islandHeight(bx, bz);
      const cairn = mesh(scene, rock, creekStone, [bx, base + 0.21, bz], [0.39, 0.30, 0.40]);
      cairn.castShadow = true;
      mesh(scene, rock, sunnyStone, [bx + 0.04, base + 0.53, bz], [0.27, 0.18, 0.27]);
    }
  }
}

function ribbon(scene, material, sections) {
  const positions = [];
  const uv = [];
  const indices = [];
  sections.forEach(([first, second], i) => {
    positions.push(...first, ...second);
    uv.push(i / (sections.length - 1), 0, i / (sections.length - 1), 1);
    if (i > 0) indices.push(i * 2 - 2, i * 2 - 1, i * 2, i * 2, i * 2 - 1, i * 2 + 1);
  });
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  geometry.setIndex(indices);
  mesh(scene, geometry, material, [0, 0, 0]);
}

function water(scene, radius) {
  const surface = new THREE.ShaderMaterial({
    side: THREE.DoubleSide,
    uniforms: { phase: { value: 0 } },
    vertexShader: `
      varying vec2 point;
      void main() {
        point = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.);
      }`,
    fragmentShader: `
      uniform float phase;
      varying vec2 point;
      void main() {
        float bands = sin(point.x * 57. + point.y * 9. - phase * 2.8) * .5 + .5;
        float ribbons = sin(point.x * 23. - point.y * 22. + phase * 1.9) * .5 + .5;
        float foam = pow(bands * ribbons, 6.) * .7;
        float shore = smoothstep(.77, .985, abs(point.y * 2. - 1.));
        vec3 blue = mix(vec3(.021,.31,.32), vec3(.069,.52,.44), shore);
        gl_FragColor = vec4(blue + foam * vec3(.49,.53,.43) + shore * .10, 1.);
        #include <colorspace_fragment>
      }`,
  });
  const sections = [];
  let head;
  let tail;
  for (let i = 0; i <= 184; i++) {
    const x = -radius + i * radius * 2 / 184;
    const center = riverCenter(x);
    if (Math.hypot(x, Math.abs(center) + 2.03) > radius + 0.06) continue;
    head ??= [x, center];
    tail = [x, center];
    sections.push([[x, ISLAND_WATER_LEVEL, center - 1.95], [x, ISLAND_WATER_LEVEL, center + 1.95]]);
  }
  const outlets = [[head, -1], [tail, 1]].map(([[, z], direction]) => {
    const lip = [-1.95, 1.95].map(offset => [
      direction * (Math.sqrt(radius * radius - (z + offset) ** 2) + 0.14), ISLAND_WATER_LEVEL, z + offset,
    ]);
    if (direction < 0) sections.unshift(lip);
    else sections.push(lip);
    return { lip, z, direction };
  });
  ribbon(scene, surface, sections);
  const creek = Array.from({ length: 70 }, (_, i) => {
    const z = -19 + i * 16.8 / 69;
    const x = creekCenter(z);
    const width = Math.min(0.64, 0.10 + i * 0.14);
    const y = creekLevel(z) + ISLAND_WATER_LEVEL - 0.007;
    return [[x - width, y, z], [x + width, y, z]];
  });
  ribbon(scene, surface, creek);
  for (const [x, z, size] of [[-7.2, -19.2, 0.77], [-6.2, -19.7, 0.61], [-6.88, -20.1, 1.03], [-5.87, -17.9, 0.43]]) {
    mesh(scene, rock, creekStone, [x, Math.max(creekLevel(z), islandHeight(x, z)) + size * 0.24, z], [size, size * 0.59, size]);
  }

  const veil = surface.clone();
  veil.uniforms.phase = surface.uniforms.phase;
  veil.fragmentShader = veil.fragmentShader
    .replace('vec3(.021,.31,.32), vec3(.069,.52,.44)', 'vec3(.12,.54,.59), vec3(.42,.77,.78)')
    .replace('pow(bands * ribbons, 6.) * .7', 'pow(bands * ribbons, 4.) * .95 + pow(bands, 12.) * .43 * (1. - point.x)');
  const spray = [];
  const foam = new THREE.MeshBasicMaterial({
    color: '#d9f8ef', transparent: true, opacity: 0.64, depthWrite: false,
  });
  const random = randomFrom(704);
  for (const { lip, z, direction } of outlets) {
    const reach = direction < 0 ? 15.1 : 11.2;
    for (const [x, , bank] of lip) {
      const shelf = mesh(scene, rock, sunnyStone, [x - direction * 0.58, -0.65, bank + Math.sign(bank - z) * 0.49],
        [1.18, 0.61, 0.46]);
      shelf.rotation.set(0.07, direction * 0.26, -direction * 0.33);
    }
    const cascade = Array.from({ length: 25 }, (_, i) => {
      const t = i / 24;
      return lip.map(([x, , edge], side) => [
        x + direction * reach * t ** 0.83, ISLAND_WATER_LEVEL - 22.5 * t, edge + (side ? 1 : -1) * t * 0.92,
      ]);
    });
    ribbon(scene, veil, cascade);
    for (let i = 0; i < 27; i++) {
      const t = random();
      const particle = mesh(scene, round, foam, [0, 0, 0]);
      spray.push({
        particle, direction, reach, x: (lip[0][0] + lip[1][0]) * 0.5,
        z: z + (random() - 0.5) * (4.2 + t * 2.5), phase: t, size: 0.07 + random() * 0.31,
      });
    }
  }
  for (const crossing of ISLAND_CROSSINGS) {
    const center = riverCenter(crossing);
    const bridge = new THREE.Group();
    bridge.position.set(crossing, 0.085, center);
    scene.add(bridge);
    for (let i = -8; i <= 8; i++) {
      const cross = mesh(bridge, cube, i % 4 === 0 ? clay : plank, [0, 0, i * 0.30], [2.78, 0.092, 0.276]);
      cross.rotation.y = (i % 2 ? 1 : -1) * 0.018;
      cross.receiveShadow = true;
    }
    for (const side of [-1, 1]) {
      mesh(bridge, cube, rootWood, [side * 1.32, -0.051, 0], [0.095, 0.15, 5.2]);
      for (const z of [-2.45, 0, 2.45]) {
        mesh(bridge, bark, clay, [side * 1.34, 0.16, z], [0.22, 0.44, 0.22]);
      }
    }
  }
  return seconds => {
    surface.uniforms.phase.value = seconds;
    for (const { particle, x, z, direction, reach, phase, size } of spray) {
      const flow = (phase + seconds * 0.5) % 1;
      particle.position.set(x + direction * ((reach + 0.4) * flow ** 0.83 + 0.16), -23.4 * flow, z + Math.sin(seconds + phase * 28) * 0.21);
      const radius = size * (0.4 + flow);
      particle.scale.set(radius * 0.65, radius * 1.8, radius);
    }
  };
}

function nature(scene, layout) {
  const foliage = ['#519c59', '#7caf52', '#93bc56', '#548963', '#d19d61']
    .map(color => new THREE.MeshStandardMaterial({ color, roughness: 1, flatShading: true }));
  const random = randomFrom(3117);
  for (const site of layout.cover) {
    const root = new THREE.Group();
    root.position.set(site.x, site.ground, site.z);
    const height = site.h - site.ground;
    root.rotation.y = site.angle ?? 0;
    scene.add(root);
    if (site.strata) {
      for (const [offset, level, width, thickness, material] of [
        [0.01, 0.09, 0.68, 0.53, deepStone],
        [-0.06, 0.46, 0.65, 0.28, creekStone],
        [0.04, 0.66, 0.61, 0.24, sunnyStone],
        [-0.08, 0.84, 0.49, 0.19, creekStone],
        [-0.13, 0.94, 0.42, 0.09, moss],
      ]) {
        const ledge = mesh(root, rock, material, [site.w * offset, height * level, 0],
          [site.w * width, height * thickness, site.d * width]);
        ledge.rotation.set(0.06, 0.15, -0.09);
        ledge.castShadow = ledge.receiveShadow = true;
      }
    } else if (site.nature === 'rock') {
      const stone = mesh(root, rock, random() > 0.5 ? creekStone : sunnyStone,
        [0, height * 0.33 - 0.24, 0], [site.w * 0.65, height * 0.75 + 0.24, site.d * 0.62]);
      stone.castShadow = true;
      stone.receiveShadow = true;
      const cap = mesh(root, rock, moss, [-0.13, height * 0.81, 0.03], [site.w * 0.49, height * 0.24, site.d * 0.47]);
      cap.rotation.y = random() * 2;
    } else {
      const trunk = mesh(root, bark, clay, [0, 1.81, 0], [site.w, 3.65, site.d]);
      trunk.castShadow = true;
      const leaf = foliage[Math.floor(random() * foliage.length)];
      for (const [x, y, z, size] of [[0, 3.67, 0, 1.52], [-0.93, 3.46, -0.27, 1.13], [0.91, 3.64, 0.35, 1.19], [0.30, 4.60, -0.27, 1.31]]) {
        const crown = mesh(root, leafy, leaf, [x, y, z], [size, size * 0.85, size * 0.92]);
        crown.rotation.y = random() * Math.PI;
        crown.castShadow = true;
      }
    }
  }

  const flowers = ['#f9dd6d', '#f0aebf', '#fff1d4']
    .map(color => new THREE.MeshBasicMaterial({ color }));
  const plant = new THREE.ConeGeometry(0.12, 0.33, 4).translate(0, 0.12, 0);
  const flower = new THREE.SphereGeometry(0.077, 6, 4);
  const stems = new THREE.InstancedMesh(plant, foliage[1], 220);
  const blossoms = flowers.map(material => new THREE.InstancedMesh(flower, material, 80));
  const placement = new THREE.Matrix4();
  const counts = [0, 0, 0];
  let total = 0;
  while (total < 220) {
    const angle = random() * Math.PI * 2;
    const distance = Math.sqrt(random()) * (layout.edgeRadius - 0.8);
    const x = Math.cos(angle) * distance;
    const z = Math.sin(angle) * distance;
    if (Math.abs(z - riverCenter(x)) < 2.8 || islandFord(x, z) || islandTrailDistance(x, z) < 1.25) continue;
    const ground = islandHeight(x, z);
    stems.setMatrixAt(total, placement.makeTranslation(x, ground + 0.01, z));
    const index = total % 3;
    blossoms[index].setMatrixAt(counts[index]++, placement.makeTranslation(x, ground + 0.31 + random() * 0.09, z));
    total++;
  }
  stems.instanceMatrix.needsUpdate = true;
  scene.add(stems);
  blossoms.forEach((item, i) => {
    item.count = counts[i];
    item.instanceMatrix.needsUpdate = true;
    scene.add(item);
  });
}

function wildlife(scene) {
  const hares = [];
  for (const [i, spot] of [[-8, -12], [14, 11], [-17, 13]].entries()) {
    const body = new THREE.Group();
    scene.add(body);
    const fur = i % 2 ? white : new THREE.MeshStandardMaterial({ color: '#b78360', roughness: 0.98 });
    mesh(body, round, fur, [0, 0.29, 0.06], [0.30, 0.30, 0.46]);
    mesh(body, round, fur, [0, 0.49, -0.23], [0.24, 0.24, 0.28]);
    mesh(body, round, white, [0, 0.33, 0.45], [0.135, 0.13, 0.15]);
    for (const side of [-1, 1]) {
      mesh(body, round, fur, [side * 0.12, 0.78, -0.23], [0.085, 0.32, 0.095]).rotation.z = side * 0.19;
      mesh(body, round, ear, [side * 0.126, 0.82, -0.294], [0.048, 0.237, 0.035]).rotation.z = side * 0.19;
      mesh(body, round, black, [side * 0.17, 0.54, -0.37], [0.034, 0.033, 0.032]);
    }
    hares.push({ body, spot, phase: i * 1.925 });
  }
  const ducks = [];
  for (let i = 0; i < 3; i++) {
    const body = new THREE.Group();
    scene.add(body);
    mesh(body, round, white, [0, 0.15, 0.12], [0.33, 0.23, 0.50]);
    mesh(body, round, white, [0, 0.42, -0.22], [0.21, 0.20, 0.21]);
    mesh(body, cube, new THREE.MeshStandardMaterial({ color: '#e49a45' }), [0, 0.36, -0.42], [0.24, 0.082, 0.17]);
    for (const side of [-1, 1]) mesh(body, round, black, [side * 0.12, 0.475, -0.35], [0.03, 0.03, 0.03]);
    ducks.push({ body, phase: i * 0.85 });
  }
  return seconds => {
    for (const { body, spot, phase } of hares) {
      const age = seconds * 0.47 + phase;
      const x = spot[0] + Math.cos(age) * 1.32;
      const z = spot[1] + Math.sin(age) * 1.14;
      body.position.set(x, islandHeight(x, z) + Math.max(0, Math.sin(age * 7)) * 0.36, z);
      body.rotation.y = Math.atan2(Math.sin(age), -Math.cos(age));
    }
    for (const { body, phase } of ducks) {
      const age = seconds * 0.12 + phase * 0.21;
      const x = 8.4 + Math.sin(age) * 3.5 + phase * 0.43;
      const z = riverCenter(x) + Math.sin(phase) * 0.32;
      const slope = riverCenter(x + 0.09) - riverCenter(x - 0.09);
      const direction = Math.sign(Math.cos(age));
      body.position.set(x, 0.045 + Math.sin(seconds * 3.9 + phase) * 0.055, z);
      body.rotation.y = Math.atan2(-direction, -slope / 0.18 * direction);
    }
  };
}

function sky(scene) {
  const daylight = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false,
    vertexShader: `
      varying vec3 ray;
      void main() {
        ray = position;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.);
      }`,
    fragmentShader: `
      varying vec3 ray;
      void main() {
        vec3 direction = normalize(ray);
        vec3 horizon = vec3(.64, .86, .76);
        vec3 sky = mix(horizon, vec3(.18, .52, .72), smoothstep(-.05, .65, direction.y));
        float glow = pow(max(0., dot(direction, normalize(vec3(-.53, .31, -.61)))), 12.);
        gl_FragColor = vec4(sky + glow * vec3(.36, .24, .11), 1.);
        #include <colorspace_fragment>
      }`,
  });
  mesh(scene, new THREE.SphereGeometry(230, 26, 16), daylight, [0, 0, 0]).renderOrder = -1;

  const grass = new THREE.MeshStandardMaterial({ color: '#7ca877', roughness: 1 });
  const stone = new THREE.MeshStandardMaterial({ color: '#658b81', roughness: 1, flatShading: true });
  for (const [x, y, z, size] of [[-25, 7, 66, 6.6], [49, 12, 64, 4.8], [64, 3, -18, 5.6], [-36, 10, -61, 8], [-69, 6, 7, 5.9]]) {
    const islet = mesh(scene, rock, stone, [x, y - size * 0.58, z], [size, size * 0.92, size * 0.7]);
    islet.rotation.set(0.13, x * 0.13, z * 0.016);
    mesh(scene, leafy, grass, [x, y, z], [size * 0.97, size * 0.25, size * 0.71]);
    mesh(scene, bark, clay, [x - size * 0.17, y + size * 0.34, z], [size * 0.15, size * 0.54, size * 0.15]);
    mesh(scene, leafy, grass, [x - size * 0.18, y + size * 0.72, z], [size * 0.34, size * 0.41, size * 0.32]);
  }
  const clouds = [];
  const rand = randomFrom(12);
  for (let i = 0; i < 10; i++) {
    const group = new THREE.Group();
    scene.add(group);
    for (let j = 0; j < 3; j++) {
      mesh(group, round, cloudSkin, [j * 2.86, Math.sin(j * 1.34) * 0.7, 0], [3.43 - j * 0.35, 1.36 + rand() * 0.52, 1.86]);
    }
    clouds.push({ group, angle: i * Math.PI * 0.2, height: 12 + rand() * 7, extent: 48 + rand() * 19 });
  }
  mesh(scene, round, new THREE.MeshBasicMaterial({ color: '#fff1b6', fog: false }), [-53, 31, -61], [4.6, 4.6, 4.6]);
  return seconds => clouds.forEach(item => {
    const angle = item.angle + seconds * 0.013;
    item.group.position.set(Math.cos(angle) * item.extent, item.height, Math.sin(angle) * item.extent);
    item.group.rotation.y = -angle;
  });
}

export function installIsland(scene, layout) {
  ground(scene, layout.edgeRadius);
  lookouts(scene);
  nature(scene, layout);
  const stream = water(scene, layout.edgeRadius);
  const animals = wildlife(scene);
  const clouds = sky(scene);
  return time => {
    const seconds = time / 1000;
    stream(seconds);
    animals(seconds);
    clouds(seconds);
  };
}
