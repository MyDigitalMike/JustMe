Shader "JustMe/Parallax Ruins"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _UvRect ("Sprite rectangle", Vector) = (0,0,1,1)
        _MaskCenter ("Visibility center", Vector) = (0,0,0,0)
        _MaskRadius ("Visibility radius", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _UvRect, _MaskCenter;
                float _MaskRadius;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 worldXY:TEXCOORD1; half4 color:COLOR; };
            V Vert(A input)
            {
                V output; float3 world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(world); output.worldXY=world.xy; output.uv=input.uv; output.color=input.color; return output;
            }
            half4 Frag(V input):SV_Target
            {
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
                float2 uv=(input.uv-_UvRect.xy)/_UvRect.zw;
                float2 edge=smoothstep(0,.08,uv)*smoothstep(0,.08,1-uv);
                color.a*=edge.x*edge.y;
                if(_MaskRadius>=0)color.a*=1-smoothstep(max(.0001,_MaskRadius*.75),max(.0002,_MaskRadius),distance(input.worldXY,_MaskCenter.xy));
                return color;
            }
            ENDHLSL
        }
    }
}
