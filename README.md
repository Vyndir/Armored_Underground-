# Armored Underground

Night-city street racing, but you drive a mech. An original browser game that mixes
underground racing (tuning, rival crews, a city full of race events, credits and rep)
with mech assembly and boost-skating combat.

Built with [Three.js](https://threejs.org/) and [Vite](https://vite.dev/). All art is
procedural; no external assets.

## Run it

Requires Node.js 18+.

```bash
npm install
npm run dev
```

Vite opens the game at http://localhost:5173. Click **IGNITE** to start.

`npm run build` produces a static build in `dist/` that you can host anywhere.

## Controls

| Input | Action |
| --- | --- |
| WASD | Move (camera relative) |
| Mouse | Look / aim (click the game to capture the mouse) |
| Shift (hold) | Boost, drains EN |
| Q or Left Ctrl | Quick boost (burst dodge / overtake) |
| Space | Jump, hold in the air to hover |
| Left mouse | Fire arm weapon (auto-aims at your lock target) |
| Right mouse | Shoulder unit (missiles need a lock; mines drop behind you) |
| R | Reload |
| E | Interact (garage, race markers) |
| Esc | Pause |

## What's in the vertical slice

- **City**: a procedural 10×10 block night city with lit windows, neon signage,
  streetlights and bloom. You can jump onto rooftops.
- **Mech assembly**: 8 part slots (head, core, arms, legs, booster, generator, arm weapon,
  shoulder unit), 26 parts. Parts change the model and the stats: AP, weight vs leg load
  limit, ground and boost speed, EN capacity and recovery, grip, jump, lock range, DPS.
  Leg types handle differently (bipeds, reverse-joint jumpers, drifty hover legs, heavy legs).
- **Garage (Rig Shop)**: the amber **G** on the minimap. Buy and equip parts, compare stat
  deltas on hover, and set primary, secondary and neon paint.
- **Race events**: four events (two sprints, two circuits) unlocked by rep, with rival mechs
  that race the route, manage their boost, and shoot, missile or mine you. Knock rivals down
  for bonus credits; if your AP hits zero you reboot at the last gate.
- **Progression**: prize credits and rep per finishing position, best results saved, all
  stored in browser localStorage.
- **HUD**: position and standings, lap/gate, race timer, AP and EN bars, weapon status,
  speedometer, lock-on box, heading-up minimap with the route ahead.

## Code map

| File | What it does |
| --- | --- |
| `src/main.js` | Renderer, camera, game states, player input, roam markers |
| `src/city.js` | City generation and collision (buildings are AABBs) |
| `src/parts.js` | Part catalogue and stat derivation |
| `src/mech.js` | Procedural mech model built from parts, animation |
| `src/actor.js` | Mech physics, energy, armor and weapon state |
| `src/weapons.js` | Bullets, rail, missiles, mines, explosions, particles |
| `src/race.js` | Race events, gates, grid, standings, payouts |
| `src/ai.js` | Rival racer driving and combat |
| `src/garage.js` | Rig shop UI and showroom scene |
| `src/hud.js` | HUD and minimap |
| `src/audio.js` | Synthesized engine, boost and weapon sounds |
| `src/save.js` | localStorage save |
