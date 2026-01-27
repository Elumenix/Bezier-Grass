Shader "Custom/Grass"
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
        
        [Header(Blade Behavior)]
        _WindScale ("Wind Scale", Range(0.0, .25)) = .1
        _WindPower ("Wind Power", Range(0.0, 1.5)) = .75
        _WindSpeed ("Wind Speed", Range(0.0, 5.0)) = 1.5
        _LodRange ("LOD Range", Vector) = (200, 500, 0, 0)
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

        // All data for grass shape is stored on the cpu for efficiency rather than needing to be passed every frame
        // This also means that all instances of this shader share this data rather than every chunk/instance needing it's own version
        static const half arcTBuffer[8] = { 0.001f, 0.4f, 0.6f, 0.7f, 0.8f, 0.88f, 0.95f, 1.0f };
        static const half lodTBuffer[8] = { 0.001f, 0.001f, 0.001f, 0.55f, 0.8f, 1.0f, 1.0f, 1.0f };
        static const half arcLODBuffer[4] = { 0.001f, 0.5f, 0.8f, 1.0f };
        StructuredBuffer<GrassBlade> grassBlades; // From the compute shader
        
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

            CBUFFER_START(UnityPerMaterial)
                float _GrassLodMode;
            CBUFFER_END



            void Vertex(Attributes input, uint instanceID : SV_InstanceID, out Varyings o)
            {
                GrassBlade blade = grassBlades[instanceID];
                
                uint vertex = input.vertexID;
                uint pair = vertex / 2;
                half t = 0;

                // This "Branch" uses a CBuffer that will be constant for every instance this draw call
                // As all blades will follow the same branch, there is expected to be near 0 performance impact from this "Branch"
                if (_GrassLodMode > 0.5) // Should be 1 if true, 0 if false
                {
                    // We want to switch to a low detail grass blade if far away from the mesh. To make this seamless, we're
                    // stretching vertices towards the end of the grass blade so that it fades to a low detail mesh instead of instantly changing
                    float distanceToCamera = distance(blade.position, _WorldSpaceCameraPos);
                    float lodValue = saturate((distanceToCamera - _LodRange.x) / (_LodRange.y - _LodRange.x));

                    // We're precomputing ArcT instead of doing an expensive arc length parameterization calculation in the vertex shader
                    // There's no closed form solution for arc length parameterization for cubic bezier curves, so this is much easier
                    t = lerp(arcTBuffer[pair], lodTBuffer[pair], lodValue);
                    o.uv = float2(vertex == 14 ? .5 : vertex % 2, pair / 7.0f);
                }
                else
                {
                    // If low lod, we don't need to worry about transitioning lods at all.
                    t = arcLODBuffer[pair];
                    o.uv = float2(vertex == 6 ? .5 : vertex % 2, t);
                }

                float3 pos;
                float3 tangentVec;
                CalculateBezierCurve(blade, t, pos, tangentVec);
                //CalculateWindDisplacement(blade, t, pos, tangentVec);
                tangentVec = normalize(tangentVec); // Was unnormalized to this point because it simplified math in wind displacement
            
                // Blade will get skinnier the further up it goes, with the last one being along the center
                float sideOffset = blade.dimensions.x - (blade.dimensions.x * t * t);
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
            
                float3 widthDir = ViewSpaceAdjustment(blade, pos, tangentVec);
                pos += widthDir * sideOffset * odd;

                // Normals are rounded so that the blades don't look as flat and reflect light better
                float3 GeometricNormalOS = cross(tangentVec, widthDir);
                float normalizedWidth = sideOffset / blade.dimensions.x;
                float3 roundingOffset = widthDir * odd * normalizedWidth * _NormalCurvature;
                float3 roundedNormal = normalize(GeometricNormalOS + roundingOffset);

                float3 terrainNormalOS = TransformWorldToObject(blade.terrainNormal);
                float3 roundedTerrainNormal = normalize(terrainNormalOS + roundingOffset);


                float3 positionWS = TransformObjectToWorld(pos) + blade.position;
                float3 bladeDir = float3(-widthDir.z, 0, widthDir.x);

                // Uv's need to be normalized by blade parameters instead of world space because the arcScaling
                // between triangles throughs the accuracy of true uv's off, causing zig-zags
                o.uv = float2(dot(positionWS - blade.position, normalize(widthDir)) / (blade.dimensions.x * 0.5),
                    dot(positionWS - blade.position, normalize(bladeDir)) / blade.dimensions.y);
                

                // Passing data to the fragment shader
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = normalize(TransformObjectToWorldNormal(roundedNormal));
                o.vertexID = vertex;
                o.terrainNormalWS = normalize(TransformObjectToWorldNormal(roundedTerrainNormal));
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target 
            {
                /*
                float facing = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0); // normals for back faces should be reversed
                float3 normals = (input.normalWS * facing + 1) * 0.5; 
                return half4(normals, 1);
                */
                
                half4 pbr = CalculateGrassLighting(input, isFrontFace);
                return pbr;
            }
            ENDHLSL
        }
    }
}
