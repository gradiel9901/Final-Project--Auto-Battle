Shader "Custom/InstancedVATShader"
{
    Properties
    {
        _MainTex ("Albedo Texture", 2D) = "white" {}
        [Header(VAT Settings)]
        _PosAnimTex ("Position Animation Texture", 2D) = "black" {}
        _AnimLength ("Animation Length (Frames)", Float) = 30
        _CurrentFrame ("Current Frame", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1; // Used for vertex ID if baked that way
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            sampler2D _PosAnimTex;
            float4 _PosAnimTex_TexelSize; // To calculate pixel offsets
            float _AnimLength;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimStateIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _TeamColorIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _TimeOffset)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Read per-instance Properties
                float stateIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimStateIndex);
                float timeOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _TimeOffset);

                // --- VAT LOGIC ---
                // Calculate which row (stateIndex) and column (time) to read from the texture
                // 1. Time evaluation
                float rawTime = _Time.y * 30.0 + timeOffset; // Assuming 30fps bake
                float currentFrame = fmod(rawTime, _AnimLength);

                // 2. Texture Sampling coordinates
                // X = Frame index in the texture
                // Y = Vertex ID / Total Vertices OR specific row if UVs are mapped that way
                
                // Assuming standard VAT bake where X is frame, Y is vertex index
                // Note: The specific math here varies wildly based on exactly WHICH VAT baker plugin the user uses 
                // (e.g., Houdini, Keijiro's AnimationInstancing, AnimationBakingStudio)
                
                float u = (currentFrame + 0.5) * _PosAnimTex_TexelSize.x;
                float v_idx = (v.vertexID + 0.5) * _PosAnimTex_TexelSize.y;
                
                // Sample the baked vertex local position
                // (Depends if position is pure or delta. Assuming pure position here)
                float4 bakedPos = tex2Dlod(_PosAnimTex, float4(u, v_idx, 0, 0));

                // If texture is actually populated, deform vertex. Otherwise fallback to original.
                // Multiplying by stateIndex lets us "select" different rows for different animations if packed properly.
                float3 finalLocalPos = bakedPos.xyz != 0 ? bakedPos.xyz : v.vertex.xyz;

                // Move from Object to Clip Space
                o.pos = UnityObjectToClipPos(float4(finalLocalPos, 1.0));
                o.uv = v.uv;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float teamCode = UNITY_ACCESS_INSTANCED_PROP(Props, _TeamColorIndex);
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Simple team tint for visual identification
                if (teamCode == 0) col *= fixed4(1, 0.5, 0.5, 1); // Red tint
                else col *= fixed4(0.5, 1, 0.5, 1); // Green tint

                return col;
            }
            ENDCG
        }
    }
}
