using System.Collections.Generic;
using UnityEngine;
using static PlanetStructs;

public class IcoSphere 
{
    // Variables
    public List<Vector3> Vertices { get; private set; }
    public List<int> Indices { get; private set; }
    private Dictionary<Vector3, int> Cache; // Holds midPoints
    private GameObject Parent;
    private float Radius = 1.0f;
    private float LOD = 1.0f;
    private Vector3 CameraPos;


    public IcoSphere(GameObject parent)
    {
        Parent = parent;
        Vertices = new List<Vector3>();
        Indices = new List<int>();
        Cache = new Dictionary<Vector3, int>();

        //Initialize();
    }
    public void GenerateIcosahedron(float radius, float lod)
    {
        Radius = radius;
        LOD = lod;
        CameraPos = Camera.main.transform.position;
        // Clearing, but not deallocating, memory
        Vertices.Clear();
        Indices.Clear();
        Cache.Clear();
        
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
            Subdivide(i1, i2, i3, 1f);
        }
    }
    
    private void Subdivide(int i1, int i2, int i3, float size)
    {
        if (size < .01f)
        {
            AddTriangle(i1, i2, i3);
            return;
        }
        
        Vector3 v1 = Vertices[i1].normalized;
        Vector3 v2 = Vertices[i2].normalized;
        Vector3 v3 = Vertices[i3].normalized;

        // MidPoints based on the vertices of this triangle
        Vector3 m1 = (v1 + v2).normalized;
        Vector3 m2 = (v2 + v3).normalized;
        Vector3 m3 = (v3 + v1).normalized;
        
        // Get or Make associated indices
        int i4 = AddVertex(m1);
        int i5 = AddVertex(m2);
        int i6 = AddVertex(m3);
        
        // Get the vertices in world space so that distances may be calculated
        Vector3 e1World = Parent.transform.TransformPoint(m1 * Radius);
        Vector3 e2World = Parent.transform.TransformPoint(m2 * Radius);
        Vector3 e3World = Parent.transform.TransformPoint(m3 * Radius);


        float d1 = Vector3.Distance(CameraPos, e1World);
        float d2 = Vector3.Distance(CameraPos, e2World);
        float d3 = Vector3.Distance(CameraPos, e3World);

        float threshold = size * Radius * LOD;

        bool edgeTest1 = d1 >= threshold;
        bool edgeTest2 = d2 >= threshold;
        bool edgeTest3 = d3 >= threshold;
        
        
        // If a distance doesn't reach a threshold, make a triangle
        if (edgeTest1 && edgeTest2 && edgeTest3)
        {
            AddTriangle(i1, i2, i3);
            return;
        }
        
        
        int[] newIndices = { i1, i4, i6, i6, i4, i5, i4, i2, i5, i6, i5, i3 };

        if (edgeTest1)
            ReplaceIndices(ref newIndices, i4, i1);
        if (edgeTest2)
            ReplaceIndices(ref newIndices, i5, i2);
        if (edgeTest3)
            ReplaceIndices(ref newIndices, i6, i3);

        bool[] valid = { !edgeTest1, true, !edgeTest2, !edgeTest3 };

        for (int i = 0; i < 4; i++)
        {
            if (valid[i])
            {
                int newI1 = newIndices[i * 3];
                int newI2 = newIndices[i * 3 + 1];
                int newI3 = newIndices[i * 3 + 2];

                if (newI1 != newI2 && newI2 != newI3 && newI1 != newI3)
                    Subdivide(newI1, newI2, newI3, size / 2);
            }
        }
    }
    
    private int AddVertex(Vector3 vertex)
    {
        Vector3 scaledVertex = vertex * Radius;
        if (Cache.TryGetValue(scaledVertex, out int index))
            return index;
        
        int newIndex = Vertices.Count;
        Vertices.Add(scaledVertex);
        Cache.Add(scaledVertex, newIndex);
        return newIndex;
    }
    
    private void AddTriangle(int i1, int i2, int i3)
    {
        if (i1 == i2 || i2 == i3 || i1 == i3)
        {
            Debug.Log("That's not good");
            return;
        }

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
