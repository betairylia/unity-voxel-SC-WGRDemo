Shader "Custom/ChunkShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        //Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard addshadow vertex:vert
        //#pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #include "../Data/Block.cginc"

        #pragma target 5.0

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            fixed4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            //float4 texcoord2 : TEXCOORD2;
            uint vid : SV_VertexID;
        };

        struct Input
        {
            float2 uv_MainTex : TEXCOORD0;
            fixed4 color : COLOR;
        };

        struct cs_Vertex
        {
            float3 position;
            float3 normal;
            float2 uv;
            Block data;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float4x4 _LocalToWorld;
        float4x4 _WorldToLocal;

        sampler2D _MainTex;

#ifdef SHADER_API_D3D11
        uniform StructuredBuffer<cs_Vertex> cs_vbuffer;
#endif

        void vert(inout appdata v)
        {
#ifdef SHADER_API_D3D11
            v.vertex = float4(cs_vbuffer[v.vid].position, 1.0f);// +float4(2.5f * sin(_Time.x * 5.0 + cs_vbuffer[v.vid].position.x * 0.3f + cs_vbuffer[v.vid].position.y * 0.15f - cs_vbuffer[v.vid].position.z * 1.3f), 0, 0, 0);
            v.normal = cs_vbuffer[v.vid].normal;
            
            Block blk = cs_vbuffer[v.vid].data;
            uint meta = GetBlockMeta(blk);
            v.color = (GetBlockID(blk) == 0xFFFF) * fixed4(
                float((meta >> 12) & (0x000F)) / 15.0,
                float((meta >>  8) & (0x000F)) / 15.0,
                float((meta >>  4) & (0x000F)) / 15.0,
                float((meta      ) & (0x000F)) / 15.0
            );
            v.texcoord = cs_vbuffer[v.vid].uv;
#else
            v.vertex = float4(0.0f, 0.0f, 0.0f, 1.0f);
            v.normal = float3(0.0f, 1.0f, 0.0f);
            v.color = fixed4(1.0, 0.5, 0.0, 1.0);
            v.texcoord = float2(0, 0);
#endif
            //v.texcoord1 = float4(0, 0, 0, 0);
            //v.texcoord2 = float4(0, 0, 0, 0);

            // Transform modification
            unity_ObjectToWorld = _LocalToWorld;
            unity_WorldToObject = _WorldToLocal;
        }

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            if (IN.color.a <= 0.9f)
            {
                o.Emission = 2.0f * (IN.color.a / 0.5f) * float4(IN.color.rgb, 1.5);
                o.Albedo = IN.color.rgb * tex2D(_MainTex, IN.uv_MainTex);
            }
            else
            {
                o.Albedo = IN.color.rgb * tex2D(_MainTex, IN.uv_MainTex);
            }
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0f;
            //o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
