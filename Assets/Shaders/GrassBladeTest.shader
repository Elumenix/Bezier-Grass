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

            void Vertex(Attributes input, out Varyings o)
            {
                // Just testing I can get this currently. This test scene doesn't need world space
                GrassBlade blade = _BladeBuffer[0];
                uint vertex = input.vertexID;
                int odd = (vertex % 2) * 2 - 1; // -1 or 1
                int height = vertex / 2;
                //float3 objectSpacePosition = input.positionOS.xyz;
                float horizontalPosition = blade.width / 2 * odd;
                float vericalPosition = height * blade.height;
                if (input.vertexID == 14) horizontalPosition = 0; // center top vertex
                
                
                o.positionCS = TransformObjectToHClip(float3(horizontalPosition, vericalPosition, 0));
            }

            float3 Fragment(Varyings input) : SV_Target 
            {
                return float3(0,1,0);
            }
            ENDHLSL
        }
    }
}
