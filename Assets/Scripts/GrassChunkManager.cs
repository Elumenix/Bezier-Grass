using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
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
    public ComputeShader grassComputeShader;
    private GraphicsBuffer highResIndexBuffer;
    private GraphicsBuffer highResArcTBuffer;
    
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
    
    // Camera reference
    private Camera mainCamera;
    private static readonly int GrassBlades = Shader.PropertyToID("grassBlades");

    void Start()
    {
        mainCamera = Camera.main;

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
        
        float[] arcT = new[] { 0.001f, 0.33f, 0.49f, 0.62f, 0.73f, 0.38f, 0.92f, 1.0f };
        highResArcTBuffer = new GraphicsBuffer(Target.Constant, 8, sizeof(float));
        highResArcTBuffer.SetData(arcT);
        
        InitializeChunks();
    }

    private void OnApplicationQuit()
    {
        // Prevent memory problems
        highResIndexBuffer?.Release();
        highResArcTBuffer?.Release();
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
                
                GrassChunk chunk = new GrassChunk(grassMaterial)
                {
                    coordinate = coord,
                    chunkBounds = GetChunkBounds(coord)
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
        // ToDo: Bounds for the chunk should be checked to see if it is even visible before running compute shader and instancing
        //if (!IsChunkVisible(chunk)) return;
        
        // Reset the buffer. A different amount may be culled this frame
        chunk.grassBuffer.SetCounterValue(0);
        
        // TODO: These might not even need to be in the manager class, I might expand the chunk class instead
        grassComputeShader.SetBuffer(0, GrassBlades, chunk.grassBuffer);
        grassComputeShader.SetFloat("scale", scale);
        grassComputeShader.SetVector("startPosition", chunk.chunkBounds.min);
        grassComputeShader.SetFloat("grassDist", grassDist);
        
        grassComputeShader.Dispatch(0, 1, 1, 1);

        
        // TODO: uncomment this after compute shader is set up properly
        int instanceCountOffset = sizeof(uint); // byte offset of the second uint
        GraphicsBuffer.CopyCount(
            chunk.grassBuffer,
            chunk.commandBuffer,
            instanceCountOffset
        );
        
        
        // TODO: LOD changes will need to happen somewhere around here
        chunk.rp.matProps.SetConstantBuffer("_ArcLengthData", highResArcTBuffer, 0, highResArcTBuffer.count * sizeof(float));
        chunk.rp.matProps.SetBuffer(GrassBlades, chunk.grassBuffer);
        Graphics.RenderPrimitivesIndexedIndirect(chunk.rp, MeshTopology.Triangles, highResIndexBuffer, chunk.commandBuffer);
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
        // TODO: When getting to culling, we'll probably have offsets so we need to make sure the chunk is expanded a little to prevent premature culling
        // TODO: This works for flat terrain currently, vertical bounds may need to change when adding hills
        
        // Because unity's procedural instancing function still requires a bounds, we set it to the bounds for the chunk
        // The function treats rendering as all grass blades or no grass blades, so we make sure the bounds covers the whole chunk
        Vector3 chunkMin = terrainPosition + new Vector3(
            chunkCoord.x * chunkSizeX,
            0,
            chunkCoord.y * chunkSizeZ
        );
        
        // Grass blades vertical height also needs to fit the chunk to prevent culling (20 is a pretty safe number)
        Vector3 chunkSize = new Vector3(chunkSizeX, 20, chunkSizeZ);
        return new Bounds(chunkMin + chunkSize * 0.5f, chunkSize);
    }
    
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
            Gizmos.DrawWireCube(chunk.chunkBounds.center, chunk.chunkBounds.size);
        }
    }
}

[System.Serializable]
public class GrassChunk
{
    public Vector2Int coordinate;
    public Bounds chunkBounds;
    public GraphicsBuffer grassBuffer;
    public GraphicsBuffer commandBuffer;
    public RenderParams rp;

    

    public GrassChunk(Material grassMaterial)
    {
        grassBuffer = new GraphicsBuffer(Target.Structured | Target.Append, 1024, sizeof(float) * 12);
        rp = new RenderParams(grassMaterial)
        {
            matProps = new MaterialPropertyBlock()
        };
        
        // This should never be resized, so setting it here is fine
        commandBuffer = new GraphicsBuffer(Target.IndirectArguments, 1, sizeof(uint) * 5);
        
        IndirectDrawIndexedArgs[] args = new IndirectDrawIndexedArgs[1];
        args[0] = new IndirectDrawIndexedArgs
        {
            indexCountPerInstance = 39,
            instanceCount         = 0, // Resized in draw call
            startIndexLocation    = 0,
            baseVertexLocation    = 0,
            startInstanceLocation = 0
        };
        commandBuffer.SetData(args);
    }
    
    public void Dispose()
    {
        grassBuffer.Release();
        commandBuffer.Release();
    }
}

[StructLayout(LayoutKind.Sequential)]
struct IndirectDrawIndexedArgs
{
    public uint indexCountPerInstance;   // number of indices in one mesh/instance
    public uint instanceCount;           // how many instances to draw
    public uint startIndexLocation;      // offset into the index buffer (in indices)
    public uint baseVertexLocation;      // add to each index from index buffer
    public uint startInstanceLocation;   // add to SV_InstanceID
}
