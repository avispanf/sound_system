Shader "BadShift/Window sunlight"
{
    Properties { [HDR] _Tint("Scattered light", Color) = (1,.21,.06,.18) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Vertex { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Pixel { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Pixel Vert(Vertex v)
            {
                Pixel o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Pixel i) : SV_Target
            {
                half edge = smoothstep(0,.16,min(i.uv.x,1-i.uv.x));
                half fade = smoothstep(0,.04,i.uv.y) * pow(saturate(1-i.uv.y),1.35);
                return half4(_Tint.rgb, _Tint.a * edge * fade);
            }
            ENDHLSL
        }
    }
}
