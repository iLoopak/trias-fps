import { WEAPONS } from './public/rules.js';

export class Practice {
  constructor(room, pilot, now) {
    this.room = room;
    this.pilot = pilot;
    this.phase = 0;
    this.lane = room.layout.revision ? 6.8 : 0;
    this.placePilot();
    this.target = room.addPlayer('practice-target', 'Cvičný terč', 1, 0, now);
    Object.assign(this.target, { hp: 0, away: true, respawnAt: Infinity });
  }

  placePilot() {
    Object.assign(this.pilot, { x: this.lane, y: 0, z: this.lane ? -28 : -24, yaw: -Math.PI, pitch: 0, weapon: 'pulse' });
    this.pilot.loadout.pulse = { ammo: WEAPONS.pulse.magazine, reserve: Infinity };
  }

  dodge(player) {
    if (player === this.pilot && this.phase === 0) this.phase = 1;
  }

  echo(player, now) {
    if (player !== this.pilot || this.phase !== 1) return;
    this.phase = 2;
    this.lineReady = false;
    this.targetAt = now + .6;
  }

  perk(player, key) {
    if (player === this.pilot && key === 'scan' && this.phase === 2 && player.chain >= 3) this.phase = 3;
  }

  step(now) {
    if (this.phase !== 2 || this.pilot.chain >= 3 || this.target.hp > 0 || now < this.targetAt) return;
    if (!this.lineReady) {
      this.room.spawn(this.pilot, now);
      this.placePilot();
      this.lineReady = true;
    }
    this.room.spawn(this.target, now);
    Object.assign(this.target, { x: this.lane, y: 0, z: (this.lane ? -20.5 : -14) - (this.pilot.chain % 3) * 1.35,
      yaw: 0, hp: 65, safeUntil: now });
    this.targetAt = now + .8;
  }

  snapshot() { return [this.phase, Math.min(3, this.pilot.chain)]; }
}
