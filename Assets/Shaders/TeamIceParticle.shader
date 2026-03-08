Shader "VFX/TeamIceParticle"
{
    Properties
    {
        [HDR] _Color ("Main Ice Color", Color) = (0.2, 0.5, 1.2, 1) // Base Blue Glow
        [HDR] _CoreColor ("Hot Core (Frost)", Color) = (1.5, 2.5, 3.5, 1) // Glowing cyan core
        _Speed ("Shatter Speed", Float) = 1.0
        _Sharpness ("Crystal Sharpness", Range(1, 10)) = 5.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
            };

            half4 _Color;
            half4 _CoreColor;
            float _Speed;
            float _Sharpness;

            // Helper for 2D rotation
            float2 rotate(float2 v, float a) {
                float s = sin(a);
                float c = cos(a);
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Move UVs to -1 to 1 range
                float2 uv = i.uv * 2.0 - 1.0;
                
                // Add spin based on particle lifetime/speed and vertex position
                uv = rotate(uv, _Time.y * _Speed + i.localPos.x);

                // Convert to polar coordinates for 6-point symmetry
                float angle = atan2(uv.y, uv.x);
                float radius = length(uv);

                // Create 6-fold symmetry folding (snowflakes)
                float sectorAngle = 3.14159 / 3.0; // 60 degrees
                float localAngle = fmod(angle + 3.14159, sectorAngle) - sectorAngle/2.0;
                
                // Create the crystal snowflake arms
                float arm = abs(localAngle) * radius;
                
                // Sharpen and shape the crystal
                float crystalMask = smoothstep(0.1, 0.02, arm) * smoothstep(1.0, 0.2, radius);
                
                // Add some secondary branches to the snowflake
                float branchDist = abs(radius - 0.4);
                float branches = smoothstep(0.05, 0.01, abs(localAngle * radius - 0.1 * branchDist));
                crystalMask = max(crystalMask, branches * smoothstep(0.8, 0.2, radius));

                // Soft fade at absolute edges to prevent square cut-offs
                float edgeFade = smoothstep(1.0, 0.8, radius);
                float finalMask = crystalMask * edgeFade;

                // Color tinting
                half3 finalColor = lerp(_Color.rgb, _CoreColor.rgb, finalMask);

                // Combine with particle color & alpha mask. Tone down alpha multiplier.
                return half4(finalColor * i.color.rgb, finalMask * i.color.a * 0.5);
            }
            ENDCG
        }
    }
}
