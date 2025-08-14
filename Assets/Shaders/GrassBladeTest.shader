Shader "Custom/GrassBladeTest"
{
    Properties
    {
        [Header(Property Settings)]
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1.0)) = 0.5
        _Metallic ("Metallic", Range(0,1.0)) = 0.0
        _Occlusion ("Occlusion", Range(0,1.0)) = 1.0
        _Translucency ("Translucency", Range(0.0, 1.0)) = 0.3
        _NormalCurvature ("Normal Curve", Range(0.0, 1.0)) = 0.5
        
        [Header(Camera View Space Projection Settings)]
        _AdjustmentThreshold ("Adjustment Threshold", Range(0.0,1.0)) = 0.25
        _AdjustmentStrength ("Adjustment Strength", Range(0.0,1.0)) = 0.75
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
        StructuredBuffer<TestGrassBlade> _BladeBuffer;
        StructuredBuffer<float> _ArcLengthTBuffer;
        StructuredBuffer<float> _LodTBuffer;

        // Hash will not be a part of the final implementation. Because we're testing control points on the cpu here,
        // We need something that will maintain precision between C# and HLSL, so we need to do this specifically here
        float LoDValue;
        
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
            #pragma target 5.0;
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma shader_feature _FORWARD_PLUS
            #pragma shader_feature_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma shader_feature_fragment _ADDITIONAL_LIGHT_SHADOWS
            

            float2 rand2(float2 p) {
                return frac(sin(float2(dot(p, float2(127.1f, 311.7f)), dot(p, float2(269.5f, 183.3f)))) * 43758.5453f);
            }

            /// NOTE: THIS IS NOT AN EFFICIENT SHADER, IT'S MEANT TO RENDER A SINGLE GRASS BLADE WITHOUT THE SUPPORT
            /// OFF THE COMPUTE SHADER THAT MAKES ALL OF THIS MUCH MORE EFFICIENT. THIS IS MAINLY COPYING THE PIPELINE
            /// FROM THERE SO THAT I CAN DIRECTLY TEST OUT SHAPE CHANGES HERE. DO NOT COPY THIS CODE FOR ELSEWHERE.
            void Vertex(Attributes input, out Varyings o)
            {
                TestGrassBlade testBlade = _BladeBuffer[0];
                GrassBlade blade = CreateGrassBlade(testBlade);
                
                // We're precomputing ArcT instead of doing an expensive arc length perameterization calculation in the vertex shader
                // There's no closed form solution for arc length parameterization for cubic beziers, so this is much easier
                uint vertex = input.vertexID;
                float t = _ArcLengthTBuffer[vertex / 2];
                t = lerp(t, _LodTBuffer[vertex / 2], LoDValue);
            
                
                float3 pos;
                float3 tangentVec;
                CalculateBezierCurve(blade, t, pos, tangentVec);
            
                // Blade will get skinnier the further up it goes, with the last one being along the center
                float sideOffset = blade.width - (blade.width * t * t);
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
            
                float3 widthDir = ViewSpaceAdjustment(blade, pos, tangentVec);
                pos += widthDir * sideOffset * odd;

                // Normals are rounded so that the blades don't look as flat and reflect light better
                float3 GeometricNormalOS = cross(tangentVec, widthDir);
                float normalizedWidth = sideOffset / blade.width;
                float3 roundingOffset = widthDir * odd * normalizedWidth * _NormalCurvature;
                float3 roundedNormal = normalize(GeometricNormalOS + roundingOffset);


                // Passing data to the fragment shader
                float3 positionWS = TransformObjectToWorld(pos) + blade.position;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = normalize(TransformObjectToWorldNormal(roundedNormal));
                o.vertexID = vertex;
                o.uv = float2(vertex == 14 ? .5 : vertex % 2, t);
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target 
            {

                half4 pbr = CalculateGrassLighting(input, isFrontFace);
                return pbr;
                // Set data needed to calculate lighting
                /*InputData lightData = (InputData)0;
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
                
                float3 color = UniversalFragmentBlinnPhong(lightData, surfaceData); //+ unity_AmbientSky;
                return float4(color, 1.0);*/
            }
            ENDHLSL
        }

        /*
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore

            // Universal Pipeline keywords
            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex1 // Modified version of the one in the include package
            #pragma fragment ShadowPassFragment // From the include package

            // Most of this Shadow pass is overriding or using methods from these includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            struct Att
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(Att input, float3 positionOS)
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }
            
            Varyings ShadowPassVertex1(Att input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                GrassBlade blade = _BladeBuffer[0];
                float3 up = float3(0,1,0);
                float3 p0 = float3(0,0,0);
                float2 facing2D = normalize(hash * 2.0f - float2(1,1)); // Random values between 0 and 1
                float3 facing = float3(facing2D.x, 0, facing2D.y);
                float3 widthDir = float3(facing2D.y, 0, -facing2D.x); // Orthogonal normal to facing
                float3 p3 = p0 + facing * blade.tilt + up * blade.height;
                float3 p1 = p0 + up * (blade.height * blade.bend);
                float3 diff = p3 - p0;
                float3 midPoint = 0.5f * diff;
                float3 bladeDir = normalize(diff);
                float3 awayDir = cross(-widthDir, bladeDir);
                float3 p2 = (p0 + midPoint) + awayDir * blade.bend;
                
                if (swaying == 1.0f) {
                    float phaseOffset = hash.x * 1.57;
                    float rawSpeedMult = 2.0 * (hash.x + 1.0) * (windStrength + 1.0);
                    float speedMult = round(rawSpeedMult); // Force to integer multiples, confirms loop
                    float maxAmplitude = 0.01 * (windStrength + 3.0);
                    float timeMod = _Time[1] % (2.0 * acos(-1.0));
                    p3 = p3 + sin(timeMod*speedMult + phaseOffset) * maxAmplitude * awayDir;
                    p2 = p2 + sin(timeMod*speedMult + 1.57 + phaseOffset) * maxAmplitude / 2.0 * awayDir;
                }

                
                float3 c0 = p0;                        // Offset
                float3 c1 = 3 * (p1 - p0);             // t
                float3 c2 = 3 * (p0 - 2 * p1 + p2);    // t^2
                float3 c3 = p3 - 3 * p2 + 3 * p1 - p0; // t^3
                uint vertex = input.vertexID;
                float t = _ArcLengthTBuffer[vertex / 2];

                // Get the correct point along the bezier curve
                // float3 pos = c3t^3 + c2t^2 + c1t + c0
                // float3 pos = ((c3 * t + c2) * t + c1) * t + c0;
                float3 pos = mad(mad(mad(c3, t, c2), t, c1), t, c0);
                
                
                // Blade will get skinnier the further up it goes, with point 14 (the last one) being along the center
                float sideOffset = blade.width - ((blade.width / 7.0) * (vertex / 2));
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
                pos += widthDir * sideOffset * odd;
                

                #if defined(_ALPHATEST_ON)
                output.uv = TRANSFORM_TEX(float2(vertex == 14 ? .5 : vertex % 2, t), _BaseMap);
                #endif

                output.positionCS = GetShadowPositionHClip(input, pos);
                output.positionCS = TransformObjectToHClip(pos);
                return output;
            }
            
            ENDHLSL
        }*/
    }
}
