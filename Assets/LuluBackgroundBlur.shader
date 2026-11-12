
Shader "UI/LuluBackgroundBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurRadius ("Blur Radius", Range(0,20)) = 9
        _Darkness ("Darkness", Range(0,1)) = 0.38
        _Saturation ("Saturation", Range(0,1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _BlurRadius;
            float _Darkness;
            float _Saturation;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _BlurRadius;

                fixed4 col = tex2D(_MainTex, i.uv) * 4.0;

                col += tex2D(_MainTex, i.uv + float2( d.x, 0)) * 2.0;
                col += tex2D(_MainTex, i.uv + float2(-d.x, 0)) * 2.0;
                col += tex2D(_MainTex, i.uv + float2(0,  d.y)) * 2.0;
                col += tex2D(_MainTex, i.uv + float2(0, -d.y)) * 2.0;

                col += tex2D(_MainTex, i.uv + float2( d.x,  d.y));
                col += tex2D(_MainTex, i.uv + float2(-d.x,  d.y));
                col += tex2D(_MainTex, i.uv + float2( d.x, -d.y));
                col += tex2D(_MainTex, i.uv + float2(-d.x, -d.y));

                col /= 16.0;

                float luminance = dot(
                    col.rgb,
                    float3(0.299, 0.587, 0.114)
                );

                col.rgb = lerp(
                    luminance.xxx,
                    col.rgb,
                    _Saturation
                );

                col.rgb *= (1.0 - _Darkness);

                return col * i.color;
            }
            ENDCG
        }
    }
}
