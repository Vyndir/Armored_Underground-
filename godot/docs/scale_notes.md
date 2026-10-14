# Scale notes: waterfront blockout

## The frame (placeholder asset)

The game currently uses `assets/mech/kestrel.glb`: the generated v3 mech build
(`art/mech/v3`, default loadout). It is a stand-in and is **not** the
reference-matched Kestrel from the separate Blender master
(`Kestrel-v3.blend`), which was not opened or changed. The asset is
replaceable: `MechRig.setup()` loads it and scales it from the constants in
`scripts/site.gd`.

### Measured on the model (model units, headless Godot, neutral rest pose)

| Feature | Height |
|---|---|
| Soles (underside) | -0.065 |
| Helmet shell top | 7.52 |
| **Helmet top: crown plate (used as the reference)** | **7.59** |
| Crest fin (thin, 3 cm) | 7.76 |
| Whip aerial | 7.98 |
| Antenna | 8.09 |
| Knee joint (shin bone head) | 2.75 |
| Hip joint (thigh bone head) | 4.50 |

Helmet top means the solid head armour (crown plate). The antenna, the whip
aerial and the thin crest fin are excluded.

### Scaling

`MECH_SCALE = 6.8 / (7.59 + 0.065) = 0.8883`. The scale is uniform on the visual
model and its skeleton, so the rig's joint offsets scale with it. The soles are
lifted onto the ground plane. The booster flames and their light scale with the
frame.

### Verified in-engine after scaling (QA `scale` line)

| Feature | Height |
|---|---|
| Helmet top | 6.800 m |
| Antenna top (total height) | 7.244 m |
| Soles | 0.000 m (ground contact) |
| Knee joint | 2.50 m |
| Hip joint | 4.06 m |
| Collider | capsule 6.85 m tall, radius 1.85 m, from the ground |

The old collider was 8.4 m tall with a 2.1 m radius, about 1 m above the helmet.

The chase camera's pivot height and arm length scale with the frame: pivot
5.5 m, arm 13.3 m at rest.

## Water

- Water surface at y = -2.2. The training shelf floor is at -3.6, so the water is **1.4 m deep**.
- On the scaled frame the knee joint is 2.5 m above the soles, so the surface sits about 1.1 m below the knee: shin depth.
- The swell near the quay is damped to roughly ±0.15 m.
- The shelf floor collider is a box placed exactly at the visible sea bed. It carries on 15 m past the booms.
- The bay beyond drops to 4–25 m.

## Hangar and door

- Blockout: 60 × 70 × 21 m (eave). There are three bays at 20 m spacing, and the gantries are proportioned to the frame: decks at 2.3, 4.6 and 6.9 m, towers 9.2 m.
- The overhead crane runs at 14.6 m, and its hook clears 8.2 m.

Door width comes from use, not from shrinking the old opening:

- one frame at 4.6 m across shoulders and rifle, plus a flatbed or second frame alongside;
- a 14 m allowance for a future wing or jet-mode span with 5 m margins.

That gives a **24 m** opening. For heights, two options were compared from gameplay height:

| Opening | Height vs frame | Clearance |
|---|---|---|
| 24 × 11 m (default) | 1.6× helmet height | 3.8 m above the antenna; a full jump (2.98 m apex) still clears the header |
| 24 × 17 m | 2.5× helmet height | the earlier proposal |

Use `--door=WxH` to switch between them.
