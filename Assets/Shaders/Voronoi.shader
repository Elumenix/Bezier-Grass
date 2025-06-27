Shader "Custom/Voronoi"
{
    Properties
    {
        _BlueNoise ("Noise Texture", 2D) = "white" {}
        _NumPoints ("Resolution", Int) = 10
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
            Texture2D _BlueNoise;
            SamplerState sampler_BlueNoise;

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

            float2 rand2(float2 p) {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }
            
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float3 Fragment(Varyings input) : SV_Target 
            {
                float2 uv = input.uv * _NumPoints;
                float2 baseCell = floor(uv);
                float minDistToCell = 10.0;
                float2 closestCell;

                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 cell = baseCell + float2(x,y);

                        float2 wrappedCell = fmod(cell + _NumPoints, _NumPoints);
                        
                        float2 cellPosition = wrappedCell + rand2(wrappedCell);
                        float2 diff = cellPosition - uv;


                        float2 wrappedDiff = diff;
                        if (abs(diff.x) > _NumPoints * 0.5) {
                            wrappedDiff.x = diff.x - sign(diff.x) * _NumPoints;
                        }
                        if (abs(diff.y) > _NumPoints * 0.5) {
                            wrappedDiff.y = diff.y - sign(diff.y) * _NumPoints;
                        }

                        
                        float distToCell = length(wrappedDiff);

                        if (distToCell < minDistToCell)
                        {
                            minDistToCell = distToCell;
                            closestCell = wrappedCell;
                        }
                    }
                }

                // for color
                float random = rand2(closestCell);
                //return random;
                return minDistToCell;
            }
            ENDHLSL
        }
    }
}
