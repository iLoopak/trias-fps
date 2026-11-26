import { SNAPSHOT_INTERVAL } from './netcode.js';

const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
const blend = (a, b, t) => a + (b - a) * t;
const angle = (a, b, t) => a + Math.atan2(Math.sin(b - a), Math.cos(b - a)) * t;
const MAX_EXTRAPOLATION = 42;

export class SnapshotClock {
  latest = undefined;
  jitter = 0;
  rendered = -Infinity;

  receive(server, local) {
    if (!Number.isFinite(server)) return false;
    const previous = this.latest;
    if (previous && server <= previous.server) return false;
    const delay = local - server;
    const reset = !previous || server - previous.server > 450 || local - previous.local > 450;
    if (reset) {
      this.jitter = 0;
      this.offset = delay;
      this.rendered = -Infinity;
    } else {
      const variance = Math.abs((local - previous.local) - (server - previous.server));
      this.jitter = blend(this.jitter, Math.min(160, variance), 0.13);
      this.offset = Math.min(delay, this.offset + 0.30);
    }
    this.latest = { server, local };
    return reset;
  }

  time(local) {
    if (!this.latest) return undefined;
    const buffer = clamp(SNAPSHOT_INTERVAL * 1.35 + this.jitter * 2, 45, 123);
    const estimate = Math.min(local - this.offset - buffer, this.latest.server + MAX_EXTRAPOLATION);
    this.rendered = Math.max(this.rendered, estimate);
    return this.rendered;
  }
}

export function rememberPose(history, time, pose) {
  if (!Number.isFinite(time) || time <= (history.at(-1)?.time ?? -Infinity)) return;
  history.push({ time, pose });
  if (history.length > 10) history.shift();
}

// Independent from authoritative state and local input prediction.
export function presentedPose(history, time, out) {
  if (!Number.isFinite(time) || !history.length) return false;
  while (history.length > 2 && history[1].time <= time) history.shift();
  const first = history[0];
  const last = history[1] ?? first;
  const a = first.pose;
  const b = last.pose;
  const t = first === last ? 1 : clamp((time - first.time) / (last.time - first.time), 0, 1);
  out.x = blend(a.x, b.x, t);
  out.y = blend(a.y, b.y, t);
  out.z = blend(a.z, b.z, t);
  out.yaw = angle(a.yaw, b.yaw, t);
  out.pitch = blend(a.pitch, b.pitch, t);
  out.vx = blend(a.vx, b.vx, t);
  out.vz = blend(a.vz, b.vz, t);
  const stance = t < 0.5 ? a : b;
  out.grounded = stance.grounded;
  out.sprint = stance.sprint;
  out.crouch = stance.crouch;
  out.dodgeTime = stance.dodgeTime;
  out.reload = stance.reload;
  out.turret = stance.turret;
  const ahead = clamp(time - last.time, 0, MAX_EXTRAPOLATION) / 1000;
  if (ahead && b.grounded && b.turret < 0 && b.vehicle < 0) {
    out.x += b.vx * ahead;
    out.z += b.vz * ahead;
  }
  return true;
}
