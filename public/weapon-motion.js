const profiles = {
  pulse:   { travel: 0.084, lift: 0.126, twist: 0.022, settle: 147, cycle: 0.035, dip: 0.37, roll: 0.30 },
  scatter: { travel: 0.183, lift: 0.237, twist: 0.076, settle: 365, cycle: 0.086, dip: 0.31, roll: -0.36 },
  lance:   { travel: 0.128, lift: 0.094, twist: 0.008, settle: 250, cycle: 0.043, dip: 0.22, roll: 0.49 },
  arc:     { travel: 0.048, lift: 0.060, twist: 0.040, settle: 95, cycle: 0.073, dip: 0.34, roll: -0.24 },
  rift:    { travel: 0.172, lift: 0.171, twist: 0.047, settle: 422, cycle: 0.107, dip: 0.43, roll: 0.33 },
};

const unit = value => Math.max(0, Math.min(1, value));
const smooth = (from, to, value) => {
  const t = unit((value - from) / (to - from));
  return t * t * (3 - 2 * t);
};

export function recoilPose(weapon, elapsed, alternate) {
  const config = profiles[weapon];
  const t = unit(elapsed / config.settle);
  const boost = alternate ? 1.30 : 1;
  const shove = (1 - t) ** 3 * boost;
  const rebound = Math.sin(t * Math.PI * 3.25) * (1 - t) ** 2;
  const mechanism = weapon === 'scatter'
    ? smooth(0.12, 0.39, t) * (1 - smooth(0.45, 0.85, t))
    : weapon === 'rift' ? Math.sin(t * Math.PI) * (1 - t) : (1 - t) ** 3;
  return {
    travel: config.travel * (shove - rebound * 0.25),
    pitch: config.lift * (shove + rebound * 0.31),
    roll: config.twist * (shove - rebound * 0.43),
    cycle: mechanism * config.cycle * boost,
    flash: (1 - t) ** 4,
  };
}

export function reloadPose(weapon, progress) {
  const config = profiles[weapon];
  if (progress == null) return { pitch: 0, roll: 0, tray: 0, reach: 0 };
  const settle = smooth(0, 0.23, progress) * (1 - smooth(0.68, 1, progress));
  const eject = smooth(0.19, 0.42, progress) * (1 - smooth(0.49, 0.76, progress));
  return {
    pitch: config.dip * settle,
    roll: config.roll * settle,
    tray: eject,
    reach: smooth(0.13, 0.36, progress) * (1 - smooth(0.59, 0.89, progress)),
  };
}
