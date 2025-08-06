using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
struct IndirectDrawIndexedArgs
{
    public uint indexCountPerInstance;   // number of indices in one mesh/instance
    public uint instanceCount;           // how many instances to draw
    public uint startIndexLocation;      // offset into the index buffer (in indices)
    public uint baseVertexLocation;      // add to each index from index buffer
    public uint startInstanceLocation;   // add to SV_InstanceID
}

public class GrassChunk
{
    public readonly Vector2Int coordinate;
    public readonly Bounds bounds;
    private readonly GraphicsBuffer grassBuffer;
    private readonly GraphicsBuffer commandBuffer;
    private readonly GraphicsBuffer lowLodCommandBuffer;
    private RenderParams rp;
    private bool isHighLOD;
    
    // Saved shader property to prevent string lookup
    private static readonly int StartPosition = Shader.PropertyToID("startPosition");
    private static readonly int GrassBlades = Shader.PropertyToID("grassBlades");


    public GrassChunk(Vector2Int chunkCoord)
    {
        coordinate = chunkCoord;
        bounds = GetChunkBounds();
        grassBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Append, 4096, sizeof(float) * 22);
        rp = new RenderParams(GrassChunkManager.grassMaterial)
        {
            matProps = new MaterialPropertyBlock()
        };
        
        // This should never be resized, so setting it here is fine
        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
        
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
        
        // This should never be resized, so setting it here is fine
        lowLodCommandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
        args[0].indexCountPerInstance = 15;
        lowLodCommandBuffer.SetData(args);
    }
    
    public void Dispose()
    {
        grassBuffer.Release();
        commandBuffer.Release();
        lowLodCommandBuffer.Release();
    }
    
    private Bounds GetChunkBounds()
    {
        // TODO: When getting to culling, we'll probably have offsets so we need to make sure the chunk is expanded a little to prevent premature culling
        // TODO: This works for flat terrain currently, vertical bounds may need to change when adding hills

        // We'll reuse this variable a few times, so we'll put it on the local stack
        float chunkSize = GrassChunkManager.chunkSize;
        
        // Because unity's procedural instancing function still requires a bounds, we set it to the bounds for the chunk
        // The function treats rendering as all grass blades or no grass blades, so we make sure the bounds covers the whole chunk
        Vector3 chunkMin = GrassChunkManager.terrainPosition + new Vector3(
            coordinate.x * chunkSize,
            0,
            coordinate.y * chunkSize
        );
        
        // TODO: 20 is definitely too large, figure out a better number when we get proper scales set up
        // Grass blades vertical height also needs to fit the chunk to prevent culling (20 is a pretty safe number)
        Vector3 chunkArea = new Vector3(chunkSize, 20, chunkSize);
        return new Bounds(chunkMin + chunkArea * 0.5f, chunkArea);
    }

    /// <summary>
    /// This version calculates the position of grass blades in the chunk and which ones should be culled using a compute shader.
    /// It also determines whether the blades for the entire chunk should be high LOD or low LOD.
    /// </summary>
    /// <param name="grassComputeShader">A reference to the compute shader we use for to calculate blade positions.</param>
    /// <param name="cameraPos">Reference to the current camera so that the distance can be calculated for the correct LOD.</param>
    public void CalculateAndDrawChunk(ref ComputeShader grassComputeShader, ref Vector3 cameraPos)
    {
        // Reset the buffer. A different amount may be culled this frame
        grassBuffer.SetCounterValue(0);
        
        grassComputeShader.SetBuffer(0, GrassBlades, grassBuffer);
        grassComputeShader.SetVector(StartPosition, bounds.min);
        grassComputeShader.Dispatch(0, 4, 1, 4);
        
        
        //Vector3 cameraPos = mainCamera.transform.position;
        rp.matProps.SetBuffer(GrassBlades, grassBuffer);
        rp.worldBounds = bounds;
        Vector3 nearestPoint = bounds.ClosestPoint(cameraPos);
        float distanceFromChunk = Vector3.Distance(nearestPoint, cameraPos);

        // This is set up in the GrassMaterial
        if (distanceFromChunk >= 25.0f) // low LOD
        {
            isHighLOD = false;
            GraphicsBuffer.CopyCount(
                grassBuffer,
                lowLodCommandBuffer,
                4
            );

            rp.material = GrassChunkManager.grassLODMaterial;
            Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, GrassChunkManager.lowResIndexBuffer, lowLodCommandBuffer);
        }
        else // high LOD
        {
            isHighLOD = true;
            GraphicsBuffer.CopyCount(
                grassBuffer,
                commandBuffer,
                4
            );

            rp.material = GrassChunkManager.grassMaterial;
            Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, GrassChunkManager.highResIndexBuffer, commandBuffer);
        }
    }

    /// <summary>
    /// This draw method uses the previously set up append buffer to quickly render the chunk.
    /// CalculateAndDrawChunk should have been called at least once before using this method so blade positioning is known.
    /// </summary>
    public void DrawChunk()
    {
        // We don't need to set back up any data or do new calculation. We just redraw the buffer.
        if (isHighLOD) 
        {
            Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, GrassChunkManager.highResIndexBuffer, commandBuffer);

        }
        else 
        {
            Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, GrassChunkManager.lowResIndexBuffer, lowLodCommandBuffer);
        }
    }
}
