using UnityEditor;
using UnityEngine;

namespace AU.EditorTools
{
    /// <summary>Imports the Blender mech, assigns URP materials, normalizes orientation
    /// (+Z forward, feet at origin, ~8m tall) and saves it as a prefab.</summary>
    public static class MechImporter
    {
        const string Fbx = "Assets/AU/Art/Mech/stray.fbx";
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
            imp.SaveAndReimport();

            assets.mechPaint = Mats.LitMat("Mech_Paint", new Color(0.23f, 0.26f, 0.31f), 0.62f, 0.55f);
            assets.mechAccent = Mats.LitMat("Mech_Accent", new Color(0.85f, 0.45f, 0.08f), 0.6f, 0.35f);
            assets.mechFrame = Mats.LitMat("Mech_Frame", new Color(0.07f, 0.075f, 0.085f), 0.5f, 0.85f);
            assets.mechJoint = Mats.LitMat("Mech_Joint", new Color(0.025f, 0.025f, 0.03f), 0.35f, 0.2f);
            assets.mechSteel = Mats.LitMat("Mech_Steel", new Color(0.55f, 0.57f, 0.6f), 0.75f, 1f);
            assets.mechGlow = Mats.LitMat("Mech_Glow", Color.black, 0.5f, 0f, null, null, null, new Color(0f, 0.85f, 1f) * 3.5f);
            assets.mechThrust = Mats.LitMat("Mech_Thrust", Color.black, 0.5f, 0f, null, null, null, new Color(1f, 0.6f, 0.25f) * 8f);
            assets.mechGlass = Mats.LitMat("Mech_Glass", new Color(0.01f, 0.012f, 0.015f), 0.95f, 0.4f);

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
                    mats[i] = n.StartsWith("Paint_Primary") ? assets.mechPaint
                        : n.StartsWith("Paint_Accent") ? assets.mechAccent
                        : n.StartsWith("Frame") ? assets.mechFrame
                        : n.StartsWith("Joint") ? assets.mechJoint
                        : n.StartsWith("Steel") ? assets.mechSteel
                        : n.StartsWith("Emissive_Neon") ? assets.mechGlow
                        : n.StartsWith("Emissive_Thruster") ? assets.mechThrust
                        : n.StartsWith("Visor") ? assets.mechGlass
                        : assets.mechFrame;
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            // Normalize scale to ~8m and orientation to +Z forward
            Bounds b = Bounds(inst);
            float height = b.size.y;
            if (height > 0.01f && Mathf.Abs(height - 8f) > 1.5f)
            {
                float s = 8f / height;
                inst.transform.localScale *= s;
                b = Bounds(inst);
            }
            var visor = FindDeep(inst.transform, "Head_Visor");
            if (visor)
            {
                var vr = visor.GetComponent<Renderer>();
                Vector3 vc = vr ? vr.bounds.center : visor.position;
                if (vc.z - b.center.z < 0) inst.transform.localRotation = Quaternion.Euler(0, 180, 0) * inst.transform.localRotation;
            }
            b = Bounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            assets.mechPrefab = prefab;
            EditorUtility.SetDirty(assets);
            Debug.Log($"[AU] Mech prefab built, height {height:F2} -> normalized");
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
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
