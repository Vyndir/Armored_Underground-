"""v3 frame and the starter loadout parts."""
import math
from mathutils import Vector
from toolkit import *
from common import Limb


def frame(rig):
    begin('frame')
    # pelvis block with crotch armor and hip actuators
    loft('Pelvis', [((0, 0.02, 4.28), (0, 0, 1), chamfer(1.3, 0.95, 0.24)),
                    ((0, 0.02, 4.62), (0, 0, 1), chamfer(1.24, 0.92, 0.22)),
                    ((0, 0.02, 4.82), (0, 0, 1), chamfer(1.05, 0.8, 0.2))], 'frame', 'pelvis', bevel=0.03)
    prism('Crotch', poly((-0.4, 0.3), (0.4, 0.3), (0.2, -0.32), (-0.2, -0.32)), 0.22, (0, -0.55, 4.28), '-Y', 'paint', 'pelvis', taper=0.8, panel=0.05)
    light('CrotchLight', (0, -0.68, 4.42), 0.3, 0.035, 'pelvis')
    prism('Tail', poly((-0.35, 0.25), (0.35, 0.25), (0.25, -0.25), (-0.25, -0.25)), 0.2, (0, 0.55, 4.35), '+Y', 'frame', 'pelvis', taper=0.85)
    with Mirror():
        ring_stack('HipAct.L', (0.66, 0, 4.5), (1, 0, 0), [0.4, 0.34, 0.42, 0.3], [0.08, 0.2, 0.06, 0.08], 'frame', 'thigh.L')
        prism('HipGuard.L', poly((-0.35, -0.3), (0.38, -0.25), (0.42, 0.2), (0.05, 0.38), (-0.38, 0.22)), 0.12, (0.98, -0.02, 4.55), '+X', 'paint', 'pelvis', taper=0.88, panel=0.05)
    # spine: segmented, with pistons and cables
    for i, z in enumerate((5.06, 5.28, 5.5, 5.7)):
        loft(f'SpineSeg{i}', [((0, 0.08, z), (0, 0, 1), chamfer(0.72 - i * 0.02, 0.58, 0.15)),
                              ((0, 0.08, z + 0.15), (0, 0, 1), chamfer(0.66 - i * 0.02, 0.54, 0.13))], 'joint' if i % 2 else 'frame', 'spine', bevel=0.015)
    with Mirror():
        piston('SpinePiston.L', (0.36, 0.3, 4.86), (0.4, 0.34, 5.86), 0.05, 'spine')
        cable('SpineCable.L', [(0.22, 0.42, 4.86), (0.34, 0.5, 5.3), (0.26, 0.44, 5.86)], 0.035, 'spine')
        cable('SpineCable2.L', [(0.12, -0.25, 4.9), (0.2, -0.32, 5.35), (0.14, -0.28, 5.86)], 0.028, 'spine')
    # neck
    ring_stack('Neck', (0, -0.12, 7.0), (0, 0, 1), [0.25, 0.2, 0.27], [0.07, 0.13, 0.05], 'frame', 'head')
    with Mirror():
        cable('NeckCable.L', [(0.22, 0.05, 6.86), (0.3, -0.02, 6.98), (0.16, -0.08, 7.1)], 0.026, 'head')
        ring_stack('ShoulderAct.L', (1.26, 0, 6.42), (1, 0, 0), [0.34, 0.3, 0.37, 0.26], [0.08, 0.24, 0.07, 0.06], 'frame', 'shoulder.L')
        cyl('ShoulderAxle.L', (1.0, 0, 6.42), (1.55, 0, 6.42), 0.14, 'steel', 'shoulder.L', 12)
    return finish(rig)


# ------------------------------------------------------------------ head
def hd_pike(rig):
    begin('hd_pike')
    c = Vector((0, -0.18, 7.22))
    loft('Base', [((0, -0.12, 7.0), (0, 0, 1), chamfer(0.66, 0.78, 0.14)), ((0, -0.12, 7.16), (0, 0, 1), chamfer(0.62, 0.74, 0.13))], 'frame', 'head')
    helmet = poly((-0.6, -0.12), (-0.46, 0.08), (-0.2, 0.26), (0.22, 0.3), (0.44, 0.16), (0.44, -0.22), (-0.3, -0.26))
    helm = prism('Helmet', helmet, 0.72, c, '+X', 'paint', 'head', bevel=0.03)
    for sx in (1, -1):
        slots(helm, c + Vector((sx * 0.36, 0.05, 0.12)), c + Vector((sx * 0.36, 0.3, 0.1)), 3, (sx, 0, 0), 0.04, 0.12, 0.025)
    prism('Crown', poly((-0.3, 0.0), (0.0, 0.1), (0.36, 0.06), (0.36, -0.04), (-0.3, -0.06)), 0.3, c + Vector((0, 0.05, 0.27)), '+X', 'accent', 'head', bevel=0.02)
    # visor: slanted glass band with a lit slit
    prism('Visor', poly((-0.3, -0.06), (0.3, -0.06), (0.26, 0.06), (-0.26, 0.06)), 0.08, c + Vector((0, -0.52, 0.02)), tuple(Vector((0, -1, 0.35)).normalized()), 'glass', 'head', bevel=0.01)
    light('Slit', c + Vector((0, -0.565, 0.035)), 0.42, 0.025, 'head', normal=tuple(Vector((0, -1, 0.35)).normalized()))
    with Mirror():
        cyl('Eye.L', c + Vector((0.2, -0.5, -0.1)), c + Vector((0.2, -0.6, -0.1)), 0.055, 'glow', 'head', 10)
        cyl('SensorPod.L', c + Vector((0.38, -0.1, -0.02)), c + Vector((0.5, -0.1, -0.02)), 0.11, 'frame', 'head', 12)
        cyl('SensorLens.L', c + Vector((0.5, -0.1, -0.02)), c + Vector((0.53, -0.1, -0.02)), 0.07, 'glass', 'head', 12)
        prism('Cheek.L', poly((-0.25, -0.12), (0.2, -0.1), (0.25, 0.08), (-0.2, 0.1)), 0.06, c + Vector((0.39, -0.28, -0.12)), '+X', 'frame', 'head', bevel=0.01)
    prism('Chin', poly((-0.22, 0.0), (0.22, 0.0), (0.16, -0.12), (-0.16, -0.12)), 0.14, c + Vector((0, -0.48, -0.2)), '-Y', 'frame', 'head', bevel=0.012)
    vents('ChinVent', c + Vector((0, -0.56, -0.25)), 0.26, 0.08, 3, 'head')
    prism('Antenna', poly((0, 0), (0.06, 0), (0.02, 0.62), (0.0, 0.62)), 0.03, c + Vector((0.24, 0.25, 0.25)), '+X', 'steel', 'head', bevel=0.004)
    prism('Fin', poly((-0.25, 0), (0.15, 0), (0.25, 0.22), (-0.05, 0.12)), 0.03, c + Vector((0, 0.18, 0.32)), '+X', 'accent', 'head', bevel=0.004)
    return finish(rig)


# ------------------------------------------------------------------ core
def cr_ferro(rig):
    begin('cr_ferro')
    FRONT = (0, -1, 0)
    # segmented belly: two overlapping armored bands
    loft('Belly1', [((0, -0.1, 5.08), (0, 0, 1), ridge(0.92, 0.3, 0.06, 0.45, 0.8, 0.2), FRONT),
                    ((0, -0.1, 5.36), (0, 0, 1), ridge(1.02, 0.36, 0.06, 0.45, 0.8, 0.2), FRONT)], 'paint', 'spine', bevel=0.02)
    loft('Belly2', [((0, -0.08, 5.4), (0, 0, 1), ridge(0.98, 0.34, 0.06, 0.45, 0.8, 0.2), FRONT),
                    ((0, -0.08, 5.7), (0, 0, 1), ridge(1.1, 0.4, 0.06, 0.45, 0.8, 0.2), FRONT)], 'frame', 'spine', bevel=0.02)
    light('BellyLight', (0, -0.5, 5.55), 0.36, 0.03, 'spine')
    with Mirror():
        prism('Flank.L', poly((-0.3, -0.26), (0.28, -0.2), (0.3, 0.24), (-0.22, 0.3)), 0.1, (0.56, -0.02, 5.4), '+X', 'frame', 'spine', taper=0.88, panel=0.03)
    # recessed rib cage under the chest block
    loft('Ribs', [((0, 0.1, 5.7), (0, 0, 1), chamfer(1.45, 1.0, 0.3), FRONT), ((0, 0.08, 6.08), (0, 0, 1), chamfer(2.0, 1.2, 0.35), FRONT)], 'frame', 'chest', bevel=0.02)
    vents('RibVent', (0, -0.52, 5.92), 0.9, 0.2, 5, 'chest', normal=(0, -1, -0.25))
    # upper chest block: crisp chamfered bottom edge, broad flat front, tapering top
    chest = loft('Chest', [((0, 0.06, 6.04), (0, 0, 1), ridge(2.2, 0.6, 0.64, 0.5, 0.86, 0.25), FRONT),
                   ((0, 0.05, 6.14), (0, 0, 1), ridge(2.38, 0.82, 0.68, 0.5, 0.86, 0.25), FRONT),
                   ((0, 0.05, 6.78), (0, 0, 1), ridge(2.46, 0.88, 0.68, 0.5, 0.86, 0.25), FRONT),
                   ((0, 0.08, 7.0), (0, 0, 1), ridge(1.95, 0.58, 0.6, 0.5, 0.86, 0.25), FRONT)], 'paint', 'chest', bevel=0.025)
    # prow: two raked plates forming a V over the chest front, accent seam between
    with Mirror():
        n = Vector((0.36, -0.92, 0.16)).normalized()
        prism('Prow.L', poly((-0.42, -0.34), (0.4, -0.3), (0.44, 0.3), (-0.34, 0.36)), 0.07, (0.44, -0.96, 6.44), tuple(n), 'paint', 'chest', taper=0.9, panel=0.035)
        light('ProwLight.L', (0.42, -1.0, 6.14), 0.4, 0.03, 'chest', normal=tuple(n))
        stripes('Haz.L', (0.62, -0.97, 6.72), 0.26, 0.06, 4, 'chest', normal=tuple(n))
        # flank armor boxes
        prism('ChestFlank.L', poly((-0.42, -0.32), (0.4, -0.28), (0.44, 0.3), (-0.4, 0.34)), 0.16, (1.2, 0.02, 6.45), '+X', 'paint', 'chest', taper=0.85, panel=0.04)
        vents('FlankVent.L', (1.29, 0.02, 6.32), 0.42, 0.22, 4, 'chest', normal=(1, 0, 0))
        prism('Trap.L', poly((-0.42, -0.36), (0.36, -0.3), (0.42, 0.3), (-0.38, 0.36)), 0.09, (0.72, 0.06, 6.98), tuple(Vector((0.35, 0, 1)).normalized()), 'paint', 'chest', up=(0, 1, 0), taper=0.88, panel=0.03)
        bolts('TrapBolt.L', (0.5, 0.3, 7.03), (0.9, 0.3, 6.9), 3, 'chest', normal=(0.35, 0, 1))
    prism('Seam', poly((-0.05, 0.36), (0.05, 0.36), (0.03, -0.38), (-0.03, -0.38)), 0.05, (0, -1.06, 6.44), tuple(Vector((0, -1, 0.14)).normalized()), 'accent', 'chest', bevel=0.008)
    for sx in (1, -1):
        side_n = Vector((sx * 0.86, -0.5, 0)).normalized()
        recess(chest, (sx * 1.0, -0.55, 6.62), tuple(side_n), 0.5, 0.06, 0.03, light=True, bone='chest')
        slots(chest, (sx * 1.02, -0.5, 6.3), (sx * 1.1, -0.36, 6.3), 3, tuple(side_n), 0.12, 0.05, 0.03)
    recess(chest, (0, 0.0, 7.0), (0, 0, 1), 0.7, 0.4, 0.03, up=(0, 1, 0))
    text('Number', '07', (0.62, -0.99, 6.5), 0.15, 'chest', normal=tuple(Vector((0.36, -0.92, 0.16)).normalized()))
    # collar with a raised front neck guard
    loft('Collar', [((0, 0.06, 6.95), (0, 0, 1), chamfer(1.2, 0.95, 0.24)), ((0, 0.04, 7.1), (0, 0, 1), chamfer(0.92, 0.78, 0.2))], 'frame', 'chest', bevel=0.02)
    prism('NeckGuard', poly((-0.5, -0.12), (0.5, -0.12), (0.4, 0.16), (-0.4, 0.16)), 0.08, (0, -0.52, 7.08), tuple(Vector((0, -1, 0.45)).normalized()), 'paint', 'chest', taper=0.9, panel=0.025)
    # back
    prism('Back', chamfer(1.8, 0.95, 0.2), 0.18, (0, 0.72, 6.35), '+Y', 'frame', 'chest', taper=0.88, panel=0.05)
    with Mirror():
        prism('Blade.L', poly((-0.4, -0.3), (0.38, -0.26), (0.42, 0.28), (-0.3, 0.34)), 0.08, (0.72, 0.78, 6.62), tuple(Vector((0.3, 1, 0.15)).normalized()), 'paint', 'chest', taper=0.9, panel=0.03)
    greebles('BackG', (0, 0.83, 6.66), (1, 0, 0), (0, 0, 1), 1.3, 0.22, 10, 'chest', (0, 1, 0))
    with Mirror():
        vents('BackVent.L', (0.52, 0.83, 6.12), 0.4, 0.3, 5, 'chest', normal=(0, 1, 0))
    return finish(rig)


def _hand(name_prefix):
    H = Limb('hand.L')
    prism(name_prefix + 'Palm', chamfer(0.44, 0.5, 0.08), 0.36, H.p(0.35), '+X', 'frame', 'hand.L', up=tuple(H.u), bevel=0.02)
    prism(name_prefix + 'Knuckle', chamfer(0.46, 0.14, 0.04), 0.3, H.p(0.75, 0, -0.06), '+X', 'joint', 'hand.L', up=tuple(H.u), bevel=0.01)
    for i in range(4):
        sx = -0.15 + i * 0.1
        a = H.p(0.85, sx, -0.12)
        b = a + H.d * 0.2 + H.f * 0.08
        c = b + H.d * 0.16 + H.f * 0.12
        cyl(f'{name_prefix}Finger{i}a', a, b, 0.045, 'frame', 'hand.L', 8, smooth=False)
        cyl(f'{name_prefix}Finger{i}b', b, c, 0.04, 'joint', 'hand.L', 8, smooth=False)
    t0 = H.p(0.4, -0.22, -0.1)
    cyl(name_prefix + 'Thumb', t0, t0 + H.f * 0.22 + H.s * -0.08 + H.d * 0.1, 0.05, 'frame', 'hand.L', 8, smooth=False)


def ar_hand(rig):
    begin('ar_hand')
    U, F = Limb('upperarm.L'), Limb('forearm.L')
    with Mirror():
        # angular stepped pauldron: main block, outer shield, top step, light and marks
        sh = Vector((2.02, -0.02, 6.4))
        prism('Pauldron.L', poly((-0.72, -0.34), (0.56, -0.4), (0.72, 0.04), (0.48, 0.46), (-0.36, 0.52), (-0.76, 0.16)), 0.78, sh, '+X', 'paint', 'upperarm.L', taper=0.86, bevel=0.03, rot=(0, 0.18, 0))
        shield = prism('PauldronShield.L', poly((-0.62, -0.3), (0.46, -0.34), (0.6, 0.02), (0.4, 0.38), (-0.3, 0.42), (-0.64, 0.12)), 0.07, sh + Vector((0.42, 0, 0)), '+X', 'paint', 'upperarm.L', taper=0.92, panel=0.04, rot=(0, 0.18, 0))
        slots(shield, sh + Vector((0.47, -0.3, -0.18)), sh + Vector((0.47, 0.25, -0.18)), 5, (0.98, 0, 0.18), 0.04, 0.1, 0.025)
        prism('PauldronStep.L', poly((-0.5, -0.06), (0.36, -0.06), (0.3, 0.08), (-0.44, 0.08)), 0.62, sh + Vector((0.0, -0.06, 0.52)), '+X', 'accent', 'upperarm.L', bevel=0.012, rot=(0, 0.18, 0))
        prism('PauldronLip.L', poly((-0.6, -0.04), (0.5, -0.04), (0.5, 0.05), (-0.6, 0.05)), 0.7, sh + Vector((0.0, 0.0, -0.36)), '+X', 'frame', 'upperarm.L', bevel=0.01, rot=(0, 0.18, 0))
        light('PauldronLight.L', sh + Vector((0.465, -0.42, 0.05)), 0.03, 0.3, 'upperarm.L', normal=(1, 0, 0))
        text('Pmark.L', 'K-H', sh + Vector((0.465, 0.18, -0.12)), 0.13, 'upperarm.L', normal=(1, 0, 0))
        stripes('PauldronHaz.L', sh + Vector((0.465, 0.18, 0.2)), 0.36, 0.07, 4, 'upperarm.L', normal=(1, 0, 0))
        # upper arm frame + outer sleeve
        loft('UpperFrame.L', [(U.p(0.12), U.d, chamfer(0.42, 0.46, 0.1), tuple(U.f)), (U.p(0.98), U.d, chamfer(0.38, 0.42, 0.09), tuple(U.f))], 'frame', 'upperarm.L')
        loft('Sleeve.L', [(U.p(0.3, 0.12, 0), U.d, ridge(0.5, 0.2, 0.04, 0.3, 0.6), (1, 0, 0)), (U.p(0.92, 0.12, 0), U.d, ridge(0.46, 0.18, 0.04, 0.3, 0.6), (1, 0, 0))], 'paint', 'upperarm.L', bevel=0.02)
        cable('ArmCable.L', [U.p(0.15, -0.2, 0.15), U.p(0.6, -0.26, 0.2), F.p(0.15, -0.24, 0.18)], 0.03, 'forearm.L')
        # elbow
        ring_stack('Elbow.L', F.a, (1, 0, 0), [0.3, 0.26, 0.32], [0.1, 0.28, 0.08], 'frame', 'forearm.L')
        piston('ElbowPiston.L', U.p(0.45, -0.24, -0.25), F.p(0.35, -0.26, -0.3), 0.055, 'forearm.L')
        # forearm: inner frame + big ridged outer armor + front guard
        loft('ForeFrame.L', [(F.p(0.05), F.d, chamfer(0.46, 0.5, 0.1), tuple(F.f)), (F.p(0.98), F.d, chamfer(0.42, 0.46, 0.1), tuple(F.f))], 'frame', 'forearm.L')
        fore = loft('Forearm.L', [(F.p(0.06, 0.1, 0.02), F.d, ridge(0.7, 0.32, 0.14, 0.32, 0.6), (1, 0, 0)),
                                  (F.p(0.42, 0.13, 0.04), F.d, ridge(0.86, 0.44, 0.16, 0.32, 0.6), (1, 0, 0)),
                                  (F.p(0.56, 0.13, 0.04), F.d, ridge(0.84, 0.42, 0.16, 0.32, 0.6), (1, 0, 0))], 'paint', 'forearm.L', bevel=0.025)
        loft('ForearmLower.L', [(F.p(0.5, 0.11, 0.03), F.d, ridge(0.74, 0.38, 0.13, 0.32, 0.6), (1, 0, 0)),
                                (F.p(0.97, 0.08, 0.01), F.d, ridge(0.56, 0.26, 0.11, 0.32, 0.6), (1, 0, 0))], 'paint', 'forearm.L', bevel=0.022)
        loft('ForeGuard.L', [(F.p(0.15, 0, 0.18), F.d, ridge(0.5, 0.2, 0.05, 0.3, 0.6), tuple(F.f)), (F.p(0.9, 0, 0.2), F.d, ridge(0.46, 0.24, 0.05, 0.3, 0.6), tuple(F.f))], 'accent', 'forearm.L', bevel=0.02)
        vents('ForeVent.L', F.p(0.45, 0, -0.27), 0.3, 0.32, 4, 'forearm.L', normal=tuple(-F.f), up=tuple(F.u))
        slots(fore, F.p(0.3, 0.52, 0.14), F.p(0.75, 0.53, 0.14), 4, tuple((Vector((1, 0, 0)) * 0.8 + F.f * 0.6).normalized()), 0.04, 0.12, 0.025, up=tuple(F.u))
        recess(fore, F.p(0.55, 0.52, -0.12), tuple((Vector((1, 0, 0)) * 0.8 - F.f * 0.6).normalized()), 0.05, 0.42, 0.03, up=tuple(F.u), light=True, bone='forearm.L')
        light('ForeLight.L', F.p(0.72, 0.53, 0.0), 0.025, 0.3, 'forearm.L', normal=(1, 0, 0), up=tuple(F.u))
        ring_stack('Wrist.L', F.p(1.0), F.d, [0.22, 0.2], [0.06, 0.08], 'joint', 'hand.L')
        _hand('')
    return finish(rig)


def _foot(scale=1.0):
    s = scale
    Ft = Limb('foot.L')
    sole_z = 0.0
    prism('Sole.L', chamfer(0.92 * s, 1.95 * s, 0.18 * s, 0.25 * s), 0.12, (0.68, -0.28, sole_z + 0.06), '+Z', 'rubber', 'foot.L', up=(0, -1, 0), bevel=0.015)
    loft('FootBody.L', [((0.68, -0.28, 0.12), (0, 0, 1), chamfer(0.86 * s, 1.85 * s, 0.18 * s, 0.25 * s), (0, -1, 0)),
                        ((0.68, -0.18, 0.42), (0, 0, 1), chamfer(0.66 * s, 1.15 * s, 0.14, 0.2), (0, -1, 0))], 'frame', 'foot.L', bevel=0.025)
    prism('Toe.L', poly((-0.42, -0.1), (0.42, -0.1), (0.38, 0.12), (0.2, 0.2), (-0.2, 0.2), (-0.38, 0.12)), 0.55 * s, (0.68, -1.05 * s, 0.24), tuple(Vector((0, -0.2, 1)).normalized()), 'paint', 'foot.L', up=(0, -1, 0), taper=0.9, panel=0.04)
    prism('Heel.L', poly((-0.28, 0), (0.28, 0), (0.2, 0.35), (-0.2, 0.35)), 0.4 * s, (0.68, 0.72 * s, 0.13), '+Y', 'paint', 'foot.L', taper=0.85)
    with_sides = [(0.5 * s, 'FootSideO.L', 1), (-0.5 * s, 'FootSideI.L', -1)]
    for dx, nm, sg in with_sides:
        prism(nm, poly((-0.7, 0), (0.6, 0), (0.45, 0.25), (-0.5, 0.3)), 0.06, (0.68 + dx * 0.98, -0.25, 0.18), (sg, 0, 0), 'paint' if sg > 0 else 'frame', 'foot.L', up=(0, 0, 1))
    ring_stack('Ankle.L', (0.68, 0.25, 0.62), (1, 0, 0), [0.3, 0.26, 0.32], [0.08, 0.3, 0.08], 'frame', 'foot.L')
    prism('AnkleGuard.L', poly((-0.3, -0.2), (0.3, -0.2), (0.24, 0.25), (-0.24, 0.25)), 0.12, (0.68, -0.15, 0.55), tuple(Vector((0, -1, 0.6)).normalized()), 'accent', 'foot.L', taper=0.85)


def lg_strider(rig):
    begin('lg_strider')
    T, S = Limb('thigh.L'), Limb('shin.L')
    f_t, f_s = tuple(T.f), tuple(S.f)
    with Mirror():
        # thigh: exposed inner frame + ridged front shell + side plate + rear piston
        loft('ThighFrame.L', [(T.p(0.08), T.d, chamfer(0.5, 0.56, 0.12), f_t), (T.p(0.92), T.d, chamfer(0.44, 0.5, 0.1), f_t)], 'frame', 'thigh.L')
        thigh = loft('ThighShell.L', [(T.p(0.06, 0.02, 0.1), T.d, ridge(0.72, 0.36, 0.04, 0.3, 0.62), f_t),
                              (T.p(0.45, 0.03, 0.14), T.d, ridge(0.84, 0.44, 0.04, 0.3, 0.62), f_t),
                              (T.p(0.88, 0.02, 0.1), T.d, ridge(0.64, 0.32, 0.04, 0.3, 0.62), f_t)], 'paint', 'thigh.L', bevel=0.025)
        prism('ThighSide.L', poly((-0.48, -0.6), (0.42, -0.56), (0.48, 0.55), (-0.32, 0.65)), 0.08, T.p(0.42, 0.32, -0.02), '+X', 'paint', 'thigh.L', up=tuple(T.u), taper=0.9, panel=0.035)
        stripes('ThighHaz.L', T.p(0.76, 0.365, -0.02), 0.44, 0.08, 5, 'thigh.L', normal=(1, 0, 0), up=tuple(T.u))
        slots(thigh, T.p(0.2, 0.14, 0.5), T.p(0.42, 0.14, 0.52), 4, tuple((T.f * 0.85 + T.s * 0.5).normalized()), 0.12, 0.04, 0.025, up=tuple(T.u))
        recess(thigh, T.p(0.62, -0.16, 0.5), tuple((T.f * 0.85 - T.s * 0.5).normalized()), 0.05, 0.36, 0.03, up=tuple(T.u), light=True, bone='thigh.L')
        piston('ThighPiston.L', T.p(0.12, 0.05, -0.32), T.p(0.85, 0.05, -0.3), 0.065, 'thigh.L')
        greebles('ThighG.L', T.p(0.5, -0.24, -0.1), tuple(T.d), (0, -1, 0), 0.6, 0.2, 6, 'thigh.L', (-1, 0, 0))
        # knee
        ring_stack('Knee.L', S.a, (1, 0, 0), [0.36, 0.32, 0.38], [0.1, 0.42, 0.1], 'frame', 'shin.L')
        loft('KneeCap.L', [(S.p(-0.08, 0, 0.3), S.d, ridge(0.62, 0.22, 0.05, 0.3, 0.6), f_s), (S.p(0.14, 0, 0.36), S.d, ridge(0.7, 0.3, 0.05, 0.3, 0.6), f_s)], 'accent', 'shin.L', bevel=0.02)
        # shin: frame + long ridged front shell + calf shell
        loft('ShinFrame.L', [(S.p(0.05), S.d, chamfer(0.48, 0.52, 0.12), f_s), (S.p(0.95), S.d, chamfer(0.42, 0.46, 0.1), f_s)], 'frame', 'shin.L')
        shin = loft('ShinShell.L', [(S.p(0.16, 0, 0.14), S.d, ridge(0.72, 0.34, 0.04, 0.28, 0.6), f_s),
                             (S.p(0.52, 0, 0.18), S.d, ridge(0.86, 0.44, 0.04, 0.28, 0.6), f_s),
                             (S.p(0.92, 0, 0.12), S.d, ridge(0.66, 0.28, 0.04, 0.28, 0.6), f_s)], 'paint', 'shin.L', bevel=0.025)
        light('ShinLight.L', S.p(0.62, 0.39, 0.15), 0.025, 0.5, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        loft('Greave.L', [(S.p(0.1, 0, 0.2), S.d, ridge(0.78, 0.34, 0.03, 0.3, 0.62), f_s),
                          (S.p(0.38, 0, 0.27), S.d, ridge(0.9, 0.4, 0.03, 0.3, 0.62), f_s)], 'paint', 'shin.L', bevel=0.02)
        prism('Tasset.L', poly((-0.36, -0.42), (0.34, -0.38), (0.4, 0.36), (0.0, 0.44), (-0.38, 0.36)), 0.06, (1.05, -0.05, 4.2), tuple(Vector((1, -0.1, 0.12)).normalized()), 'paint', 'pelvis', taper=0.92, panel=0.03)
        cyl('TassetHinge.L', (1.0, -0.3, 4.62), (1.0, 0.2, 4.62), 0.05, 'steel', 'pelvis', 10)
        facet = (S.f * 0.8 + S.s * 0.6).normalized()
        recess(shin, S.p(0.5, 0.24, 0.5), tuple(facet), 0.06, 0.5, 0.03, up=tuple(S.u), light=True, bone='shin.L')
        slots(shin, S.p(0.25, -0.2, 0.52), S.p(0.25, -0.05, 0.56), 3, tuple((S.f * 0.85 - S.s * 0.5).normalized()), 0.05, 0.16, 0.03, up=tuple(S.u))
        recess(shin, S.p(0.8, 0, 0.42), tuple(S.f), 0.16, 0.22, 0.025, up=tuple(S.u))
        text('ShinMark.L', 'LG-03', S.p(0.3, 0.4, 0.12), 0.1, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        loft('Calf.L', [(S.p(0.12, 0, -0.16), S.d, ridge(0.56, 0.28, 0.04, 0.35, 0.6), tuple(-S.f)),
                        (S.p(0.45, 0, -0.18), S.d, ridge(0.66, 0.36, 0.04, 0.35, 0.6), tuple(-S.f)),
                        (S.p(0.8, 0, -0.14), S.d, ridge(0.5, 0.22, 0.04, 0.35, 0.6), tuple(-S.f))], 'frame', 'shin.L', bevel=0.02)
        vents('CalfVent.L', S.p(0.42, 0.3, -0.18), 0.32, 0.4, 5, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        piston('AnklePiston.L', S.p(0.55, -0.27, -0.12), S.p(0.98, -0.25, -0.1), 0.05, 'shin.L')
        cable('ShinCable.L', [S.p(0.1, -0.26, 0.0), S.p(0.5, -0.3, -0.08), S.p(0.95, -0.24, 0.0)], 0.03, 'shin.L')
        _foot()
    return finish(rig)


def _nozzle(i, base, r, length, tilt=(0, 0.35, -1)):
    d = Vector(tilt).normalized()
    a = Vector(base)
    loft(f'Bell{i}', [(a, d, circle(r * 0.75, 20)), (a + d * length * 0.4, d, circle(r * 0.9, 20)), (a + d * length, d, circle(r * 1.15, 20))],
         'steel', 'booster', cap=False, bevel=0, smooth=True)
    loft(f'BellInner{i}', [(a + d * length * 0.98, d, circle(r * 1.08, 20)), (a + d * length * 0.3, d, circle(r * 0.7, 20))], 'joint', 'booster', cap=False, bevel=0, smooth=True)
    cyl(f'Burner{i}', a + d * length * 0.32, a + d * length * 0.36, r * 0.68, 'thrust', 'booster', 18, marker=None)
    cyl(f'Nozzle_{i}', a + d * length * 0.97, a + d * length, r * 0.2, 'joint', 'booster', 8, marker=f'Nozzle_{i}')
    for k in range(4):
        ang = k * math.pi / 2 + math.pi / 4
        off = Vector((math.cos(ang), math.sin(ang) * 0.4, 0)) * r * 1.2
        prism(f'Vane{i}_{k}', rect(0.04, length * 0.5), r * 0.5, a + d * length * 0.75 + off, tuple(off.normalized() if off.length else Vector((1, 0, 0))), 'frame', 'booster', up=tuple(-d), bevel=0.004)


def bs_flare(rig):
    begin('bs_flare')
    loft('Pack', [((0, 1.0, 5.3), (0, 0, 1), chamfer(1.5, 0.5, 0.15)), ((0, 1.04, 6.5), (0, 0, 1), chamfer(1.4, 0.56, 0.15))], 'frame', 'booster')
    vents('PackVent', (0, 1.33, 6.05), 0.36, 0.55, 6, 'booster', normal=(0, 1, 0))
    for i, x in enumerate((0.58, -0.58)):
        # engine nacelle: ridged loft with an intake collar and a stabiliser fin
        loft(f'Nacelle{i}', [((x, 1.4, 6.55), (0, 0, 1), ridge(0.5, 0.28, 0.22, 0.4, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.42, 6.35), (0, 0, 1), ridge(0.6, 0.34, 0.26, 0.4, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.44, 5.55), (0, 0, 1), ridge(0.62, 0.36, 0.28, 0.4, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.44, 5.2), (0, 0, 1), ridge(0.5, 0.3, 0.24, 0.4, 0.7, 0.2), (0, 1, 0))], 'paint', 'booster', bevel=0.02)
        ring_stack(f'Intake{i}', (x, 1.42, 6.62), (0, 0, 1), [0.2, 0.17], [0.05, 0.08], 'frame', 'booster')
        prism(f'Fin{i}', poly((-0.3, 0), (0.25, 0), (0.35, 0.38), (-0.05, 0.3)), 0.04, (x + (0.3 if x > 0 else -0.3), 1.5, 5.9), (1 if x > 0 else -1, 0, 0), 'accent', 'booster', bevel=0.006)
        light(f'PodLight{i}', (x, 1.79, 6.0), 0.05, 0.6, 'booster', normal=(0, 1, 0))
        text(f'Mark{i}', 'BS-1', (x, 1.79, 5.55), 0.09, 'booster', normal=(0, 1, 0))
        _nozzle(i, (x, 1.46, 5.05), 0.21, 0.55)
    return finish(rig)


# ------------------------------------------------------------------ weapon (right hand, built in place)
def wp_hornet(rig):
    begin('wp_hornet')
    x = -1.95
    loft('Receiver', [((x, -0.6, 3.25), (0, -1, 0), chamfer(0.3, 0.42, 0.06)), ((x, -1.8, 3.28), (0, -1, 0), chamfer(0.28, 0.4, 0.06))], 'frame', 'hand.R')
    prism('Body', chamfer(1.3, 0.3, 0.06), 0.34, (x, -1.2, 3.47), '+X', 'paint', 'hand.R', up=(0, 0, 1), taper=1.0, panel=0.03)
    loft('Shroud', [((x, -1.8, 3.34), (0, -1, 0), chamfer(0.24, 0.26, 0.05)), ((x, -2.9, 3.34), (0, -1, 0), chamfer(0.2, 0.22, 0.04))], 'paint', 'hand.R')
    for k in range(5):
        prism(f'ShroudVent{k}', rect(0.12, 0.04), 0.02, (x + 0.11, -2.0 - k * 0.15, 3.38), '+X', 'joint', 'hand.R', bevel=0)
    cyl('Barrel', (x, -2.9, 3.34), (x, -3.75, 3.34), 0.06, 'steel', 'hand.R', 12, marker='Barrel')
    cyl('Brake', (x, -3.75, 3.34), (x, -3.98, 3.34), 0.09, 'frame', 'hand.R', 12)
    loft('Mag', [((x, -0.75, 3.05), (0, 0, -1), chamfer(0.18, 0.4, 0.04)), ((x, -0.68, 2.55), (0, 0, -1), chamfer(0.16, 0.36, 0.04))], 'accent', 'hand.R')
    loft('Scope', [((x, -1.0, 3.72), (0, -1, 0), circle(0.08, 12)), ((x, -1.6, 3.72), (0, -1, 0), circle(0.08, 12))], 'frame', 'hand.R', smooth=True)
    cyl('ScopeLens', (x, -1.6, 3.72), (x, -1.62, 3.72), 0.065, 'glass', 'hand.R', 12)
    light('Ammo', (x - 0.155, -1.2, 3.45), 0.6, 0.04, 'hand.R', normal=(-1, 0, 0))
    text('GunMark', 'HORNET', (x + 0.172, -1.15, 3.5), 0.09, 'hand.R', normal=(1, 0, 0))
    return finish(rig)


# ------------------------------------------------------------------ shoulder (left)
def sh_swarm(rig):
    begin('sh_swarm')
    c = Vector((1.15, 0.5, 7.1))
    loft('Pod', [(c + Vector((0, -0.65, 0)), (0, -1, 0), chamfer(0.95, 0.72, 0.12)), (c + Vector((0, 0.65, 0)), (0, -1, 0), chamfer(0.9, 0.68, 0.14))], 'paint', 'pod.L', bevel=0.03)
    prism('Cover', chamfer(0.9, 1.2, 0.08), 0.06, c + Vector((0, 0.0, 0.38)), '+Z', 'accent', 'pod.L', up=(0, 1, 0), panel=0.04)
    prism('Face', chamfer(0.84, 0.62, 0.08), 0.06, c + Vector((0, -0.67, 0)), '-Y', 'frame', 'pod.L', bevel=0.01)
    for ix in range(3):
        for iz in range(2):
            p = c + Vector((-0.24 + ix * 0.24, -0.7, -0.13 + iz * 0.26))
            cyl(f'Tube{ix}{iz}', p + Vector((0, 0.05, 0)), p, 0.095, 'joint', 'pod.L', 12)
            cyl(f'Tip{ix}{iz}', p + Vector((0, 0.01, 0)), p + Vector((0, -0.005, 0)), 0.06, 'thrust', 'pod.L', 10)
    cyl('Hinge', c + Vector((-0.48, -0.3, 0.3)), c + Vector((-0.48, 0.5, 0.3)), 0.05, 'steel', 'pod.L', 10)
    loft('Mount', [(c + Vector((0, 0, -0.36)), (0, 0, -1), chamfer(0.4, 0.5, 0.08)), (c + Vector((0, 0, -0.6)), (0, 0, -1), chamfer(0.3, 0.4, 0.06))], 'frame', 'pod.L')
    text('PodMark', 'SWARM-6', c + Vector((0.476, 0.1, 0.05)), 0.11, 'pod.L', normal=(1, 0, 0))
    stripes('PodHaz', c + Vector((0.476, -0.4, -0.2)), 0.35, 0.08, 4, 'pod.L', normal=(1, 0, 0))
    return finish(rig)


DEFAULT = [frame, hd_pike, cr_ferro, ar_hand, lg_strider, bs_flare, wp_hornet, sh_swarm]
