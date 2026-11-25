const SHOTS = {
  pulse: { fallback: [172, 43, 0.1, 'sawtooth'], gain: 0.74 },
  scatter: { fallback: [100, 21, 0.21, 'triangle'], gain: 0.64 },
  lance: { fallback: [252, 47, 0.28, 'sawtooth'], gain: 0.73 },
  arc: { fallback: [285, 68, 0.07, 'square'], gain: 0.53 },
};

export class GameAudio {
  constructor() {
    this.buffers = new Map();
  }

  preload() {
    this.files ??= Promise.all([...Object.keys(SHOTS), 'pickup'].map(async key => {
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

  shot(key, local, distance, pan) {
    const shot = SHOTS[key];
    const level = local ? 1 : 0.68 * (1 - distance / 29) ** 2;
    if (!this.sample(key, shot.gain * level, pan, 0.97 + Math.random() * 0.06)) {
      this.tone(...shot.fallback, 0.075 * level, pan);
    }
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
