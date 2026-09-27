// Rivers, leylines and map markers over the world's ground (WorldRenderer). Each vertex carries its anchor point and
// an offset direction; the width is the larger of a world width and a minimum in pixels, so lines and markers stay
// legible at every zoom while their paths never change. Each mark shows only within its range of _Level (a minor
// river leaves the atlas view) and only where the map knows the ground (the knowledge texture, by meso cell).
Shader "Hidden/GatewayToGenesis/WorldMarks"
{
    Properties
    {
        _FogTex ("Knowledge (by meso cell)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            float4 _MesoInfo;
            float _Level, _WorldPerPixel, _Pulse;

            struct appdata
            {
                float4 vertex : POSITION;   // anchor (x, y)
                float3 normal : NORMAL;     // offset direction (x, y), already scaled for miters
                float4 color : COLOR;
                float4 size : TEXCOORD0;    // x: world width, y: minimum pixels, z: shape (0 line), w: fog mode (0 hide in fog, 1 always)
                float4 range : TEXCOORD1;   // x, y: visible _Level range; zw: meso cell of the anchor
                float2 local : TEXCOORD2;   // position across the mark (-1..1)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 local : TEXCOORD0;
                float shape : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float width = max(v.size.x, v.size.y * _WorldPerPixel);
                float4 anchor = v.vertex;
                anchor.xy += v.normal.xy * width * 0.5;
                o.pos = UnityObjectToClipPos(anchor);
                float fade = saturate((_Level - v.range.x) * 4.0 + 1.0) * saturate((v.range.y - _Level) * 4.0 + 1.0);
                float2 uv = (v.range.zw + _MesoInfo.xx + 0.5) / _MesoInfo.y;
                float knowledge = tex2Dlod(_FogTex, float4(uv, 0, 0)).r;
                float seen = v.size.w > 0.5 ? 1.0 : (knowledge > 0.7 ? 1.0 : knowledge > 0.25 ? 0.6 : 0.0);
                o.color = v.color;
                o.color.a *= fade * seen;
                o.local = v.local;
                o.shape = v.size.z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = i.local;
                float a = 1.0;
                float r = length(l);
                if (i.shape < 0.5) a = smoothstep(1.0, 0.55, abs(l.y));                 // line
                else if (i.shape < 1.5) a = smoothstep(1.0, 0.8, r);                     // dot
                else if (i.shape < 2.5) a = smoothstep(1.0, 0.85, r) * smoothstep(0.45, 0.6, r); // ring
                else if (i.shape < 3.5) a = smoothstep(1.0, 0.85, abs(l.x) + abs(l.y)); // diamond
                else if (i.shape < 4.5)                                                  // double ring (a Basin)
                    a = max(smoothstep(1.0, 0.88, r) * smoothstep(0.7, 0.8, r), smoothstep(0.5, 0.4, r) * smoothstep(0.2, 0.3, r));
                else if (i.shape < 5.5)                                                  // seven-pointed star (the capital)
                {
                    float ang = atan2(l.y, l.x);
                    float star = 0.62 + 0.38 * cos(ang * 7.0);
                    a = smoothstep(1.0, 0.9, r / star) ;
                }
                else                                                                     // a tent (a unit's camp)
                {
                    float tent = max(-l.y - 0.6, abs(l.x) - (0.8 - l.y) * 0.5714);
                    a = smoothstep(0.08, -0.02, tent);
                }
                float4 c = i.color;
                c.a *= a;
                return c;
            }
            ENDCG
        }
    }
}
