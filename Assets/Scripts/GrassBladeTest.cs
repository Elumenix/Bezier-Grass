using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;

struct TestGrassBlade
{
    public float3 position;
    public float width;
    public float height;
    public float2 facing;
    public float tilt;
    public float bend;
};

[ExecuteInEditMode]
public class GrassBladeTest : MonoBehaviour
{
    public Material bladeMaterial;
    public float width = 1;
    public float height = 3;
    public float tilt = 3;
    public float bend = 1;
    public float2 facing;
    [Range(0, 1)] public float LODValue = 0;

    [Header("ArcLengthParameterization")] 
    public List<float> distribution = new List<float>(8){
        0.0f, 0.16f, 0.27f, .37f, 0.5f, 0.67f, 0.85f, 1.0f
    };
    public List<float> lodDistribution = new List<float>(8)
    {
        0.001f, 0.001f, 0.001f, 0.45f, 0.85f, 1.0f, 1.0f, 1.0f
    };
    
    [Header("Bezier Point References")]
    public GameObject p0;
    public GameObject p1;
    public GameObject p2;
    public GameObject p3;
    
    private Mesh grassBladeData;
    private ComputeBuffer bladeBuffer;
    private ComputeBuffer arcLengthTBuffer;
    private ComputeBuffer lodTBuffer;
    private RenderParams rp;
    private MaterialPropertyBlock mpb;
    private Matrix4x4[] instanceData;
    private Matrix4x4 t;
    private static readonly int BladeBuffer = Shader.PropertyToID("_BladeBuffer");
    private static readonly int Hash = Shader.PropertyToID("hash");
    private static readonly int ArcLengthTBuffer = Shader.PropertyToID("_ArcLengthTBuffer");
    private static readonly int DValue = Shader.PropertyToID("LoDValue");
    private static readonly int LodTBuffer = Shader.PropertyToID("_LodTBuffer");

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitializeData();
    }

    private void InitializeData()
    {
        t = Matrix4x4.identity;
        instanceData = new[] {t};
        bladeBuffer?.Release();
        bladeBuffer = new ComputeBuffer(1, sizeof(float) * 9);
        arcLengthTBuffer?.Release();
        arcLengthTBuffer = new ComputeBuffer(8, sizeof(float));
        lodTBuffer?.Release();
        lodTBuffer = new ComputeBuffer(8, sizeof(float));
        mpb = new MaterialPropertyBlock();
        rp = new RenderParams(bladeMaterial);
        /*{
            receiveShadows = true,
            shadowCastingMode = ShadowCastingMode.On
        };*/

        grassBladeData = new Mesh
        {
            vertices = new Vector3[15],
            bounds = new Bounds(new Vector3(0, height - height / 4, 0), new Vector3(2, height*1.5f, 2)),
        };
        
        grassBladeData.SetIndices(new []
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
        }, MeshTopology.Triangles, 0);
    }

    private void OnValidate()
    {
        if (grassBladeData != null)
        {
            // Bounds needs to be centered on the blade and fit both it's height and width, otherwise it will cull early
            grassBladeData.bounds = new Bounds(new Vector3(0, height - height / 4, 0), new Vector3(2, height*1.5f, 2));
            UpdatePoints();
        }
    }

    private void OnApplicationQuit()
    {
        bladeBuffer?.Release();
        arcLengthTBuffer?.Release();
        lodTBuffer?.Release();
    }

    // Only needed because ExecuteInEditMode is used, so buffers need to be cleaned while quitting unity and entering playmode
    private void OnDisable()
    {
        bladeBuffer?.Release();
        arcLengthTBuffer?.Release();
        lodTBuffer?.Release();
    }

    // Update is called once per frame
    void Update()
    {
        if (bladeBuffer == null) InitializeData();
        
        // This classes object is being used as position, as if it were a seed. We want the grass centered in the scene for testing
        TestGrassBlade blade = new TestGrassBlade()
        {
            position = float3.zero,
            width = this.width,
            height = this.height,
            facing = this.facing,
            tilt = this.tilt,
            bend = this.bend,
        };
        
        bladeBuffer!.SetData(new [] {blade});
        arcLengthTBuffer.SetData(distribution);
        lodTBuffer.SetData(lodDistribution);
        mpb.SetBuffer(BladeBuffer, bladeBuffer);
        mpb.SetBuffer(ArcLengthTBuffer, arcLengthTBuffer);
        mpb.SetBuffer(LodTBuffer, lodTBuffer);
        mpb.SetFloat(DValue, LODValue);
        rp.matProps = mpb;
        
        Graphics.RenderMeshInstanced(rp, grassBladeData, 0, instanceData);
    }

    
    Vector2 rand2(Vector2 p) {
        return frac(sin(float2(dot(p, float2(127.1f, 311.7f)), dot(p, float2(269.5f, 183.3f)))) * 43758.5453f);
    }
    
    [ContextMenu("Reset Points based on Position")]
    public void ResetPoints()
    {
        p0.transform.position = Vector2.zero;
        
        // randomness based off position of this classes object so that I can randomize while still centering the blade
        Vector2 bladeHash2D = rand2(new Vector2(transform.position.x, transform.position.z));
        bend = bladeHash2D.x < 0.05f ? 0.0f : .1f;
        tilt = bladeHash2D.y;
        facing = normalize(bladeHash2D * 2.0f - Vector2.one); // Random values between 0 and 1

        // Endpoint is based on height and tilt
        p3.transform.position = p0.transform.position + new Vector3(facing.x, 0, facing.y) * tilt + Vector3.up * height;
        
        // Above the starting point. How long until bending starts a lot more
        p1.transform.position = p0.transform.position + Vector3.up * (height * bend);

        Vector3 midPoint = 0.5f * (p3.transform.position - p0.transform.position);
        Vector3 widthDir = new Vector3(facing.y, 0, -facing.x);
        Vector3 bladeDir = normalize(p3.transform.position - p0.transform.position);
        Vector3 awayDir = cross(-widthDir, bladeDir);

        p2.transform.position = (p0.transform.position + midPoint) + awayDir * bend;
    }

    // Doesn't move bend, tilt, height, facing
    private void UpdatePoints()
    {
        facing = normalize(facing);
        p0.transform.position = Vector2.zero;

        // Endpoint is based on height and tilt
        p3.transform.position = p0.transform.position + new Vector3(facing.x, 0, facing.y) * tilt + Vector3.up * height;
        
        // Above the starting point. How long until bending starts a lot more
        p1.transform.position = p0.transform.position + Vector3.up * (height * bend);

        Vector3 midPoint = 0.5f * (p3.transform.position - p0.transform.position);
        Vector3 widthDir = new Vector3(facing.y, 0, -facing.x);
        Vector3 bladeDir = normalize(p3.transform.position - p0.transform.position);
        Vector3 awayDir = cross(-widthDir, bladeDir);

        p2.transform.position = (p0.transform.position + midPoint) + awayDir * bend;
    }

    /*private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawCube(new Vector3(0, height - height / 4, 0), new Vector3(2, height*1.5f, 2));
    }*/
}
