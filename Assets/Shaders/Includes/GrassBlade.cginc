#ifndef GRASSBLADE
#define GRASSBLADE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct GrassBlade
{
    float3 position;
    float width;
    float height;
    float2 facing;
    float tilt;
    float bend;
    float3 nearestClumpPosition;
};

struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS :TEXCOORD0;
    uint vertexID : TEXCOORD1;
    float3 normalWS : NORMAL;
    float2 uv : TEXCOORD2;
};

float4 _Color;
float _Glossiness;
float _Specular;
float _Occlusion;
float windStrength;
float swaying;
float2 _LodRange;

void CalculateBezierCurve(GrassBlade blade, float t, out float3 pos, out float3 normalOS, out float3 widthDir)
{
    // Get some blade data
    float3 up = float3(0,1,0);
    float2 facing2D = blade.facing;
    float3 facing = float3(facing2D.x, 0, facing2D.y);
    widthDir = float3(facing2D.y, 0, -facing2D.x); // Orthogonal normal to facing

    // STEP 1: Get the four control points of the cubic Bézier curve
    // Root of the grass blade in object space
    float3 p0 = float3(0,0,0);
    
    // Endpoint is based on height and tilt
    float3 p3 = p0 + facing * blade.tilt + up * blade.height;
    
    // Above the starting point. How long until bending starts a lot more
    float3 p1 = p0 + up * (blade.height * blade.bend);

    float3 diff = p3 - p0;
    float3 midPoint = 0.5f * diff;
    float3 bladeDir = normalize(diff);
    float3 awayDir = cross(-widthDir, bladeDir);

    // Towards the middle and away from the blade. Predominantly controls bend and shape of the blade
    float3 p2 = (p0 + midPoint) + awayDir * blade.bend;


    // STEP 2: Adjust the curve to simulate wind if appropriate
    // Perlin noise will be used instead of this, but this is a good test for grass flexibility
    if (swaying == 1.0f) {
        float phaseOffset = blade.height * 1.57;
        float rawSpeedMult = 2.0 * (blade.height + 1.0) * (windStrength + 1.0);
        float speedMult = round(rawSpeedMult); // Force to integer multiples, confirms loop
        float maxAmplitude = 0.01 * (windStrength + 3.0);
        float timeMod = _Time[1] % (2.0 * acos(-1.0));
        p3 = p3 + sin(timeMod*speedMult + phaseOffset) * maxAmplitude * awayDir;
        p2 = p2 + sin(timeMod*speedMult + 1.57 + phaseOffset) * maxAmplitude / 2.0 * awayDir;
    }


    // STEP 3: Get the correct position for each vertex on the Bézier curve
    // compute coefficients for a more efficient bezier calculation
    float3 c0 = p0;                        // Offset
    float3 c1 = 3 * (p1 - p0);             // t
    float3 c2 = 3 * (p0 - 2 * p1 + p2);    // t^2
    float3 c3 = p3 - 3 * p2 + 3 * p1 - p0; // t^3
    
    // Get the correct point along the Bézier curve
    // float3 pos = c3t^3 + c2t^2 + c1t + c0
    // float3 pos = ((c3 * t + c2) * t + c1) * t + c0;
    pos = mad(mad(mad(c3, t, c2), t, c1), t, c0);

    
    // STEP 4: Get the derivative of the cubic Bézier curve in order to find the normals
    // The derivative of a cubic Bézier curve is a quadratic Bézier curve
    // Quadratic Bézier curve control points
    float3 d0 = 3 * (p1 - p0);
    float3 d1 = 3 * (p2 - p1);
    float3 d2 = 3 * (p3 - p2);

    // Coefficients
    float3 cd0 = d0;                 // Offset
    float3 cd1 = 2 * (d1-d0);        // t
    float3 cd2 = d0 - (2 * d1) + d2; // t^2

    float3 derivative = mad(mad(cd2, t, cd1), t, cd0);
    float3 tangentVec = normalize(derivative);
    normalOS = cross(tangentVec, widthDir);
}

float4 CalculateGrassLighting(Varyings input)
{
    // Setting up some data ahead of time
    float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    float3 normalDirWS = normalize(input.normalWS);
                
    // Because we're doing cull off and making the mesh double-sided that way, we need to actually know whether
    // we're looking at the front or back of the mesh currently so that the same lighting isn't used for both sides
    float facing = dot(input.normalWS, viewDirWS);
    if (facing < 0)
    {
        normalDirWS = -normalDirWS;
    }
                
    // Set data needed to calculate lighting
    InputData lightData = (InputData)0;
    lightData.positionWS = input.positionWS;
    lightData.normalWS = normalDirWS;
    lightData.viewDirectionWS = viewDirWS;
    lightData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    lightData.bakedGI = SampleSH(lightData.normalWS); 
                
    // Surface data is for additional data from textures. 
    SurfaceData surfaceData;
    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.albedo = _Color.rgb;
    surfaceData.alpha = 1.0;
    surfaceData.smoothness = _Glossiness;
    surfaceData.specular = _Specular;
    surfaceData.occlusion = _Occlusion;
                
    // Apply PBR Lighting
    float4 pbr = UniversalFragmentPBR(lightData, surfaceData);
    return pbr;
}

#endif