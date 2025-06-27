using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

public class GrassChunkManager : MonoBehaviour
{
    [Header("Terrain Setup")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private int chunksPerSide = 8;
    
    [Header("Grass Settings")]
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Mesh grassMesh;
    //[SerializeField] private float grassDensity = 10f; // grass per square meter
    public ComputeShader grassPositionComputeShader;
    public float scale = 32;
    private float grassDist;
    
    // Chunk management
    //private Dictionary<Vector2Int, GrassChunk> activeChunks = new Dictionary<Vector2Int, GrassChunk>();
    private GrassChunk[] activeChunks;
    
    // Terrain properties
    private float chunkSizeX;
    private float chunkSizeZ;
    private Vector2Int totalChunkCount;
    private Vector3 terrainPosition;
    private Vector3 terrainSize;
    private RenderParams rp;
    
    // Camera reference
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        rp = new RenderParams(grassMaterial);
        InitializeChunks();
    }
    
    void InitializeChunks()
    {
        terrainPosition = terrain.transform.position;
        terrainSize = terrain.terrainData.size;
        
        // Terrain may not be perfectly square. The chunks should be though
        chunkSizeX = terrainSize.x / chunksPerSide;
        chunkSizeZ = terrainSize.z / chunksPerSide;
        grassDist = chunkSizeX / 32;
        totalChunkCount = new Vector2Int(chunksPerSide, chunksPerSide);
        activeChunks = new GrassChunk[totalChunkCount.x * totalChunkCount.y];

        int i = 0;
        for (int x = 0; x < chunksPerSide; x++)
        {
            for (int z = 0; z < chunksPerSide; z++)
            {
                Vector2Int coord = new Vector2Int(x, z);
                
                GrassChunk chunk = new GrassChunk
                {
                    coordinate = coord,
                    bounds = GetChunkBounds(coord)
                };
    
                //GenerateGrassForChunk(chunk);
                activeChunks[i] = chunk;
                i++;
            }
        }  
        
        Debug.Log($"Initialized grass chunks: {totalChunkCount.x}x{totalChunkCount.y}, " +
                  $"chunk size: {chunkSizeX:F1}x{chunkSizeZ:F1}");
    }
    
    void Update()
    {
        UpdateActiveChunks();
    }
    
    void UpdateActiveChunks()
    {
        foreach (GrassChunk chunk in activeChunks)
        {
            GenerateGrassForChunk(chunk);
        }
    }
    
    void GenerateGrassForChunk(GrassChunk chunk)
    {
        //if (!IsChunkVisible(chunk)) return;
        
        // TODO: These might not even need to be in the manager class, I might expand the chunk class instead
        grassPositionComputeShader.SetBuffer(0, "grassTransforms", chunk.grassBuffer);
        grassPositionComputeShader.SetFloat("scale", scale);
        grassPositionComputeShader.SetVector("startPosition", chunk.bounds.min);
        grassPositionComputeShader.SetFloat("grassDist", grassDist);
        
        grassPositionComputeShader.Dispatch(0, 1, 1, 1);
        chunk.grassBuffer.GetData(chunk.matrices);
        
        // TODO: Switch to drawMeshInstancedIndirect, It apparently has no mesh limit so it may be easier to use
        Graphics.RenderMeshInstanced(rp, grassMesh, 0, chunk.matrices);
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
    }
    
    bool IsChunkVisible(GrassChunk chunk)
    {
        // Simple frustum culling
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds);
    }*/
    
    
    public Bounds GetChunkBounds(Vector2Int chunkCoord)
    {
        Vector3 chunkMin = terrainPosition + new Vector3(
            chunkCoord.x * chunkSizeX,
            0,
            chunkCoord.y * chunkSizeZ
        );
        
        Vector3 chunkSize = new Vector3(chunkSizeX, terrainSize.y, chunkSizeZ);
        return new Bounds(chunkMin + chunkSize * 0.5f, chunkSize);
    }
    
    void OnDestroy()
    {
        // Clean up all chunks
        foreach (GrassChunk chunk in activeChunks)
        {
            chunk.Dispose();
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
            Vector3 start = terrainPosition + new Vector3(x * chunkSizeX, 0, 0);
            Vector3 end = start + new Vector3(0, 0, terrainSize.z);
            Gizmos.DrawLine(start, end);
        }
        
        for (int z = 0; z <= chunksPerSide; z++)
        {
            Vector3 start = terrainPosition + new Vector3(0, 0, z * chunkSizeZ);
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

[System.Serializable]
public class GrassChunk
{
    public Vector2Int coordinate;
    public Bounds bounds;
    public ComputeBuffer grassBuffer;
    public Matrix4x4[] matrices;

    public GrassChunk()
    {
        grassBuffer = new ComputeBuffer(1024, sizeof(float) * 16); // holds Transforms
        matrices = new Matrix4x4[1024];
        grassBuffer.SetData(matrices);
    }
    
    public void Dispose()
    {
        grassBuffer.Release();
    }
}