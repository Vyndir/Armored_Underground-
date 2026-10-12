using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AU
{
    public enum Slot { Head, Core, Arms, Legs, Booster, Generator, Weapon, Shoulder }
    public enum LegType { Biped, ReverseJoint, Hover, Heavy }
    public enum WeaponKind { Rifle, Scatter, Rail, Gatling }
    public enum ShoulderKind { Missiles, Mines, HeavyMissile }

    [Serializable]
    public class Part
    {
        public string id, name, maker, desc;
        public Slot slot;
        public int price;
        public float weight, ap;
        // head
        public float lockRange;
        // core
        public float boostMult = 1, stability = 1;
        // arms
        public float fireRate = 1, recoilControl = 1;
        // legs
        public LegType legType;
        public float load, speed = 1, jump = 1, grip = 1;
        // booster
        public float thrust = 1, drain = 1, qbForce = 1, qbCost = 1;
        // generator
        public float enCap, enRegen, enDelay = 1;
        // weapon
        public WeaponKind weapon;
        public float dmg, rate, speedMps, spread, reload, impact;
        public int mag, pellets = 1;
        // shoulder
        public ShoulderKind shoulder;
        public int count;
        public float cooldown;
    }

    /// <summary>Part catalogue. Names and makers are original to this project.</summary>
    public static class Parts
    {
        public static readonly List<Part> All = new()
        {
            // HEADS
            new Part { id = "hd_pike", slot = Slot.Head, name = "HD-01 PIKE", maker = "Kessel Heavy", price = 0, weight = 3, ap = 320, lockRange = 160, desc = "Standard sensor head. Reliable lock range." },
            new Part { id = "hd_moth", slot = Slot.Head, name = "HD-07 MOTH", maker = "Volta Dynamics", price = 1800, weight = 2, ap = 190, lockRange = 220, desc = "Wide-band sensors. Long lock range, thin plating." },
            new Part { id = "hd_bastion", slot = Slot.Head, name = "HD-22 BASTION", maker = "Grav-Ost", price = 2600, weight = 5, ap = 580, lockRange = 130, desc = "Armored dome. Short-sighted but tough." },
            // CORES
            new Part { id = "cr_ferro", slot = Slot.Core, name = "CR-10 FERRO", maker = "Kessel Heavy", price = 0, weight = 12, ap = 2300, boostMult = 1.0f, stability = 1.0f, desc = "Balanced frame core." },
            new Part { id = "cr_wasp", slot = Slot.Core, name = "CR-31 WASP", maker = "Volta Dynamics", price = 3200, weight = 8, ap = 1550, boostMult = 1.08f, stability = 0.8f, desc = "Slim racing core with tuned thrust ducts. +8% boost speed." },
            new Part { id = "cr_anvil", slot = Slot.Core, name = "CR-50 ANVIL", maker = "Grav-Ost", price = 4800, weight = 18, ap = 3400, boostMult = 0.96f, stability = 1.4f, desc = "Brick of a core. Very hard to stagger." },
            // ARMS
            new Part { id = "ar_hand", slot = Slot.Arms, name = "AR-02 HANDLER", maker = "Kessel Heavy", price = 0, weight = 7, ap = 820, fireRate = 1.0f, recoilControl = 1f, desc = "General-purpose manipulators." },
            new Part { id = "ar_needle", slot = Slot.Arms, name = "AR-15 NEEDLE", maker = "Volta Dynamics", price = 2200, weight = 5, ap = 540, fireRate = 1.18f, recoilControl = 1.2f, desc = "Precision servo arms. +18% fire rate." },
            new Part { id = "ar_hauler", slot = Slot.Arms, name = "AR-40 HAULER", maker = "Grav-Ost", price = 2600, weight = 10, ap = 1260, fireRate = 0.9f, recoilControl = 0.9f, desc = "Industrial arms, heavily plated." },
            // LEGS
            new Part { id = "lg_strider", slot = Slot.Legs, name = "LG-03 STRIDER", maker = "Kessel Heavy", price = 0, legType = LegType.Biped, weight = 14, ap = 1650, load = 62, speed = 1.0f, jump = 1.0f, grip = 1.0f, desc = "Bipedal all-rounder." },
            new Part { id = "lg_hopper", slot = Slot.Legs, name = "LG-19 HOPPER", maker = "Volta Dynamics", price = 2900, legType = LegType.ReverseJoint, weight = 11, ap = 1220, load = 52, speed = 1.04f, jump = 1.6f, grip = 1.05f, desc = "Reverse-joint legs. Huge jumps, rooftop shortcuts." },
            new Part { id = "lg_glide", slot = Slot.Legs, name = "LG-27 GLIDE", maker = "Silt & Sons", price = 4200, legType = LegType.Hover, weight = 13, ap = 1060, load = 56, speed = 1.16f, jump = 0.75f, grip = 0.7f, desc = "Hover skirt legs. Fastest on asphalt, drifts in corners." },
            new Part { id = "lg_bulwark", slot = Slot.Legs, name = "LG-60 BULWARK", maker = "Grav-Ost", price = 5200, legType = LegType.Heavy, weight = 22, ap = 2950, load = 92, speed = 0.86f, jump = 0.75f, grip = 1.15f, desc = "Heavy legs. Carries anything." },
            // BOOSTERS
            new Part { id = "bs_flare", slot = Slot.Booster, name = "BS-1 FLARE", maker = "Kessel Heavy", price = 0, weight = 3, thrust = 1.0f, drain = 1.0f, qbForce = 1.0f, qbCost = 1f, desc = "Standard booster." },
            new Part { id = "bs_comet", slot = Slot.Booster, name = "BS-4 COMET", maker = "Volta Dynamics", price = 3600, weight = 4, thrust = 1.2f, drain = 1.35f, qbForce = 1.15f, qbCost = 1.2f, desc = "Racing booster. +20% top boost speed, thirsty." },
            new Part { id = "bs_tern", slot = Slot.Booster, name = "BS-2 TERN", maker = "Silt & Sons", price = 2400, weight = 2, thrust = 1.08f, drain = 0.78f, qbForce = 0.85f, qbCost = 0.7f, desc = "Efficient cruiser. Sips energy." },
            // GENERATORS
            new Part { id = "gn_ember", slot = Slot.Generator, name = "GN-A EMBER", maker = "Kessel Heavy", price = 0, weight = 5, enCap = 2000, enRegen = 640, enDelay = 1f, desc = "Standard generator." },
            new Part { id = "gn_coil", slot = Slot.Generator, name = "GN-C COIL", maker = "Grav-Ost", price = 2600, weight = 7, enCap = 2800, enRegen = 560, enDelay = 1.1f, desc = "Deep capacity cells." },
            new Part { id = "gn_surge", slot = Slot.Generator, name = "GN-S SURGE", maker = "Volta Dynamics", price = 3400, weight = 6, enCap = 1800, enRegen = 960, enDelay = 0.7f, desc = "Rapid recovery output." },
            // R-ARM WEAPONS
            new Part { id = "wp_hornet", slot = Slot.Weapon, name = "WR-HORNET RIFLE", maker = "Kessel Heavy", price = 0, weight = 4, weapon = WeaponKind.Rifle, dmg = 80, rate = 6, mag = 30, reload = 1.6f, speedMps = 520, spread = 0.006f, impact = 60, desc = "Assault rifle. Steady damage." },
            new Part { id = "wp_gale", slot = Slot.Weapon, name = "WR-GALE GATLING", maker = "Silt & Sons", price = 1900, weight = 6, weapon = WeaponKind.Gatling, dmg = 30, rate = 16, mag = 120, reload = 2.4f, speedMps = 480, spread = 0.018f, impact = 22, desc = "Spin-up gatling. Shreds stagger meters." },
            new Part { id = "wp_maul", slot = Slot.Weapon, name = "WR-MAUL SCATTER", maker = "Grav-Ost", price = 2700, weight = 6, weapon = WeaponKind.Scatter, dmg = 38, rate = 1.6f, mag = 6, reload = 2.2f, speedMps = 420, spread = 0.06f, pellets = 9, impact = 30, desc = "Scattergun. Devastating up close." },
            new Part { id = "wp_lance", slot = Slot.Weapon, name = "WR-LANCE RAIL", maker = "Volta Dynamics", price = 5600, weight = 8, weapon = WeaponKind.Rail, dmg = 620, rate = 0.7f, mag = 4, reload = 3.0f, speedMps = 2000, spread = 0, impact = 450, desc = "Charged rail cannon. One heavy hit." },
            // SHOULDERS
            new Part { id = "sh_swarm", slot = Slot.Shoulder, name = "SH-SWARM MISSILES", maker = "Kessel Heavy", price = 0, weight = 5, shoulder = ShoulderKind.Missiles, count = 6, dmg = 85, cooldown = 4.5f, impact = 70, desc = "Six homing micro-missiles. Needs lock." },
            new Part { id = "sh_mines", slot = Slot.Shoulder, name = "SH-CALTROP MINES", maker = "Silt & Sons", price = 2200, weight = 4, shoulder = ShoulderKind.Mines, count = 3, dmg = 340, cooldown = 5, impact = 300, desc = "Drops proximity mines behind you. Racer favorite." },
            new Part { id = "sh_spear", slot = Slot.Shoulder, name = "SH-SPEAR MISSILE", maker = "Grav-Ost", price = 3800, weight = 7, shoulder = ShoulderKind.HeavyMissile, count = 1, dmg = 480, cooldown = 6, impact = 420, desc = "Single heavy homing warhead." },
        };

        public static Part Get(string id) => All.FirstOrDefault(p => p.id == id);
        public static IEnumerable<Part> OfSlot(Slot s) => All.Where(p => p.slot == s);
        public static readonly Slot[] Slots = (Slot[])Enum.GetValues(typeof(Slot));
        public static string SlotLabel(Slot s) => s switch
        {
            Slot.Weapon => "R-ARM UNIT",
            Slot.Shoulder => "SHOULDER UNIT",
            _ => s.ToString().ToUpperInvariant(),
        };
    }

    [Serializable]
    public class Loadout
    {
        public string head = "hd_pike", core = "cr_ferro", arms = "ar_hand", legs = "lg_strider",
            booster = "bs_flare", generator = "gn_ember", weapon = "wp_hornet", shoulder = "sh_swarm";
        public Color paint = new(0.23f, 0.26f, 0.31f), accent = new(0.85f, 0.45f, 0.08f), neon = new(0f, 0.85f, 1f);

        public string this[Slot s]
        {
            get => s switch { Slot.Head => head, Slot.Core => core, Slot.Arms => arms, Slot.Legs => legs, Slot.Booster => booster, Slot.Generator => generator, Slot.Weapon => weapon, _ => shoulder };
            set { switch (s) { case Slot.Head: head = value; break; case Slot.Core: core = value; break; case Slot.Arms: arms = value; break; case Slot.Legs: legs = value; break; case Slot.Booster: booster = value; break; case Slot.Generator: generator = value; break; case Slot.Weapon: weapon = value; break; default: shoulder = value; break; } }
        }

        public Part P(Slot s) => Parts.Get(this[s]) ?? Parts.OfSlot(s).First();
        public Loadout Clone() => (Loadout)MemberwiseClone();
    }

    /// <summary>Flight and combat numbers derived from a loadout.</summary>
    public class MechStats
    {
        public float ap, weight, carried, load, groundSpeed, boostSpeed, accel, grip, jump, enCap, enRegen, enDelay,
            boostDrain, qbCost, qbForce, lockRange, fireRateMult, stability, overdriveSpeed;
        public bool overweight;
        public LegType legType;
        public Part weapon, shoulder;

        public static MechStats From(Loadout l, float scale = 1f)
        {
            var s = new MechStats();
            Part head = l.P(Slot.Head), core = l.P(Slot.Core), arms = l.P(Slot.Arms), legs = l.P(Slot.Legs),
                bs = l.P(Slot.Booster), gn = l.P(Slot.Generator);
            s.weapon = l.P(Slot.Weapon); s.shoulder = l.P(Slot.Shoulder);
            s.ap = head.ap + core.ap + arms.ap + legs.ap;
            s.carried = head.weight + core.weight + arms.weight + bs.weight + gn.weight + s.weapon.weight + s.shoulder.weight;
            s.weight = s.carried + legs.weight;
            s.load = legs.load;
            float over = Mathf.Max(0, s.carried - s.load);
            s.overweight = over > 0;
            float wf = Mathf.Max(0.55f, 1 - over * 0.025f) * (1 - (s.weight - 50) * 0.003f);
            s.legType = legs.legType;
            s.groundSpeed = 30f * legs.speed * wf * scale;
            s.boostSpeed = 64f * bs.thrust * core.boostMult * wf * scale;
            s.overdriveSpeed = s.boostSpeed * 1.38f;
            s.accel = 48f * wf * scale;
            s.grip = legs.grip;
            s.jump = 16f * legs.jump;
            s.enCap = gn.enCap; s.enRegen = gn.enRegen; s.enDelay = 0.55f * gn.enDelay;
            s.boostDrain = 420f * bs.drain;
            s.qbCost = 520f * bs.qbCost;
            s.qbForce = 50f * bs.qbForce;
            s.lockRange = head.lockRange;
            s.fireRateMult = arms.fireRate;
            s.stability = core.stability;
            return s;
        }
    }
}
