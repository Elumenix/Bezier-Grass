using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class GrassChunkManager : MonoBehaviour
{
    [Header("Terrain Setup")]
    [SerializeField] private Terrain terrain;
    [Range(1, 128)] public int chunksPerSide;
    
    [Header("Grass Settings")]
    public static Material grassMaterial;
    public static Material grassLODMaterial;
    [SerializeField] private Mesh grassMesh;
    //[SerializeField] private float grassDensity = 10f; // grass per square meter
    public ComputeShader grassComputeShader;
    public static GraphicsBuffer highResIndexBuffer;
    public static GraphicsBuffer lowResIndexBuffer;
    private GraphicsBuffer grassDesc;
    
    public float scale = 32;
    private float grassDist;
    public static float chunkSize;
    
    // Chunk management
    //private Dictionary<Vector2Int, GrassChunk> activeChunks = new Dictionary<Vector2Int, GrassChunk>();
    private GrassChunk[] activeChunks;
    
    // Terrain properties
    private Vector2Int totalChunkCount;
    public static Vector3 terrainPosition;
    private Vector3 terrainSize;
    [SerializeField] private GrassShape grassShapeRange; 
    private static GrassShape grassShape;
    
    // Camera reference
    private Camera mainCamera;
    private static readonly int Shape = Shader.PropertyToID("GrassShape");
    private static readonly int Scale = Shader.PropertyToID("scale");
    private static readonly int GrassDist = Shader.PropertyToID("grassDist");
    private static readonly int FrustumData = Shader.PropertyToID("frustumData");

    private void Awake()
    {
        grassMaterial = Resources.Load<Material>("Materials/GrassMaterial");
        grassLODMaterial = Resources.Load<Material>("Materials/GrassLODMaterial");
        
        // Temp, updated during start
        grassShape = new GrassShape();
    }

    void Start()
    {
        mainCamera = Camera.main;
        grassShape = grassShapeRange;

        uint[] indices = new uint[]
        {
            0, 1, 3,
            3, 2, 0,
            2, 3, 5,
            5, 4, 2,
            4, 5, 7,
            7, 6, 4,
            6, 7, 9,
            9, 8, 6,
            8, 9, 11,
            11, 10, 8,
            10, 11, 13,
            13, 12, 10,
            12, 13, 14
        };
        highResIndexBuffer = new GraphicsBuffer(Target.Index, 39, sizeof(uint));
        highResIndexBuffer.SetData(indices);
        
        uint[] indicesLOD = new uint[]
        {
            0, 1, 3,
            3, 2, 0,
            2, 3, 5,
            5, 4, 2,
            4, 5, 6,
        };
        lowResIndexBuffer = new GraphicsBuffer(Target.Index, 15, sizeof(uint));
        lowResIndexBuffer.SetData(indicesLOD);
        
        grassDesc = new GraphicsBuffer(Target.Constant, 1, sizeof(float) * 6);
        grassDesc.SetData(new [] { grassShape });
        
        // This lasts for the life of the program, so it can be set now
        grassComputeShader.SetConstantBuffer(Shape, grassDesc, 0, 32);

        
        InitializeChunks();
    }

    private void OnApplicationQuit()
    {
        // Prevent memory problems
        highResIndexBuffer?.Release();
        lowResIndexBuffer?.Release();
        grassDesc?.Release();
    }

    private void OnValidate()
    {
        if (grassDesc == null) return;
        grassDesc.SetData(new [] { grassShape });
        grassComputeShader.SetConstantBuffer(Shape, grassDesc, 0, 32);
    }

    void InitializeChunks()
    {
        terrainPosition = terrain.transform.position;
        terrainSize = terrain.terrainData.size;
        
        // Terrain may not be perfectly square. The chunks should be though
        chunkSize = terrainSize.x / chunksPerSide;
        grassDist = chunkSize / 32;
        totalChunkCount = new Vector2Int(chunksPerSide, chunksPerSide);
        activeChunks = new GrassChunk[totalChunkCount.x * totalChunkCount.y];

        int i = 0;
        for (int x = 0; x < chunksPerSide; x++)
        {
            for (int z = 0; z < chunksPerSide; z++)
            {
                Vector2Int coord = new Vector2Int(x, z);
                GrassChunk chunk = new GrassChunk(coord);
    
                activeChunks[i] = chunk;
                i++;
            }
        }  
        
        // At this point, some information based on chunk positioning for the compute shader will be set and stay unchanged
        grassComputeShader.SetFloat(Scale, scale);
        grassComputeShader.SetFloat(GrassDist, grassDist);
    }
    
    void Update()
    {
        UpdateActiveChunks();
    }
    
    void UpdateActiveChunks()
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        float[] frustumData = new float[24];

        // If the chunk is in bounds, we further cull in the compute shader to only blades visible on screen
        // This results in less contention to the append buffer and prevents rendering redundant blades
        // We need to collect the camera data needed to do frustum culling in the compute shader here 
        for (int i = 0; i < 6; i++)
        {
            frustumData[i * 4 + 0] = frustumPlanes[i].normal.x;
            frustumData[i * 4 + 1] = frustumPlanes[i].normal.y;
            frustumData[i * 4 + 2] = frustumPlanes[i].normal.z;
            frustumData[i * 4 + 3] = frustumPlanes[i].distance;
        }
        grassComputeShader.SetFloats(FrustumData, frustumData);
        
        
        // See if we should render each active chunk
        foreach (GrassChunk chunk in activeChunks)
        {
            // Simple frustum culling
            // While this is already done automatically by the draw calls, doing this early
            // lets us skip the compute shader call, which should speed up the program
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds))
            {
                chunk.DrawChunk(ref grassComputeShader);
            }
        }
    }
    
    /*int GetChunkSeed(Vector2Int coord)
    {
        // Generate consistent seed based on chunk coordinates
        return coord.x * 73856093 ^ coord.y * 19349663;
    }
    
    void DestroyChunk(Vector2Int coord)
    {
        if (activeChunks.TryGetValue(coord, out GrassChunk chunk))
        {
            chunk.Dispose();
            activeChunks.Remove(coord);
        }
    }*/
    
    void OnDestroy()
    {
        // Clean up all chunks
        foreach (GrassChunk chunk in activeChunks)
        {
            chunk?.Dispose();
        }
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (terrain == null) return;
        
        // Draw chunk grid
        Gizmos.color = Color.yellow;
        for (int x = 0; x <= chunksPerSide; x++)
        {
            Vector3 start = terrainPosition + new Vector3(x * chunksPerSide, 0, 0);
            Vector3 end = start + new Vector3(0, 0, terrainSize.z);
            Gizmos.DrawLine(start, end);
        }
        
        for (int z = 0; z <= chunksPerSide; z++)
        {
            Vector3 start = terrainPosition + new Vector3(0, 0, z * chunksPerSide);
            Vector3 end = start + new Vector3(terrainSize.x, 0, 0);
            Gizmos.DrawLine(start, end);
        }
        
        // Draw active chunks
        Gizmos.color = Color.green;
        foreach (GrassChunk chunk in activeChunks)
        {
            Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
        }
    }
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 32)]
public struct GrassShape
{
    public Vector2 height;
    public Vector2 tilt;
    public Vector2 bend;
};
