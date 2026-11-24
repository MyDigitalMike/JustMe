Shader "JustMe/Soft Background Fog"
{
    Properties
    {
        _FogColor ("Fog color", Color) = (.5, .62, .72, 1)
        _Density ("Density", Range(0,1)) = .4
        _FogTime ("Time", Float) = 0
        _CameraOffset ("Camera offset", Vector) = (0,0,0,0)
        _Flow ("Flow", Vector) = (1,.035,.008,0)
        _MaskCenter ("Visibility center", Vector) = (0,0,0,0)
        _MaskRadius ("Visibility radius", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density, _FogTime, _MaskRadius;
                float4 _CameraOffset, _Flow, _MaskCenter;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 worldXY : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world); output.worldXY = world.xy;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell), Hash(cell+float2(1,0)), f.x), lerp(Hash(cell+float2(0,1)), Hash(cell+1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.worldXY-_CameraOffset.xy)*float2(.12,.4)*_Flow.x + _Flow.yz*_FogTime + _Flow.w;
                float cloud = Noise(p)*.55 + Noise(p*2.03)*.3 + Noise(p*4.07)*.15;
                float wisps = .55 + .45*sin(p.y*2.5 + Noise(p*.5)*5);
                float alpha = smoothstep(.22,.78,cloud) * wisps * _Density;
                if (_MaskRadius >= 0)
                    alpha *= 1-smoothstep(max(.0001,_MaskRadius*.75),max(.0002,_MaskRadius),distance(input.worldXY,_MaskCenter.xy));
                return half4(_FogColor.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
