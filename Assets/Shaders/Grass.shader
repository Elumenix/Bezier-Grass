Shader "Custom/Grass"
{
    Properties
    {
        [Toggle] swaying ("Sway Blade", Float) = 0
        windStrength ("Wind Strength", Range(0.0, 5.0)) = 0.5
    }
    SubShader
    {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        
        // Data accessible in all passes
        HLSLINCLUDE
        #include "Includes/GrassBlade.cginc"

        // Fastest possible - no CPU-GPU transfer at all
        static const float arcTBuffer[8] = { 0.001f, 0.33f, 0.49f, 0.62f, 0.73f, 0.83f, 0.92f, 1.0f };
        StructuredBuffer<GrassBlade> grassBlades;

        // Hash will not be a part of the final implementation. Because we're testing control points on the cpu here,
        // We need something that will maintain precision between C# and HLSL, so we need to do this specifically here
        float windStrength;
        float swaying;
        
        ENDHLSL


        Pass
        {
            Name "ForwardPass"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            
            Cull Off // disable back-face culling
            HLSLPROGRAM
            #define _SPECULAR_COLOR
            #pragma vertex Vertex
            #pragma fragment Fragment
            
            #pragma multi_compile_instancing
            #pragma shader_feature _FORWARD_PLUS
            #pragma shader_feature_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma shader_feature_fragment _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                //float4 positionOS : POSITION;
                uint vertexID : SV_VertexID;
                //float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS :TEXCOORD0;
                uint vertexID : TEXCOORD1;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD2;
            };
            
            
            void Vertex(Attributes input, uint instanceID : SV_InstanceID, out Varyings o)
            {
                GrassBlade blade = grassBlades[instanceID];
                float3 up = float3(0,1,0);

                // STEP 1: Get the four control points of the cubic bezier curve
                
                // Root of the grass blade in object space
                float3 p0 = float3(0,0,0);

                float2 facing2D = blade.facing;
                float3 facing = float3(facing2D.x, 0, facing2D.y);
                float3 widthDir = float3(facing2D.y, 0, -facing2D.x); // Orthogonal normal to facing


                // Endpoint is based on height and tilt
                float3 p3 = p0 + facing * blade.tilt + up * blade.height;
                
                // Above the starting point. How long until bending starts a lot more
                float3 p1 = p0 + up * (blade.height * blade.bend);

                float3 diff = p3 - p0;
                float3 midPoint = 0.5f * diff;
                float3 bladeDir = normalize(diff);
                
                float3 awayDir = cross(-widthDir, bladeDir);

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


                // STEP 3: Get the correct position for each vertex on the bezier curve
                
                // compute coefficients for a more efficient bezier calculation
                float3 c0 = p0;                        // Offset
                float3 c1 = 3 * (p1 - p0);             // t
                float3 c2 = 3 * (p0 - 2 * p1 + p2);    // t^2
                float3 c3 = p3 - 3 * p2 + 3 * p1 - p0; // t^3

                // We're precomputing ArcT instead of doing an expensive arc length perameterization calculation in the vertex shader
                // There's no closed form solution for arc length parameterization for cubic beziers, so this is much easier
                uint vertex = input.vertexID;
                float t = arcTBuffer[vertex / 2];

                

                // Get the correct point along the bezier curve
                // float3 pos = c3t^3 + c2t^2 + c1t + c0
                // float3 pos = ((c3 * t + c2) * t + c1) * t + c0;
                float3 pos = mad(mad(mad(c3, t, c2), t, c1), t, c0);
                //if (vertex == 4) pos = float3(0,arcTBuffer[5],0);


                // STEP 4: Get the derivative of the cubic bezier curve in order to find the normals
                // The derivative of a cubic bezier curve is a quadratic bezier curve

                // Quadratic bezier curve control points
                float3 d0 = 3 * (p1 - p0);
                float3 d1 = 3 * (p2 - p1);
                float3 d2 = 3 * (p3 - p2);

                // Coefficients
                float3 cd0 = d0;                 // Offset
                float3 cd1 = 2 * (d1-d0);        // t
                float3 cd2 = d0 - (2 * d1) + d2; // t^2

                float3 derivative = mad(mad(cd2, t, cd1), t, cd0);
                float3 tangentVec = normalize(derivative);
                float3 normalOS = cross(tangentVec, widthDir);
                
                // Blade will get skinnier the further up it goes, with point 14 (the last one) being along the center
                float sideOffset = blade.width - ((blade.width / 7.0) * (vertex / 2));
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
                pos += widthDir * sideOffset * odd;

                float3 positionWS = TransformObjectToWorld(pos) + blade.position;
                
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorld(normalOS);
                o.vertexID = vertex;
                o.uv = float2(vertex == 14 ? .5 : vertex % 2, t);
            }

            float4 Fragment(Varyings input) : SV_Target 
            {
                // Set data needed to calculate lighting
                InputData lightData = (InputData)0;
                lightData.positionWS = input.positionWS;
                lightData.normalWS = normalize(input.normalWS);
                lightData.viewDirectionWS = GetWorldSpaceViewDir(input.positionWS);
                lightData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                
                // Surface data is for additional data from textures. 
                SurfaceData surfaceData;
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                surfaceData.albedo = float3(0.2,0.8,0.2); // Green
                surfaceData.alpha = 1.0;
                surfaceData.smoothness = 0.5;
                surfaceData.specular = 0;

                float t = input.vertexID / 15.0;
                return float4(t,0,0, 1);

                if (input.vertexID == 1) return float4(1,1,1,1);
                else return float4(0,1,0,1);
                
                float3 color = UniversalFragmentBlinnPhong(lightData, surfaceData); //+ unity_AmbientSky;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}