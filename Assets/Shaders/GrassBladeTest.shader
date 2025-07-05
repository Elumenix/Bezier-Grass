Shader "Custom/GrassBladeTest"
{
    Properties
    {
        [Toggle] swaying ("Sway Blade", Float) = 0
        windStrength ("Wind Strength", Range(0.0, 5.0)) = 0.5
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

            // Hash will not be a part of the final implementation. Because we're testing control points on the cpu here,
            // We need something that will maintain precision between C# and HLSL, so we need to do this specifically here
            float2 hash;
            StructuredBuffer<GrassBlade> _BladeBuffer;
            float windStrength;
            float swaying;

            struct Attributes
            {
                //float4 positionOS : POSITION;
                uint vertexID : SV_VertexID;
                //float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                uint vertexID : TEXCOORD0;
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

                // STEP 1: Get the four control points of the cubic bezier curve
                
                // Root of the grass blade in object space
                float3 p0 = float3(0,0,0);
        
                // randomness based off position of this classes object so that I can randomize while still centering the blade
                //float2 bladeHash2D = rand2(blade.position.xz);

                float2 facing2D = normalize(hash * 2.0f - float2(1,1)); // Random values between 0 and 1
                float3 facing = float3(facing2D.x, 0, facing2D.y);
                float3 widthDir = float3(facing2D.y, 0, -facing2D.x);


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
                    float phaseOffset = hash.x * 1.57;
                    float rawSpeedMult = 2.0 * (hash.x + 1.0) * (windStrength + 1.0);
                    float speedMult = round(rawSpeedMult); // Force to integer multiples, confirms loop
                    float maxAmplitude = 0.01 * (windStrength + 3.0);
                    float timeMod = _Time[1] % (2.0 * acos(-1.0));
                    p3 = p3 + sin(timeMod*speedMult + phaseOffset)*maxAmplitude*awayDir;
                    p2 = p2 + sin(timeMod*speedMult + 1.57 + phaseOffset)*maxAmplitude/2.0*awayDir;
                }


                // STEP 3: Get the derivative (quadratic bezier curve) to normalize vertices positions on the curve
                // Essentially arc length parameterization. Distance on a bezier curve isn't even, so we're mathmatically solving that

                // Calculate quadratic bezier curve control points
                float3 d0 = 3.0 * (p1 - p0);

                // Precompute coefficients for fast position/derivative evaluation
                float3 c0 = p0;
                float3 c1 = d0;
                float3 c2 = 3.0 * (p2 - p1) - d0;
                float3 c3 = p3 - p0 - d0 - c2;

                
                uint vertex = input.vertexID;
                float t = (vertex / 2) / 7.0; // Interpolated in pairs
                t = sqrt(t);
                float t2 = t * t;
                float t3 = t * t2;

                // Position on cubic curve after arc-length normalization
                float3 pos = c0 + c1 * t + c2 * t2 + c3 * t3;
                
                
                // Blade will get skinnier the further up it goes, with point 14 (the last one) being along the center
                float sideOffset = blade.width - ((blade.width / 7.0) * (vertex / 2));

                int odd = (vertex % 2) * 2 - 1; // -1 or 1
                pos += widthDir * sideOffset * odd;
                o.positionCS = TransformObjectToHClip(pos);
                o.vertexID = vertex;
            }

            float3 Fragment(Varyings input) : SV_Target 
            {
                if (input.vertexID == 0) return float3(1,1,1);
                return float3(0,1,0);
            }
            ENDHLSL
        }
    }
}
