import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Match } from '../match.js';
import { CHARACTERS, DEFAULT_WEAPON, WEAPONS } from '../public/rules.js';

test('pilot choice controls the starting weapon and resets pickups on respawn', () => {
  for (const [id, kit] of Object.entries(CHARACTERS)) {
    const match = new Match('PILOTS', 'tdm', false);
    const socket = { id };
    assert(match.join(socket, id, 0, id));
    const pilot = socket.player;
    assert.equal(pilot.weapon, kit.weapon);
    assert.deepEqual(pilot.loadout.pulse, { ammo: WEAPONS.pulse.magazine, reserve: Infinity });
    if (kit.weapon !== DEFAULT_WEAPON) assert.deepEqual(pilot.loadout[kit.weapon], { ammo: WEAPONS[kit.weapon].magazine, reserve: WEAPONS[kit.weapon].magazine });
    const pickup = match.layout.pickups.find(item => item.type === kit.weapon || (kit.weapon === DEFAULT_WEAPON && item.type === 'rift'));
    Object.assign(pilot, pickup);
    match.updatePickups(1);
    assert.deepEqual(pilot.loadout[pickup.type], { ammo: WEAPONS[pickup.type].magazine, reserve: WEAPONS[pickup.type].reserve });
    match.spawn(pilot, 2);
    assert.equal(pilot.weapon, kit.weapon);
    assert.equal(pilot.loadout[kit.weapon].ammo, WEAPONS[kit.weapon].magazine);
    assert.equal(pilot.loadout.rift, undefined);
  }
});

test('a full bot squad has all four starting weapons and can switch to Pulse', () => {
  const match = new Match('BOTS', 'tdm', true);
  match.fillBots(0);
  for (let team = 0; team < 3; team++) {
    const squad = [...match.players.values()].filter(pilot => pilot.team === team);
    assert.equal(new Set(squad.map(pilot => pilot.character)).size, 4);
    assert.deepEqual(new Set(squad.map(pilot => pilot.weapon)), new Set(Object.values(CHARACTERS).map(kit => kit.weapon)));
    const specialist = squad.find(pilot => pilot.weapon !== DEFAULT_WEAPON);
    const specialistAmmo = specialist.loadout[specialist.weapon];
    specialistAmmo.ammo = 0;
    match.step(0.1);
    assert.notEqual(specialist.weapon, DEFAULT_WEAPON);
    assert(specialist.reloadAt > 0);
    specialistAmmo.reserve = 0;
    match.step(0.15);
    assert.equal(specialist.weapon, DEFAULT_WEAPON);
  }
});
