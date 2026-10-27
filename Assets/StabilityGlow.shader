Shader "LuluInBeat/StabilityGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.8, 0.6, 1, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0
        _Softness ("Softness", Range(0.1, 5)) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Intensity;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex =
                    UnityObjectToClipPos(v.vertex);

                o.uv =
                    v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered =
                    i.uv - 0.5;

                // 좌우로 부드럽게 사라짐
                float horizontal =
                    1.0 -
                    smoothstep(
                        0.0,
                        0.5,
                        abs(centered.x)
                    );

                // 위아래 끝도 부드럽게 사라짐
                float vertical =
                    1.0 -
                    smoothstep(
                        0.35,
                        0.5,
                        abs(centered.y)
                    );

                float glow =
                    pow(
                        horizontal,
                        _Softness
                    ) *
                    vertical;

                fixed4 color =
                    _Color;

                color.rgb *=
                    _Intensity;

                color.a =
                    glow *
                    _Color.a *
                    _Intensity;

                return color;
            }

            ENDCG
        }
    }
}