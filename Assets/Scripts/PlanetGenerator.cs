using System;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private IcoSphere planetSphere;
    
    [Range(0, 10)]
    public int subDivisions;

    void Awake()
    {
        // Set up the mesh Stuff
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = new Mesh();
        mesh = meshFilter.sharedMesh;
        mesh.name = "PlanetMesh";
        meshRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    }
    
    void Start()
    {
        planetSphere = new IcoSphere();
        GenerateMesh();
    }
    

    /*private void Update()
    {
        if (Time.frameCount == 100)
        {
            planetSphere = new IcoSphere();
        }
        else if (Time.frameCount == 160)
        {
            GenerateMesh();
        }
    }*/

    private void OnValidate()
    {
        // This will break things if it runs on the first frame
        if (Time.frameCount > 1)
        {
            GenerateMesh();
        }
    }

    private void GenerateMesh()
    {
        Debug.Log("Generating Sphere");
        planetSphere.Subdivide(subDivisions);

        // Luckily this can be copied directly
        Vector3[] vertices = planetSphere.Vertices.ToArray();

        int polygonCount = planetSphere.Polygons.Count;
        int[] indices = new int[polygonCount * 3];
        for (int i = 0; i < polygonCount; i++)
        {
            IcoSphere.Polygon p = planetSphere.Polygons[i];
            int baseIndex = i * 3;

            indices[baseIndex] = p.v1;
            indices[baseIndex + 1] = p.v2;
            indices[baseIndex + 2] = p.v3;
        }

        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices, 0, vertices.Length, MeshUpdateFlags.DontValidateIndices | 
                                                       MeshUpdateFlags.DontResetBoneBounds |
                                                       MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.SetNormals(vertices, 0, vertices.Length, MeshUpdateFlags.DontValidateIndices | 
                                                      MeshUpdateFlags.DontResetBoneBounds |
                                                      MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.triangles = indices;
        
        // Optimizations
        mesh.RecalculateBounds();
        //mesh.Optimize(); // This might be bad if I do LOD (Which I probably will)
        
        Debug.Log("Faces: " + polygonCount + "     Vertices: " + vertices.Length + "     Indices: " + indices.Length);
    }
}
