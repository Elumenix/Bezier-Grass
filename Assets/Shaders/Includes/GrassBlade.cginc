#ifndef GRASSBLADE
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define GRASSBLADE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "PerlinNoise.hlsl"

struct GrassBlade
{
    float3 position;
    float hash;
    float2 dimensions;
    float3 widthDir;
    float3 terrainNormal;
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
    float3 terrainNormalWS : TEXCOORD3;
};

half4 _Color;
half _Glossiness;
half _Metallic;
half _Occlusion;
half _ViewAdj;
half _WindDirection;
half _WindScale;
half _WindPower;
half _WindSpeed;
float2 _LodRange;
float _AdjustmentThreshold;
float _AdjustmentStrength;
float _NormalCurvature;
float _TerrainNormalAdjustment;

// Helper function
// If you're wondering why the name of this is different here than in the compute shader and C# version, it's because
// the name was silently conflicting with a background function and crashing the program
float2 RotateVec2D(float2 v, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return float2(
        v.x * c - v.y * s,
        v.x * s + v.y * c
    );
}

/// Essentially copying part of the compute shader code to make a grassblade. This should only be used for testing
GrassBlade CreateGrassBlade(TestGrassBlade blade)
{
    GrassBlade newBlade;

    newBlade.position = blade.position;
    newBlade.dimensions = float2(blade.width, blade.tilt);
    newBlade.hash = .34982983f; // Doesn't matter in testing really
    newBlade.terrainNormal = float3(0,0,0); // Doesn't matter in testing

    
    float3 up = float3(0,1,0);
    float3 widthDir = float3(blade.facing.y, 0, -blade.facing.x); // Orthogonal normal to facing
    float3 forwardDir = float3(blade.facing.x, 0, blade.facing.y); 
    
    float3 p0 = float3(0,0,0); // Root of the grass blade in object space.

    float radius = blade.height / blade.bend;
    float k = (4.0f / 3.0f) * tan(blade.bend / 4.0f);

    float2 P3_2D = float2(radius * sin(blade.bend), radius * (1.0f - cos(blade.bend)));
    float2 P1_2D = k * radius * float2(1.0f, 0);
    float2 P2_2D = P3_2D - k * radius * float2(cos(blade.bend), sin(blade.bend));

    P1_2D = RotateVec2D(P1_2D, blade.tilt);
    P2_2D = RotateVec2D(P2_2D, blade.tilt);
    P3_2D = RotateVec2D(P3_2D, blade.tilt);
        
    float3 p1 = P1_2D.x * forwardDir + P1_2D.y * up;
    float3 p2 = P2_2D.x * forwardDir + P2_2D.y * up;
    float3 p3 = P3_2D.x * forwardDir + P3_2D.y * up;
    
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

    // Not normalized because it simplifies the math in CalculateWindDisplacement
    // If not calling that function, tangentVec should be immediately normalized before use
    tangentVec = derivative;
}

// This is Deprecated currently and is now happening in the compute shader, where direct values of the grass shape
// are altered rather than the vertices. This saves us from some weird looking blades and also makes it so that
// we don't need to recalculate an estimate for the tangent.
void CalculateWindDisplacement(GrassBlade blade, float t, in out float3 pos, in out float3 tangentVec)
{
    // PART 1
    // Calculate wind and offset the vertices of the blade by it to create a natural wind pattern across the map
    
    // Per-blade random value (consistent for each blade based on its position)
    float bladeRandom = frac(sin(dot(blade.position.xz, float2(12.9898, 78.233))) * 43758.5453);
    
    // Two noises at different speeds and weights so that there isn't an obvious pattern
    // Second noise also accounts for some per-blade randomness, so all nearby blades don't act exactly identical
    float perlinValue = perlin(blade.position.xz * _WindScale + _WindSpeed * _Time.y);
    float perlinValue2 = perlin((blade.position.xz + float2(134.26, -1035.98) + bladeRandom * 50.0) * _WindScale + _WindSpeed * _Time.y * .5);

    // Wind should only have strength if it is actually moving
    float windStrength = (perlinValue * .75 + perlinValue2 * .25) * _WindPower * _WindSpeed;
    windStrength *= lerp(0.70, 1.5, bladeRandom); // Each blade 70% to 150% responsive

    // TODO: Should add option to change wind direction
    float3 windDir = normalize(float3(-1.3,0,2.4));

    float heightFactor = t * t; // falloff so that tip reacts more
    float bendAmount = windStrength * heightFactor;

    // Create a bending offset that moves in the wind direction but accounts for blade orientation
    // Project wind direction onto the plane perpendicular to the tangent
    float3 horizontalWind = windDir - dot(windDir, tangentVec) * tangentVec;
    horizontalWind = normalize(horizontalWind);

    // Calculate the bend displacement
    // This will cause some major stretching under the map at high wind values but those are never visible
    float3 bendOffset = horizontalWind * bendAmount;
    bendOffset.y -= bendAmount * bendAmount * 0.5;
    pos += bendOffset;

    // PART 2
    // Recalculate the tangent vector as it was changed along with the blades shape 
    
    // Calculate how wind changes with height (derivative of bendOffset with respect to t)
    float dHeightFactor = 2 * t; // derivative of t^2
    float dBendAmount = windStrength * dHeightFactor;

    // Wind contribution to the derivative
    float3 windTangentContribution = horizontalWind * dBendAmount;
    windTangentContribution.y -= bendAmount * t; // derivative of the vertical component

    // Add wind effect to the tangent. Sum of the derivatives is still the derivative.
    tangentVec = tangentVec + windTangentContribution;
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
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    float facing = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0); // Allows reversing normals of back faces

    // normalDirWS is the normals for individual blades, which causes a lot of aliasing and not a lot of light-based
    // terrain normal is better used for the PBR for a cleaner grass simulation
    half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    float3 normalDirWS = normalize(input.normalWS * facing); // Normals reversed to differentiate front and back
    float3 terrainNormalWS = normalize(input.terrainNormalWS); // Terrain Normal use looks better when blades are more uniform
    float3 finalNormal = normalize((1 -_TerrainNormalAdjustment) * normalDirWS + terrainNormalWS * _TerrainNormalAdjustment);


    // For bakedGI specifically, the terrain normal is going to be used for all back faces. PBR has a bit of trouble with
    // backface lighting, and it looks better to use the more consistent value for it in both cases
    float3 ambientNormal = IS_FRONT_VFACE(isFrontFace, finalNormal, terrainNormalWS);
    IS_FRONT_VFACE(isFrontFace, finalNormal, terrainNormalWS);
    
    
    // Set data needed to calculate lighting
    InputData lightData = (InputData)0;
    lightData.positionWS = input.positionWS;
    lightData.normalWS = finalNormal;
    lightData.viewDirectionWS = viewDirWS;
    lightData.shadowCoord = shadowCoord;
    lightData.bakedGI = SampleSH(ambientNormal);
    lightData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    lightData.vertexLighting = VertexLighting(input.positionWS, finalNormal);
                
    // Surface data is for additional data from textures. 
    SurfaceData surfaceData;
    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.albedo = _Color.rgb * lerp(.7f, 1.0f, input.uv.y); // Top is lighter than the bottom
    surfaceData.alpha = 1.0;
    surfaceData.smoothness = _Glossiness * lerp(.55f, 1.0f, input.uv.y);
    surfaceData.metallic = _Metallic;
    surfaceData.occlusion = _Occlusion * lerp(.5f, 1.0f, input.uv.y);
                
    // Apply PBR Lighting
    half4 pbr = UniversalFragmentPBR(lightData, surfaceData);
    return pbr;
}

#endif