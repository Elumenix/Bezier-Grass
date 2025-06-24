using System;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class GrassManager : MonoBehaviour
{
    public ComputeShader grassPositionComputeShader;
    public Mesh grassMesh;
    public Material grassMaterial;
    private Matrix4x4[] matrices;
    private int grassDimensions = 32;
    private const int grassBlades = 1024;
    private const int grassVertices = 15;
    private ComputeBuffer grassBuffer;
    void Start()
    {
        grassBuffer = new ComputeBuffer(1024, sizeof(float) * 16);
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
        grassPositionComputeShader.SetInt("dimensions", grassDimensions);
        
        grassPositionComputeShader.Dispatch(0, 1, 1, 1);
        grassBuffer.GetData(matrices);
        
        Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrices);
    }
}
