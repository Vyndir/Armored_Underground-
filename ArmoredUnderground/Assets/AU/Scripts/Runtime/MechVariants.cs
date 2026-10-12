using UnityEngine;

namespace AU
{
    /// <summary>
    /// Part swapping on the modular mech: every part has its own meshes named
    /// P_&lt;partId&gt;__&lt;bone or marker&gt;. Shows the equipped part in each slot
    /// (plus the shared P_frame) and hides the rest.
    /// </summary>
    public static class MechVariants
    {
        public static void Apply(Transform model, Loadout l)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (!n.StartsWith("P_")) continue;
                int sep = n.IndexOf("__", System.StringComparison.Ordinal);
                if (sep < 0) continue;
                string id = n.Substring(2, sep - 2);
                bool on = id == "frame" || Equipped(l, id);
                r.gameObject.SetActive(on);
            }
        }

        static bool Equipped(Loadout l, string id)
        {
            foreach (Slot s in Parts.Slots) if (l[s] == id) return true;
            return false;
        }

        /// <summary>Find the active renderer for a part marker, e.g. ("wp_hornet", "Barrel").</summary>
        public static Renderer Marker(Transform model, string partId, string marker)
        {
            string name = $"P_{partId}__{marker}";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == name) return r;
            return null;
        }

        public static System.Collections.Generic.List<Renderer> Markers(Transform model, string partId, string prefix)
        {
            var list = new System.Collections.Generic.List<Renderer>();
            string start = $"P_{partId}__{prefix}";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.StartsWith(start)) list.Add(r);
            return list;
        }

        /// <summary>World-space bounds of a skinned part in its current pose (bind-pose mesh bounds through its bone).</summary>
        public static (Vector3 center, Vector3 axisMin, Vector3 axisMax) PoseBounds(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh && smr.bones.Length > 0 && smr.sharedMesh.bindposes.Length > 0)
            {
                var mesh = smr.sharedMesh;
                var b = mesh.bounds;
                int bi = 0;
                var bw = mesh.boneWeights;
                if (bw != null && bw.Length > 0) bi = Mathf.Clamp(bw[0].boneIndex0, 0, smr.bones.Length - 1);
                Matrix4x4 m = smr.bones[bi].localToWorldMatrix * mesh.bindposes[Mathf.Min(bi, mesh.bindposes.Length - 1)];
                // longest axis endpoints
                Vector3 e = b.extents;
                Vector3 axis = e.x >= e.y && e.x >= e.z ? new Vector3(e.x, 0, 0) : e.y >= e.z ? new Vector3(0, e.y, 0) : new Vector3(0, 0, e.z);
                return (m.MultiplyPoint3x4(b.center), m.MultiplyPoint3x4(b.center - axis), m.MultiplyPoint3x4(b.center + axis));
            }
            var bb = r.bounds;
            return (bb.center, bb.center - Vector3.forward * bb.extents.z, bb.center + Vector3.forward * bb.extents.z);
        }
    }
}
