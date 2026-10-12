using UnityEngine;

namespace AU
{
    /// <summary>References to generated assets the runtime needs. Lives in Resources.</summary>
    [CreateAssetMenu(menuName = "AU/Game Assets")]
    public class GameAssets : ScriptableObject
    {
        public Material rain, splash, steam, spark, smoke, fire, tracer, tracerEnemy, thruster, thrusterCore, gate, gateBeam, markerGlow, explosion, decalScorch;
        public Material mechPaint, mechAccent, mechFrame, mechJoint, mechSteel, mechGlow, mechThrust, mechGlass;
        public GameObject mechPrefab;
        public Texture2D softDot;

        static GameAssets _i;
        public static GameAssets I => _i ? _i : (_i = Resources.Load<GameAssets>("GameAssets"));
    }
}
