Shader "Texture3D/VolumeShaderSmooth"
{
    Properties
    {
        _MainTex("Texture", 3D) = "white" {}
        _Alpha("Alpha", float) = 1.0
        _StepSize("Step Size", float) = 0.002
        _MaxSteps("Max Steps", int) = 2048
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            Blend One OneMinusSrcAlpha
            LOD 100

            Pass
            {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Maximum allowed amount of raymarching samples
            #define MAX_STEP_COUNT _MaxSteps

            // Allowed floating point inaccuracy
            #define EPSILON 0.00001f

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 vectorToSurface : TEXCOORD1;
            };

            sampler3D _MainTex;
            float4 _MainTex_ST;
            float _Alpha;
            float _StepSize;
            int _MaxSteps;  // New uniform for max step count

            v2f vert(appdata v)
            {
                v2f o;

                // Transform vertex to world space
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;

                // Calculate vector from camera to vertex in world space
                o.vectorToSurface = worldPos - _WorldSpaceCameraPos;

                // Transform vertex to clip space
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 BlendUnder(float4 color, float4 newColor)
            {
                color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                color.a += (1.0 - color.a) * newColor.a;
                return color;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Start raymarching at the front surface of the object
                float3 rayOrigin = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;

                // Use vector from camera to object surface to get ray direction
                float3 rayDirection = mul((float3x3)unity_WorldToObject, normalize(i.vectorToSurface));

                float4 color = float4(0, 0, 0, 0);
                float3 samplePosition = rayOrigin;
                float currentStepSize = _StepSize;

                // Raymarch through object space
                [loop]
                for (int j = 0; j < MAX_STEP_COUNT; j++)
                {
                    // Accumulate color only within unit cube bounds
                    if (abs(samplePosition.x) < 0.5f + EPSILON && abs(samplePosition.y) < 0.5f + EPSILON && abs(samplePosition.z) < 0.5f + EPSILON)
                    {
                        // Use trilinear filtering for smoother results
                        float3 samplePosTex = samplePosition + float3(0.5f, 0.5f, 0.5f);
                        float4 sampledColor = tex3D(_MainTex, samplePosTex);
                        //sampledColor.a /= max(sampledColor.a, EPSILON);
                        sampledColor.a *= _Alpha;

                        // Normalize the sampled color to avoid darkening
                        sampledColor.rgb /= max(sampledColor.a, EPSILON); //  c est la qu il y a la solution !!!

                        // Smooth the edges by modifying alpha value
                        float densityThreshold = 0.01; // Adjust as necessary
                        sampledColor.a = smoothstep(densityThreshold, 1.0, sampledColor.a);

                        color = BlendUnder(color, sampledColor);
                        //color = sampledColor;

                        // If we hit a non-transparent voxel, reduce step size for better accuracy
                        if (sampledColor.a > EPSILON)
                        {
                            currentStepSize = _StepSize / 2.0f;
                        }
                        else
                        {
                            currentStepSize = _StepSize;
                        }
                    }
                    samplePosition += rayDirection * currentStepSize;

                    // Exit early if color is fully opaque
                    if (color.a >= 0.99f)
                    {
                        break;
                    }
                }

                return color;
            }
            ENDCG
        }
        }
            FallBack "Unlit/Transparent" 
}