Shader "Custom/Voronoi"
{
    Properties
    {
        _NumPoints ("Number of Points", Int) = 10
        _Seed ("Random Seed", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            int _NumPoints;
            float _Seed;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Fragment(Varyings input) : SV_Target 
            {
                float2 uv = input.uv;
                
                // TODO: Implement Voronoi algorithm here
                // Return grayscale value (0-1)
                
                return 0.5; // Placeholder
            }
            ENDHLSL
        }
    }
}
