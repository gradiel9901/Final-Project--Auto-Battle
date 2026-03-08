Shader "VFX/TeamFireParticle"
{
    Properties
    {
        [HDR] _Color ("Main Color", Color) = (1.2, 0.6, 0.2, 1) // Base Orange Glow
        [HDR] _CoreColor ("Core Color", Color) = (2.5, 2.0, 1.2, 1) // Glowing core
        _Speed ("Animation Speed", Float) = 2.0
        _NoiseScale ("Noise Scale", Float) = 5.0
        _Density ("Fire Density", Range(0, 5)) = 1.5
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
            float _NoiseScale;
            float _Density;

            // Simple 3D noise
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                 lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                 lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
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
                // Soft particle circular alpha
                float dist = distance(i.uv, float2(0.5, 0.5));
                float circleAlpha = smoothstep(0.5, 0.1, dist);

                // Animated Noise
                float3 noisePos = i.localPos * _NoiseScale;
                noisePos.y -= _Time.y * _Speed; // Move noise up like fire
                float n = noise(noisePos);
                
                // Flame shaping
                float flameMask = pow(n * circleAlpha, _Density);
                
                // Color ramp: Core is yellow/white, edges are orange/red
                half3 finalColor = lerp(_Color.rgb, _CoreColor.rgb, flameMask);

                return half4(finalColor * i.color.rgb, flameMask * i.color.a);
            }
            ENDCG
        }
    }
}
