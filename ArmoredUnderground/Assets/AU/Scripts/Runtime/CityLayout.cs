using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    /// <summary>
    /// The street plan of the Kessel Ward district, shared by the editor city
    /// generator and runtime systems (routes, minimap, spawn points).
    /// X runs east, Z runs north, meters.
    /// </summary>
    public static class CityLayout
    {
        // North-south avenues: x position and road width (curb to curb).
        public static readonly float[] AvenueX = { -360, -230, -120, 0, 110, 240, 360 };
        public static readonly float[] AvenueW = { 20, 16, 22, 34, 18, 24, 20 };
        // East-west streets: z position and width.
        public static readonly float[] StreetZ = { -420, -280, -150, -40, 80, 200, 320, 440 };
        public static readonly float[] StreetW = { 22, 16, 22, 28, 16, 24, 20, 22 };

        public const float Sidewalk = 5f;
        public static float MinX => AvenueX[0];
        public static float MaxX => AvenueX[AvenueX.Length - 1];
        public static float MinZ => StreetZ[0];
        public static float MaxZ => StreetZ[StreetZ.Length - 1];

        public static Vector3 Node(int ave, int street) => new Vector3(AvenueX[ave], 0, StreetZ[street]);

        public class RouteDef
        {
            public string id, name, type, blurb;
            public int laps, rivals, repRequired;
            public float skill;
            public int[] payout, repGain;
            public Vector2Int[] nodes; // (avenue index, street index)
            public List<Vector3> Points()
            {
                var l = new List<Vector3>();
                foreach (var n in nodes) l.Add(Node(n.x, n.y));
                return l;
            }
        }

        public static readonly RouteDef[] Routes =
        {
            new RouteDef {
                id = "neonmile", name = "NEON MILE", type = "CIRCUIT", laps = 2, rivals = 3, repRequired = 0, skill = 0.9f,
                blurb = "Two laps of Kessel Ward. Long boulevard straight, tight downtown turns.",
                payout = new[] { 4000, 2200, 1100, 400 }, repGain = new[] { 4, 2, 1, 0 },
                nodes = new[] { new Vector2Int(3, 2), new Vector2Int(3, 6), new Vector2Int(5, 6), new Vector2Int(5, 3),
                                new Vector2Int(4, 3), new Vector2Int(4, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) },
            },
            new RouteDef {
                id = "dash", name = "DOWNTOWN DASH", type = "SPRINT", laps = 1, rivals = 3, repRequired = 2, skill = 1.0f,
                blurb = "Point to point across the ward, west docks to the east market.",
                payout = new[] { 6000, 3000, 1500, 500 }, repGain = new[] { 5, 3, 1, 0 },
                nodes = new[] { new Vector2Int(0, 6), new Vector2Int(2, 6), new Vector2Int(2, 4), new Vector2Int(3, 4),
                                new Vector2Int(3, 0), new Vector2Int(5, 0), new Vector2Int(5, 3), new Vector2Int(6, 3) },
            },
        };

        public static readonly Vector3 GarageDoor = new Vector3(-60, 0, -40 + 14 + Sidewalk);

        public static bool IsAvenueRoad(float x, out int idx)
        {
            for (int i = 0; i < AvenueX.Length; i++)
                if (Mathf.Abs(x - AvenueX[i]) < AvenueW[i] * 0.5f) { idx = i; return true; }
            idx = -1; return false;
        }
    }
}
