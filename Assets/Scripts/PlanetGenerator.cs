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
    public float lodDetail = 1.0f;
    

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
        planetSphere = new IcoSphere(gameObject);
        GenerateMesh();
    }
    

    private void Update()
    {
        GenerateMesh();
    }

    private void GenerateMesh()
    {
        planetSphere.GenerateIcosahedron(radius, lodDetail);

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
