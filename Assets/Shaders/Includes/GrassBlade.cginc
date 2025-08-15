#ifndef GRASSBLADE
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define GRASSBLADE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct GrassBlade
{
    float3 position;
    float3 nearestClumpPosition;
    float2 dimensions;
    float3 widthDir;
    float4x3 coefficients;
};

struct TestGrassBlade
{
    float3 position;
    float width;
    float height;
    float2 facing;
    float tilt;
    float bend;
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
half _Metallic;
half _Occlusion;
half _ViewAdj;
half windStrength;
half swaying;
float2 _LodRange;
float _AdjustmentThreshold;
float _AdjustmentStrength;
float _NormalCurvature;
float _Translucency;

/// Essentially copying part of the compute shader code to make a grassblade. This should only be used for testing
GrassBlade CreateGrassBlade(TestGrassBlade blade)
{
    GrassBlade newBlade;

    newBlade.nearestClumpPosition = float3(0,0,0);
    newBlade.position = blade.position;
    newBlade.dimensions = float2(blade.width, blade.tilt);

    
    float3 up = float3(0,1,0);
    float3 widthDir = float3(blade.facing.y, 0, -blade.facing.x); // Orthogonal normal to facing
    
    float3 p0 = float3(0,0,0); // Root of the grass blade in object space.
    float3 p1 = p0 + up * (blade.height * blade.bend); // Above the starting point. Controls how rigid the base is.
    float3 p3 = p0 + float3(blade.facing.x, 0, blade.facing.y) * blade.tilt + up * blade.height; // Endpoint is based on height and tilt.

    float3 diff = p3 - p0;
    float3 midPoint = 0.5f * diff;
    float3 bladeDir = normalize(diff);
    float3 awayDir = normalize(cross(-widthDir, bladeDir));

    // Towards the middle and away from the blade. Predominantly controls how bent the blade is
    float3 p2 = p0 + midPoint + awayDir * blade.bend;
    
    float3 c0 = p0;                        // Offset
    float3 c1 = 3 * (p1 - p0);             // t
    float3 c2 = 3 * (p0 - 2 * p1 + p2);    // t^2
    float3 c3 = p3 - 3 * p2 + 3 * p1 - p0; // t^3

    newBlade.coefficients = float4x3(c0, c1, c2, c3);
    newBlade.widthDir = widthDir;
    return newBlade;
}

void CalculateBezierCurve(GrassBlade blade, float t, out float3 pos, out float3 tangentVec)
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
    tangentVec = normalize(derivative);
}

float3 ViewSpaceAdjustment(GrassBlade blade, float3 pos, float3 tangentVec)
{
    // Direction the camera is pointing
    float3 cameraDir = normalize(_WorldSpaceCameraPos - (pos + blade.position));
            
    // Calculate blade facing (0 = Edge towards camera (Invisible), 1 = Exactly facing camera)
    float edgeAngle = 1.0 - abs(dot(cameraDir, blade.widthDir));

    // Linear blend from adjustmentThreshold to 1.0
    float adjustAmount = saturate((_AdjustmentThreshold - edgeAngle) / _AdjustmentThreshold);

    // Determines the percentage that the blade will be rotated from its base rotation to the camera
    adjustAmount *= _AdjustmentStrength;
            
    // Project the faces to point towards the camera 
    float3 cameraFacingWidthDir = normalize(cross(cameraDir, tangentVec));

    // Interpolate between innate facing and camera facing based on the adjustAmount
    return lerp(blade.widthDir, cameraFacingWidthDir, adjustAmount);
}

half4 CalculateGrassLighting(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC)
{
    // Setting up some data ahead of time
    float facing = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    float3 normalDirWS = normalize(input.normalWS) * facing;
    Light light = GetMainLight();
    
    // Set data needed to calculate lighting
    InputData lightData = (InputData)0;
    lightData.positionWS = input.positionWS;
    lightData.normalWS = normalDirWS;
    lightData.viewDirectionWS = viewDirWS;
    lightData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    lightData.bakedGI = SampleSH(lightData.normalWS);
    lightData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    lightData.vertexLighting = VertexLighting(input.positionWS, normalDirWS);
                
    // Surface data is for additional data from textures. 
    SurfaceData surfaceData;
    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.albedo = _Color.rgb * lerp(.7f, 1.0f, input.uv.y); // Top is lighter than the bottom
    surfaceData.alpha = 1.0;
    surfaceData.smoothness = _Glossiness * lerp(.55f, 1.0f, input.uv.y);
    surfaceData.metallic = _Metallic;
    surfaceData.occlusion = _Occlusion * lerp(.3f, 1.0f, input.uv.y);
                
    // Apply PBR Lighting
    half4 pbr = UniversalFragmentPBR(lightData, surfaceData);
    
    // Subsurface scattering on back faces, so that the back of grass doesn't look as abnormally dark
    half backLight = saturate(dot(-normalDirWS, light.direction));
    half3 translucentLight = backLight * _Translucency * _Color.rgb;
    half3 subSurfaceLight = translucentLight * light.shadowAttenuation * light.distanceAttenuation * light.color;
    return pbr + half4(subSurfaceLight, 1);
}

#endif