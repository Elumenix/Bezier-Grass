using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class GrassChunkManager : MonoBehaviour
{
    [Header("Debug")] 
    public bool updateEveryFrame = false;
    
    [Header("Terrain Setup")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private Material grassMat;
    public static Material grassMaterial;
    public ComputeShader grassComputeShader;
    
    [Header("Grass Settings")]
    [Range(1, 128)] public int chunksPerSide;
    [SerializeField] private GrassShape grassShapeRange; 
    private static GrassShape grassShape;
    
    [Header("Grass Clump Settings")]
    [Range(1, 128)] public int patternSize;
    [Range(0, 100)] public float scale = 32;
    [Range(0, 1)] public float clumpSeparation = .15f;
    [Range(0, 1)] public float clumpDirection = .15f;
    
    // Chunk management
    //private Dictionary<Vector2Int, GrassChunk> activeChunks = new Dictionary<Vector2Int, GrassChunk>();
    private GrassChunk[] activeChunks;
    private Vector2Int totalChunkCount;
    public static Vector3 terrainPosition;
    private Vector3 terrainSize;
    
    // Buffers and Shareables
    private GraphicsBuffer grassDesc;
    public static GraphicsBuffer highResIndexBuffer;
    public static GraphicsBuffer lowResIndexBuffer;
    public static float chunkSize;
    private static Camera mainCamera;
    
    // Shader Property Lookups
    private static readonly int Shape = Shader.PropertyToID("GrassShape");
    private static readonly int Scale = Shader.PropertyToID("scale");
    private static readonly int GrassDist = Shader.PropertyToID("grassDist");
    private static readonly int FrustumData = Shader.PropertyToID("frustumData");
    private static readonly int PatternSize = Shader.PropertyToID("patternSize");
    private static readonly int ClumpSeparation = Shader.PropertyToID("clumpSeparation");
    private static readonly int MapSize = Shader.PropertyToID("mapSize");
    private static readonly int ClumpDirection = Shader.PropertyToID("clumpDirection");

    private void Awake()
    {
        grassMaterial = grassMat;
    }

    private void Start()
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

    private void InitializeChunks()
    {
        terrainPosition = terrain.transform.position;
        terrainSize = terrain.terrainData.size;
        
        // Terrain may not be perfectly square. The chunks should be though
        chunkSize = terrainSize.x / chunksPerSide;
        float grassDist = chunkSize / 128.0f;
        
        // At this point, some information based on chunk positioning for the compute shader will be set and stay unchanged
        grassComputeShader.SetFloat(Scale, scale);
        grassComputeShader.SetFloat(GrassDist, grassDist);
        grassComputeShader.SetFloat(PatternSize, patternSize);
        grassComputeShader.SetFloat(ClumpSeparation, clumpSeparation);
        grassComputeShader.SetFloat(ClumpDirection, clumpDirection);
        grassComputeShader.SetFloat(MapSize, terrainSize.x);
        
        // Set up chunk tracking 
        totalChunkCount = new Vector2Int(chunksPerSide, chunksPerSide);
        activeChunks = new GrassChunk[totalChunkCount.x * totalChunkCount.y];
        Vector3 cameraPos = mainCamera.transform.position; //SceneView.lastActiveSceneView.camera.transform.position;
        
        // Padding is added to the width because the way grass blades are rotated and sized means they can extend outside
        // the strict chunk boundaries. This would cause some very visible culling as you turn away from or walk between chunks.
        // Padding is based on the grassShape parameters because it specifies the max reach of the blades
        float maxLength = grassShape.grassLength + grassShape.lengthVariance / 2.0f;
        Vector3 chunkPadding = new(maxLength, maxLength, maxLength);

        int i = 0;
        for (int x = 0; x < chunksPerSide; x++)
        {
            for (int z = 0; z < chunksPerSide; z++)
            {
                Vector2Int coord = new Vector2Int(x, z);
                GrassChunk chunk = new GrassChunk(coord, ref chunkPadding);
                
                // Immediately Call to fill the graphics buffers
                chunk.CalculateAndDrawChunk(ref grassComputeShader, ref cameraPos);
    
                activeChunks[i] = chunk;
                i++;
            }
        }  
    }
    
    void Update()
    {
        // It isn't ideal to alter the compute buffers every frame, so this is set aside as a debug option 
        if (updateEveryFrame)
        {
            // Update the grass shape
            grassShape = grassShapeRange;
            grassDesc.SetData(new [] { grassShape });
            grassComputeShader.SetConstantBuffer(Shape, grassDesc, 0, 32);
            
            
            terrainPosition = terrain.transform.position;
            terrainSize = terrain.terrainData.size;
        
            // Terrain may not be perfectly square. The chunks should be though
            chunkSize = terrainSize.x / chunksPerSide;
            float grassDist = chunkSize / 128.0f;
        
            // At this point, some information based on chunk positioning for the compute shader will be set and stay unchanged
            grassComputeShader.SetFloat(Scale, scale);
            grassComputeShader.SetFloat(GrassDist, grassDist);
            grassComputeShader.SetFloat(PatternSize, patternSize);
            grassComputeShader.SetFloat(ClumpSeparation, clumpSeparation);
            grassComputeShader.SetFloat(ClumpDirection, clumpDirection);
            grassComputeShader.SetFloat(MapSize, terrainSize.x);
        }
        
        UpdateActiveChunks();
    }
    
    void UpdateActiveChunks()
    {
        // If we're still loading the data, don't try reading from the list
        if (activeChunks == null) return;
        
        
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
        Vector3 cameraPos = SceneView.lastActiveSceneView.camera.transform.position; //mainCamera.transform.position;
        
        // See if we should render each active chunk
        foreach (GrassChunk chunk in activeChunks)
        {
            // Simple frustum culling
            // While this is already done automatically by the draw calls, doing this early
            // lets us skip the compute shader call, which should speed up the program
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds))
            {
                chunk.CalculateAndDrawChunk(ref grassComputeShader,ref cameraPos);
                //chunk.DrawChunk();
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
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 32)]
public struct GrassShape
{
    [Range(0,5)] public float grassLength;
    [Range(0,1)] public float tilt;
    [Range(0,1)] public float bend;
    [Range(0,1)] public float lengthVariance;
    [Range(0,.5f)] public float tiltVariance;
    [Range(0,.5f)] public float bendVariance;
};
