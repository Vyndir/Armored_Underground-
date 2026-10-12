"""v3 alternative parts: heads, cores, arms, legs, boosters, weapons, shoulder units."""
import math
from mathutils import Vector
from toolkit import *
from common import Limb
from parts_default import _hand, _foot, _nozzle

FRONT = (0, -1, 0)


# ================================================================== HEADS
def hd_moth(rig):
    begin('hd_moth')
    c = Vector((0, -0.16, 7.2))
    loft('Base', [((0, -0.12, 7.0), (0, 0, 1), chamfer(0.6, 0.72, 0.14)), ((0, -0.12, 7.12), (0, 0, 1), chamfer(0.56, 0.68, 0.12))], 'frame', 'head')
    # flat wide sensor body
    loft('Body', [(c + Vector((0, 0, -0.08)), (0, 0, 1), ridge(0.78, 0.52, 0.42, 0.4, 0.7, 0.2), FRONT),
                  (c + Vector((0, 0.02, 0.12)), (0, 0, 1), ridge(0.7, 0.46, 0.4, 0.4, 0.7, 0.2), FRONT),
                  (c + Vector((0, 0.06, 0.22)), (0, 0, 1), ridge(0.44, 0.26, 0.3, 0.4, 0.7, 0.2), FRONT)], 'paint', 'head', bevel=0.02)
    # single big lens
    cyl('LensHousing', c + Vector((0, -0.4, 0.0)), c + Vector((0, -0.58, 0.0)), 0.17, 'frame', 'head', 20)
    cyl('Lens', c + Vector((0, -0.58, 0.0)), c + Vector((0, -0.6, 0.0)), 0.13, 'glass', 'head', 20)
    cyl('Iris', c + Vector((0, -0.6, 0.0)), c + Vector((0, -0.612, 0.0)), 0.05, 'glow', 'head', 14)
    with Mirror():
        # swept antenna wings
        prism('Wing.L', poly((-0.05, -0.02), (0.7, 0.1), (0.78, 0.16), (0.1, 0.1)), 0.035, c + Vector((0.28, 0.05, 0.12)), '+Y', 'paint', 'head', bevel=0.004, up=(0, 0, 1))
        prism('WingEdge.L', poly((0.55, 0.08), (0.78, 0.15), (0.8, 0.19), (0.57, 0.12)), 0.04, c + Vector((0.28, 0.05, 0.12)), '+Y', 'accent', 'head', bevel=0.003, up=(0, 0, 1))
        light('WingTip.L', c + Vector((1.04, 0.05, 0.3)), 0.06, 0.02, 'head', normal=(0, -1, 0))
        cyl('SubLens.L', c + Vector((0.25, -0.42, -0.06)), c + Vector((0.25, -0.48, -0.06)), 0.055, 'glow', 'head', 10)
        vents('SideVent.L', c + Vector((0.39, 0.0, 0.0)), 0.3, 0.14, 3, 'head', normal=(1, 0, 0))
    prism('Chin', poly((-0.2, 0), (0.2, 0), (0.14, -0.1), (-0.14, -0.1)), 0.12, c + Vector((0, -0.38, -0.16)), '-Y', 'frame', 'head', bevel=0.01)
    return finish(rig)


def hd_bastion(rig):
    begin('hd_bastion')
    c = Vector((0, -0.14, 7.28))
    loft('Base', [((0, -0.1, 6.98), (0, 0, 1), chamfer(0.9, 0.95, 0.18)), ((0, -0.1, 7.14), (0, 0, 1), chamfer(0.86, 0.92, 0.17))], 'frame', 'head')
    loft('Dome', [(c + Vector((0, 0, -0.14)), (0, 0, 1), ridge(1.0, 0.62, 0.5, 0.35, 0.75, 0.2), FRONT),
                  (c + Vector((0, 0, 0.12)), (0, 0, 1), ridge(0.98, 0.6, 0.5, 0.35, 0.75, 0.2), FRONT),
                  (c + Vector((0, 0.04, 0.34)), (0, 0, 1), ridge(0.66, 0.36, 0.38, 0.35, 0.75, 0.2), FRONT)], 'paint', 'head', bevel=0.025)
    prism('Crest', poly((-0.45, 0), (0.4, 0), (0.3, 0.14), (-0.35, 0.12)), 0.12, c + Vector((0, 0.06, 0.38)), '+X', 'accent', 'head', bevel=0.01)
    prism('VisorBand', rect(0.8, 0.12), 0.06, c + Vector((0, -0.62, 0.06)), tuple(Vector((0, -1, 0.12)).normalized()), 'frame', 'head', bevel=0.01)
    light('Slit', c + Vector((0, -0.655, 0.065)), 0.68, 0.03, 'head', normal=tuple(Vector((0, -1, 0.12)).normalized()))
    loft('Jaw', [(c + Vector((0, -0.3, -0.32)), (0, 0, 1), ridge(0.8, 0.38, 0.1, 0.4, 0.8, 0.2), FRONT), (c + Vector((0, -0.3, -0.12)), (0, 0, 1), ridge(0.86, 0.42, 0.1, 0.4, 0.8, 0.2), FRONT)], 'frame', 'head', bevel=0.015)
    vents('JawVent', c + Vector((0, -0.72, -0.22)), 0.42, 0.14, 4, 'head')
    with Mirror():
        prism('Cheek.L', poly((-0.3, -0.25), (0.28, -0.22), (0.3, 0.22), (-0.26, 0.26)), 0.08, c + Vector((0.52, -0.12, -0.06)), '+X', 'paint', 'head', taper=0.9, panel=0.025)
        bolts('CheekBolt.L', c + Vector((0.565, -0.3, 0.1)), c + Vector((0.565, 0.06, 0.1)), 3, 'head', normal=(1, 0, 0))
    return finish(rig)


# ================================================================== CORES
def cr_wasp(rig):
    begin('cr_wasp')
    loft('Belly', [((0, -0.06, 5.06), (0, 0, 1), ridge(0.7, 0.28, 0.06, 0.4, 0.8, 0.2), FRONT), ((0, -0.06, 5.68), (0, 0, 1), ridge(0.82, 0.34, 0.06, 0.4, 0.8, 0.2), FRONT)], 'frame', 'spine', bevel=0.02)
    light('BellyLine', (0, -0.42, 5.4), 0.03, 0.4, 'spine')
    loft('Ribs', [((0, 0.1, 5.68), (0, 0, 1), chamfer(1.1, 0.9, 0.25), FRONT), ((0, 0.08, 6.05), (0, 0, 1), chamfer(1.6, 1.05, 0.3), FRONT)], 'frame', 'chest', bevel=0.02)
    # slim chest with a sharp forward keel
    loft('Chest', [((0, 0.08, 5.98), (0, 0, 1), ridge(1.7, 0.85, 0.55, 0.12, 0.55, 0.15), FRONT),
                   ((0, 0.06, 6.08), (0, 0, 1), ridge(1.95, 1.05, 0.58, 0.12, 0.55, 0.15), FRONT),
                   ((0, 0.06, 6.72), (0, 0, 1), ridge(2.1, 1.1, 0.6, 0.12, 0.55, 0.15), FRONT),
                   ((0, 0.1, 6.98), (0, 0, 1), ridge(1.6, 0.7, 0.55, 0.12, 0.55, 0.15), FRONT)], 'paint', 'chest', bevel=0.022)
    prism('KeelBlade', poly((-0.03, 0.42), (0.03, 0.42), (0.02, -0.4), (-0.02, -0.4)), 0.06, (0, -1.2, 6.42), tuple(Vector((0, -1, 0.1)).normalized()), 'accent', 'chest', bevel=0.006)
    with Mirror():
        n = Vector((0.62, -0.78, 0.1)).normalized()
        light('KeelLight.L', (0.26, -1.06, 6.4), 0.04, 0.5, 'chest', normal=tuple(n))
        loft('Intake.L', [((0.86, -0.15, 6.05), (0, 1, 0), chamfer(0.36, 0.42, 0.08), (1, 0, 0)), ((0.92, 0.45, 6.05), (0, 1, 0), chamfer(0.3, 0.36, 0.07), (1, 0, 0))], 'frame', 'chest')
        vents('IntakeVent.L', (0.86, -0.36, 6.05), 0.3, 0.34, 4, 'chest', normal=(0, -1, 0))
        prism('Trap.L', poly((-0.36, -0.3), (0.3, -0.26), (0.36, 0.26), (-0.32, 0.3)), 0.07, (0.62, 0.08, 6.98), tuple(Vector((0.4, 0, 1)).normalized()), 'paint', 'chest', up=(0, 1, 0), taper=0.88, panel=0.03)
        stripes('Haz.L', (0.5, -0.86, 6.7), 0.24, 0.06, 4, 'chest', normal=tuple(n))
    text('Number', '31', (-0.5, -0.86, 6.42), 0.15, 'chest', normal=tuple(Vector((-0.62, -0.78, 0.1)).normalized()))
    loft('Spine', [((0, 0.62, 5.6), (0, 0, 1), ridge(0.36, 0.28, 0.05, 0.3, 0.6), (0, 1, 0)), ((0, 0.66, 6.85), (0, 0, 1), ridge(0.3, 0.32, 0.05, 0.3, 0.6), (0, 1, 0))], 'accent', 'chest', bevel=0.015)
    loft('Collar', [((0, 0.08, 6.95), (0, 0, 1), chamfer(1.0, 0.85, 0.2)), ((0, 0.06, 7.08), (0, 0, 1), chamfer(0.8, 0.7, 0.18))], 'frame', 'chest', bevel=0.02)
    return finish(rig)


def cr_anvil(rig):
    begin('cr_anvil')
    loft('Belly', [((0, -0.08, 5.06), (0, 0, 1), chamfer(1.2, 1.0, 0.22), FRONT), ((0, -0.08, 5.7), (0, 0, 1), chamfer(1.45, 1.15, 0.26), FRONT)], 'paint', 'spine', bevel=0.025)
    vents('BellyVent', (0, -0.66, 5.38), 0.7, 0.3, 5, 'spine')
    loft('Chest', [((0, 0.04, 5.68), (0, 0, 1), chamfer(2.4, 1.6, 0.4), FRONT),
                   ((0, 0.04, 5.82), (0, 0, 1), chamfer(2.95, 1.9, 0.45), FRONT),
                   ((0, 0.04, 6.9), (0, 0, 1), chamfer(3.1, 1.95, 0.45), FRONT),
                   ((0, 0.06, 7.12), (0, 0, 1), chamfer(2.4, 1.5, 0.4), FRONT)], 'paint', 'chest', bevel=0.03)
    prism('Plate', chamfer(2.3, 0.95, 0.2, 0.1), 0.18, (0, -1.02, 6.3), tuple(Vector((0, -1, 0.08)).normalized()), 'paint', 'chest', taper=0.9, panel=0.05)
    stripes('Haz', (0, -1.13, 6.66), 1.6, 0.16, 10, 'chest', normal=tuple(Vector((0, -1, 0.08)).normalized()))
    text('Number', '50', (0.72, -1.12, 6.2), 0.22, 'chest', normal=tuple(Vector((0, -1, 0.08)).normalized()))
    bolts('PlateBolt', (-1.0, -1.115, 5.92), (1.0, -1.115, 5.92), 9, 'chest', normal=(0, -1, 0))
    light('PlateLight', (-0.55, -1.12, 6.18), 0.5, 0.04, 'chest')
    with Mirror():
        prism('Shield.L', poly((-0.8, -0.55), (0.75, -0.5), (0.8, 0.5), (-0.75, 0.55)), 0.24, (1.6, 0.04, 6.3), '+X', 'accent', 'chest', taper=0.86, panel=0.06)
        vents('ShieldVent.L', (1.73, 0.4, 6.1), 0.5, 0.36, 5, 'chest', normal=(1, 0, 0))
        piston('ShoulderRam.L', (1.2, 0.6, 6.9), (1.45, 0.3, 7.3), 0.07, 'chest')
    loft('Collar', [((0, 0.05, 7.08), (0, 0, 1), chamfer(1.6, 1.3, 0.3)), ((0, 0.04, 7.26), (0, 0, 1), chamfer(1.1, 0.95, 0.24))], 'frame', 'chest', bevel=0.02)
    prism('Back', chamfer(2.4, 1.0, 0.2), 0.22, (0, 1.02, 6.35), '+Y', 'frame', 'chest', taper=0.88, panel=0.05)
    greebles('BackG', (0, 1.14, 6.7), (1, 0, 0), (0, 0, 1), 1.8, 0.24, 14, 'chest', (0, 1, 0))
    return finish(rig)


# ================================================================== ARMS
def _arm(pid, scale, extra, pauldron_style):
    U, F = Limb('upperarm.L'), Limb('forearm.L')
    s = scale
    with Mirror():
        sh = Vector((2.02, -0.02, 6.4))
        if pauldron_style == 'sleek':
            loft('Pauldron.L', [((2.0, -0.5, 6.44), (0, 1, 0), ridge(0.75, 0.3, 0.1, 0.2, 0.6, 0.15), (0.7, 0, 0.7)),
                                ((2.02, 0.0, 6.5), (0, 1, 0), ridge(0.95, 0.42, 0.12, 0.2, 0.6, 0.15), (0.7, 0, 0.7)),
                                ((1.98, 0.5, 6.42), (0, 1, 0), ridge(0.7, 0.26, 0.1, 0.2, 0.6, 0.15), (0.7, 0, 0.7))], 'paint', 'upperarm.L', bevel=0.02)
            prism('PauldronFin.L', poly((-0.4, 0), (0.3, 0), (0.4, 0.2), (-0.2, 0.14)), 0.03, sh + Vector((0.2, 0.0, 0.48)), '+X', 'accent', 'upperarm.L', bevel=0.004)
        else:
            k = 1.18
            prism('Pauldron.L', poly((-0.72 * k, -0.34 * k), (0.56 * k, -0.4 * k), (0.72 * k, 0.04 * k), (0.48 * k, 0.46 * k), (-0.36 * k, 0.52 * k), (-0.76 * k, 0.16 * k)), 0.9, sh + Vector((0.06, 0, 0)), '+X', 'paint', 'upperarm.L', taper=0.86, bevel=0.035, rot=(0, 0.18, 0))
            prism('PauldronShield.L', poly((-0.7, -0.36), (0.6, -0.4), (0.72, 0.02), (0.48, 0.44), (-0.36, 0.48), (-0.76, 0.14)), 0.09, sh + Vector((0.56, 0, 0)), '+X', 'accent', 'upperarm.L', taper=0.92, panel=0.05, rot=(0, 0.18, 0))
            stripes('PHaz.L', sh + Vector((0.61, 0.1, -0.2)), 0.5, 0.09, 5, 'upperarm.L', normal=(1, 0, 0))
            piston('PauldronRam.L', sh + Vector((-0.3, 0.45, -0.3)), U.p(0.5, -0.1, -0.28), 0.06, 'upperarm.L')
        light('PauldronLight.L', sh + Vector((0.5, -0.4, 0.05)), 0.03, 0.28, 'upperarm.L', normal=(1, 0, 0))
        loft('UpperFrame.L', [(U.p(0.12), U.d, chamfer(0.42 * s, 0.46 * s, 0.1), tuple(U.f)), (U.p(0.98), U.d, chamfer(0.38 * s, 0.42 * s, 0.09), tuple(U.f))], 'frame', 'upperarm.L')
        loft('Sleeve.L', [(U.p(0.3, 0.12 * s, 0), U.d, ridge(0.5 * s, 0.2 * s, 0.04, 0.3, 0.6), (1, 0, 0)), (U.p(0.92, 0.12 * s, 0), U.d, ridge(0.46 * s, 0.18 * s, 0.04, 0.3, 0.6), (1, 0, 0))], 'paint', 'upperarm.L', bevel=0.02)
        ring_stack('Elbow.L', F.a, (1, 0, 0), [0.3 * s, 0.26 * s, 0.32 * s], [0.1, 0.28 * s, 0.08], 'frame', 'forearm.L')
        piston('ElbowPiston.L', U.p(0.45, -0.24 * s, -0.25 * s), F.p(0.35, -0.26 * s, -0.3 * s), 0.055 * s, 'forearm.L')
        loft('ForeFrame.L', [(F.p(0.05), F.d, chamfer(0.46 * s, 0.5 * s, 0.1), tuple(F.f)), (F.p(0.98), F.d, chamfer(0.42 * s, 0.46 * s, 0.1), tuple(F.f))], 'frame', 'forearm.L')
        loft('Forearm.L', [(F.p(0.1, 0.1 * s, 0.02), F.d, ridge(0.66 * s, 0.3 * s, 0.14 * s, 0.32, 0.6), (1, 0, 0)),
                           (F.p(0.55, 0.12 * s, 0.04), F.d, ridge(0.86 * s, 0.42 * s, 0.16 * s, 0.32, 0.6), (1, 0, 0)),
                           (F.p(0.95, 0.1 * s, 0.02), F.d, ridge(0.72 * s, 0.32 * s, 0.14 * s, 0.32, 0.6), (1, 0, 0))], 'paint', 'forearm.L', bevel=0.025)
        vents('ForeVent.L', F.p(0.45, 0, -0.27 * s), 0.3 * s, 0.32, 4, 'forearm.L', normal=tuple(-F.f), up=tuple(F.u))
        ring_stack('Wrist.L', F.p(1.0), F.d, [0.22, 0.2], [0.06, 0.08], 'joint', 'hand.L')
        extra(U, F, s)
        _hand('')


def ar_needle(rig):
    begin('ar_needle')
    def extra(U, F, s):
        cyl('Sensor.L', Vector((2.32, 0.2, 6.62)), Vector((2.32, -0.62, 6.66)), 0.04, 'steel', 'upperarm.L', 8)
        light('SensorTip.L', Vector((2.32, -0.64, 6.66)), 0.06, 0.06, 'upperarm.L')
        light('ForeLine.L', F.p(0.5, 0.42 * s + 0.02, 0.02), 0.02, 0.5, 'forearm.L', normal=(1, 0, 0), up=tuple(F.u))
    _arm('ar_needle', 0.78, extra, 'sleek')
    return finish(rig)


def ar_hauler(rig):
    begin('ar_hauler')
    def extra(U, F, s):
        loft('Bracer.L', [(F.p(0.42, 0.08, 0.05), F.d, chamfer(1.05, 1.0, 0.2), tuple(F.f)), (F.p(0.92, 0.08, 0.05), F.d, chamfer(1.0, 0.95, 0.2), tuple(F.f))], 'accent', 'forearm.L', bevel=0.03)
        stripes('BracerHaz.L', F.p(0.6, 0.08, 0.56), 0.6, 0.1, 6, 'forearm.L', normal=tuple(F.f), up=tuple(F.u))
        bolts('BracerBolt.L', F.p(0.45, 0.6, 0.0), F.p(0.9, 0.6, 0.0), 4, 'forearm.L', normal=(1, 0, 0))
        piston('ForeRam.L', F.p(0.1, -0.35, 0.3), F.p(0.85, -0.35, 0.35), 0.07, 'forearm.L')
    _arm('ar_hauler', 1.22, extra, 'heavy')
    return finish(rig)


# ================================================================== LEGS
def lg_hopper(rig):
    begin('lg_hopper')
    T, S = Limb('thigh.L'), Limb('shin.L')
    with Mirror():
        loft('ThighFrame.L', [(T.p(0.08), T.d, chamfer(0.44, 0.5, 0.1), tuple(T.f)), (T.p(0.92), T.d, chamfer(0.4, 0.44, 0.1), tuple(T.f))], 'frame', 'thigh.L')
        # swept-back thigh armor gives the reverse-joint silhouette
        loft('ThighShell.L', [(T.p(0.02, 0.02, -0.1), T.d, ridge(0.66, 0.4, 0.05, 0.3, 0.62), tuple(-T.f)),
                              (T.p(0.45, 0.03, -0.3), T.d, ridge(0.78, 0.5, 0.05, 0.3, 0.62), tuple(-T.f)),
                              (T.p(0.9, 0.02, -0.12), T.d, ridge(0.58, 0.32, 0.05, 0.3, 0.62), tuple(-T.f))], 'paint', 'thigh.L', bevel=0.025)
        prism('ThighFront.L', poly((-0.28, -0.55), (0.28, -0.5), (0.3, 0.5), (-0.28, 0.55)), 0.07, T.p(0.45, 0, 0.3), tuple(T.f), 'frame', 'thigh.L', up=tuple(T.u), panel=0.03)
        piston('ThighPiston.L', T.p(0.15, 0.28, 0.1), T.p(0.85, 0.28, 0.12), 0.06, 'thigh.L')
        ring_stack('Knee.L', S.a, (1, 0, 0), [0.32, 0.28, 0.34], [0.1, 0.36, 0.1], 'frame', 'shin.L')
        loft('ShinFrame.L', [(S.p(0.05), S.d, chamfer(0.4, 0.44, 0.1), tuple(S.f)), (S.p(0.95), S.d, chamfer(0.36, 0.4, 0.09), tuple(S.f))], 'frame', 'shin.L')
        loft('ShinShell.L', [(S.p(0.12, 0, 0.12), S.d, ridge(0.56, 0.3, 0.04, 0.3, 0.6), tuple(S.f)),
                             (S.p(0.55, 0, 0.16), S.d, ridge(0.64, 0.36, 0.04, 0.3, 0.6), tuple(S.f)),
                             (S.p(0.92, 0, 0.1), S.d, ridge(0.5, 0.24, 0.04, 0.3, 0.6), tuple(S.f))], 'paint', 'shin.L', bevel=0.02)
        prism('Spur.L', poly((-0.12, -0.5), (0.12, -0.5), (0.05, 0.45), (-0.05, 0.45)), 0.16, S.p(0.3, 0, -0.5), tuple(-S.f), 'accent', 'shin.L', up=tuple(Vector((0, 0.5, 1)).normalized()), bevel=0.012)
        piston('ShinRam.L', S.p(0.15, 0.25, -0.2), S.p(0.9, 0.25, -0.15), 0.06, 'shin.L')
        light('ShinLine.L', S.p(0.55, 0.33, 0.16), 0.02, 0.5, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        # claw foot
        loft('FootBody.L', [((0.68, -0.3, 0.1), (0, 0, 1), chamfer(0.6, 1.5, 0.15, 0.2), (0, -1, 0)), ((0.68, -0.2, 0.38), (0, 0, 1), chamfer(0.48, 0.95, 0.12, 0.18), (0, -1, 0))], 'frame', 'foot.L', bevel=0.02)
        for i, dx in enumerate((-0.2, 0.2)):
            prism(f'Claw{i}.L', poly((-0.08, 0), (0.08, 0), (0.03, 0.55), (-0.03, 0.55)), 0.16, (0.68 + dx, -1.25, 0.1), tuple(Vector((0, -0.25, 1)).normalized()), 'steel', 'foot.L', up=(0, -1, 0), bevel=0.01)
        prism('HeelClaw.L', poly((-0.08, 0), (0.08, 0), (0.03, 0.4), (-0.03, 0.4)), 0.14, (0.68, 0.5, 0.1), (0, 0, 1), 'steel', 'foot.L', up=(0, 1, 0), bevel=0.01)
        prism('Toe.L', poly((-0.3, -0.1), (0.3, -0.1), (0.24, 0.14), (-0.24, 0.14)), 0.4, (0.68, -0.85, 0.32), tuple(Vector((0, -0.2, 1)).normalized()), 'paint', 'foot.L', up=(0, -1, 0), taper=0.9, panel=0.03)
        ring_stack('Ankle.L', (0.68, 0.25, 0.62), (1, 0, 0), [0.26, 0.22, 0.28], [0.08, 0.26, 0.08], 'frame', 'foot.L')
    return finish(rig)


def lg_glide(rig):
    begin('lg_glide')
    T, S = Limb('thigh.L'), Limb('shin.L')
    with Mirror():
        loft('ThighFrame.L', [(T.p(0.08), T.d, chamfer(0.52, 0.56, 0.12), tuple(T.f)), (T.p(0.92), T.d, chamfer(0.46, 0.5, 0.1), tuple(T.f))], 'frame', 'thigh.L')
        loft('ThighShell.L', [(T.p(0.06, 0.04, 0.1), T.d, ridge(0.8, 0.4, 0.05, 0.3, 0.62), tuple(T.f)), (T.p(0.88, 0.04, 0.1), T.d, ridge(0.72, 0.34, 0.05, 0.3, 0.62), tuple(T.f))], 'paint', 'thigh.L', bevel=0.025)
        prism('ThighFin.L', poly((-0.6, 0), (0.55, 0), (0.4, 0.22), (-0.45, 0.16)), 0.05, T.p(0.45, 0.42, 0), '+X', 'accent', 'thigh.L', up=tuple(T.u), bevel=0.006)
        ring_stack('Knee.L', S.a, (1, 0, 0), [0.4, 0.34, 0.42], [0.1, 0.44, 0.1], 'frame', 'shin.L')
        # thick shin flaring into the hover skirt
        loft('Shin.L', [(S.p(0.08, 0, 0.05), S.d, ridge(0.8, 0.4, 0.4, 0.35, 0.65), tuple(S.f)),
                        (S.p(0.6, 0, 0.08), S.d, ridge(1.0, 0.5, 0.48, 0.35, 0.65), tuple(S.f)),
                        (S.p(0.98, 0, 0.05), S.d, ridge(1.2, 0.62, 0.58, 0.35, 0.65), tuple(S.f))], 'paint', 'shin.L', bevel=0.025)
        light('ShinRim.L', S.p(0.7, 0.52, 0.0), 0.025, 0.55, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        loft('Skirt.L', [((0.68, -0.1, 0.18), (0, 0, 1), chamfer(1.3, 1.85, 0.35), FRONT), ((0.68, -0.1, 0.65), (0, 0, 1), chamfer(1.12, 1.6, 0.3), FRONT)], 'frame', 'foot.L', bevel=0.025)
        loft('SkirtArmor.L', [((0.68, -0.1, 0.45), (0, 0, 1), chamfer(1.36, 1.92, 0.36), FRONT), ((0.68, -0.1, 0.7), (0, 0, 1), chamfer(1.2, 1.7, 0.32), FRONT)], 'paint', 'foot.L', bevel=0.02)
        cyl('PadRing.L', (0.68, -0.1, 0.16), (0.68, -0.1, 0.2), 0.56, 'steel', 'foot.L', 28)
        cyl('Pad.L', (0.68, -0.1, 0.14), (0.68, -0.1, 0.16), 0.48, 'glow', 'foot.L', 28)
        vents('SkirtVent.L', (0.68, -1.02, 0.38), 0.8, 0.22, 4, 'foot.L')
        light('SkirtLine.L', (1.35, -0.1, 0.56), 0.03, 1.4, 'foot.L', normal=(1, 0, 0), up=(0, 1, 0))
        stripes('SkirtHaz.L', (0.68, -1.07, 0.6), 0.6, 0.08, 6, 'foot.L')
    return finish(rig)


def lg_bulwark(rig):
    begin('lg_bulwark')
    T, S = Limb('thigh.L'), Limb('shin.L')
    with Mirror():
        loft('ThighFrame.L', [(T.p(0.08), T.d, chamfer(0.66, 0.7, 0.15), tuple(T.f)), (T.p(0.92), T.d, chamfer(0.6, 0.64, 0.14), tuple(T.f))], 'frame', 'thigh.L')
        loft('ThighShell.L', [(T.p(0.04, 0.05, 0.14), T.d, chamfer(1.05, 0.5, 0.16), tuple(T.f)), (T.p(0.9, 0.05, 0.16), T.d, chamfer(0.95, 0.46, 0.15), tuple(T.f))], 'paint', 'thigh.L', bevel=0.03)
        prism('ThighSide.L', chamfer(1.4, 0.95, 0.18), 0.12, T.p(0.45, 0.5, 0), '+X', 'paint', 'thigh.L', up=tuple(T.u), taper=0.9, panel=0.05)
        piston('ThighRam.L', T.p(0.1, 0.3, -0.45), T.p(0.85, 0.3, -0.4), 0.09, 'thigh.L')
        piston('ThighRam2.L', T.p(0.1, -0.3, -0.45), T.p(0.85, -0.3, -0.4), 0.09, 'thigh.L')
        ring_stack('Knee.L', S.a, (1, 0, 0), [0.48, 0.42, 0.5], [0.12, 0.6, 0.12], 'frame', 'shin.L')
        prism('KneeCap.L', chamfer(1.0, 0.8, 0.2), 0.24, S.p(0.04, 0, 0.5), tuple((S.f + Vector((0, 0, 0.3))).normalized()), 'accent', 'shin.L', taper=0.85, panel=0.05)
        loft('ShinShell.L', [(S.p(0.12, 0, 0.05), S.d, chamfer(1.15, 1.2, 0.22), tuple(S.f)), (S.p(0.92, 0, 0.0), S.d, chamfer(1.25, 1.3, 0.24), tuple(S.f))], 'paint', 'shin.L', bevel=0.03)
        stripes('ShinHaz.L', S.p(0.4, 0, 0.66), 0.8, 0.12, 6, 'shin.L', normal=tuple(S.f), up=tuple(S.u))
        vents('ShinVent.L', S.p(0.55, 0.63, 0), 0.6, 0.5, 5, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        text('ShinMark.L', 'LG-60', S.p(0.8, 0.63, 0.2), 0.13, 'shin.L', normal=(1, 0, 0), up=tuple(S.u))
        _foot(1.3)
    return finish(rig)


# ================================================================== BOOSTERS
def bs_comet(rig):
    begin('bs_comet')
    loft('Pack', [((0, 1.0, 5.2), (0, 0, 1), chamfer(1.8, 0.56, 0.16)), ((0, 1.04, 6.55), (0, 0, 1), chamfer(1.7, 0.6, 0.16))], 'frame', 'booster')
    for i, x in enumerate((0.72, 0.0, -0.72)):
        loft(f'Nacelle{i}', [((x, 1.45, 6.75), (0, 0, 1), ridge(0.42, 0.26, 0.2, 0.3, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.48, 6.5), (0, 0, 1), ridge(0.52, 0.32, 0.24, 0.3, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.5, 5.2), (0, 0, 1), ridge(0.54, 0.34, 0.26, 0.3, 0.7, 0.2), (0, 1, 0)),
                             ((x, 1.5, 4.85), (0, 0, 1), ridge(0.44, 0.28, 0.22, 0.3, 0.7, 0.2), (0, 1, 0))], 'paint', 'booster', bevel=0.02)
        light(f'Line{i}', (x, 1.83, 5.8), 0.04, 0.9, 'booster', normal=(0, 1, 0))
        _nozzle(i, (x, 1.52, 4.7), 0.18, 0.5)
    for x in (1.05, -1.05):
        prism(f'Fin{x}', poly((-0.45, 0), (0.3, 0), (0.45, 0.5), (-0.1, 0.4)), 0.05, (x, 1.55, 6.2), (1 if x > 0 else -1, 0, 0), 'accent', 'booster', bevel=0.006)
    text('Mark', 'COMET', (0, 1.33, 6.25), 0.1, 'booster', normal=(0, 1, 0))
    return finish(rig)


def bs_tern(rig):
    begin('bs_tern')
    loft('Pack', [((0, 1.0, 5.45), (0, 0, 1), chamfer(1.1, 0.46, 0.12)), ((0, 1.02, 6.4), (0, 0, 1), chamfer(1.0, 0.5, 0.12))], 'frame', 'booster')
    with Mirror():
        # swept wing booster
        prism('Wing.L', poly((-0.1, -0.25), (0.9, 0.0), (0.95, 0.2), (0.0, 0.3)), 0.12, (0.5, 1.25, 6.1), '+Y', 'paint', 'booster', up=(0, 0, 1), taper=0.9, panel=0.025, rot=(0, 0, 0))
        prism('WingEdge.L', poly((0.7, -0.05), (0.95, 0.0), (0.98, 0.22), (0.72, 0.18)), 0.13, (0.5, 1.25, 6.1), '+Y', 'accent', 'booster', up=(0, 0, 1), bevel=0.005)
        light('WingLine.L', (0.95, 1.32, 6.15), 0.4, 0.025, 'booster', normal=(0, 1, 0))
    for i, x in enumerate((0.55, -0.55)):
        loft(f'Pod{i}', [((x, 1.32, 6.0), (0, 0, 1), circle(0.17, 14)), ((x, 1.34, 5.3), (0, 0, 1), circle(0.2, 14))], 'paint', 'booster', bevel=0.01, smooth=True)
        _nozzle(i, (x, 1.36, 5.25), 0.16, 0.42)
    return finish(rig)


# ================================================================== WEAPONS
def wp_gale(rig):
    begin('wp_gale')
    x = -1.95
    loft('Drum', [((x - 0.38, -0.75, 2.85), (1, 0, 0), circle(0.38, 20)), ((x + 0.38, -0.75, 2.85), (1, 0, 0), circle(0.38, 20))], 'paint', 'hand.R', bevel=0.015, smooth=True)
    cyl('DrumCap', (x + 0.38, -0.75, 2.85), (x + 0.42, -0.75, 2.85), 0.3, 'frame', 'hand.R', 20)
    loft('Body', [((x, -0.6, 3.3), (0, -1, 0), chamfer(0.46, 0.5, 0.1)), ((x, -1.7, 3.32), (0, -1, 0), chamfer(0.42, 0.46, 0.1))], 'frame', 'hand.R')
    prism('Shroud', chamfer(1.0, 0.36, 0.08), 0.5, (x, -1.2, 3.62), '+X', 'paint', 'hand.R', panel=0.03)
    for k in range(6):
        a = k / 6 * math.pi * 2
        o = Vector((math.cos(a) * 0.13, 0, math.sin(a) * 0.13))
        cyl(f'Barrel{k}', Vector((x, -1.7, 3.36)) + o, Vector((x, -3.2, 3.36)) + o, 0.045, 'steel', 'hand.R', 8, smooth=True)
    for k, yy in enumerate((-2.0, -2.6, -3.1)):
        cyl(f'Clamp{k}', (x, yy, 3.36), (x, yy - 0.08, 3.36), 0.21, 'frame', 'hand.R', 16)
    cyl('Barrel', (x, -3.2, 3.36), (x, -3.24, 3.36), 0.05, 'joint', 'hand.R', 8, marker='Barrel')
    cable('Belt', [(x - 0.2, -0.75, 3.1), (x - 0.35, -1.0, 3.3), (x - 0.2, -1.4, 3.35)], 0.06, 'hand.R', 'frame')
    stripes('Haz', (x + 0.26, -1.1, 3.62), 0.5, 0.1, 5, 'hand.R', normal=(1, 0, 0))
    text('Mark', 'GALE', (x + 0.26, -1.35, 3.35), 0.1, 'hand.R', normal=(1, 0, 0))
    return finish(rig)


def wp_maul(rig):
    begin('wp_maul')
    x = -1.95
    loft('Body', [((x, -0.55, 3.15), (0, -1, 0), chamfer(0.5, 0.6, 0.12)), ((x, -2.0, 3.2), (0, -1, 0), chamfer(0.46, 0.56, 0.12))], 'paint', 'hand.R', bevel=0.02)
    loft('Bore', [((x, -2.0, 3.32), (0, -1, 0), circle(0.22, 18)), ((x, -3.05, 3.32), (0, -1, 0), circle(0.22, 18))], 'frame', 'hand.R', smooth=True)
    loft('BoreInner', [((x, -3.06, 3.32), (0, -1, 0), circle(0.16, 18)), ((x, -2.8, 3.32), (0, -1, 0), circle(0.16, 18))], 'joint', 'hand.R', cap=True, smooth=True)
    cyl('Barrel', (x, -3.05, 3.32), (x, -3.1, 3.32), 0.06, 'joint', 'hand.R', 8, marker='Barrel')
    prism('Pump', chamfer(0.6, 0.26, 0.06), 0.42, (x, -2.3, 3.05), '+X', 'accent', 'hand.R', panel=0.02)
    for k in range(6):
        cyl(f'Shell{k}', (x + 0.26, -0.8 - k * 0.14, 3.05), (x + 0.26, -0.8 - k * 0.14, 3.32), 0.05, 'accent', 'hand.R', 10)
    text('Mark', 'MAUL', (x - 0.235, -1.3, 3.25), 0.1, 'hand.R', normal=(-1, 0, 0))
    return finish(rig)


def wp_lance(rig):
    begin('wp_lance')
    x = -1.95
    loft('Capacitor', [((x, -0.5, 3.05), (0, -1, 0), chamfer(0.56, 0.62, 0.12)), ((x, -1.6, 3.08), (0, -1, 0), chamfer(0.52, 0.58, 0.12))], 'paint', 'hand.R', bevel=0.02)
    for k in range(4):
        cyl(f'Coil{k}', (x, -0.65 - k * 0.24, 3.07), (x, -0.73 - k * 0.24, 3.07), 0.33, 'frame', 'hand.R', 16)
        light(f'CoilLight{k}', (x + 0.335, -0.69 - k * 0.24, 3.07), 0.04, 0.03, 'hand.R', normal=(1, 0, 0))
    for side in (1, -1):
        prism(f'Rail{side}', rect(2.3, 0.28), 0.08, (x + side * 0.16, -2.65, 3.2), '+X', 'steel', 'hand.R', up=(0, 0, 1), bevel=0.008)
    prism('RailGlow', rect(2.1, 0.06), 0.18, (x, -2.65, 3.2), '+X', 'glow', 'hand.R', up=(0, 0, 1), bevel=0)
    cyl('Barrel', (x, -3.78, 3.2), (x, -3.82, 3.2), 0.05, 'joint', 'hand.R', 8, marker='Barrel')
    stripes('Haz', (x + 0.29, -1.2, 3.28), 0.5, 0.08, 5, 'hand.R', normal=(1, 0, 0))
    text('Mark', 'LANCE', (x - 0.265, -1.1, 3.1), 0.11, 'hand.R', normal=(-1, 0, 0))
    return finish(rig)


# ================================================================== SHOULDERS
def sh_mines(rig):
    begin('sh_mines')
    c = Vector((1.15, 0.6, 7.25))
    loft('Drum', [(c + Vector((0, -0.6, 0)), (0, -1, 0), circle(0.48, 22)), (c + Vector((0, 0.6, 0)), (0, -1, 0), circle(0.48, 22))], 'paint', 'pod.L', bevel=0.015, smooth=True)
    for k, yy in enumerate((-0.55, -0.1, 0.35)):
        cyl(f'Band{k}', c + Vector((0, yy, 0)), c + Vector((0, yy + 0.1, 0)), 0.5, 'frame', 'pod.L', 22)
    cyl('Cap', c + Vector((0, -0.62, 0)), c + Vector((0, -0.68, 0)), 0.38, 'frame', 'pod.L', 22)
    light('Chute', c + Vector((0, 0.66, -0.15)), 0.4, 0.2, 'pod.L', normal=(0, 1, 0), mclass='thrust')
    stripes('Haz', c + Vector((0, -0.7, 0)), 0.5, 0.1, 5, 'pod.L')
    loft('Mount', [(c + Vector((0, 0, -0.45)), (0, 0, -1), chamfer(0.4, 0.5, 0.08)), (c + Vector((0, 0, -0.7)), (0, 0, -1), chamfer(0.3, 0.4, 0.06))], 'frame', 'pod.L')
    return finish(rig)


def sh_spear(rig):
    begin('sh_spear')
    c = Vector((1.15, 0.35, 7.15))
    loft('Tube', [(c + Vector((0, -1.25, 0)), (0, -1, 0), chamfer(0.56, 0.56, 0.12)), (c + Vector((0, 1.25, 0)), (0, -1, 0), chamfer(0.5, 0.5, 0.11))], 'paint', 'pod.L', bevel=0.02)
    cyl('Muzzle', c + Vector((0, -1.25, 0)), c + Vector((0, -1.32, 0)), 0.24, 'frame', 'pod.L', 18)
    cyl('Warhead', c + Vector((0, -1.27, 0)), c + Vector((0, -1.3, 0)), 0.17, 'thrust', 'pod.L', 16)
    prism('Fin', poly((-0.4, 0), (0.3, 0), (0.42, 0.32), (-0.2, 0.26)), 0.05, c + Vector((0, 1.0, 0.28)), '+X', 'accent', 'pod.L', bevel=0.005)
    light('Line', c + Vector((0.285, 0, 0)), 0.02, 1.8, 'pod.L', normal=(1, 0, 0), up=(0, 1, 0))
    text('Mark', 'SPEAR', c + Vector((0.285, 0.5, -0.12)), 0.12, 'pod.L', normal=(1, 0, 0))
    loft('Mount', [(c + Vector((0, 0.2, -0.28)), (0, 0, -1), chamfer(0.4, 0.5, 0.08)), (c + Vector((0, 0.2, -0.55)), (0, 0, -1), chamfer(0.3, 0.4, 0.06))], 'frame', 'pod.L')
    return finish(rig)


EXTRA = [hd_moth, hd_bastion, cr_wasp, cr_anvil, ar_needle, ar_hauler, lg_hopper, lg_glide, lg_bulwark,
         bs_comet, bs_tern, wp_gale, wp_maul, wp_lance, sh_mines, sh_spear]
