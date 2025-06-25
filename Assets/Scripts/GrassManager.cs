using System;
using System.Drawing;
using Attributes;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

public class GrassManager : MonoBehaviour
{
    [Header("Required")]
    public Material voronoiMat;
    public ComputeShader grassPositionComputeShader;
    public Mesh grassMesh;
    public Material grassMaterial;
    public Texture2D noiseTexture;
    
    [Header("Settings")]
    public int textureSize = 512;
    
    [Header("Output")]
    [LargeTexturePreview, SerializeField]
    private RenderTexture voronoiTexture; 
    public RenderTexture VoronoiTexture => voronoiTexture;
    

    private int grassDimensions = 32;
    private const int grassBlades = 1024;
    private const int grassVertices = 15;
    private ComputeBuffer grassBuffer;
    private Matrix4x4[] matrices;

    
    
    void Start()
    {
        grassBuffer = new ComputeBuffer(1024, sizeof(float) * 16);
        GenerateVoronoiTexture();
        /*int grassDim = grassBlades / 2;

        for (int i = 0; i < grassDim; i++)
        {
            float x = (i / (float)grassDim) - .5f;
            for (int j = 0; j < grassDim; j++)
            {
                float y = (j / (float) grassDim) - .5f;
            }
        }*/
    }

    private void OnApplicationQuit()
    {
        grassBuffer?.Release();
    }

    // Update is called once per frame
    void Update()
    {
        matrices = new Matrix4x4[1024];
        grassBuffer.SetData(matrices);

        Matrix4x4[] goodMatrices = new Matrix4x4[1024];

        
        int i = 0;
        for (int x = 0; x < grassDimensions; x++)
        {
            for (int z = 0; z < grassDimensions; z++)
            {
                goodMatrices[i] = Matrix4x4.TRS(new Vector3(x * 2, 0, z * 2),
                    Quaternion.Euler(0,0,90), Vector3.one);
                i++;
            }
        }
        
        grassPositionComputeShader.SetBuffer(0, "grassTransforms", grassBuffer);
        grassPositionComputeShader.SetTexture(0, "_BlueNoiseTexture", noiseTexture);
        
        grassPositionComputeShader.SetInt("dimensions", grassDimensions);
        
        grassPositionComputeShader.Dispatch(0, 1, 1, 1);
        grassBuffer.GetData(matrices);
        
        RenderParams rp = new RenderParams(grassMaterial);
        Graphics.RenderMeshInstanced(rp, grassMesh, 0, matrices);
    }

    /*private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, voronoiMat);
    }*/
    
    [ContextMenu("Generate Voronoi Texture")]
    public void GenerateVoronoiTexture()
    {
        // Create RenderTexture
        if (voronoiTexture != null) voronoiTexture.Release();
        
        voronoiTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
        voronoiTexture.Create();
        
        // Render to texture
        Graphics.Blit(null, voronoiTexture, voronoiMat);
    }
    
    void OnDestroy()
    {
        if (voronoiTexture != null)
            voronoiTexture.Release();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i <= grassDimensions; i++)
        {
            Gizmos.DrawLine(new Vector3(i, 0, 0), new Vector3(i, 0, grassDimensions));
            Gizmos.DrawLine(new Vector3(0, 0, i), new Vector3(grassDimensions, 0, i));
            
            /*Gizmos.DrawLine(new Vector3(i * .5f, 0, 0), new Vector3(i * .5f, 0, grassDimensions));
            Gizmos.DrawLine(new Vector3(0, 0, i * .5f), new Vector3(grassDimensions, 0, i * .5f));*/
        }
    }
}
