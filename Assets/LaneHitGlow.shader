Shader "RuruInBeat/LaneHitGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.75, 0.7, 1, 1)
        _Intensity ("Intensity", Range(0, 3)) = 1.5
        _SideSoftness ("Side Softness", Range(0.01, 0.5)) = 0.18
        _BottomSoftness ("Bottom Softness", Range(0.01, 0.5)) = 0.08
        _TopFadeStart ("Top Fade Start", Range(0, 1)) = 0.35
        _CenterBoost ("Center Boost", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off

        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float _Intensity;
            float _SideSoftness;
            float _BottomSoftness;
            float _TopFadeStart;
            float _CenterBoost;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 좌우 가장자리 부드럽게
                float sideDistance =
                    min(uv.x, 1.0 - uv.x);

                float sideFade =
                    smoothstep(
                        0.0,
                        _SideSoftness,
                        sideDistance
                    );

                // 아래쪽도 딱 잘린 사각형처럼 보이지 않게
                float bottomFade =
                    smoothstep(
                        0.0,
                        _BottomSoftness,
                        uv.y
                    );

                // 위로 갈수록 자연스럽게 사라짐
                float topFade =
                    1.0 -
                    smoothstep(
                        _TopFadeStart,
                        1.0,
                        uv.y
                    );

                // 가운데 부분을 조금 더 밝게
                float center =
                    1.0 -
                    abs(uv.x - 0.5) * 2.0;

                center = saturate(center);

                float centerGlow =
                    1.0 +
                    center *
                    _CenterBoost;

                float alpha =
                    sideFade *
                    bottomFade *
                    topFade *
                    i.color.a;

                fixed4 color =
                    _Color *
                    i.color;

                color.rgb *=
                    _Intensity *
                    centerGlow;

                color.a = alpha;

                return color;
            }

            ENDCG
        }
    }
}