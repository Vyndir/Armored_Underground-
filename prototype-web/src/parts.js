// Part catalogue. All names are original to this project.
// Each part has: id, name, price, weight, ap (armor points) plus slot-specific stats.

export const SLOTS = ['head', 'core', 'arms', 'legs', 'booster', 'generator', 'weapon', 'shoulder'];

export const SLOT_LABELS = {
  head: 'HEAD', core: 'CORE', arms: 'ARMS', legs: 'LEGS',
  booster: 'BOOSTER', generator: 'GENERATOR', weapon: 'R-ARM UNIT', shoulder: 'SHOULDER UNIT',
};

export const PARTS = {
  head: [
    { id: 'hd_pike', name: 'HD-01 PIKE', price: 0, weight: 3, ap: 300, lockRange: 140, desc: 'Standard sensor head. Reliable lock range.' },
    { id: 'hd_moth', name: 'HD-07 MOTH', price: 1800, weight: 2, ap: 180, lockRange: 190, desc: 'Featherweight wide-band sensors. Long lock range, thin plating.' },
    { id: 'hd_bastion', name: 'HD-22 BASTION', price: 2600, weight: 5, ap: 560, lockRange: 120, desc: 'Armored dome. Short-sighted but tough.' },
  ],
  core: [
    { id: 'cr_ferro', name: 'CR-10 FERRO', price: 0, weight: 12, ap: 2200, boostMult: 1.0, desc: 'Balanced frame core.' },
    { id: 'cr_wasp', name: 'CR-31 WASP', price: 3200, weight: 8, ap: 1500, boostMult: 1.08, desc: 'Slim racing core with optimized thrust ducts. +8% boost speed.' },
    { id: 'cr_anvil', name: 'CR-50 ANVIL', price: 4800, weight: 18, ap: 3300, boostMult: 0.97, desc: 'Brick of a core. Shrugs off fire.' },
  ],
  arms: [
    { id: 'ar_hand', name: 'AR-02 HANDLER', price: 0, weight: 7, ap: 800, fireRate: 1.0, desc: 'General-purpose manipulators.' },
    { id: 'ar_needle', name: 'AR-15 NEEDLE', price: 2200, weight: 5, ap: 520, fireRate: 1.18, desc: 'Precision servo arms. +18% fire rate.' },
    { id: 'ar_hauler', name: 'AR-40 HAULER', price: 2600, weight: 10, ap: 1250, fireRate: 0.9, desc: 'Industrial arms, heavily plated.' },
  ],
  legs: [
    { id: 'lg_strider', name: 'LG-03 STRIDER', price: 0, type: 'biped', weight: 14, ap: 1600, load: 62, speed: 1.0, jump: 1.0, grip: 1.0, desc: 'Bipedal all-rounder.' },
    { id: 'lg_hopper', name: 'LG-19 HOPPER', price: 2900, type: 'reverse', weight: 11, ap: 1200, load: 52, speed: 1.04, jump: 1.6, grip: 1.05, desc: 'Reverse-joint legs. Huge jumps, good for rooftop shortcuts.' },
    { id: 'lg_glide', name: 'LG-27 GLIDE', price: 4200, type: 'hover', weight: 13, ap: 1050, load: 56, speed: 1.16, jump: 0.75, grip: 0.72, desc: 'Hover skirt legs. Fastest on asphalt, drifts in corners.' },
    { id: 'lg_bulwark', name: 'LG-60 BULWARK', price: 5200, type: 'heavy', weight: 22, ap: 2900, load: 90, speed: 0.86, jump: 0.75, grip: 1.15, desc: 'Heavy tread-stance legs. Carries anything.' },
  ],
  booster: [
    { id: 'bs_flare', name: 'BS-1 FLARE', price: 0, weight: 3, ap: 0, boost: 1.0, drain: 1.0, qb: 1.0, desc: 'Standard booster.' },
    { id: 'bs_comet', name: 'BS-4 COMET', price: 3600, weight: 4, ap: 0, boost: 1.2, drain: 1.35, qb: 1.15, desc: 'Racing booster. +20% top boost speed, thirsty.' },
    { id: 'bs_tern', name: 'BS-2 TERN', price: 2400, weight: 2, ap: 0, boost: 1.08, drain: 0.78, qb: 0.85, desc: 'Efficient cruiser. Sips energy.' },
  ],
  generator: [
    { id: 'gn_ember', name: 'GN-A EMBER', price: 0, weight: 5, ap: 0, cap: 2000, regen: 620, desc: 'Standard generator.' },
    { id: 'gn_coil', name: 'GN-C COIL', price: 2600, weight: 7, ap: 0, cap: 2800, regen: 560, desc: 'Deep capacity cells.' },
    { id: 'gn_surge', name: 'GN-S SURGE', price: 3400, weight: 6, ap: 0, cap: 1800, regen: 950, desc: 'Rapid recovery output.' },
  ],
  weapon: [
    { id: 'wp_hornet', name: 'WR-HORNET RIFLE', price: 0, weight: 4, ap: 0, kind: 'bullet', dmg: 70, rate: 6, mag: 30, reload: 1.6, speed: 420, spread: 0.008, pellets: 1, desc: 'Assault rifle. Steady damage.' },
    { id: 'wp_gale', name: 'WR-GALE SMG', price: 1900, weight: 3, ap: 0, kind: 'bullet', dmg: 28, rate: 15, mag: 60, reload: 1.8, speed: 380, spread: 0.02, pellets: 1, desc: 'High fire rate, close range.' },
    { id: 'wp_maul', name: 'WR-MAUL SCATTER', price: 2700, weight: 6, ap: 0, kind: 'bullet', dmg: 34, rate: 1.6, mag: 6, reload: 2.2, speed: 340, spread: 0.06, pellets: 8, desc: 'Scattergun. Devastating up close.' },
    { id: 'wp_lance', name: 'WR-LANCE RAIL', price: 5600, weight: 8, ap: 0, kind: 'rail', dmg: 520, rate: 0.7, mag: 4, reload: 3.0, speed: 1400, spread: 0.0, pellets: 1, desc: 'Charged rail cannon. One heavy hit.' },
  ],
  shoulder: [
    { id: 'sh_swarm', name: 'SH-SWARM MISSILES', price: 0, weight: 5, ap: 0, kind: 'missile', count: 4, dmg: 90, cooldown: 4.5, desc: 'Four homing micro-missiles. Needs lock.' },
    { id: 'sh_mines', name: 'SH-CALTROP MINES', price: 2200, weight: 4, ap: 0, kind: 'mine', count: 3, dmg: 320, cooldown: 5, desc: 'Drops proximity mines behind you. Racer favorite.' },
    { id: 'sh_spear', name: 'SH-SPEAR MISSILE', price: 3800, weight: 7, ap: 0, kind: 'missile', count: 1, dmg: 420, cooldown: 6, desc: 'Single heavy homing warhead.' },
  ],
};

export const DEFAULT_LOADOUT = {
  head: 'hd_pike', core: 'cr_ferro', arms: 'ar_hand', legs: 'lg_strider',
  booster: 'bs_flare', generator: 'gn_ember', weapon: 'wp_hornet', shoulder: 'sh_swarm',
};

export const DEFAULT_PAINT = { primary: '#2b3340', secondary: '#c9ccd2', neon: '#00e5ff' };

export function getPart(slot, id) {
  return PARTS[slot].find(p => p.id === id) || PARTS[slot][0];
}

// Derive flight/combat stats from a loadout.
export function computeStats(loadout) {
  const p = {};
  for (const s of SLOTS) p[s] = getPart(s, loadout[s]);
  const ap = SLOTS.reduce((a, s) => a + (p[s].ap || 0), 0);
  const carried = SLOTS.filter(s => s !== 'legs').reduce((a, s) => a + p[s].weight, 0);
  const totalWeight = carried + p.legs.weight;
  const load = p.legs.load;
  const over = Math.max(0, carried - load);
  const weightFactor = Math.max(0.55, 1 - over * 0.025) * (1 - (totalWeight - 50) * 0.003);
  return {
    parts: p,
    ap,
    weight: totalWeight,
    carried,
    load,
    overweight: over > 0,
    groundSpeed: 30 * p.legs.speed * weightFactor,
    boostSpeed: 62 * p.booster.boost * p.core.boostMult * weightFactor,
    accel: 46 * weightFactor,
    grip: p.legs.grip,
    jump: 15 * p.legs.jump,
    enCap: p.generator.cap,
    enRegen: p.generator.regen,
    boostDrain: 420 * p.booster.drain,
    qbCost: 520 * p.booster.drain,
    qbForce: 48 * p.booster.qb,
    lockRange: p.head.lockRange,
    fireRateMult: p.arms.fireRate,
    hover: p.legs.type === 'hover',
  };
}
