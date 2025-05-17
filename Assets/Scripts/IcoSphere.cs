using System.Collections.Generic;
using UnityEngine;

public class IcoSphere 
{
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
    
    // Variables
    public List<Polygon> Polygons { get; private set; }
    public List<Vector3> Vertices { get; private set; }


    public IcoSphere(int subDivisions)
    {
        Initialize(subDivisions);
    }
    
    private void Initialize(int subDivisions)
    {
        // Essentially (10 * 4^x) + 2 : This is the exact number of expected vertices for this many subdivisions
        int capacity = 10 * (1 << (2 * subDivisions)) + 2;
        Vertices = new List<Vector3>(capacity);
        Polygons = new List<Polygon>();

        float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
        
        // 12 vertices of base icosahedron
        Vertices.AddRange(new[]
        {
            new Vector3(-1, t, 0).normalized,
            new Vector3(1, t, 0).normalized,
            new Vector3(-1, -t, 0).normalized,
            new Vector3(1, -t, 0).normalized,
            new Vector3(0, -1, t).normalized,
            new Vector3(0, 1, t).normalized,
            new Vector3(0, -1, -t).normalized,
            new Vector3(0, 1, -t).normalized,
            new Vector3(t, 0, -1).normalized,
            new Vector3(t, 0, 1).normalized,
            new Vector3(-t, 0, -1).normalized,
            new Vector3(-t, 0, 1).normalized
        });
        
        // 20 faces of base icosahedron
        Polygons.AddRange(new []
        {
            new Polygon(0, 11, 5),
            new Polygon(0, 5, 1),
            new Polygon(0, 1, 7),
            new Polygon(0, 7, 10),
            new Polygon(0, 10, 11),
            new Polygon(1, 5, 9),
            new Polygon(5, 11, 4),
            new Polygon(11, 10, 2),
            new Polygon(10, 7, 6),
            new Polygon(7, 1, 8),
            new Polygon(3, 9, 4),
            new Polygon(3, 4, 2),
            new Polygon(3, 2, 6),
            new Polygon(3, 6, 8),
            new Polygon(3, 8, 9),
            new Polygon(4, 9, 5),
            new Polygon(2, 4, 11),
            new Polygon(6, 2, 10),
            new Polygon(8, 6, 7),
            new Polygon(9, 8, 1)
        });
        
        Subdivide(subDivisions);
    }
    
    private void Subdivide(int subDivisions)
    {
        

        for (int i = 0; i < subDivisions; i++)
        {
            int n = Polygons.Count;
            
            // Only Polygons (indices) are being directly replaced. Vertices are being added and saved to the list as we find midPoints
            List<Polygon> newPolygons = new List<Polygon>(Polygons.Count * 4);
            
            // Create map to save midPoints so that I don't keep reusing them, Unique to this level
            Dictionary<long, int> midPointMap = new Dictionary<long, int>(Polygons.Count * 3);
            
            for (int j = 0; j < n; j++)
            {
                Polygon p = Polygons[j];
                
                // Find midpoints between all vertices to create new triangles
                int ab = GetMidPoint(p.v1, p.v2, midPointMap);
                int bc = GetMidPoint(p.v2, p.v3, midPointMap);
                int ca = GetMidPoint(p.v3, p.v1, midPointMap);

                // Create four new polygons from these vertices
                newPolygons.AddRange(new []
                {
                    new Polygon(p.v1, ab, ca), 
                    new Polygon(p.v2, bc, ab),
                    new Polygon(p.v3, ca, bc),
                    new Polygon(ab, bc, ca)
                });
            }
            
            // Replace old polygons with subdivided ones. For either next recursion or reading indices
            Polygons = newPolygons;
        }
    }

    private int GetMidPoint(int a, int b, Dictionary<long, int> midPointMap)
    {
        // Create a key using the larger and smaller index
        int smallerIndex = Mathf.Min(a, b);
        int greaterIndex = Mathf.Max(a, b);
        long key = ((long)smallerIndex << 32) + greaterIndex;

        // If the key has already been seen, the midpoint is already in the list
        if (midPointMap.TryGetValue(key, out int ret))
        {
            return ret;
        }
        
        // Find the midPoint
        Vector3 p1 = Vertices[a];
        Vector3 p2 = Vertices[b];
        Vector3 middle = (p1 + p2).normalized;

        // MidPoint will be on the end of the vertex list
        ret = Vertices.Count;
        Vertices.Add(middle);
        
        // Make sure a key points to the midpoint
        midPointMap.Add(key, ret);
        return ret;
    }
}
