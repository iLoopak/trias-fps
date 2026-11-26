import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ARMORY } from '../public/rules.js';
import { recoilPose, reloadPose } from '../public/weapon-motion.js';

test('each weapon has a distinct kick that returns to rest', () => {
  const opening = ARMORY.map(key => recoilPose(key, 0, false));
  assert.equal(new Set(opening.map(pose => pose.travel)).size, ARMORY.length);
  assert(recoilPose('scatter', 0, true).travel > recoilPose('scatter', 0, false).travel);
  assert(recoilPose('scatter', 135, false).cycle > recoilPose('scatter', 0, false).cycle);
  for (const weapon of ARMORY) for (const value of Object.values(recoilPose(weapon, 750, false))) assert.equal(Math.abs(value), 0);
});

test('reload lowers and opens the weapon, with no offset after completion', () => {
  for (const weapon of ARMORY) {
    const middle = reloadPose(weapon, 0.46);
    assert(middle.pitch > 0 && middle.tray > 0 && middle.reach > 0);
    for (const progress of [null, 0, 1]) {
      for (const value of Object.values(reloadPose(weapon, progress))) assert.equal(Math.abs(value), 0);
    }
  }
});
