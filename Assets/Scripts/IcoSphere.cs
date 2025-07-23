using System.Collections.Generic;
using UnityEngine;
using static PlanetStructs;

public class IcoSphere 
{
    // Variables
    public List<Vector3> Vertices { get; private set; }
    public List<int> Indices { get; private set; }
    //public List<Vector2> UVs { get; private set; }
    private readonly Dictionary<Vector3, int> Cache; // Holds midPoints
    private readonly GameObject Parent;
    private float Radius = 1.0f;
    private Vector3 CameraPos;
    private readonly List<LodThreshold> Thresholds;
    private Camera _camera;


    public IcoSphere(GameObject parent, List<LodThreshold> thresholds)
    {
        Parent = parent;
        Vertices = new List<Vector3>();
        Indices = new List<int>();
        //UVs = new List<Vector2>();
        Cache = new Dictionary<Vector3, int>();
        Thresholds = thresholds;
    }

    public void GenerateIcosahedron(float radius)
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }
        
        Radius = radius;
        CameraPos = _camera!.transform.position;
        
        // Clearing, but not deallocating, memory
        Vertices.Clear();
        Indices.Clear();
        //UVs.Clear();
        Cache.Clear();
        
        // Golden ration creates icosahedron proportions
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] initialVertices = new []
        {
            new Vector3(-1f, t, 0f).normalized,
            new Vector3(1f, t, 0f).normalized,
            new Vector3(-1f, -t, 0f).normalized,
            new Vector3(1f, -t, 0f).normalized,
            new Vector3(0f, -1f, t).normalized,
            new Vector3(0f, 1f, t).normalized,
            new Vector3(0f, -1f, -t).normalized,
            new Vector3(0f, 1f, -t).normalized,
            new Vector3(t, 0f, -1f).normalized,
            new Vector3(t, 0f, 1f).normalized,
            new Vector3(-t, 0f, -1f).normalized,
            new Vector3(-t, 0f, 1f).normalized,
        };

        foreach (Vector3 vertex in initialVertices)
        {
            AddVertex(vertex);
        }

        int[] initialTriangles = new int[]
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,
            0, 10, 11,   1, 5, 9,    5, 11, 4,   11, 10, 2,
            10, 7, 6,    7, 1, 8,    3, 9, 4,    3, 4, 2,
            3, 2, 6,    3, 6, 8,    3, 8, 9,    4, 9, 5,
            2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        for (int i = 0; i < initialTriangles.Length; i += 3)
        {
            int i1 = initialTriangles[i];
            int i2 = initialTriangles[i + 1];
            int i3 = initialTriangles[i + 2];
            Subdivide(i1, i2, i3, 0);
        }
    }
    
    private void Subdivide(int i1, int i2, int i3, int depth)
    {
        // Get normalized vertex positions from list
        Vector3 v1 = Vertices[i1].normalized;
        Vector3 v2 = Vertices[i2].normalized;
        Vector3 v3 = Vertices[i3].normalized;

        // MidPoints based on the vertices of this triangle
        Vector3 m1 = (v1 + v2).normalized;
        Vector3 m2 = (v2 + v3).normalized;
        Vector3 m3 = (v3 + v1).normalized;
        
        // Add/cache midpoints and get their indices
        int i4 = AddVertex(m1);
        int i5 = AddVertex(m2);
        int i6 = AddVertex(m3);
        
        
        // Get the vertices in world space so that distances may be calculated
        Vector3 e1World = Parent.transform.TransformPoint(m1 * Radius);
        Vector3 e2World = Parent.transform.TransformPoint(m2 * Radius);
        Vector3 e3World = Parent.transform.TransformPoint(m3 * Radius);

        // Camera distance to midpoints
        float d1 = Vector3.Distance(CameraPos, e1World);
        float d2 = Vector3.Distance(CameraPos, e2World);
        float d3 = Vector3.Distance(CameraPos, e3World);
        
        bool edgeTest1 = true;
        bool edgeTest2 = true;
        bool edgeTest3 = true;

        // See if each edge needs to be subdivided further based on it's distance to the camera
        for (int i = 0; i < Thresholds.Count; i++)
        {
            if (d1 < Thresholds[i].distance && depth < Thresholds[i].lod)
            {
                edgeTest1 = false;
            }
            
            if (d2 < Thresholds[i].distance && depth < Thresholds[i].lod)
            {
                edgeTest2 = false;
            }
            
            if (d3 < Thresholds[i].distance && depth < Thresholds[i].lod)
            {
                edgeTest3 = false;
            }
        }
        
        // If all edges are beyond threshold, use original triangle
        if (edgeTest1 && edgeTest2 && edgeTest3)
        {
            AddTriangle(i1, i2, i3);
            return;
        }
        
        // New triangle configuration after subdivision if all edge tests were to pass (4 triangles from subdivision)
        //        i1
        //       / \
        //     i4---i6
        //    /  \ / \  
        //   i2--i5--i3
        int[] newIndices = { i1, i4, i6, i6, i4, i5, i4, i2, i5, i6, i5, i3 };

        // If an edge is too far away for subdivision to be necessary, we skip the midpoint and use the full edge
        // This method allows for seamless transition between triangles
        if (edgeTest1)
            ReplaceIndices(ref newIndices, i4, i1);
        if (edgeTest2)
            ReplaceIndices(ref newIndices, i5, i2);
        if (edgeTest3)
            ReplaceIndices(ref newIndices, i6, i3);
        
        // Which triangles midpoints weren't replaced. Center triangle is always kept to maintain transition
        bool[] valid = { !edgeTest1, true, !edgeTest2, !edgeTest3 };

        // If an edge was successful, that is now a parent triangle
        for (int i = 0; i < 4; i++)
        {
            if (valid[i])
            {
                // Get vertices for new parent triangle
                int newI1 = newIndices[i * 3];
                int newI2 = newIndices[i * 3 + 1];
                int newI3 = newIndices[i * 3 + 2];

                // Subdivide further if not a degenerate triangle (rare but possible)
                if (newI1 != newI2 && newI2 != newI3 && newI1 != newI3)
                    Subdivide(newI1, newI2, newI3, depth + 1);
            }
        }
    }
    
    // What the cache does here is prevent the same vertex being put in the vertices list multiple times
    // which can happen because triangles share edges. This is a problem that needs to be dealt with regardless of implementation
    private int AddVertex(Vector3 vertex)
    {
        Vector3 scaledVertex = vertex * Radius;
        if (Cache.TryGetValue(scaledVertex, out int index))
            return index;
        
        int newIndex = Vertices.Count;
        Vertices.Add(scaledVertex);
        
        // UVs are now done in the shader directly because it's more efficient, but this is essentially the same logic
        // UVs that go across the edge of the texture don't work because the triangles aren't aligned to it.
        /*Vector3 vert = vertex.normalized;
        float u = 1.0f - (0.5f + Mathf.Atan2(vert.z, vert.x) / (2 * Mathf.PI));        
        float v = 1.0f - (0.5f - Mathf.Asin(vert.y) / Mathf.PI); 
        UVs.Add(new Vector2(u, v));*/
        
        Cache.Add(scaledVertex, newIndex);
        return newIndex;
    }
    
    private void AddTriangle(int i1, int i2, int i3)
    {
        // After a lot of testing I've never had this problem, but I'm saving the comment in case it ever acts weird 
        /*if (i1 == i2 || i2 == i3 || i1 == i3)
        {
            Debug.Log("That's not good");
            return;
        }*/

        Indices.Add(i1);
        Indices.Add(i2);
        Indices.Add(i3);
    }

    private void ReplaceIndices(ref int[] indices, int oldIndex, int newIndex)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] == oldIndex)
                indices[i] = newIndex;
        }
    }
}
