using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PlanetStructs;

//[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private IcoSphere planetSphere;
    public float radius = 100.0f;
    private Vector3 previousPosition;
    private Transform cameraTransform;
    public List<LodThreshold> LodThresholds;

    

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
        planetSphere = new IcoSphere(gameObject, LodThresholds);
        cameraTransform = Camera.main!.transform;
        previousPosition = cameraTransform.position;
        GenerateMesh();
    }
    

    private void Update()
    {
        if (Vector3.Distance(cameraTransform.position, previousPosition) > 10)
        {
            previousPosition = cameraTransform.position;
            GenerateMesh();
        }
    }

    private void GenerateMesh()
    {
        planetSphere.GenerateIcosahedron(radius);

        // Luckily this can be copied directly
        Vector3[] vertices = planetSphere.Vertices.ToArray();
        int[] indices = planetSphere.Indices.ToArray();
        

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
    }
}
