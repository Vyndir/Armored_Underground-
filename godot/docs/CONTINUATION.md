# Continuation note: waterfront environment-art pass

**Branch:** `waterfront-artpass`

**Base:** `waterfront-blockout` at f4bcc31, tagged `checkpoint/artpass-baseline`.

**Stage checkpoints:**
- `checkpoint/artpass-stage1`: vegetation LOD and cost reduction.

## Decisions in force for this pass
- **Door:** 24 m wide × 17 m clear. The default changes from 11 m; `--door=WxH` still overrides it.
- **Wingspan:** 14 m is only a planning assumption and is not verified. Clearance must be rechecked against the finished Kestrel at in-game scale.
- **Gait:** keep v02 unchanged; the F4 tuning panel stays in. The user's F4 playtest is still pending.
- **Arenas:** positions are provisional. Silhouettes and visibility can change, but placement stays easy to move.
- **Off limits:**
  - the Kestrel Blender master: no Blender renders or bakes;
  - mech scale and movement;
  - new gameplay systems.

## How to test (one Godot process at a time; check Blender is idle first)
- **Godot:** `C:/Users/miles/AppData/Local/Programs/Godot/4.7.2-stable/Godot_v4.7.2-stable_win64_console.exe`
- **Review views:** `--path godot -- --autotest <dir> --skip-intro --review`. Add `--night` for night views.
- **Full QA:** `--path godot -- --autotest <dir> --skip-intro`.
- **Performance:** `--path godot -- --autotest <dir> --skip-intro --perf-tour --perf <dir>`. Add `--laptop` for the lighter preset.

## Stage plan
1. Baseline and cost reduction. **Done.** Results on a 1080p tour:
   - Default: 47 → 66.9 fps; 1% low 34 → 52 fps.
   - Laptop: 73.5 → 98.2 fps; 1% low 59 → 80 fps.
   - Primitives: 10.6 M → 3.1 M on Default.
2. The 17 m doorway, the hangar identity and foreground materials (concrete, markings, damp shoreline).
3. Water and coast, trees and mountains, and the distant composition (bridge, districts, arenas).
4. Day and night refinement, then optional ambience.
5. QA, before/after review package, and final note.
