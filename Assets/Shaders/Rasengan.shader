Shader "Custom/Rasengan"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _MainColor("Energy Color", Color) = (0.2, 0.6, 1.0, 1.0)
        [HDR] _CoreColor("Core Color", Color) = (1.0, 1.0, 1.0, 1.0)
        
        [Header(Animation Settings)]
        _Speed("Swirl Speed", Float) = 15.0
        _RotationSpeed("Overall Rotation", Float) = 5.0
        _NoiseScale("Distortion Scale", Float) = 20.0
        _BandFrequency("Energy Band Frequency", Float) = 30.0
        
        [Header(Glow Settings)]
        _FresnelPower("Outer Edge Sharpness", Float) = 3.0
        _FresnelIntensity("Outer Glow Intensity", Float) = 2.0
        _CoreSize("Core Focus", Float) = 4.0

        [Header(Vertex Displacement)]
        _DisplacementStrength("Displacement Strength", Float) = 0.2
        _DisplacementSpeed("Displacement Speed", Float) = 5.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        LOD 100
        
        // Additive or Lightly blended transparent
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : NORMAL;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor;
                half4 _CoreColor;
                float _Speed;
                float _RotationSpeed;
                float _NoiseScale;
                float _BandFrequency;
                float _FresnelPower;
                float _FresnelIntensity;
                float _CoreSize;
                float _DisplacementStrength;
                float _DisplacementSpeed;
            CBUFFER_END

            // --- NOISE FUNCTIONS ---
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ps_noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                 lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                 lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            float2x2 RotMatrix(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2x2(c, -s, s, c);
            }
            // -----------------------

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // VERTEX DISPLACEMENT
                // Generate a chaotic flow based on position and time
                float3 noisePos = input.positionOS.xyz * 4.0 + float3(0, _Time.y * _DisplacementSpeed, 0);
                float displacementAmt = ps_noise(noisePos) * 2.0 - 1.0; // Range -1 to 1
                
                // Displace along the normal vector
                float3 displacedPosOS = input.positionOS.xyz + (input.normalOS * displacementAmt * _DisplacementStrength);
                
                // Transform to World Space
                VertexPositionInputs vertexInput = GetVertexPositionInputs(displacedPosOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                output.uv = input.uv;
                
                // Normal transform to World Space
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // 1. CORE
                float NdotV = saturate(dot(input.normalWS, viewDirWS));
                float coreMask = pow(max(NdotV, 0.001), _CoreSize);
                
                // 2. FRESNEL (OUTER GLOW)
                float fresnelMask = pow(1.0 - NdotV, _FresnelPower);
                
                // 3. SWIRLING ENERGY
                float2 uvCenter = float2(0.5, 0.5);
                float distanceFromCenter = distance(input.uv, uvCenter);
                
                float twistAngle = _Time.y * _RotationSpeed + (distanceFromCenter * 15.0);
                float2 rotatedUV = mul(RotMatrix(twistAngle), input.uv - uvCenter) + uvCenter;
                
                float3 noisePos = float3(rotatedUV.x * _NoiseScale, rotatedUV.y * _NoiseScale, _Time.y * _Speed);
                float chaoticNoise = ps_noise(noisePos);
                
                float bandMask = sin(distanceFromCenter * _BandFrequency - _Time.y * (_Speed * 2.0));
                bandMask = bandMask * 0.5 + 0.5;
                bandMask *= chaoticNoise;
                bandMask = smoothstep(0.3, 0.7, bandMask);
                
                // 4. COMBINING FRAGMENT COLOR
                half4 finalColor = half4(0,0,0,0);
                
                // Additive Core
                finalColor += _CoreColor * coreMask * 2.5;
                // Outer shell edge glow
                finalColor += _MainColor * fresnelMask * _FresnelIntensity;
                // Swirl Bands (suppressed in the dead center)
                float midLayerMask = (1.0 - coreMask) * bandMask;
                finalColor += _MainColor * midLayerMask * 1.5;
                
                // Transparency Alpha Calculation
                float totalAlpha = saturate(fresnelMask + coreMask + (midLayerMask * 0.8));
                finalColor.a = totalAlpha * _MainColor.a;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
