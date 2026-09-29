// Rivers, leylines and map markers over the world's ground (WorldRenderer). Each vertex carries its anchor point and
// an offset direction; the width is the larger of a world width and a minimum in pixels, so lines and markers stay
// legible at every zoom while their paths never change. Each mark shows only within its range of _Level (a minor
// river leaves the atlas view) and only where the map knows the ground (the knowledge texture, by meso cell).
// Vertex colours are sRGB (converted for the linear colour space). Markers wear a dark outline so they read on any
// ground; lines darken toward their banks.
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

            // How much larger a marker's quad is than its shape, to hold the outline.
            #define OUTLINE 1.28

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
                // Markers (not lines, not the glow) grow to make room for their outline.
                bool outlined = v.size.z > 0.5 && (v.size.z < 6.5 || v.size.z > 7.5);
                if (outlined) width *= OUTLINE;
                float4 anchor = v.vertex;
                anchor.xy += v.normal.xy * width * 0.5;
                o.pos = UnityObjectToClipPos(anchor);
                float fade = saturate((_Level - v.range.x) * 4.0 + 1.0) * saturate((v.range.y - _Level) * 4.0 + 1.0);
                float2 uv = (v.range.zw + _MesoInfo.xx + 0.5) / _MesoInfo.y;
                float knowledge = tex2Dlod(_FogTex, float4(uv, 0, 0)).r;
                float seen = v.size.w > 0.5 ? 1.0 : (knowledge > 0.7 ? 1.0 : knowledge > 0.25 ? 0.6 : 0.0);
                o.color = v.color;
            #ifndef UNITY_COLORSPACE_GAMMA
                o.color.rgb = GammaToLinearSpace(o.color.rgb);
            #endif
                o.color.a *= fade * seen;
                o.local = outlined ? v.local * OUTLINE : v.local;
                o.shape = v.size.z;
                return o;
            }

            // Signed distance to a convex polygon's edge (positive outside; the corners listed counter-clockwise).
            float Edge(float2 p, float2 a, float2 b) { float2 e = b - a; return dot(p - a, normalize(float2(e.y, -e.x))); }
            float Tri(float2 p, float2 a, float2 b, float2 c) { return max(max(Edge(p, a, b), Edge(p, b, c)), Edge(p, c, a)); }

            // A band's mark by what it is (8 creature, 9 Atonalis, 10 humans, 11 demihumans, 12 humanoids), as a signed distance.
            float Band(float shape, float2 l)
            {
                if (shape < 8.5)                                                                   // a paw print (a creature)
                {
                    float pad = length((l - float2(0.0, -0.3)) * float2(1.0, 1.3)) - 0.48;
                    float toes = min(min(length(l - float2(-0.64, 0.12)) - 0.2, length(l - float2(-0.24, 0.52)) - 0.21),
                                     min(length(l - float2(0.24, 0.52)) - 0.21, length(l - float2(0.64, 0.12)) - 0.2));
                    return min(pad, toes);
                }
                if (shape < 9.5)                                                                   // a jagged burst of static round a hollow (an Atonalis)
                {
                    float r = length(l);
                    float k = atan2(l.y, l.x) * 9.0 / 6.2831853 + 0.25;
                    float spike = lerp(0.42, 1.0, pow(abs(frac(k) * 2.0 - 1.0), 1.6)) * (fmod(floor(k) + 20.0, 2.0) < 0.5 ? 1.0 : 0.76);
                    return max((r - spike) * 0.6, 0.26 - r);
                }
                if (shape < 10.5)                                                                  // a heater shield (humans)
                {
                    float2 m = float2(abs(l.x), l.y);
                    return max(m.y - 0.8, m.y > 0.1 ? m.x - 0.74 : length(m - float2(-0.45, 0.1)) - 1.19);
                }
                if (shape < 11.5)                                                                  // a horned head (demihumans)
                {
                    float2 m = float2(abs(l.x), l.y);
                    return min(length(l - float2(0.0, -0.28)) - 0.52, Tri(m, float2(0.18, 0.1), float2(0.58, -0.04), float2(0.84, 0.95)));
                }
                if (shape < 12.5)                                                                  // a hooded figure (humanoids)
                {
                    float head = length(l - float2(0.0, 0.5)) - 0.3;
                    float body = max(max(-0.95 - l.y, l.y - 0.12), abs(l.x) - lerp(0.72, 0.36, saturate((l.y + 0.95) / 1.07)));
                    return min(head, body);
                }
                if (shape < 13.5)                                                                  // a lumpy, dripping puddle (a Formless Mass)
                {
                    float a = atan2(l.y, l.x);
                    float blob = length(float2(l.x, l.y * 1.3 + 0.18)) - (0.7 + 0.1 * sin(a * 3.0) + 0.07 * sin(a * 5.0 + 1.3));
                    return min(blob, min(length(l - float2(0.42, 0.55)) - 0.15, length(l - float2(-0.3, 0.62)) - 0.1));
                }
                if (shape < 14.5)                                                                  // a fish (a creature of the water)
                {
                    float body = (length(float2((l.x + 0.12) / 0.72, l.y / 0.46)) - 1.0) * 0.46;
                    float tail = Tri(l, float2(0.42, 0.0), float2(0.95, -0.5), float2(0.95, 0.5));
                    return max(min(body, tail), 0.09 - length(l - float2(-0.48, 0.1)));
                }
                float shell = (length(float2(l.x / 0.6, l.y / 0.95)) - 1.0) * 0.6;                // a cocoon, split by its seam
                return max(shell, 0.05 - abs(l.x + 0.12 * sin(l.y * 6.0)));
            }

            // The coverage of a marker's shape at a point (-1..1 across it).
            float Shape(float shape, float2 l)
            {
                float r = length(l);
                if (shape < 1.5) return smoothstep(1.0, 0.8, r);                                   // dot
                if (shape < 2.5) return smoothstep(1.0, 0.85, r) * smoothstep(0.45, 0.6, r);      // ring
                if (shape < 3.5) return smoothstep(1.0, 0.85, abs(l.x) + abs(l.y));               // diamond
                if (shape < 4.5)                                                                   // double ring (a Basin)
                    return max(smoothstep(1.0, 0.88, r) * smoothstep(0.7, 0.8, r), smoothstep(0.5, 0.4, r) * smoothstep(0.2, 0.3, r));
                if (shape > 7.5) return smoothstep(0.05, -0.03, Band(shape, l));
                if (shape < 5.5)                                                                   // seven-pointed star (the capital)
                {
                    float ang = atan2(l.y, l.x) - 1.5707963;
                    float star = lerp(0.38, 1.0, abs(frac(ang * 7.0 / 6.2831853 + 0.5) * 2.0 - 1.0));
                    return smoothstep(1.0, 0.9, r / star);
                }
                float tent = max(-l.y - 0.6, abs(l.x) - (0.8 - l.y) * 0.5714);                     // a tent (a unit's camp)
                return smoothstep(0.08, -0.02, tent);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = i.local;
                float4 c = i.color;
                if (i.shape < 0.5)
                {
                    // A line: full in its middle, darker toward its banks.
                    float across = abs(l.y);
                    c.rgb *= lerp(1.0, 0.45, smoothstep(0.3, 0.9, across));
                    c.a *= smoothstep(1.0, 0.75, across);
                    return c;
                }
                if (i.shape > 6.5 && i.shape < 7.5)
                {
                    // A soft glow (the capital's light).
                    float r = length(l);
                    c.a *= pow(saturate(1.0 - r), 2.2);
                    return c;
                }
                // The shape, and the same shape a little larger in dark ink behind it.
                float inner = Shape(i.shape, l);
                float outer = Shape(i.shape, l / OUTLINE);
                if (i.shape > 1.5 && i.shape < 2.5) outer = max(outer, Shape(i.shape, l / 1.12));
                float3 ink = float3(0.012, 0.01, 0.012);
                c.rgb = lerp(ink, c.rgb, inner);
                c.a *= max(inner, outer * 0.9);
                return c;
            }
            ENDCG
        }
    }
}
