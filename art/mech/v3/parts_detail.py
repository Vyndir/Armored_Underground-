"""Secondary detail pass for the default loadout: fasteners, hydraulics, hoses,
heat-sink fins, grab rails, rib bands, claws and stencils layered over the
primary armour. Registered as pre-finish hooks so each addition is baked and
skinned with its part."""
import math
from mathutils import Vector
from toolkit import *
from toolkit import PRE_FINISH
from common import Limb


def _frame(normal, up=(0, 0, 1)):
    return frame_from(Vector(normal), Vector(up))


def corner_bolts(name, center, normal, hw, hh, bone, up=(0, 0, 1), lift=0.0, r=0.028):
    """Four bolt heads at the corners of a plate face."""
    x, y, n = _frame(normal, up)
    c = Vector(center) + n * lift
    for i, (su, sv) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        p = c + x * su * hw + y * sv * hh
        cyl(f'{name}{i}', p - n * 0.015, p + n * 0.025, r, 'steel', bone, 6, bevel=0, smooth=False)


def rail(name, a, b, out, bone, standoff=0.1, r=0.024):
    """Grab rail: two standoffs and a bar."""
    a, b, o = Vector(a), Vector(b), Vector(out).normalized()
    cyl(name + '_A', a, a + o * standoff, r * 0.8, 'steel', bone, 8)
    cyl(name + '_B', b, b + o * standoff, r * 0.8, 'steel', bone, 8)
    cyl(name + '_Bar', a + o * standoff, b + o * standoff, r, 'steel', bone, 10)


def fins(name, center, along, normal, n, spacing, w, h, bone, mclass='frame', t=0.024):
    """Row of thin heat-sink plates standing off a surface."""
    al, nm = Vector(along).normalized(), Vector(normal).normalized()
    for k in range(n):
        p = Vector(center) + al * (k - (n - 1) / 2) * spacing + nm * h / 2
        prism(f'{name}{k}', rect(w, h), t, p, tuple(al), mclass, bone, bevel=0.004, up=tuple(nm))


def gear_ring(name, center, axis, radius, n, bone, tooth=0.06, width=0.18):
    """Teeth around an actuator drum."""
    c, ax = Vector(center), Vector(axis).normalized()
    u = ax.cross(Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))).normalized()
    v = ax.cross(u)
    for k in range(n):
        a = k / n * math.tau
        off = u * math.cos(a) + v * math.sin(a)
        prism(f'{name}{k}', rect(0.055, width), tooth, c + off * (radius + tooth / 2), tuple(off), 'steel', bone, bevel=0.004, up=tuple(ax))


def hose(name, pts, r, bone, collars=True):
    """Braided hose with steel end collars."""
    cable(name, pts, r, bone)
    if collars:
        for i, (p, q) in enumerate(((pts[0], pts[1]), (pts[-1], pts[-2]))):
            p, q = Vector(p), Vector(q)
            d = (q - p).normalized()
            cyl(f'{name}_Col{i}', p - d * 0.02, p + d * 0.07, r * 1.45, 'steel', bone, 10)


# ------------------------------------------------------------------ per part
def d_frame():
    with Mirror():
        gear_ring('ShGear.L', (1.26, 0, 6.42), (1, 0, 0), 0.37, 16, 'shoulder.L')
        for k in range(8):
            a = k / 8 * math.tau
            p = Vector((0.88, math.cos(a) * 0.27, 4.5 + math.sin(a) * 0.27))
            cyl(f'HipBolt{k}.L', p - Vector((0.015, 0, 0)), p + Vector((0.025, 0, 0)), 0.026, 'steel', 'thigh.L', 6, bevel=0, smooth=False)
        hose('SpineLine.L', [(0.3, -0.26, 4.88), (0.4, -0.38, 5.35), (0.3, -0.3, 5.88)], 0.03, 'spine')
        hose('HipHose.L', [(0.42, 0.36, 4.72), (0.6, 0.5, 4.5), (0.72, 0.34, 4.18)], 0.034, 'pelvis')
        prism('HipSkirt.L', poly((-0.3, -0.24), (0.3, -0.2), (0.26, 0.22), (-0.26, 0.24)), 0.05, (0.42, 0.52, 4.42),
              tuple(Vector((0.2, 1, -0.15)).normalized()), 'paint', 'pelvis', taper=0.9, panel=0.025)
    bolts('PelvisBolt', (-0.55, -0.47, 4.74), (0.55, -0.47, 4.74), 6, 'pelvis')
    for k, z in enumerate((5.13, 5.35, 5.57, 5.77)):
        bolts(f'SpineBolt{k}_', (-0.22, -0.22, z), (0.22, -0.22, z), 3, 'spine', r=0.022)


def d_head():
    c = Vector((0, -0.18, 7.22))
    fins('HeatFin', c + Vector((0, 0.34, 0.02)), (1, 0, 0), (0, 1, 0), 6, 0.1, 0.22, 0.1, 'head')
    with Mirror():
        bolts('HelmBolt.L', c + Vector((0.366, -0.35, 0.16)), c + Vector((0.366, 0.05, 0.16)), 4, 'head', r=0.022, normal=(1, 0, 0))
        cyl('LensRing.L', c + Vector((0.5, -0.1, -0.02)), c + Vector((0.545, -0.1, -0.02)), 0.085, 'steel', 'head', 14)
    cyl('Whip', c + Vector((-0.26, 0.2, 0.26)), c + Vector((-0.26, 0.26, 0.72)), 0.014, 'steel', 'head', 6)
    cyl('WhipTip', c + Vector((-0.26, 0.26, 0.72)), c + Vector((-0.26, 0.265, 0.76)), 0.03, 'glow', 'head', 8)
    prism('Brow', poly((-0.32, -0.025), (0.32, -0.025), (0.27, 0.035), (-0.27, 0.035)), 0.16, c + Vector((0, -0.5, 0.13)),
          tuple(Vector((0, -1, 0.9)).normalized()), 'frame', 'head', bevel=0.008)


def d_core():
    n = Vector((0.36, -0.92, 0.16)).normalized()
    with Mirror():
        corner_bolts('ProwBolt.L', (0.44, -0.96, 6.44), tuple(n), 0.32, 0.27, 'chest', lift=0.05)
        rail('FlankRail.L', (1.28, -0.2, 6.72), (1.28, 0.2, 6.72), (1, 0, 0), 'chest')
        corner_bolts('FlankBolt.L', (1.2, 0.02, 6.45), (1, 0, 0), 0.34, 0.26, 'chest', lift=0.09)
        tn = Vector((0.35, 0, 1)).normalized()
        x, y, _ = _frame(tn, (0, 1, 0))
        text('NoStep.L', 'NO STEP', Vector((0.72, 0.06, 6.98)) + tn * 0.07 - y * 0.12, 0.07, 'chest', normal=tuple(tn), up=(0, 1, 0))
        hose('BellyHose.L', [(0.5, -0.3, 5.12), (0.62, -0.36, 5.45), (0.66, -0.2, 5.82)], 0.032, 'spine')
        fins('BackFin.L', (0.52, 0.84, 6.55), (1, 0, 0), (0, 1, 0), 5, 0.08, 0.2, 0.08, 'chest')
        prism('Strap.L', rect(0.06, 0.7), 0.03, (1.29, 0.02, 6.48), (1, 0, 0), 'frame', 'chest', bevel=0.006)
    bolts('BellyBolt', (-0.35, -0.47, 5.2), (0.35, -0.47, 5.2), 5, 'spine', r=0.024)
    bolts('NeckGuardBolt', (-0.38, -0.56, 7.06), (0.38, -0.56, 7.06), 5, 'chest', r=0.022, normal=(0, -1, 0.45))


def d_arms():
    U, F = Limb('upperarm.L'), Limb('forearm.L')
    with Mirror():
        piston('BicepPiston.L', U.p(0.35, 0.38, -0.12), U.p(0.92, 0.36, -0.12), 0.042, 'upperarm.L')
        for k, t in enumerate((0.25, 0.45, 0.65)):
            for j, sd in enumerate((-0.17, 0.17)):
                p = F.p(t, sd, 0.37)
                cyl(f'GuardBolt{k}{j}.L', p - F.f * 0.015, p + F.f * 0.025, 0.024, 'steel', 'forearm.L', 6, bevel=0, smooth=False)
        back = -F.f
        prism('ElbowGuard.L', poly((-0.2, -0.22), (0.2, -0.22), (0.24, 0.16), (0.0, 0.26), (-0.24, 0.16)), 0.08,
              F.a + back * 0.36 + Vector((0, 0, 0.04)), tuple(back), 'accent', 'forearm.L', up=tuple(F.u), taper=0.85, panel=0.025)
        hose('ForeHose.L', [F.p(0.12, -0.3, -0.12), F.p(0.5, -0.36, -0.2), F.p(0.9, -0.28, -0.12)], 0.03, 'forearm.L')
        for k, t in enumerate((0.35, 0.7)):
            loft(f'ArmBand{k}.L', [(U.p(t - 0.025), U.d, chamfer(0.47, 0.51, 0.11), tuple(U.f)),
                                   (U.p(t + 0.025), U.d, chamfer(0.47, 0.51, 0.11), tuple(U.f))], 'steel', 'upperarm.L', bevel=0.008)


def d_legs():
    T, S = Limb('thigh.L'), Limb('shin.L')
    with Mirror():
        corner_bolts('ThighSideBolt.L', T.p(0.42, 0.32, -0.02), (1, 0, 0), 0.32, 0.4, 'thigh.L', up=tuple(T.u), lift=0.045)
        piston('AnklePiston2.L', S.p(0.55, 0.33, -0.1), S.p(0.98, 0.3, -0.08), 0.045, 'shin.L')
        hose('KneeHose.L', [T.p(0.7, -0.3, -0.05), T.p(0.98, -0.34, -0.2), S.p(0.2, -0.3, -0.22)], 0.032, 'shin.L')
        fins('CalfFin.L', S.p(0.3, 0, -0.34), tuple(S.d), tuple(-S.f), 5, 0.08, 0.3, 0.07, 'shin.L')
        corner_bolts('KneeBolt.L', S.p(0.04, 0, 0.62), tuple(S.f), 0.2, 0.08, 'shin.L', up=tuple(S.u))
        for k, dx in enumerate((-0.28, 0.0, 0.28)):
            prism(f'Claw{k}.L', poly((-0.09, 0), (0.09, 0), (0.05, 0.26), (-0.05, 0.26)), 0.09, (0.68 + dx, -1.16, 0.1), '+Z', 'steel', 'foot.L',
                  up=(0, -1, 0), taper=0.7, bevel=0.01)
        corner_bolts('TassetBolt.L', (1.05, -0.05, 4.2), tuple(Vector((1, -0.1, 0.12)).normalized()), 0.28, 0.32, 'pelvis', lift=0.035)
        bolts('SoleBolt.L', (1.15, -1.0, 0.16), (1.15, 0.4, 0.16), 6, 'foot.L', r=0.026, normal=(1, 0, 0))


def d_booster():
    fins('PackFin', (0, 1.02, 6.5), (1, 0, 0), (0, 0, 1), 6, 0.1, 0.42, 0.12, 'booster')
    with Mirror():
        hose('FuelLine.L', [(0.14, 1.26, 6.4), (0.2, 1.36, 6.0), (0.17, 1.32, 5.5), (0.3, 1.36, 5.18)], 0.035, 'booster')
        rail('PackRail.L', (0.76, 0.85, 5.5), (0.76, 0.85, 6.3), (1, 0, 0), 'booster', standoff=0.08)
    bolts('PackBolt', (-0.5, 1.27, 5.45), (0.5, 1.27, 5.45), 5, 'booster', normal=(0, 1, 0))


def d_weapon():
    x = -1.95
    prism('RailBase', rect(1.2, 0.03), 0.1, (x, -1.2, 3.635), '+X', 'frame', 'hand.R', bevel=0.004)
    for k in range(12):
        prism(f'RailTooth{k}', rect(0.04, 0.03), 0.12, (x, -0.66 - k * 0.1, 3.665), '+X', 'frame', 'hand.R', bevel=0.003)
    for k, yy in enumerate((-1.12, -1.48)):
        cyl(f'ScopeRing{k}', (x, yy, 3.72), (x, yy - 0.05, 3.72), 0.1, 'steel', 'hand.R', 12)
    loft('Foregrip', [((x, -2.3, 3.22), (0, 0, -1), chamfer(0.12, 0.15, 0.03)), ((x, -2.33, 2.92), (0, 0, -1), chamfer(0.11, 0.13, 0.03))], 'joint', 'hand.R')
    bolts('GunBolt', (x + 0.172, -0.72, 3.36), (x + 0.172, -1.7, 3.36), 6, 'hand.R', normal=(1, 0, 0), r=0.022)
    bolts('GunBoltI', (x - 0.172, -0.72, 3.36), (x - 0.172, -1.7, 3.36), 6, 'hand.R', normal=(-1, 0, 0), r=0.022)
    hose('MagFeed', [(x - 0.1, -0.72, 2.7), (x - 0.24, -0.6, 2.95), (x - 0.17, -0.7, 3.3)], 0.028, 'hand.R')


def d_pod():
    c = Vector((1.15, 0.5, 7.1))
    for k, yy in enumerate((-0.35, 0.1, 0.55)):
        loft(f'Rib{k}', [(c + Vector((0, yy - 0.03, 0)), (0, -1, 0), chamfer(1.0, 0.77, 0.14)),
                         (c + Vector((0, yy + 0.03, 0)), (0, -1, 0), chamfer(1.0, 0.77, 0.14))], 'frame', 'pod.L', bevel=0.008)
    rail('PodRail', c + Vector((0.5, -0.4, 0.22)), c + Vector((0.5, 0.45, 0.22)), (1, 0, 0), 'pod.L', standoff=0.07)
    corner_bolts('FaceBolt', c + Vector((0, -0.67, 0)), (0, -1, 0), 0.36, 0.25, 'pod.L', lift=0.035)
    hose('PodFeed', [c + Vector((-0.2, 0.3, -0.36)), c + Vector((-0.35, 0.35, -0.55)), c + Vector((-0.28, 0.25, -0.75))], 0.03, 'pod.L')


PRE_FINISH.update({
    'frame': d_frame, 'hd_pike': d_head, 'cr_ferro': d_core, 'ar_hand': d_arms,
    'lg_strider': d_legs, 'bs_flare': d_booster, 'wp_hornet': d_weapon, 'sh_swarm': d_pod,
})
