using System.Collections.Generic;
using UnityEngine;

public static class PlanetStructs
{
    public static Dictionary<int, float> detailLevelDistances = new Dictionary<int, float>()
    {
        {0, Mathf.Infinity},
        {1, 60f},
        {2, 25f},
        {3, 10f},
        {4, 4f},
        {5, 1.5f},
        {6, 0.7f},
        {7, 0.3f},
        {8, 0.1f}
    };

    public static float size = 5.0f;
    
    public struct Polygon
    {
        public readonly int v1, v2, v3;

        public Polygon(int v1, int v2, int v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }
    
    // Believe it or not, Adding to a dictionary takes over 80% of the time spent in this class once you get to 6 Divisions
    // Making a custom key that's much faster to check is the biggest optimization in this class
    public readonly struct EdgeKey : System.IEquatable<EdgeKey>
    {
        private readonly int A;
        private readonly int B;

        public EdgeKey(int a, int b)
        {
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(EdgeKey other) => A == other.A && B == other.B;

        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + A;
                hash = hash * 31 + B;
                return hash;
            }
        }
    }
}
