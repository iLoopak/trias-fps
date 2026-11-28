#!/usr/bin/env node
// Kinetic Lab: repeatable movement measurements on the authoritative TRIAS rules (shared/rules.js, which the
// Unity Motor mirrors) and on the real server loop (match.js) with a simulated network. Every scenario is a
// fixed input script, the network uses a seeded generator, so two runs of the same code print the same numbers.
//
//   node tools/kinetic-lab.mjs            human-readable report
//   node tools/kinetic-lab.mjs --json     the same measurements as JSON (for before/after diffs)
//
// Only the rules decide the numbers here; presentation (camera, weapon, audio) is measured in Unity.
import { Match } from '../match.js';
import { ARENAS, movePlayer, packCover, RELAY_SERVICE, TICK } from '../shared/rules.js';
import { NETWORK_VERSION, unpackSnapshot } from '../shared/netcode.js';

const FLAT = { ...RELAY_SERVICE, id: 'lab-flat', length: 400, cover: [], launchPads: [] };
const LEDGE = { ...FLAT, id: 'lab-ledge', cover: packCover([{ x: 0, z: 0, w: 12, d: 400, bottom: 0, h: 2.4 }]) };
const MOTION = ['x', 'y', 'z', 'vx', 'vy', 'vz', 'grounded', 'airJump', 'airDodge', 'dodgeCooldown', 'dodgeTime',
  'padCooldown', 'crouch', 'jumpBuffer', 'dodgeBuffer', 'coyote'];

const pilot = (extra = {}) => ({ x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, grounded: true, airJump: true, airDodge: true,
  dodgeCooldown: 0, dodgeTime: 0, padCooldown: 0, crouch: false, ...extra });
const held = (extra = {}) => ({ x: 0, z: 0, yaw: 0, sprint: false, jump: false, dodge: false, crouch: false, ...extra });
const flat = p => Math.hypot(p.vx, p.vz);
const r = (value, digits = 3) => Math.round(value * 10 ** digits) / 10 ** digits;

// Run `script(tick, pilot)` -> input for `ticks` steps; returns the per-tick trace.
function run(state, ticks, script, layout = FLAT) {
  const trace = [];
  for (let tick = 0; tick < ticks; tick++) {
    const signal = movePlayer(state, script(tick, state), TICK, layout);
    trace.push({ tick, signal, x: state.x, y: state.y, z: state.z, vx: state.vx, vz: state.vz, speed: flat(state), vy: state.vy, grounded: state.grounded });
  }
  return trace;
}

const firstTick = (trace, test) => trace.find(test)?.tick ?? -1;

function ground() {
  const out = {};
  for (const [name, sprint, target] of [['walk', false, 7.55], ['sprint', true, 10.1]]) {
    const trace = run(pilot(), 30, () => held({ z: 1, sprint }));
    out[name + '_ticks_to_90pct'] = firstTick(trace, s => s.speed >= target * 0.9) + 1;
    out[name + '_ticks_to_full'] = firstTick(trace, s => s.speed >= target - 1e-6) + 1;
    const moving = pilot({ vz: -target });
    const stop = run(moving, 30, () => held());
    out[name + '_stop_ticks'] = firstTick(stop, s => s.speed < 0.01) + 1;
    out[name + '_stop_distance_m'] = r(-stop.at(-1).z);
  }
  const strafing = pilot({ vx: 7.55 });
  const reverse = run(strafing, 30, () => held({ x: -1 }));
  out.strafe_reverse_ticks = firstTick(reverse, s => s.vx <= -7.55 + 1e-6) + 1;
  // Sprinting forward, then a hard left: ticks until the lateral speed reaches walk pace.
  const turning = pilot({ vz: -10.1 });
  const lateral = run(turning, 30, () => held({ x: -1 }));
  out.sprint_to_lateral_ticks = firstTick(lateral, s => s.vx <= -7.55 * 0.9) + 1;
  return out;
}

function jumps() {
  const out = {};
  const single = pilot();
  const trace = run(single, 60, tick => held({ jump: tick === 0 }));
  out.jump_apex_m = r(Math.max(...trace.map(s => s.y)));
  out.jump_air_ticks = firstTick(trace.slice(1), s => s.grounded);
  const sprintJump = pilot({ vz: -10.1 });
  const carried = run(sprintJump, 40, tick => held({ z: 1, sprint: true, jump: tick === 0 }));
  const landing = firstTick(carried.slice(1), s => s.grounded);
  out.sprint_jump_distance_m = r(-carried[landing].z);
  // Released Shift mid-air: speed at touchdown.
  const releaseShift = pilot({ vz: -10.1 });
  const released = run(releaseShift, 40, tick => held({ z: 1, sprint: tick < 3, jump: tick === 0 }));
  const touchdown = released[firstTick(released.slice(1), s => s.grounded)];
  out.sprint_jump_release_shift_landing_speed = r(touchdown.speed);
  const double = pilot();
  const doubled = run(double, 80, tick => held({ jump: tick === 0 || tick === 12 }));
  out.double_jump_apex_m = r(Math.max(...doubled.map(s => s.y)));
  out.double_jump_air_ticks = firstTick(doubled.slice(1), s => s.grounded);
  return out;
}

function dodges() {
  const out = {};
  for (const [name, extra] of [['ground_idle', {}], ['ground_sprint', { vz: -10.1 }]]) {
    const state = pilot(extra);
    const trace = run(state, 45, tick => held({ z: 1, sprint: true, dodge: tick === 0 }));
    out[name + '_distance_0_5s_m'] = r(-trace[14].z);
    out[name + '_distance_1s_m'] = r(-trace[29].z);
    const land = firstTick(trace.slice(1), s => s.grounded);
    out[name + '_air_ticks'] = land;
    out[name + '_touchdown_speed'] = r(trace[land - 1].speed);
    out[name + '_speed_after_touchdown_4_ticks'] = r(trace[land + 3].speed);
  }
  // Ground dodge, then hold the direction or let go: distance at touchdown.
  for (const [name, hold] of [['ground_dodge_hold', true], ['ground_dodge_release', false]]) {
    const trace = run(pilot(), 30, tick => held({ z: hold || tick === 0 ? 1 : 0, dodge: tick === 0 }));
    const land = firstTick(trace.slice(1), s => s.grounded);
    out[name + '_touchdown_distance_m'] = r(-trace[land].z);
  }
  // Air dodge from a standing jump: hold the direction versus release the keys.
  for (const [name, hold] of [['air_dodge_hold', true], ['air_dodge_release', false]]) {
    const state = pilot();
    const trace = run(state, 60, tick => held({ z: tick <= 6 || hold ? 1 : 0, jump: tick === 0, dodge: tick === 6 }));
    const land = firstTick(trace.slice(7), s => s.grounded);
    out[name + '_distance_m'] = r(-trace[land].z);
    out[name + '_touchdown_speed'] = r(trace[land - 1].speed);
  }
  // Air dodge forward, then steer right while airborne.
  const steer = pilot();
  const steered = run(steer, 60, tick => held({ z: tick <= 6 ? 1 : 0, x: tick > 10 ? 1 : 0, jump: tick === 0, dodge: tick === 6 }));
  const land = firstTick(steered.slice(7), s => s.grounded);
  out.air_dodge_steer_distance_m = r(Math.hypot(steered[land].x, steered[land].z));
  out.air_dodge_steer_touchdown_speed = r(steered[land - 1].speed);
  return out;
}

// Inputs that arrive a few ticks early or late. A press counts if the matching action happens.
function forgiveness() {
  const out = {};
  // Double jump at the apex, then press jump k ticks before the first tick a ground jump is possible.
  const doubled = run(pilot(), 80, tick => held({ jump: tick === 0 || tick === 12 }));
  const possible = firstTick(doubled.slice(1), s => s.grounded) + 1;
  for (const early of [1, 2, 3, 4]) {
    const trace = run(pilot(), possible + 12, tick => held({ jump: tick === 0 || tick === 12 || tick === possible - early }));
    out[`jump_${early}_ticks_before_landing`] = trace.slice(possible - early).some(s => s.signal === 'jump') ? 'jumped' : 'lost';
  }
  // Dodge pressed k ticks before the cooldown ends.
  const cooldownTicks = Math.ceil(0.96 / TICK - 1e-9);
  for (const early of [1, 2, 3, 4]) {
    const trace = run(pilot(), cooldownTicks + 12, tick => held({ z: 1, dodge: tick === 0 || tick === cooldownTicks - early }));
    out[`dodge_${early}_ticks_before_ready`] = trace.slice(1).some(s => s.signal === 'dodge') ? 'dodged' : 'lost';
  }
  // Walk off a 2.4 m ledge and press jump k ticks after the first airborne tick.
  for (const late of [0, 1, 2, 3, 4]) {
    const state = pilot({ x: 5.4, y: 2.4 });
    let left = -1;
    const trace = run(state, 60, (tick, now) => {
      if (left < 0 && !now.grounded) left = tick;
      return held({ x: -1, jump: left >= 0 && tick === left + late });
    }, LEDGE);
    const signal = trace.find(s => s.signal === 'jump' || s.signal === 'double')?.signal ?? 'none';
    out[`ledge_jump_${late}_ticks_late`] = signal;
    out[`ledge_jump_${late}_ticks_late_double_left`] = signal === 'jump' ? 'yes' : 'no';
  }
  return out;
}

function mulberry32(seed) {
  return () => {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

// Client prediction and reconciliation against the real Match loop, over an in-order (TCP) link with jitter.
// Like TriasClient, commands leave on frame boundaries (60 fps unless `fps`) on a 30 Hz command clock: a frame
// up to one command late sends both, a longer stall re-bases (the server has bridged it by then).
// KINETIC_LAB_CLIENT=rebase models the client before this pass, which re-based on every late frame, so below
// 30 fps it sent fewer than 30 commands a second. `hitches` adds one 50-250 ms frame every ~3 s.
function network({ latency = 0.04, jitter = 0.03, seconds = 60, seed = 11, drift = 0.0005, arena = 'service', hitches = false, fps = 60 }) {
  const random = mulberry32(seed);
  const catchUp = process.env.KINETIC_LAB_CLIENT !== 'rebase';
  const layout = arena === 'service' ? RELAY_SERVICE : ARENAS[arena];
  const match = new Match('KINETIC', 'tdm', false, layout);
  const packets = [];
  const socket = { id: 'kinetic', protocolVersion: NETWORK_VERSION, readyState: 1, bufferedAmount: 0,
    send(message) { packets.push(message); }, terminate() {} };
  match.join(socket, 'Kinetic', 0);
  const player = socket.player;
  // Movement steps the server gave this pilot per tick; other players see 0 or 2 as a hitch.
  let steps = 0, uneven = 0, reserve = 0, reserveTicks = 0;
  if (match.advancePilot) {
    const advance = match.advancePilot.bind(match);
    match.advancePilot = (...args) => { steps++; return advance(...args); };
  }
  const up = [], down = [];
  let lastUp = 0, lastDown = 0, seq = 0, yaw = player.yaw;
  let predicted = null, commands = [], alive = true;
  const corrections = [];
  const phase = random() * TICK;
  let nextSend = 0.25, nextTick = phase, frameAt = 0, nextHitch = 3;
  const sendPeriod = TICK * (1 + drift), frame = 1 / fps * (1 + drift);
  const step = 0.0005;
  let mismatchedJumps = 0, serverJumps = 0, clientJumps = 0;
  for (let t = 0; t < seconds; t += step) {
    const frameNow = t >= frameAt;
    if (frameNow) {
      frameAt += frame;
      if (hitches && t >= nextHitch) { frameAt += 0.05 + random() * 0.2; nextHitch = t + 2 + random() * 2; }
    }
    for (let burst = 0; frameNow && t >= nextSend && t < seconds - 1 && burst < (catchUp ? 2 : 1); burst++) {
      nextSend = catchUp ? nextSend + sendPeriod : Math.max(nextSend + sendPeriod, t - sendPeriod * 0.6);
      if (catchUp && t - nextSend > sendPeriod) nextSend = t - sendPeriod * 0.6;
      const k = Math.floor(t / TICK);
      yaw += (0.45 + (Math.floor(t / 3.7) % 2) * 0.8) * TICK;
      const input = { seq: ++seq, x: Math.floor(t / 0.6) % 2 ? 1 : -1, z: 1, yaw: Math.round(yaw * 1000) / 1000, pitch: 0,
        sprint: true, jump: k % 78 === 0, dodge: k % 129 === 40, crouch: false, fire: false, reload: false, brake: false,
        alt: false, grenade: false, use: false, weapon: null };
      up.push({ at: Math.max(lastUp, t + latency + random() * jitter), input });
      lastUp = up.at(-1).at;
      if (predicted && alive) {
        commands.push(input);
        if (commands.length > 24) commands.shift();
        const signal = movePlayer(predicted, input, TICK, layout);
        if (signal === 'jump' || signal === 'double') clientJumps++;
      }
    }
    while (up.length && up[0].at <= t) match.input(player, up.shift().input, t);
    if (t >= nextTick) {
      nextTick += TICK;
      steps = match.advancePilot ? 0 : 1;
      match.step(t);
      if (t > 1 && t < seconds - 1 && player.hp > 0 && steps !== 1) uneven++;
      if (t > 1 && t < seconds - 1) { reserve += player.inputQueue?.length ?? 0; reserveTicks++; }
      while (packets.length) {
        down.push({ at: Math.max(lastDown, t + latency + random() * jitter), packet: packets.shift() });
        lastDown = down.at(-1).at;
      }
    }
    while (down.length && down[0].at <= t) {
      const snapshot = unpackSnapshot(JSON.parse(down.shift().packet));
      const own = snapshot.players.find(row => row.id === player.id);
      // Like the Unity client: no prediction while dead, and a respawn re-bases without counting a correction.
      if (!own || own.hp <= 0) { alive = false; predicted = null; commands = []; continue; }
      alive = true;
      for (const event of snapshot.events ?? []) if (event.type === 'movement' && event.id === player.id && /jump|double/.test(event.action)) serverJumps++;
      const before = predicted && { x: predicted.x, y: predicted.y, z: predicted.z };
      predicted = Object.fromEntries(MOTION.map(key => [key, own[key]]));
      commands = commands.filter(command => command.seq > own.ack);
      for (const command of commands) movePlayer(predicted, command, TICK, layout);
      if (before && t < seconds - 1) corrections.push(Math.hypot(before.x - predicted.x, before.y - predicted.y, before.z - predicted.z));
    }
  }
  mismatchedJumps = Math.abs(serverJumps - clientJumps);
  corrections.sort((a, b) => a - b);
  const over = limit => corrections.filter(c => c > limit).length;
  return {
    arena, hitches, fps, latency_ms: latency * 1000, jitter_ms: jitter * 1000, snapshots: corrections.length, deaths: player.deaths,
    corrections_over_1cm_per_min: r(over(0.01) / seconds * 60, 1), corrections_over_10cm_per_min: r(over(0.1) / seconds * 60, 1),
    correction_p99_cm: r(corrections[Math.floor(corrections.length * 0.99)] * 100, 1), correction_max_cm: r(corrections.at(-1) * 100, 1),
    jump_count_client_vs_server: `${clientJumps}/${serverJumps}`, jump_mismatch: mismatchedJumps,
    observer_uneven_ticks_per_min: r(uneven / seconds * 60, 1),
    queued_commands_avg: r(reserve / Math.max(1, reserveTicks), 2),
  };
}

const report = {
  ground: ground(),
  jumps: jumps(),
  dodges: dodges(),
  forgiveness: forgiveness(),
  network: [
    network({ latency: 0.02, jitter: 0 }),
    network({ latency: 0.04, jitter: 0.015 }),
    network({ latency: 0.04, jitter: 0.035 }),
    network({ latency: 0.08, jitter: 0.06 }),
    network({ latency: 0.04, jitter: 0.015, hitches: true }),
    network({ latency: 0.04, jitter: 0.015, fps: 25 }),
    network({ latency: 0.04, jitter: 0.015, arena: 'relay' }),
    network({ latency: 0.04, jitter: 0.015, arena: 'foundry' }),
  ],
};

if (process.argv.includes('--json')) console.log(JSON.stringify(report, null, 2));
else {
  for (const [section, values] of Object.entries(report)) {
    console.log(`\n${section.toUpperCase()}`);
    if (Array.isArray(values)) for (const row of values) console.log('  ' + Object.entries(row).map(([k, v]) => `${k}=${v}`).join('  '));
    else for (const [key, value] of Object.entries(values)) console.log(`  ${key.padEnd(44)} ${value}`);
  }
}
