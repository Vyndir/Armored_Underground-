using UnityEditor;
using UnityEngine;

namespace AU.EditorTools
{
    /// <summary>Imports the Blender mech, assigns URP materials, normalizes orientation
    /// (+Z forward, feet at origin, ~8m tall) and saves it as a prefab.</summary>
    public static class MechImporter
    {
        const string Fbx = "Assets/AU/Art/Mech/v3/stray_v3.fbx";
        const string TexDir = "Assets/AU/Art/Mech/v3/textures";
        const string PrefabPath = "Assets/AU/Generated/Mech_Stray.prefab";

        public static void Build(GameAssets assets)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.SaveAndReimport();

            assets.mechPaint = Mats.LitMat("Mech_Paint", new Color(0.23f, 0.26f, 0.31f), 0.62f, 0.55f);
            assets.mechAccent = Mats.LitMat("Mech_Accent", new Color(0.85f, 0.45f, 0.08f), 0.6f, 0.35f);
            assets.mechFrame = Mats.LitMat("Mech_Frame", new Color(0.07f, 0.075f, 0.085f), 0.5f, 0.85f);
            assets.mechJoint = Mats.LitMat("Mech_Joint", new Color(0.025f, 0.025f, 0.03f), 0.35f, 0.2f);
            assets.mechSteel = Mats.LitMat("Mech_Steel", new Color(0.55f, 0.57f, 0.6f), 0.75f, 1f);
            assets.mechGlow = Mats.LitMat("Mech_Glow", Color.black, 0.5f, 0f, null, null, null, new Color(0f, 0.85f, 1f) * 3.5f);
            assets.mechThrust = Mats.LitMat("Mech_Thrust", Color.black, 0.5f, 0f, null, null, null, new Color(1f, 0.6f, 0.25f) * 8f);
            assets.mechGlass = Mats.LitMat("Mech_Glass", new Color(0.01f, 0.012f, 0.015f), 0.95f, 0.4f);
            assets.mechDecal = Mats.LitMat("Mech_Decal", new Color(0.85f, 0.86f, 0.88f), 0.45f, 0f);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("Mech_Stray");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] ? mats[i].name : "";
                    mats[i] = n.StartsWith("MB_") ? PartMaterial(n.Substring(3).Split('.', ' ')[0])
                        : n.StartsWith("MC_glow") ? assets.mechGlow
                        : n.StartsWith("MC_thrust") ? assets.mechThrust
                        : n.StartsWith("MC_glass") ? assets.mechGlass
                        : n.StartsWith("MC_steel") ? assets.mechSteel
                        : n.StartsWith("MC_decal") ? assets.mechDecal
                        : n.StartsWith("MC_joint") || n.StartsWith("MC_rubber") ? assets.mechJoint
                        : n.StartsWith("MC_accent") ? assets.mechAccent
                        : n.StartsWith("MC_paint") ? assets.mechPaint
                        : assets.mechFrame;
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            // Normalize scale to ~8m and orientation to +Z forward (measured on the default loadout)
            Bounds b = Bounds(inst);
            float height = b.size.y;
            if (height > 0.01f && Mathf.Abs(height - 8f) > 1.5f)
            {
                float s = 8f / height;
                inst.transform.localScale *= s;
                b = Bounds(inst);
            }
            var back = FindDeep(inst.transform, "P_bs_flare__booster");
            if (back)
            {
                var vr = back.GetComponent<Renderer>();
                // the booster is on the back: if it sits on +Z the model faces -Z, so turn it around
                if (vr && vr.bounds.center.z - b.center.z > 0) inst.transform.localRotation = Quaternion.Euler(0, 180, 0) * inst.transform.localRotation;
            }
            b = Bounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            assets.mechPrefab = prefab;
            EditorUtility.SetDirty(assets);
            Debug.Log($"[AU] Mech prefab built, height {height:F2} -> normalized");
        }

        static readonly string[] DefaultParts = { "P_frame", "P_hd_pike", "P_cr_ferro", "P_ar_hand", "P_lg_strider", "P_bs_flare", "P_wp_hornet", "P_sh_swarm" };

        /// <summary>Material for one part: AU/MechSurface with that part's baked texture set.</summary>
        static Material PartMaterial(string pid)
        {
            var m = Mats.Custom("MechPart_" + pid, Shader.Find("AU/MechSurface"));
            Texture2D T(string k) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{pid}_{k}.png");
            m.SetTexture("_BaseMap", T("albedo"));
            m.SetTexture("_MaskMap", T("mask"));
            m.SetTexture("_MRAMap", T("mra"));
            m.SetTexture("_BumpMap", T("normal"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = System.Array.FindAll(go.GetComponentsInChildren<Renderer>(), r => System.Array.Exists(DefaultParts, p => r.name.StartsWith(p + "__")));
            if (rs.Length == 0) rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
            return null;
        }
    }
}
