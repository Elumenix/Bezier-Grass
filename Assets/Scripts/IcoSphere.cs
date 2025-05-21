using System.Collections.Generic;
using UnityEngine;
using static PlanetStructs;

public class IcoSphere 
{
    // Variables
    public List<Vector3> Vertices { get; private set; }
    public List<Polygon> Polygons { get; private set; }
    private List<Polygon> nextPolygons;
    private List<Polygon> basePolygons;
    private Dictionary<EdgeKey, int>[] midPointCaches;
    private const int maxSubDivisions = 10;


    public IcoSphere()
    {
        
        Initialize();
    }
    
    private void Initialize()
    {
        // This is the exact number of expected vertices and indices for this many subdivisions
        // The main goal here is to preallocate and save memory, otherwise the garbage collector will tank performance
        Vertices = new List<Vector3>(10 * (1 << (2 * maxSubDivisions)) + 2); // (10 * 4^x) + 2
        Polygons = new List<Polygon>(60 * (1 << (2 * maxSubDivisions))); // 60 * 4^x
        basePolygons = new List<Polygon>(20);
        nextPolygons = new List<Polygon>(Polygons.Capacity);
        midPointCaches = new Dictionary<EdgeKey, int>[maxSubDivisions];
        for (int i = 0; i < 10; i++)
        {
            // These are essentially the possible number of edges for each division, there can't be more midpoints than that
            midPointCaches[i] = new Dictionary<EdgeKey, int>(30 * (1 << (2 * i))); // 30 * 4^x
        }
        
        
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
        basePolygons.AddRange(new []
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
    }
    
    public void Subdivide(int subDivisions)
    {
        if (subDivisions is < 0 or > maxSubDivisions) throw new System.ArgumentOutOfRangeException(nameof(subDivisions));
        
        // Back to initialized data : Clearing data without deallocating memory
        Vertices.RemoveRange(12, Vertices.Count - 12);
        Polygons.Clear();
        nextPolygons.Clear();
        Polygons.AddRange(basePolygons.GetRange(0, 20));
        
        for (int i = 0; i < subDivisions; i++)
        {
            // Empty the cache without deallocating memory
            var cache = midPointCaches[i];
            cache.Clear();

            int n = Polygons.Count;
            for (int j = 0; j < n; j++)
            {
                Polygon p = Polygons[j];
                
                // Find midpoints between all vertices to create new triangles
                int ab = GetMidPoint(p.v1, p.v2, cache);
                int bc = GetMidPoint(p.v2, p.v3, cache);
                int ca = GetMidPoint(p.v3, p.v1, cache);

                // Create four new polygons from these vertices
                nextPolygons.AddRange(new []
                {
                    new Polygon(p.v1, ab, ca), 
                    new Polygon(p.v2, bc, ab),
                    new Polygon(p.v3, ca, bc),
                    new Polygon(ab, bc, ca)
                });
            }
            
            // Replace old polygons with subdivided ones. For either next recursion or reading indices
            (Polygons, nextPolygons) = (nextPolygons, Polygons);
            nextPolygons.Clear();
        }
    }

    private int GetMidPoint(int a, int b, Dictionary<EdgeKey, int> cache)
    {
        EdgeKey key = new EdgeKey(a, b);

        // If the key has already been seen, the midpoint is already in the list
        if (cache.TryGetValue(key, out int ret))
        {
            return ret;
        }
        
        // Find the midPoint
        Vector3 middle = (Vertices[a] + Vertices[b]).normalized;

        // MidPoint will be on the end of the vertex list
        ret = Vertices.Count;
        Vertices.Add(middle);
        
        // Make sure a key points to the midpoint
        cache.Add(key, ret);
        return ret;
    }
}
