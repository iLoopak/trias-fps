const SHOTS = {
  pulse: { fallback: [172, 43, 0.1, 'sawtooth'], gain: 0.74 },
  scatter: { fallback: [100, 21, 0.21, 'triangle'], gain: 0.64 },
  lance: { fallback: [252, 47, 0.28, 'sawtooth'], gain: 0.73 },
  arc: { fallback: [285, 68, 0.07, 'square'], gain: 0.53 },
  rift: { fallback: [448, 95, 0.24, 'triangle'], gain: 0.72, synthesized: true },
};
const LOCAL_TAILS = {
  pulse: [380, 152, 0.046, 'triangle', 0.077],
  scatter: [122, 38, 0.15, 'sine', 0.16],
  lance: [970, 262, 0.17, 'triangle', 0.074],
  arc: [1040, 491, 0.038, 'triangle', 0.052],
  rift: [596, 116, 0.255, 'triangle', 0.084],
};

export class GameAudio {
  constructor() {
    this.buffers = new Map();
  }

  preload() {
    this.files ??= Promise.all([...Object.keys(SHOTS).filter(key => !SHOTS[key].synthesized), 'pickup'].map(async key => {
      try {
        const response = await fetch('/sfx/' + key + '.mp3');
        if (response.ok) return [key, await response.arrayBuffer()];
      } catch { /* The tone mixer covers unavailable samples. */ }
      return [key, null];
    }));
    return this.files;
  }

  async unlock() {
    try {
      if (!this.context) {
        const context = new AudioContext({ latencyHint: 'interactive' });
        const mix = context.createDynamicsCompressor();
        mix.threshold.value = -15;
        mix.knee.value = 22;
        mix.ratio.value = 4;
        mix.attack.value = 0.003;
        mix.release.value = 0.15;
        mix.connect(context.destination);
        this.context = context;
        this.mix = mix;
        void this.preload().then(files => {
          for (const [key, data] of files) {
            if (data) void context.decodeAudioData(data).then(buffer => this.buffers.set(key, buffer)).catch(() => {});
          }
        });
      }
      if (this.context.state === 'suspended') await this.context.resume();
    } catch { /* Audio is optional. */ }
  }

  connect(source, gain, pan) {
    const panner = this.context.createStereoPanner();
    panner.pan.value = pan;
    source.connect(gain).connect(panner).connect(this.mix);
  }

  sample(key, volume, pan = 0, rate = 1) {
    if (this.context?.state !== 'running') return false;
    const buffer = this.buffers.get(key);
    if (!buffer) return false;
    const source = this.context.createBufferSource();
    const gain = this.context.createGain();
    gain.gain.value = volume;
    source.buffer = buffer;
    source.playbackRate.value = rate;
    this.connect(source, gain, pan);
    source.start();
    return true;
  }

  tone(from, to, duration, wave, volume, pan = 0, delay = 0) {
    if (this.context?.state !== 'running') return;
    const oscillator = this.context.createOscillator();
    const gain = this.context.createGain();
    const start = this.context.currentTime + delay;
    oscillator.type = wave;
    oscillator.frequency.setValueAtTime(from, start);
    oscillator.frequency.exponentialRampToValueAtTime(to, start + duration);
    gain.gain.setValueAtTime(volume, start);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
    this.connect(oscillator, gain, pan);
    oscillator.start(start);
    oscillator.stop(start + duration);
  }

  shot(key, local, distance, pan, alt = false) {
    const shot = SHOTS[key];
    const level = local ? 1 : 0.68 * (1 - distance / 29) ** 2;
    if (!this.sample(key, shot.gain * level, pan, (alt ? 0.81 : 0.97) + Math.random() * 0.06)) {
      this.tone(...shot.fallback, 0.086 * level, pan);
      if (key === 'rift') this.tone(93, 46, 0.21, 'sine', 0.16 * level, pan, 0.025);
    }
    if (local) {
      this.tone(...LOCAL_TAILS[key]);
      if (key === 'scatter') this.tone(390, 206, 0.055, 'square', 0.034, 0, 0.16);
    }
  }

  gunAction(key, action) {
    if (action === 'empty') {
      this.tone(570, 198, 0.045, 'square', 0.037);
    } else if (action === 'open') {
      this.tone(key === 'rift' ? 378 : 263, 97, 0.11, 'triangle', 0.064, -0.12);
      this.tone(680, 315, 0.048, 'square', 0.017, -0.12, 0.13);
    } else {
      this.tone(210, 83, 0.065, 'triangle', 0.070, 0.11);
      this.tone(key === 'arc' ? 1280 : 880, 495, 0.071, 'triangle', 0.045, 0.1, 0.037);
    }
  }

  signature(action, level = 1, pan = 0) {
    if (action === 'replay') {
      this.tone(182, 71, 0.25, 'triangle', 0.115);
      this.tone(1024, 558, 0.17, 'sine', 0.068, 0, 0.046);
    } else if (action === 'respawn') {
      this.tone(146, 365, 0.26, 'triangle', 0.099);
      this.tone(484, 840, 0.25, 'sine', 0.071, 0, 0.083);
      this.tone(780, 1180, 0.18, 'sine', 0.043, 0, 0.151);
    } else if (action === 'core') {
      this.tone(126, 28, 0.78, 'sine', 0.24 * level, pan);
      this.tone(511, 58, 0.59, 'triangle', 0.17 * level, pan, 0.035);
      this.tone(886, 135, 0.44, 'triangle', 0.083 * level, pan, 0.19);
    } else if (action === 'rift' && level > 0) {
      this.tone(660, 195, 0.20, 'triangle', 0.10 * level, pan);
      this.tone(940, 340, 0.28, 'sine', 0.074 * level, pan, 0.062);
    }
  }

  engine(vehicle) {
    if (this.context?.state !== 'running') return;
    if (vehicle && !this.motor) {
      const oscillator = this.context.createOscillator();
      const filter = this.context.createBiquadFilter();
      const volume = this.context.createGain();
      oscillator.type = 'sawtooth';
      filter.type = 'lowpass';
      filter.frequency.value = 145;
      volume.gain.value = 0;
      oscillator.connect(filter).connect(volume).connect(this.mix);
      oscillator.start();
      this.motor = { oscillator, filter, volume };
    }
    if (!this.motor) return;
    const { oscillator, filter, volume } = this.motor;
    const time = this.context.currentTime;
    if (!vehicle) {
      volume.gain.setTargetAtTime(0, time, 0.08);
      return;
    }
    const heavy = vehicle.kind === 'bastion';
    oscillator.frequency.setTargetAtTime((heavy ? 29 : 48) + Math.abs(vehicle.speed) * (heavy ? 2 : 3.1), time, 0.09);
    filter.frequency.setTargetAtTime(vehicle.boosting ? 255 : heavy ? 115 : 176, time, 0.09);
    volume.gain.setTargetAtTime(vehicle.boosting ? 0.095 : 0.036 + Math.abs(vehicle.speed) * 0.0015, time, 0.08);
  }

  grenadeThrow(distance, pan) {
    const level = Math.max(0, 1 - distance / 12);
    if (level) this.tone(449, 133, 0.12, 'triangle', 0.073 * level, pan);
  }

  explosion(distance, pan, grenade) {
    const level = Math.max(0, 1 - distance / 30);
    if (!level) return;
    this.tone(grenade ? 110 : 169, 39, 0.25, 'sawtooth', 0.18 * level, pan);
    this.tone(98, 31, 0.34, 'sine', 0.20 * level, pan);
  }

  movement(action, level = 1, pan = 0) {
    if (action === 'land') this.tone(112, 51, 0.08, 'triangle', 0.06);
    if (action === 'jump') this.tone(109, 196, 0.09, 'triangle', 0.048);
    if (action === 'double') this.tone(224, 390, 0.12, 'triangle', 0.078);
    if (action === 'dodge') this.tone(262, 99, 0.16, 'triangle', 0.078);
    if (action === 'launch') {
      this.tone(111, 344, 0.22, 'triangle', 0.13 * level, pan);
      this.tone(320, 713, 0.21, 'sine', 0.064 * level, pan, 0.044);
    }
  }

  footstep(level = 1, pan = 0) {
    this.tone(95 + Math.random() * 28, 48, 0.062, 'triangle', 0.044 * level, pan);
  }

  confirm(type) {
    if (type === 'body') {
      this.tone(1420, 840, 0.078, 'triangle', 0.105);
    } else if (type === 'shield') {
      this.tone(1770, 980, 0.11, 'sine', 0.104);
      this.tone(2280, 1520, 0.08, 'triangle', 0.055, 0, 0.036);
    } else {
      this.tone(720, 1120, 0.10, 'triangle', 0.114);
      this.tone(1480, 1010, 0.185, 'sine', 0.114, 0, 0.066);
    }
  }

  damage(shield, lethal, pan) {
    if (shield) {
      this.tone(880, 180, 0.14, 'triangle', 0.14, pan);
      this.tone(315, 190, 0.18, 'sine', 0.11, pan);
    } else {
      this.tone(lethal ? 128 : 184, 37, lethal ? 0.36 : 0.16, 'sawtooth', 0.15, pan);
      this.tone(91, 29, 0.19, 'sine', 0.19, pan);
    }
  }

  pickup() {
    if (!this.sample('pickup', 0.52)) this.tone(385, 770, 0.18, 'triangle', 0.077);
  }
}
