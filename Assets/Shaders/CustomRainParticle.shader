Shader "VFX/CustomRainParticle"
{
    Properties
    {
        _Color ("Rain Color", Color) = (0.8, 0.9, 1.0, 0.3)
        _Speed ("Drop Speed", Float) = 15.0
        _StreakLength ("Streak Length", Float) = 4.0
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
            };

            half4 _Color;
            float _StreakLength;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Move UVs to -1 to 1 range
                float2 uv = i.uv * 2.0 - 1.0;
                
                // Elongate UVs vertically to create a rain streak
                uv.y /= _StreakLength;
                
                // Soft fade at edges of the drop
                float dist = length(uv);
                float dropAlpha = smoothstep(1.0, 0.2, dist);

                // Add slight gradient to the tail of the drop (top fading out)
                float tailFade = smoothstep(1.0, -1.0, i.uv.y * 2.0 - 1.0);
                
                half4 finalColor = _Color * i.color;
                return half4(finalColor.rgb, finalColor.a * dropAlpha * tailFade);
            }
            ENDCG
        }
    }
}
