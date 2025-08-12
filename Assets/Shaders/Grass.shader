Shader "Custom/Grass"
{
    Properties
    {
        [Header(Property Settings)]
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1.0)) = 0.5
        _Metallic ("Metallic", Range(0,1.0)) = 0.0
        _Occlusion ("Occlusion", Range(0,1.0)) = 1.0
        _NormalCurvature ("Normal Curve", Range(0.0, 1.0)) = 0.5
        
        [Header(Camera View Space Projection Settings)]
        _AdjustmentThreshold ("Adjustment Threshold", Range(0.0,1.0)) = 0.25
        _AdjustmentStrength ("Adjustment Strength", Range(0.0,1.0)) = 0.75
        
        [Header(Blade Behavior)]
        [Toggle] swaying ("Sway Blade", Float) = 0
        windStrength ("Wind Strength", Range(0.0, 5.0)) = 0.5
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
        static const half arcTBuffer[8] = { 0.001f, 0.33f, 0.49f, 0.62f, 0.73f, 0.83f, 0.92f, 1.0f };
        static const half lodTBuffer[8] = { 0.001f, 0.001f, 0.001f, 0.5f, 0.8f, 1.0f, 1.0f, 1.0f };
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

            void Vertex(Attributes input, uint instanceID : SV_InstanceID, out Varyings o)
            {
                GrassBlade blade = grassBlades[instanceID];

                // We want to switch to a low detail grass blade if far away from the mesh. To make this seamless, we're
                // stretching vertices towards the end of the grass blade so that it fades to a low detail mesh instead of instantly changing
                float distanceToCamera = distance(blade.position, _WorldSpaceCameraPos);
                float lodValue = saturate((distanceToCamera - _LodRange.x) / (_LodRange.y - _LodRange.x));

                // We're precomputing ArcT instead of doing an expensive arc length parameterization calculation in the vertex shader
                // There's no closed form solution for arc length parameterization for cubic bezier curves, so this is much easier
                uint vertex = input.vertexID;
                uint pair = vertex / 2;
                half t = lerp(arcTBuffer[pair], lodTBuffer[pair], lodValue);

                float3 pos;
                float3 tangentVec;
                CalculateBezierCurve(blade, t, pos, tangentVec);
            
                // Blade will get skinnier the further up it goes, with point 14 (the last one) being along the center
                float sideOffset = blade.width - (blade.width * t * t);
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
            
                float3 widthDir = viewSpaceAdjustment(blade, pos, tangentVec);
                pos += widthDir * sideOffset * odd;

                // Normals are rounded so that the blades don't look as flat and reflet light better
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

            half4 Fragment(Varyings input) : SV_Target 
            {
                //return float4(input.normalWS.xyz, 1);
                half4 pbr = CalculateGrassLighting(input);
                return pbr;
            }
            ENDHLSL
        }
    }
}
