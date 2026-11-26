export const PERSONALITIES = Object.freeze({
  aggressor: { range: 8, retreat: .19, push: .89, flank: .16, mobility: .65, echo: .46, supply: .40, team: .36, weapon: 'scatter' },
  hunter: { range: 16, retreat: .32, push: .55, flank: .83, mobility: .52, echo: .88, supply: .61, team: .56, weapon: 'rift' },
  sentinel: { range: 19, retreat: .48, push: .25, flank: .23, mobility: .23, echo: .38, supply: .53, team: .90, weapon: 'arc' },
  marksman: { range: 28, retreat: .43, push: .21, flank: .55, mobility: .32, echo: .51, supply: .48, team: .39, weapon: 'lance' },
  acrobat: { range: 13, retreat: .27, push: .62, flank: .46, mobility: .96, echo: .71, supply: .51, team: .46, weapon: 'arc' },
  opportunist: { range: 15, retreat: .39, push: .52, flank: .54, mobility: .40, echo: .55, supply: .98, team: .62, weapon: 'pulse' },
});

export const BOT_DIFFICULTIES = Object.freeze({
  recruit: { reaction: .51, error: .080, turn: 3.1, sense: .32, tactics: .40, mistake: .23 },
  regular: { reaction: .33, error: .057, turn: 4.2, sense: .24, tactics: .66, mistake: .14 },
  veteran: { reaction: .24, error: .041, turn: 5.2, sense: .20, tactics: .85, mistake: .09 },
  elite: { reaction: .18, error: .032, turn: 6.1, sense: .17, tactics: 1, mistake: .06 },
});

export const WEAPON_ROLES = {
  pulse: { distance: 19, tolerance: 32, error: 1 },
  scatter: { distance: 7, tolerance: 9, error: 1.13 },
  arc: { distance: 13, tolerance: 25, error: 1.16 },
  lance: { distance: 27, tolerance: 30, error: .66 },
  rift: { distance: 14, tolerance: 20, error: .78 },
};

const roster = Object.keys(PERSONALITIES);
const difficulties = Object.keys(BOT_DIFFICULTIES);

export function normalizeDifficulty(value) {
  return value === 'mixed' || Object.hasOwn(BOT_DIFFICULTIES, value) ? value : 'regular';
}

function hash(value) {
  let result = 0x811c9dc5;
  for (const char of value) result = Math.imul(result ^ char.codePointAt(0), 0x01000193);
  return result >>> 0;
}

export function brainFor(room, player) {
  if (player.brain?.spawn === player.spawn) return player.brain;
  const seed = hash(`${room.code}:${player.id}`);
  const slot = player.team * 4 + player.slot;
  const kind = player.hordeType ? ({ runner: 'aggressor', striker: 'hunter', crusher: 'sentinel', sentinel: 'marksman' })[player.hordeType]
    : roster[slot % roster.length];
  const difficulty = room.botDifficulty === 'mixed' ? difficulties[(seed >>> 6) % difficulties.length] : normalizeDifficulty(room.botDifficulty);
  const brain = {
    spawn: player.spawn, kind, difficulty, traits: PERSONALITIES[kind], tuning: BOT_DIFFICULTIES[difficulty],
    seed: (seed ^ Math.imul(player.spawn, 0x865bacc3)) >>> 0,
    variation: .87 + ((seed >>> 10) % 25) * .01, strafe: seed & 1 ? 1 : -1,
    lookYaw: player.yaw, lookPitch: player.pitch, yawRate: 0, pitchRate: 0,
    sensed: [], target: null, memory: null, hearing: null, hostileEcho: null, reactionAt: Infinity,
    senseAt: -1, planAt: -1, routeAt: -1, dodgeAt: -1, jumpAt: -1, echoAt: -1, switchAt: -1,
    movedAt: -1, recoverUntil: -1, stuck: 0, path: [], objective: null, intent: 'patrol',
  };
  if (!brain.seed) brain.seed = 1;
  player.brain = brain;
  return brain;
}

export function roll(brain) {
  let x = brain.seed;
  x ^= x << 13;
  x ^= x >>> 17;
  x ^= x << 5;
  brain.seed = x >>> 0;
  return brain.seed / 0x100000000;
}
