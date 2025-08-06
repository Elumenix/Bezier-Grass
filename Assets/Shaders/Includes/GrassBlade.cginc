#ifndef GRASSBLADE
#define GRASSBLADE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

struct GrassBlade
{
    float3 position;
    float3 nearestClumpPosition;
    float width;
    float3 widthDir;
    float4x3 coefficients;
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

half4 _Color;
half _Glossiness;
half _Specular;
half _Occlusion;
half windStrength;
half swaying;
float2 _LodRange;

void CalculateBezierCurve(GrassBlade blade, float t, out float3 pos, out float3 normalOS)
{
    // PART 1:
    // Get the position of the point along the curve. The exact position of the control points for this blade of grass
    // have already been computed in the compute shader and converted to coefficients. This was done so that every grass
    // blade only needs to do this operation 1 time per chunk update, rather than for every vertex of the blade every frame.
    float3 c0 = blade.coefficients[0];
    float3 c1 = blade.coefficients[1];
    float3 c2 = blade.coefficients[2];
    float3 c3 = blade.coefficients[3];
    
    // Get the correct point along the Bézier curve. Using mad operations as an optimization
    // float3 pos = c3t^3 + c2t^2 + c1t + c0
    // float3 pos = ((c3 * t + c2) * t + c1) * t + c0;
    pos = mad(mad(mad(c3, t, c2), t, c1), t, c0);


    // PART 2:
    // Now we need to get the derivative of the cubic Bézier curve in order to get the proper normal for the blade of grass.
    // The derivative of a cubic Bézier curve is a quadratic Bézier curve, which we can easily calculate and simplify for.
    // Most of this section is me showing how the math simplifies from control points all the way down to derivative coefficients.

    // (A)
    // This is a representation of the control points of the quadratic Bézier curve derived from the cubic control points.
    // d0 = 3(p1 - p0);
    // d1 = 3(p2 - p1);
    // d2 = 3(p3 - p2);

    // (B)
    // Simplifying to derivative control points by substituting (A) with the cubic coefficients.
    // d0 = c1;
    // d1 = c1+c2;
    // d2 = 3c3 + 2c2 + c1

    // (C)
    // Coefficients for the quadratic Bézier curve made by directly breaking (A) into its components.
    // dc0 = d0;            // Offset
    // dc1 = 2(d1-d0);      // t
    // dc2 = d0 - 2d1 + d2; // t^2

    // (D)
    // Coefficients for the quadratic Bézier curve substituting the equations in (C) with the values from (B).
    float3 dc0 = c1;     // Offset
    float3 dc1 = 2 * c2; // t
    float3 dc2 = 3 * c3; // t^2

    // Similar mad operations are used again for the derivative point as an optimization
    float3 derivative = mad(mad(dc2, t, dc1), t, dc0);
    float3 tangentVec = normalize(derivative);
    normalOS = cross(tangentVec, blade.widthDir);
}

half4 CalculateGrassLighting(Varyings input)
{
    // Setting up some data ahead of time
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
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
    return UniversalFragmentPBR(lightData, surfaceData);
}

#endif