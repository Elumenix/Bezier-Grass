using Unity.Mathematics;
using UnityEngine;


struct GrassBlade
{
    public float3 position;
    public float width;
    public float height;
    public float tilt;
    public float bend;
    public float3 nearestClumpPosition;
};

public class GrassBladeTest : MonoBehaviour
{
    public Material bladeMaterial;
    public float width = 1;
    public float height = 3;
    public float tilt = 3;
    public float bend = 1;
    
    private Mesh grassBladeData;
    private ComputeBuffer bladeBuffer;
    private RenderParams rp;
    private MaterialPropertyBlock mpb;
    private Matrix4x4[] instanceData;
    private Matrix4x4 t;
    private static readonly int BladeBuffer = Shader.PropertyToID("_BladeBuffer");

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        t = Matrix4x4.identity;
        instanceData = new[] {t};
        bladeBuffer = new ComputeBuffer(1, sizeof(float) * 10);
        mpb = new MaterialPropertyBlock();
        rp = new RenderParams(bladeMaterial);
        
        grassBladeData = new Mesh
        {
            vertices = new Vector3[15],
            bounds = new Bounds(new Vector3(0, height * 3.5f, 0), new Vector3(width, height * 7, width))
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
            grassBladeData.bounds = new Bounds(new Vector3(0, height * 3.5f, 0), new Vector3(width, height * 7, width));
        }
    }

    private void OnApplicationQuit()
    {
        bladeBuffer?.Release();
    }

    // Update is called once per frame
    void Update()
    {
        GrassBlade blade = new GrassBlade()
        {
            position = float3.zero,
            width = this.width,
            height = this.height,
            tilt = this.tilt,
            bend = this.bend,
            nearestClumpPosition = float3.zero
        };
        bladeBuffer.SetData(new [] {blade});
        mpb.SetBuffer(BladeBuffer, bladeBuffer);
        rp.matProps = mpb;
        
        Graphics.RenderMeshInstanced(rp, grassBladeData, 0, instanceData);
    }

    /*private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawCube(new Vector3(0, height * 3.5f, 0), new Vector3(width, height * 7, width));
    }*/
}
