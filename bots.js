import { PILOT_HP, TICK, TURRET, WEAPONS, aimVector, blocked, canDodge, distanceSquared, eyeHeight, radial, turretYaw, wallDistance } from './public/rules.js';
import { brainFor, roll, WEAPON_ROLES } from './bot-profiles.js';
import { bearing, inView, senseBots, visible } from './bot-perception.js';
import { chooseObjective, chooseWeapon, retreatPoint } from './bot-choices.js';
export { visible } from './bot-perception.js';

import { boardingVehicle, vehicleCockpit, VEHICLES, wrapAngle } from './public/vehicles.js';
const navigation = new WeakMap();

function navigationFor(layout) {
  if (navigation.has(layout)) return navigation.get(layout);
  const width = Math.max(20, Math.ceil(layout.length / 1.55));
  const step = layout.length * 2 / width;
  const cells = Array.from({ length: width ** 2 }, (_, i) => ({
    x: (i % width + 0.5) * step - layout.length,
    z: (Math.floor(i / width) + 0.5) * step - layout.length,
  }));
  const open = cell => !blocked(cell.x, cell.z, 0.65, layout.terrain?.(cell.x, cell.z) ?? 0, layout);
  const edges = cells.map((cell, i) => {
    if (!open(cell)) return [];
    return [i % width ? i - 1 : -1, i % width < width - 1 ? i + 1 : -1, i - width, i + width]
      .filter(next => cells[next] && open(cells[next])
        && open({ x: (cell.x + cells[next].x) / 2, z: (cell.z + cells[next].z) / 2 }));
  });
  const grid = { cells, edges, step, width, length: layout.length };
  navigation.set(layout, grid);
  return grid;
}

function nearestCell(position, { cells, edges, width, step, length }) {
  const x = Math.max(0, Math.min(width - 1, Math.floor((position.x + length) / step)));
  const z = Math.max(0, Math.min(width - 1, Math.floor((position.z + length) / step)));
  let index = -1, best = Infinity;
  for (let dz = -3; dz <= 3; dz++) for (let dx = -3; dx <= 3; dx++) {
    if (x + dx < 0 || x + dx >= width || z + dz < 0 || z + dz >= width) continue;
    const i = (z + dz) * width + x + dx;
    if (!edges[i].length) continue;
    const dist = distanceSquared(position, cells[i]);
    if (dist < best) { index = i; best = dist; }
  }
  if (index >= 0) return index;
  for (let i = 0; i < cells.length; i++) {
    if (!edges[i].length) continue;
    const dist = distanceSquared(position, cells[i]);
    if (dist < best) { index = i; best = dist; }
  }
  return index;
}

export function route(from, to, layout) {
  const grid = navigationFor(layout);
  const { cells, edges } = grid;
  const start = nearestCell(from, grid);
  const end = nearestCell(to, grid);
  if (start < 0 || end < 0 || start === end) return [];
  const parents = new Int16Array(cells.length).fill(-1);
  const queue = [start];
  parents[start] = start;
  for (let head = 0; head < queue.length && parents[end] === -1; head++) {
    for (const neighbor of edges[queue[head]]) {
      if (parents[neighbor] !== -1) continue;
      queue.push(neighbor);
      parents[neighbor] = queue[head];
    }
  }
  if (parents[end] === -1) return [];
  const path = [];
  for (let node = end; node !== start; node = parents[node]) path.push(cells[node]);
  return path.reverse();
}

function driveBot(room, player, brain, now) {
  const hull = room.fleet.hulls[player.vehicle];
  const spec = VEHICLES[hull.kind];
  const angle = Math.atan2(hull.x, hull.z);
  const elapsed = now - hull.mountedAt;
  if (elapsed > 23 || hull.hp < spec.hp * 0.23) {
    return { x: 0, z: 0, brake: true, use: Math.abs(hull.speed) < 3.5 };
  }
  if (!hull.patrolDirection) {
    const clockwise = radial(angle + 0.26, room.layout.roadRadius);
    const toward = Math.atan2(hull.x - clockwise.x, hull.z - clockwise.z);
    hull.patrolDirection = Math.abs(wrapAngle(toward - hull.heading)) < Math.PI / 2 ? 1 : -1;
  }
  const next = radial(angle + hull.patrolDirection * 0.31, room.layout.roadRadius);
  const desired = Math.atan2(hull.x - next.x, hull.z - next.z);
  const turn = wrapAngle(desired - hull.heading);
  if (Math.abs(hull.speed) < 1.05 && elapsed > 1.5 && now > (hull.recoverAfter ?? 0)) {
    hull.reverseUntil = now + 0.72;
    hull.recoverAfter = now + 2.0;
  }
  const reversing = now < (hull.reverseUntil ?? 0);
  const core = room.siege?.cores.find(item => item.opened & (1 << player.team));
  const baseInSight = core && distanceSquared(hull, core) < 35 ** 2 && visible(player, { ...core, y: 0 }, room.layout);
  const target = (baseInSight && { ...core, y: 0, vx: 0, vz: 0 }) ||
    (brain.target && brain.reactionAt <= now && distanceSquared(hull, brain.target) < 34 ** 2 ? brain.target : null);
  let yaw = desired;
  let pitch = 0;
  if (target) {
    const flight = spec.gun.projectile ? Math.sqrt(distanceSquared(target, hull)) / spec.gun.projectile.speed : 0;
    const x = target.x + target.vx * flight * 0.58;
    const z = target.z + target.vz * flight * 0.58;
    yaw = Math.atan2(hull.x - x, hull.z - z);
    pitch = Math.atan2(target.y + eyeHeight(target) * 0.68 - vehicleCockpit(hull), Math.hypot(x - hull.x, z - hull.z));
  }
  const aim = guideBotAim(brain, yaw, pitch);
  const seenTarget = brain.target && room.players.get(brain.target.id);
  const shootable = baseInSight || target && seenTarget?.hp > 0 && visible(player,seenTarget,room.combatLayout);

  return {
    x: Math.max(-1, Math.min(1, -turn * 1.9)) * (reversing ? -1 : 1),
    z: reversing ? -0.76 : Math.abs(turn) > 0.84 ? 0.49 : 0.94,
    sprint: Math.abs(turn) < 0.21 && elapsed > 2 && hull.boost > 0.27,
    ...aim, fire: Boolean(shootable && elapsed > 0.75 && Math.abs(wrapAngle(yaw - aim.yaw)) < .11),
  };
}


export function guideBotAim(brain, wantedYaw, wantedPitch) {
  function axis(angle, rate, target, wrapped) {
    const gap = wrapped ? wrapAngle(target - angle) : target - angle;
    const max = brain.tuning.turn;
    const desired = Math.max(-max, Math.min(max, gap * 8));
    const acceleration = 22 * TICK * brain.variation;
    rate += Math.max(-acceleration, Math.min(acceleration, desired - rate));
    return [angle + rate * TICK, rate];
  }
  [brain.lookYaw, brain.yawRate] = axis(brain.lookYaw, brain.yawRate, wantedYaw, true);
  [brain.lookPitch, brain.pitchRate] = axis(brain.lookPitch, brain.pitchRate, wantedPitch, false);
  return { yaw: brain.lookYaw, pitch: Math.max(-1.48,Math.min(1.48,brain.lookPitch)) };
}

function targetAim(player, brain, target, spec, now) {
  const speed = Math.hypot(player.vx, player.vz);
  const projected = spec.projectile ? target.distance / spec.projectile.speed * .60 : 0;
  const x = target.x + (target.vx ?? 0) * projected, z = target.z + (target.vz ?? 0) * projected;
  const distance = Math.hypot(x - player.x, z - player.z);
  const error = brain.tuning.error * brain.variation * WEAPON_ROLES[player.weapon].error
    * (.65 + Math.min(distance / 35,.70) + speed * .038 + Number(!player.grounded) * .19);
  const wobble = Math.sin(now * (4.4 + brain.variation) + player.slot * 1.7)
    + .36 * Math.sin(now * 12.7 + player.team);
  return {
    yaw: bearing(player,{ x,z }) + wobble * error + (brain.aimFault ?? 0),
    pitch: Math.atan2((target.y ?? 0) + eyeHeight(target) - .36 - player.y - eyeHeight(player),distance)
      + Math.cos(now * 5.2 + player.slot) * error * .54,
  };
}

function directionIsClear(from, to, layout) {
  const distance = Math.hypot(to.x-from.x,to.z-from.z);
  if (distance < .01) return true;
  const ray = { x: (to.x-from.x)/distance, y: 0, z: (to.z-from.z)/distance };
  return wallDistance({ x: from.x, y: from.y + .56, z: from.z },ray,distance,layout) >= distance - .04;
}

function trackMovement(room, player, brain, destination, now, travelling) {
  if (now < brain.movedAt) return;
  brain.movedAt = now + .63;
  if (brain.lastMove && travelling && player.grounded && distanceSquared(player,brain.lastMove) < .53 ** 2) {
    brain.stuck++;
    brain.path = [];
    brain.routeAt = -1;
    brain.recoverUntil = now + .46;
    brain.strafe *= -1;
    room.botMetrics.stuck++;
    if (brain.stuck % 3 === 0) {
      brain.memory = brain.hearing = null;
      brain.patrol = null;
      brain.planAt = -1;
    }
  } else brain.stuck = 0;
  brain.lastMove = { x: player.x, z: player.z };
}

function tryEcho(room, player, brain, enemy, aim, now) {
  const signature = room.signature;
  const echo = signature?.echoes.get(player.id);
  if (!echo || !enemy || now < brain.echoAt || !signature.enabled(player) ||
      player.reloadAt || player.fireAt > now || player.loadout[player.weapon].ammo <= 0 ||
      !['retreat','flank','engage'].includes(brain.intent)) return null;
  brain.echoAt = now + .22;
  if (roll(brain) > brain.traits.echo * brain.tuning.tactics * (brain.intent === 'engage' ? .31 : .82)) return null;
  const layout = room.combatLayout ?? room.layout;
  if (distanceSquared(player,echo) < 1.15 ** 2 || !visible(echo,enemy,layout)) return null;
  const spec = WEAPONS[player.weapon];
  const from = { x: echo.x,y: echo.y + eyeHeight(echo),z: echo.z };
  const realTarget = { x: enemy.x,y: enemy.y + eyeHeight(enemy) - .36,z: enemy.z };
  const distance = Math.hypot(realTarget.x-from.x,realTarget.y-from.y,realTarget.z-from.z);
  if (distance < .5 || distance > spec.range) return null;
  const shot = aimVector(aim.yaw,aim.pitch);
  const eye = { x: player.x,y:player.y+eyeHeight(player),z:player.z };
  const wall = wallDistance(eye,shot,spec.range,layout);
  const line = { x: eye.x+shot.x*wall-from.x,y:eye.y+shot.y*wall-from.y,z:eye.z+shot.z*wall-from.z };
  const len = Math.hypot(line.x,line.y,line.z);
  if (len <= .14) return null;
  const error = Math.hypot(from.x+line.x/len*distance-realTarget.x,from.y+line.y/len*distance-realTarget.y,from.z+line.z/len*distance-realTarget.z);
  if (error > (spec.projectile ? 1.65 : .94)) return null;
  return { yaw: aim.yaw, pitch: aim.pitch };
}

export function botInput(room, player, now) {
  const brain = brainFor(room,player);
  const layout = room.combatLayout ?? room.layout;
  senseBots(room,player,brain,now);
  if (player.vehicle >= 0) return driveBot(room,player,brain,now);
  if (player.turret >= 0) {
    const turret = room.turrets[player.turret];
    const target = brain.target;
    brain.turretUntil ??= now+12;
    if (now >= brain.turretUntil || player.hp < PILOT_HP * brain.traits.retreat) {
      player.vehiclePlanAt = now + 7;
      brain.turretUntil = null;
      brain.planAt = -1;
      return { x: 0, z: 0, use: true };
    }
    const last = target && room.players.get(target.id);
    const ideal = target ? targetAim(player,brain,target,TURRET,now) :
      { yaw: turret.yaw + Math.sin(now*.92)*.55,pitch: 0 };
    const aim = guideBotAim(brain,ideal.yaw,ideal.pitch);
    return { ...aim, fire: target != null && brain.reactionAt <= now && last?.hp > 0 &&
      Math.abs(wrapAngle(ideal.yaw-turretYaw(aim.yaw,turret.yaw))) < .09 && visible(player,last,layout) };
  }
  const enemy = brain.target;
  const spec = WEAPONS[player.weapon];
  const inventory = player.loadout[player.weapon];
  if (now >= brain.planAt || !brain.objective) {
    brain.planAt = now + .36 + roll(brain) * .25;
    const previous = brain.intent;
    brain.objective = chooseObjective(room,player,brain,now);
    brain.intent = brain.objective.intent;
    if (previous !== brain.intent) {
      brain.routeAt = -1;
      const line = { 'flank':'flank','retreat':'retreat','guard-point':'cover','guard-base':'cover',
        'return-flag':'flag','defend-core':'core','engage':'contact' }[brain.intent];
      brain.radio = line ? { line, until: now + 3 } : null;
    }
    if (brain.radio && (now > brain.radio.until || room.radioBot(player,brain.radio.line,now))) brain.radio = null;
    brain.aimFault = enemy && roll(brain) < brain.tuning.mistake ? (roll(brain) < .5 ? -.096 : .096) : 0;
    brain.wantedWeapon = enemy ? chooseWeapon(player,brain,enemy) : player.weapon;
  }
  let goal = brain.objective.point;
  const core = brain.objective.core;
  const seesCore = core && distanceSquared(player,core)<spec.range ** 2 && visible(player,core,layout);
  if (now >= brain.routeAt) {
    brain.routeAt = now + .64 + roll(brain) * .54;
    brain.path = directionIsClear(player,goal,layout) ? [] : route(player,goal,room.layout);
  }
  while (brain.path.length && distanceSquared(player,brain.path[0]) < 1.0 ** 2) brain.path.shift();
  const inner = room.layout.gantries[player.team*2+1];
  const flight = now - player.launchAt;
  const airborneRoute = inner && (brain.traits.mobility > .57 || brain.kind === 'marksman') && !player.grounded && flight > .29 && flight < 1.57;
  if (airborneRoute) goal = inner;
  const destination = airborneRoute ? goal : brain.path[0] ?? goal;
  const travelling = distanceSquared(player,destination) > (brain.intent === 'engage' ? 2 : 1.5) ** 2;
  const markedEcho = !enemy && !seesCore && brain.echoReactAt <= now ? brain.hostileEcho : null;
  const aimTarget = enemy ?? markedEcho;
  const ideal = aimTarget ? targetAim(player,brain,aimTarget.distance == null ?
    { ...aimTarget,distance:Math.sqrt(distanceSquared(player,aimTarget)) } : aimTarget,spec,now)
    : { yaw: bearing(player,seesCore ? core : destination),pitch: seesCore ?
      Math.atan2(core.y-player.y-eyeHeight(player),Math.sqrt(distanceSquared(player,core))) : 0 };
  if (!aimTarget && !seesCore && !travelling) {
    ideal.yaw = player.yaw + .30 * brain.strafe;
  }
  const aim = guideBotAim(brain,ideal.yaw,ideal.pitch);
  const relative = bearing(player,destination) - aim.yaw;
  let x = travelling ? -Math.sin(relative) : 0;
  let z = travelling ? Math.cos(relative) : 0;
  const tactical = enemy && (brain.intent === 'engage' || brain.intent === 'flank');
  if (tactical && !airborneRoute && (brain.intent !== 'flank' || enemy.distance < brain.traits.range * .89)) {
    const direct = (enemy.distance - brain.traits.range) / 8;
    x = Math.sin(now * (2.5 + brain.traits.mobility) + player.slot) * .59 * brain.strafe;
    z = Math.max(-.76,Math.min(.95,direct));
    if (brain.kind === 'aggressor' && enemy.distance > 4) z = Math.max(.31,z);
  }
  trackMovement(room,player,brain,destination,now,travelling && !tactical);
  if (now < brain.recoverUntil) {
    x = brain.strafe * .76;
    z = -.42;
  }
  const carrying = brain.intent === 'return-flag';
  const dodging = now >= brain.dodgeAt && !carrying && canDodge(player)
    && (enemy && enemy.distance < 19 && (brain.intent === 'retreat' || brain.traits.mobility > .42)
      || now < brain.recoverUntil && player.grounded);
  let dodge = false;
  if (dodging) {
    brain.dodgeAt = now + .52 + roll(brain)*1.02;
    dodge = roll(brain) < brain.traits.mobility * brain.tuning.tactics + (brain.intent === 'retreat' ? .24 : 0);
  }
  let jump = false;
  if (now >= brain.jumpAt && (player.grounded || player.airJump)) {
    const ready = airborneRoute && player.airJump && flight > .52 && player.vy < 1.5
      || brain.recoverUntil > now && player.grounded
      || enemy && brain.kind === 'acrobat' && (player.grounded || player.airJump && player.vy < 1.4);
    if (ready) {
      jump = true;
      brain.jumpAt = now + (player.grounded ? .36 : .85) + roll(brain) * .28;
    }
  }
  const weapon = now >= brain.switchAt && brain.wantedWeapon !== player.weapon && !player.reloadAt ? brain.wantedWeapon : null;
  if (weapon) brain.switchAt = now + 1.1 + roll(brain) * .65;
  const engaged = enemy && brain.reactionAt <= now || markedEcho;
  const aligned = Math.abs(wrapAngle(ideal.yaw-aim.yaw)) < (player.weapon === 'scatter' ? .19 : .082)
    && Math.abs(ideal.pitch-aim.pitch) < .12;
  let fire = Boolean(!weapon && aligned && inventory.ammo && (engaged || seesCore));
  let firingAt;
  if (fire && player.fireAt <= now && aimTarget) {
    // A cached position can guide a turn, but an actual shot needs fresh line of sight.
    firingAt = enemy ? room.players.get(enemy.id) : markedEcho;
    const live = enemy ? firingAt?.hp > 0 : markedEcho.expires > now && room.signature.echoes.get(markedEcho.owner.id) === markedEcho;
    fire = Boolean(live && inView(player,firingAt,aim.yaw) && visible(player,firingAt,layout) &&
      distanceSquared(player,firingAt) < (player.weapon === 'scatter' ? 13 : spec.range) ** 2);
    if (!fire && enemy) brain.senseAt = Math.max(now + .03,brain.senseAt-.07);
  }
  const echo = fire && firingAt && enemy && !dodge ? tryEcho(room,player,brain,enemy,ideal,now) : null;
  if (echo) brain.echoDecision = brain.intent;
  else if (!room.signature?.echoes.has(player.id)) brain.echoDecision = null;
  if (room.botDebug && now >= (brain.debugAt ?? 0)) {
    brain.debugAt = now + 4;
    console.error(JSON.stringify({ kind: 'bot', callsign: player.name, personality: brain.kind,difficulty: brain.difficulty,
      intent: brain.intent,target: enemy?.id,memory: brain.memory,heard: brain.hearing,waypoints: brain.path.length,
      recoveries: brain.stuck, echo: brain.echoDecision }));
  }
  return {
    ...aim,x,z,weapon,fire: fire && !echo,echo,jump,dodge,
    sprint: airborneRoute || !enemy && travelling || carrying || brain.intent === 'retreat' || tactical && z > .7 && brain.traits.push > .65,
    crouch: Boolean(enemy && brain.kind === 'marksman' && enemy.distance > 18 && Math.abs(z) < .2 && !dodge && player.grounded),
    use: Boolean(brain.objective.vehicle && boardingVehicle(player,room.fleet.hulls)===brain.objective.vehicle ||
      brain.objective.turret && distanceSquared(player,brain.objective.turret) < TURRET.dismountRadius ** 2),
    reload: Boolean(inventory.reserve && !weapon && (!enemy && inventory.ammo<spec.magazine*.38 ||
      brain.intent === 'retreat' && !fire && inventory.ammo<spec.magazine*.24)),
  };
}
