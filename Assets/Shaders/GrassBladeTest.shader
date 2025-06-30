Shader "Custom/GrassBladeTest"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Cull Off // disable back-face culling
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/GrassBlade.cginc"

            StructuredBuffer<GrassBlade> _BladeBuffer;

            struct Attributes
            {
                //float4 positionOS : POSITION;
                uint vertexID : SV_VertexID;
                //float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                //float2 uv : TEXCOORD0;
            };

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
            // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
            // #pragma instancing_options assumeuniformscaling
            UNITY_INSTANCING_BUFFER_START(Props)
                
            UNITY_INSTANCING_BUFFER_END(Props)


            float2 rand2(float2 p) {
                return frac(sin(float2(dot(p, float2(127.1f, 311.7f)), dot(p, float2(269.5f, 183.3f)))) * 43758.5453f);
            }
            
            void Vertex(Attributes input, out Varyings o)
            {
                GrassBlade blade = _BladeBuffer[0];
                float3 up = float3(0,1,0);
                
                // Root of the grass blade in object space
                float3 p0 = float3(0,0,0);
        
                // randomness based off position of this classes object so that I can randomize while still centering the blade
                float2 bladeHash2D = rand2(blade.position.xz);

                float2 facing = normalize(bladeHash2D * 2.0f - float2(1,1)); // Random values between 0 and 1

                // Endpoint is based on height and tilt
                p3.transform.position = p0.transform.position + new Vector3(facing.x, 0, facing.y) * blade.tilt + Vector3.up * height;
                
                // Above the starting point. How long until bending starts a lot more
                p1.transform.position = p0.transform.position + Vector3.up * (height * bend);

                Vector3 midPoint = 0.5f * (p3.transform.position - p1.transform.position);
                Vector3 widthDir = new Vector3(facing.y, 0, -facing.x);
                Vector3 bladeDir = normalize(p3.transform.position - p1.transform.position);
                Vector3 awayDir = cross(-widthDir, bladeDir);

                p2.transform.position = (p0.transform.position + midPoint) + awayDir * bend;



                
                

                float3 endPoint = float3(1.5, 2.5, 3.5);
                float3 away = endPoint * float3(-1,1,-1);
                float3 mid = (endPoint * .5) + away * blade.bend;

                uint vertex = input.vertexID;
                float t = (vertex / 2) / 7.0; // Interpolated in pairs

                // De Castelijau's Algorithm for Bezier Curves
                float3 A = lerp(float3(0,0,0), mid, t);
                float3 B = lerp(mid, endPoint, t);
                float3 pos = lerp(A, B, t);

                float3 tangent = normalize(lerp(float3(0,0,0) + 1e-5, endPoint, t));

                float3 side = normalize(cross(up, tangent));
                
                // Blade will get skinnier the further up it goes, with point 14 (the last one) being along the center
                float sideOffset = blade.width - ((blade.width / 7.0) * (vertex / 2));

                int odd = (vertex % 2) * 2 - 1; // -1 or 1
                pos += side * (sideOffset * odd);
                o.positionCS = TransformObjectToHClip(pos);
            }

            float3 Fragment(Varyings input) : SV_Target 
            {
                return float3(0,1,0);
            }
            ENDHLSL
        }
    }
}
