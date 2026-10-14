# Continuation note: waterfront environment-art pass

**Branch:** `waterfront-artpass`

**Base:** `waterfront-blockout` at f4bcc31, tagged `checkpoint/artpass-baseline`.

**Stage checkpoints (all pushed):**

| Tag | Contents |
|---|---|
| `checkpoint/artpass-stage1` | vegetation LOD and cost reduction |
| `checkpoint/artpass-stage2` | 17 m doorway, facade and concrete materials, warm light |
| `checkpoint/artpass-stage3` | water, coast, mountains, bridge, city districts, arenas |
| `checkpoint/artpass-stage4` | dusk preset and night refinement (**last stable**) |

The review package is in `screenshots/waterfront_artpass/`: before, after, after_night and after_dusk views, the perf CSVs and the QA logs.

## Decisions in force
- **Door:** 24 m wide × 17 m clear, now the default. `--door=WxH` still overrides it.
- **Wingspan:** `Site.WING_CLEARANCE_ASSUMED = 14` is a planning assumption and is **not verified**. It must be rechecked against the finished Kestrel at in-game scale, including any transformation motion near the doorway.
- **Gait:** v02 is unchanged; F4 opens the tuning panel. The user's F4 playtest is **pending**.
- **Arenas:** **provisional.** The positions are `BuildDistrict.CHAMPIONSHIP_ARENA` and `GROUND_ARENA`. Final placement needs ground-travel and jet-flight testing.
- **Off limits:**
  - the Kestrel Blender master: no Blender renders or bakes;
  - mech scale and movement;
  - new gameplay systems.

## How to run and test (one Godot process at a time; check Blender is idle first)
- **Godot:** `C:/Users/miles/AppData/Local/Programs/Godot/4.7.2-stable/Godot_v4.7.2-stable_win64_console.exe`
- **Play:** `--path godot`. Options after `--`: `--skip-intro`, `--laptop`, `--dusk`, `--night`.
- **Review views:** `--path godot -- --autotest <dir> --skip-intro --review`, optionally with `--night` or `--dusk`.
- **Full QA:** `--path godot -- --autotest <dir> --skip-intro`.
- **Slopes only:** `--path godot -- --autotest <dir> --skip-intro --slopes-only`.
- **Performance:** `--path godot -- --autotest <dir> --skip-intro --perf-tour --perf <dir>`. Add `--laptop` for the lighter preset.

## Results at stage 4
1080p on an RTX 3050 Ti Laptop, 133 s gameplay route, no Blender batch jobs during either run:

| Preset | Average fps | 1% low | Draw calls (avg / max) | Primitives (avg / max) | VRAM |
|---|---|---|---|---|---|
| Default | 65.6 | 51.4 | 1,989 / 3,537 | 3.22 M / 4.75 M | 1.78 GB |
| Laptop | 96.9 | 76.4 | 1,465 / 2,477 | 2.33 M / 3.34 M | 0.92 GB |

**Earlier figures on the same route.** These are historical runs, not proof of current performance.

| Run | Default fps (1% low) | Laptop fps (1% low) | Primitives |
|---|---|---|---|
| Blockout record | 47.1 (34.2) | 73.5 (59.4) | 10.6 M / 7.6 M |
| Stage 1 | 66.9 (51.9) | 98.2 (79.6) | — |

**QA:**
- **Onboarding:** all 7 steps pass, then free roam.
- **Water exits:** both exits work.
- **Boundary:** 52 attempts, worst 0.00 m outside.
- **Camera:** collision OK.
- **Scale:** 6.800 m to the helmet.
- **Jump:** apex 2.98 m.
- **Walk:** 13.5 m/s, a step every 0.36 s.
- **Slopes:** all pass. The old "south rocks" timeout was a test fault, not a traversal failure: its target lay 10 m outside the boundary fence, and its line ran into a lamp post. The test now targets points 3 m inside the fence.

## Next candidates
1. Bespoke models for the hangar facade and portal: the current geometry is still generated boxes.
2. Better yard lamp spill at night.
3. More legible bridge lamps.
4. Gentler shoreline foam.
5. Billboard art.
