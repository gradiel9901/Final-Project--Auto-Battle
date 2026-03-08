Shader "Skybox/WarzoneSkybox"
{
    Properties
    {
        [HDR] _SkyColor("Zenith Color", Color) = (0.05, 0.05, 0.05, 1) // Dark grey/brown
        [Header(Horizon)]
        _HorizonColor("Horizon Color", Color) = (0.4, 0.15, 0.02, 1) // Dimmed Fiery Orange
        _HorizonExposure("Horizon Brightness", Range(0.1, 10)) = 0.4
        _HorizonThickness("Horizon Thickness", Range(0.1, 5)) = 1.0
        
        [Header(Cloud Noise)]
        _CloudColor("Cloud Color", Color) = (0.02, 0.01, 0.01, 1) // Pitch black smoke clouds
        _CloudScale("Cloud Scale", Float) = 15.0
        _CloudSpeed("Cloud Speed", Float) = 0.5
        _CloudDensity("Cloud Density", Range(0, 1)) = 0.6
        
        [Header(Fires Below)]
        _GroundFire("Ground Reflection Glow", Color) = (0.2, 0.08, 0, 1) // Dim orange
        _FireInfluence("Ground Glow Height", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            half4 _SkyColor;
            half4 _HorizonColor;
            half _HorizonExposure;
            half _HorizonThickness;
            
            half4 _CloudColor;
            float _CloudScale;
            float _CloudSpeed;
            half _CloudDensity;
            
            half4 _GroundFire;
            half _FireInfluence;

            // Simple 2D/3D Noise for procedural clouds/smoke
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
            
            // FBM for thicker, layered smoke
            float fbm(float3 p)
            {
                float f = 0.0;
                f += 0.5000 * noise(p); p *= 2.02;
                f += 0.2500 * noise(p); p *= 2.03;
                f += 0.1250 * noise(p); p *= 2.01;
                f += 0.0625 * noise(p);
                return f;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize eye vector calculation
                float3 viewDir = normalize(i.texcoord);
                
                // --- 1. Base Gradient (Sky to Horizon) ---
                // Y dot product gives us vertical height. 1 = straight up, 0 = horizon, -1 = straight down
                float posY = viewDir.y;
                
                // Horizon blend
                float horizonBlend = saturate(pow(1.0 - abs(posY), _HorizonThickness));
                half3 baseColor = lerp(_SkyColor.rgb, _HorizonColor.rgb * _HorizonExposure, horizonBlend);
                
                // --- 2. Ground Fire Glow (Below Horizon) ---
                // If we are looking slightly downward, simulate massive fires illuminating the dust
                float groundBlend = saturate((posY * -1.0) / _FireInfluence);
                if (posY < 0)
                {
                    baseColor = lerp(baseColor, _GroundFire.rgb * _HorizonExposure, groundBlend);
                }

                // --- 3. Procedural Smoke / Ash Clouds ---
                // We project noise into the sky sphere
                // Offset by time for slow drifting smoke
                float3 noisePos = viewDir * _CloudScale;
                noisePos.x += _Time.y * _CloudSpeed;
                noisePos.z += _Time.y * (_CloudSpeed * 0.5);
                
                float cloudNoise = fbm(noisePos);
                
                // Smoothstep to create distinct thick smoke patches
                float cloudMask = smoothstep(1.0 - _CloudDensity, 1.0, cloudNoise);
                
                // Blend clouds over the sky
                half3 finalColor = lerp(baseColor, _CloudColor.rgb, cloudMask * 0.95); // 0.95 keeps slight transparency 

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
