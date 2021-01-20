﻿Shader "Hidden/NoiseBall3"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Cull Off

        CGPROGRAM

        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0

        // Hash function from H. Schechter & R. Bridson, goo.gl/RXiKaH
        uint Hash(uint s)
        {
            s ^= 2747636419u;
            s *= 2654435769u;
            s ^= s >> 16;
            s *= 2654435769u;
            s ^= s >> 16;
            s *= 2654435769u;
            return s;
        }

        // Random number (0-1)
        float Random(uint seed)
        {
            return float(Hash(seed)) / 4294967295.0; // 2^32-1
        }

        // Random point on unit sphere
        float3 RandomPoint(uint seed)
        {
            float u = Random(seed * 2 + 0) * UNITY_PI * 2;
            float z = Random(seed * 2 + 1) * 2 - 1;
            return float3(float2(cos(u), sin(u)) * sqrt(1 - z * z), z);
        }

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
            float4 color : COLOR;
            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;
            uint vid : SV_VertexID;
        };

        struct Input
        {
            float vface : VFACE;
            float4 color : COLOR;
        };

        float4x4 _LocalToWorld;
        float4x4 _WorldToLocal;

        void vert(inout appdata v)
        {
            uint t_idx = v.vid / 3;         // Triangle index
            uint v_idx = v.vid - t_idx * 3; // Vertex index

            // Time dependent random number seed
            uint seed = 10005451;
            seed = ((seed << 16) + t_idx) * 4;

            // Random triangle on unit sphere
            float3 v1 = RandomPoint(seed + 0);
            float3 v2 = RandomPoint(seed + 1);
            float3 v3 = RandomPoint(seed + 2);

            // Constraint with the extent parameter
            v2 = normalize(v1 + normalize(v2 - v1) * 1);
            v3 = normalize(v1 + normalize(v3 - v1) * 1);

            //// Displacement by noise field
            //float l1 = snoise(v1 * 1 + 0).w;
            //float l2 = snoise(v2 * 1 + 0).w;
            //float l3 = snoise(v3 * 1 + 0).w;

            //l1 = abs(l1 * l1 * l1);
            //l2 = abs(l2 * l2 * l2);
            //l3 = abs(l3 * l3 * l3);

            //v1 *= 1 + l1 * 1;
            //v2 *= 1 + l2 * 1;
            //v3 *= 1 + l3 * 1;

            // Vertex position/normal modification
            v.vertex.xyz = v_idx == 0 ? v1 : (v_idx == 1 ? v2 : v3);
            v.normal = normalize(cross(v2 - v1, v3 - v2));

            // Random emission
            v.color = 0.95 < Random(seed + 3);

            // Transform modification
            unity_ObjectToWorld = _LocalToWorld;
            unity_WorldToObject = _WorldToLocal;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = fixed4(1, 1, 1, 1);
            o.Metallic = 0;
            o.Smoothness = 0;
            //o.Normal = float3(0, 0, IN.vface < 0 ? -1 : 1); // back face support
            //o.Emission = 1 * IN.color.rgb;
        }

        ENDCG
    }
    FallBack "Diffuse"
}