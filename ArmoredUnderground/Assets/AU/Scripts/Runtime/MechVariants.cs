using UnityEngine;

namespace AU
{
    /// <summary>
    /// Visual cues for equipped parts on the shared skeleton: proportions of head,
    /// core, arms, legs, booster and pod change with the part. (Interim until each
    /// part has its own modelled mesh.) Child bones get inverse scale so changes stay local.
    /// </summary>
    public static class MechVariants
    {
        public static void Apply(Transform model, Loadout l)
        {
            Desired.Clear();
            Set(model, "head", l.head switch { "hd_moth" => new Vector3(1.25f, 0.75f, 1.15f), "hd_bastion" => new Vector3(1.25f, 1.2f, 1.2f), _ => Vector3.one });
            Set(model, "chest", l.core switch { "cr_wasp" => new Vector3(0.86f, 1f, 0.9f), "cr_anvil" => new Vector3(1.18f, 1.06f, 1.15f), _ => Vector3.one });
            var arm = l.arms switch { "ar_needle" => new Vector3(0.8f, 1f, 0.8f), "ar_hauler" => new Vector3(1.25f, 1f, 1.2f), _ => Vector3.one };
            foreach (var s in new[] { ".L", ".R" })
            {
                Set(model, "upperarm" + s, arm);
                Set(model, "forearm" + s, arm);
                var leg = l.legs switch
                {
                    "lg_hopper" => new Vector3(0.85f, 1.08f, 0.9f),
                    "lg_glide" => new Vector3(1.1f, 0.88f, 1.05f),
                    "lg_bulwark" => new Vector3(1.32f, 0.95f, 1.25f),
                    _ => Vector3.one,
                };
                Set(model, "thigh" + s, leg);
                Set(model, "shin" + s, leg);
                Set(model, "foot" + s, l.legs == "lg_bulwark" ? new Vector3(1.2f, 1.2f, 1.2f) : l.legs == "lg_glide" ? new Vector3(1.15f, 0.6f, 1.1f) : Vector3.one);
            }
            Set(model, "booster", l.booster switch { "bs_comet" => new Vector3(1.15f, 1.2f, 1.2f), "bs_tern" => new Vector3(0.9f, 0.9f, 0.9f), _ => Vector3.one });
            Set(model, "pod.L", l.shoulder switch { "sh_mines" => new Vector3(0.9f, 0.6f, 1f), "sh_spear" => new Vector3(0.8f, 1.2f, 1.6f), _ => Vector3.one });
            Set(model, "hand.R", l.weapon switch { "wp_lance" => new Vector3(1f, 1f, 1.45f), "wp_gale" => new Vector3(1.25f, 1.25f, 0.85f), "wp_maul" => new Vector3(1.3f, 1.3f, 0.9f), _ => Vector3.one });
            Resolve(model, Vector3.one);
        }

        static readonly System.Collections.Generic.Dictionary<string, Vector3> Desired = new();
        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> Rest = new();

        // local scale = desired / parent's desired, so each bone's look is independent
        static void Resolve(Transform t, Vector3 parentDesired)
        {
            Vector3 mine = parentDesired;
            if (IsBone(t.name) || t.name == "pelvis")
            {
                mine = Desired.TryGetValue(t.name, out var d) ? d : Vector3.one;
                if (!Rest.TryGetValue(t, out var rest)) Rest[t] = rest = t.localScale;
                t.localScale = Vector3.Scale(rest, new Vector3(mine.x / parentDesired.x, mine.y / parentDesired.y, mine.z / parentDesired.z));
            }
            foreach (Transform c in t) Resolve(c, mine);
        }

        static void Set(Transform root, string bone, Vector3 scale) => Desired[bone] = scale;

        static bool IsBone(string n) => n is "upperarm.L" or "upperarm.R" or "head" or "chest" or "booster" or "pod.L" or "shoulder.L" or "shoulder.R" or "forearm.L" or "forearm.R" or "hand.L" or "hand.R" or "shin.L" or "shin.R" or "foot.L" or "foot.R" or "spine" or "thigh.L" or "thigh.R";

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f) return f; }
            return null;
        }
    }
}
