Shader "Custom/SketchPreview"
{

    //--------------------------------------------------------------------------------------------
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)

        [HDR]
        _AmbientColor("Ambient Color", Color) = (0.4,0.4,0.4,1)

        [HDR]
        _SpecularColor("Specular Color", Color) = (0.9,0.9,0.9,1)

        _Glossiness("Glossiness", Float) = 32

        [HDR]
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimAmount("Rim Amount", Range(0, 1)) = 0.716

        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.1

            // Splat Map Control Texture
        _Control("Control (RGBA)", 2D) = "red" {}
       //[HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}

        // Textures
        /*[HideInInspector] _Splat2("Layer 2 (B)", 2D) = "white" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "white" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "white" {}*/

         _Splat0("Layer 0 (R)", 2D) = "white" {}
         _Splat1("Layer 1 (G)", 2D) = "white" {}
         _Splat2("Layer 2 (B)", 2D) = "white" {}
         _Splat3("Layer 3 (A)", 2D) = "white" {}
    }

        //--------------------------------------------------------------------------------------------

        SubShader
    {
        Pass
        {

            //--------------------------------------------------------------------------------------------

            Tags
            {
                "SplatCount" = "5"
                "LightMode" = "ForwardBase"
                "PassFlags" = "OnlyDirectional"
            }

        //--------------------------------------------------------------------------------------------

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag

        #pragma multi_compile_fwdbase

        #include "UnityCG.cginc"

        #include "Lighting.cginc"
        #include "AutoLight.cginc"

        //--------------------------------------------------------------------------------------------

        struct appdata
        {
            float4 vertex : POSITION;
            float4 uv : TEXCOORD0;
            float3 normal : NORMAL;
        };

    //--------------------------------------------------------------------------------------------

    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 worldNormal : NORMAL;
        float2 uv : TEXCOORD0;
        float3 viewDir : TEXCOORD1;

        SHADOW_COORDS(2)
    };

    //--------------------------------------------------------------------------------------------

    sampler2D _Control;
    float4 _Control_ST;

    sampler2D _Splat0;
    float4 _Splat0_ST;
    sampler2D _Splat1;
    float4 _Splat1_ST;
    sampler2D _Splat2;
    float4 _Splat2_ST;
    sampler2D _Splat3;
    float4 _Splat3_ST;

    //--------------------------------------------------------------------------------------------

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.worldNormal = UnityObjectToWorldNormal(v.normal);
        o.viewDir = WorldSpaceViewDir(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _Control);

        TRANSFER_SHADOW(o)
        return o;
    }

    //--------------------------------------------------------------------------------------------

    float4 _Color;

    float4 _AmbientColor;

    float4 _SpecularColor;
    float _Glossiness;

    float4 _RimColor;
    float _RimAmount;
    float _RimThreshold;

    //--------------------------------------------------------------------------------------------   

    float4 frag(v2f i) : SV_Target
    {
        float3 normal = normalize(i.worldNormal);
        float3 viewDir = normalize(i.viewDir);

        //-------------------------------------------------------------------------------------------- 

        float NdotL = dot(_WorldSpaceLightPos0, normal);

        float shadow = SHADOW_ATTENUATION(i);

        //float lightIntensity = smoothstep(0, 0.01, NdotL * shadow);
        float lightIntensity = max(0, NdotL * shadow);

        float4 light = lightIntensity * _LightColor0;

        //-------------------------------------------------------------------------------------------- 

        float3 halfVector = normalize(_WorldSpaceLightPos0 + viewDir);
        float NdotH = dot(normal, halfVector);

        float specularIntensity = pow(NdotH * lightIntensity, _Glossiness * _Glossiness);
        //float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
        float specularIntensitySmooth = specularIntensity;
        float4 specular = specularIntensitySmooth * _SpecularColor;

        //--------------------------------------------------------------------------------------------           

        float rimDot = 1 - dot(viewDir, normal);

        float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
        rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
        float4 rim = rimIntensity * _RimColor;

        //-------------------------------------------------------------------------------------------- 

        float4 sample_ = tex2D(_Control, i.uv);

        float4 splat0 = tex2D(_Splat0, TRANSFORM_TEX(i.uv, _Splat0));
        float4 splat1 = tex2D(_Splat1, TRANSFORM_TEX(i.uv, _Splat1));
        float4 splat2 = tex2D(_Splat2, TRANSFORM_TEX(i.uv, _Splat2));
        float4 splat3 = tex2D(_Splat3, TRANSFORM_TEX(i.uv, _Splat3));

        float4 splats = splat0 * sample_.r + splat1 * sample_.g + splat2 * sample_.b + splat3 * sample_.a;

        //-------------------------------------------------------------------------------------------- 

        //return (light + _AmbientColor + specular + rim) * _Color * splats;
        return (light + _AmbientColor + specular) * _Color * splats;

        //-------------------------------------------------------------------------------------------- 
    }

    ENDCG
}

UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}