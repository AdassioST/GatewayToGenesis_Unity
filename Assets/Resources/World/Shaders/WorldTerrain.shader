// The world's ground at all three scales (WorldRenderer, WORLD_GENERATION.md §3). One quad covers the world; each
// fragment finds its micro hex from its world position, walks the index-7 lattice up to its meso cell and macro
// aggregate (HexHierarchy, the same arithmetic as the C#), and reads their colours from point-sampled textures.
// _Level blends the readings (0 micro, 1 meso, 2 macro), so zooming never regenerates or moves anything. Borders
// are found by asking whether a point a pixel away belongs to another hex, cell or aggregate: a parent's outline is
// the true union of its children. At the micro reading, knowledge is read per micro hex (the hexes units walked and
// surveyed), crags (micro colour alpha 0.5) are hatched, and the hex under the cursor and a unit's destination glow.
Shader "Hidden/GatewayToGenesis/WorldTerrain"
{
    Properties
    {
        _MicroTex ("Micro colours (alpha 0.5: a crag)", 2D) = "black" {}
        _MesoTex ("Meso colours", 2D) = "black" {}
        _MacroTex ("Macro colours (by meso cell)", 2D) = "black" {}
        _FogTex ("Knowledge (by meso cell)", 2D) = "black" {}
        _MicroFogTex ("Knowledge (by micro hex)", 2D) = "black" {}
        _OverlayTex ("Overlay (by meso cell)", 2D) = "black" {}
        _StateTex ("Selection, frontier, expeditions (by meso cell)", 2D) = "black" {}
        _ReliefTex ("Escarpments, basin mist, auric grass", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MicroTex, _MesoTex, _MacroTex, _FogTex, _OverlayTex, _StateTex;
            sampler2D _ReliefTex, _MicroFogTex;
            float4 _MicroInfo;   // x: offset, y: size
            float4 _MesoInfo;    // x: offset, y: size
            float4 _HoverMicro;  // xy: the micro hex under the cursor, z: 1 to show it
            float4 _TargetMicro; // xy: the selected unit's destination, z: 1 to show it
            float _Level, _WorldPerPixel, _OverlayAlpha, _Pulse;
            float4 _FogColor, _VoidColor, _LineColor, _SelectColor, _FrontierColor, _ExpeditionColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = v.vertex.xy;
                return o;
            }

            float2 HexRound(float2 f)
            {
                float3 c = float3(f.x, -f.x - f.y, f.y);
                float3 r = round(c);
                float3 d = abs(r - c);
                if (d.x > d.y && d.x > d.z) r.x = -r.y - r.z;
                else if (d.y > d.z) r.y = -r.x - r.z;
                else r.z = -r.x - r.y;
                return float2(r.x, r.z);
            }

            // HexCoord.FromPixel with circumradius 1 (y grows up).
            float2 MicroAt(float2 p)
            {
                return HexRound(float2(0.57735027 * p.x + p.y / 3.0, -0.66666667 * p.y));
            }

            // HexHierarchy.Parent: the offset whose residue (3q + r) mod 7 matches, then F^-1.
            float2 Parent(float2 c)
            {
                float k = round(fmod(fmod(3.0 * c.x + c.y, 7.0) + 7.0, 7.0));
                float2 o = k == 3 ? float2(1, 0) : k == 1 ? float2(0, 1) : k == 5 ? float2(-1, 1) : k == 4 ? float2(-1, 0) : k == 6 ? float2(0, -1) : k == 2 ? float2(1, -1) : float2(0, 0);
                float2 d = c - o;
                return round(float2((3.0 * d.x + d.y) / 7.0, (-d.x + 2.0 * d.y) / 7.0));
            }

            float4 Cell(sampler2D t, float2 cell, float4 info)
            {
                float2 uv = (cell + info.xx + 0.5) / info.y;
                return tex2Dlod(t, float4(uv, 0, 0));
            }

            float Differs(float2 a, float2 b)
            {
                return any(a != b) ? 1.0 : 0.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world;
                float2 micro = MicroAt(p);
                float2 meso = Parent(micro);
                float2 macro = Parent(Parent(meso));

                float4 mesoColor = Cell(_MesoTex, meso, _MesoInfo);
                if (mesoColor.a < 0.3) return _VoidColor;
                bool water = mesoColor.a < 0.8;
                float4 microColor = Cell(_MicroTex, micro, _MicroInfo);
                float4 macroColor = Cell(_MacroTex, meso, _MesoInfo);

                float lv = _Level;
                float3 col = lerp(microColor.rgb, mesoColor.rgb, saturate(lv));
                col = lerp(col, macroColor.rgb, saturate(lv - 1.0));

                // Subtle strata and slow basin mist, before lenses and knowledge so they reveal nothing in fog.
                float3 relief = Cell(_ReliefTex, meso, _MesoInfo).rgb;
                float detail = saturate(1.6 - lv);
                float strata = smoothstep(0.72, 0.96, sin(p.y * 5.0 + sin(p.x * 0.7)) * 0.5 + 0.5);
                col *= 1.0 - strata * relief.r * detail * 0.18;
                float haze = sin(p.x * 0.12 + p.y * 0.24 + _Time.y * 0.035) *
                    sin(p.y * 0.09 - p.x * 0.17 - _Time.y * 0.022) * 0.5 + 0.5;
                col = lerp(col, float3(0.48, 0.52, 0.57), relief.g * haze * detail * 0.14);
                float mote = frac(sin(dot(floor(p * 5.0), float2(12.9898, 78.233))) * 43758.5453);
                col += float3(0.16, 0.115, 0.035) * step(0.975, mote) * relief.b * detail;

                float4 overlay = Cell(_OverlayTex, meso, _MesoInfo);
                col = lerp(col, overlay.rgb, overlay.a * _OverlayAlpha);

                // Borders one pixel and a bit away, in six directions.
                float w = _WorldPerPixel * 1.3;
                float eMicro = 0, eMeso = 0, eMacro = 0;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.0471976;
                    float2 q = p + float2(cos(a), sin(a)) * w;
                    float2 m2 = MicroAt(q);
                    float dm = Differs(m2, micro);
                    eMicro = max(eMicro, dm);
                    if (dm > 0)
                    {
                        float2 me2 = Parent(m2);
                        float dme = Differs(me2, meso);
                        eMeso = max(eMeso, dme);
                        if (dme > 0) eMacro = max(eMacro, Differs(Parent(Parent(me2)), macro));
                    }
                }

                // Knowledge: per micro hex at the micro reading (what units walked and surveyed), per cell beyond.
                float knowledge = lerp(Cell(_MicroFogTex, micro, _MicroInfo).r, Cell(_FogTex, meso, _MesoInfo).r, saturate(lv));
                float4 state = Cell(_StateTex, meso, _MesoInfo);
                float wet = water ? 0.3 : 1.0;
                float aMicro = 0.17 * saturate(1.0 - lv * 1.6) * wet;
                float aMeso = 0.38 * saturate(1.75 - lv) * wet;
                float aMacro = 0.3 * saturate(lv - 0.75) * (water ? 0.35 : 1.0);

                // Crags: broken rock hatched across the hex at the close readings.
                if (microColor.a < 0.75 && knowledge >= 0.25)
                {
                    float hatch = step(0.6, frac((p.x * 0.5 + p.y * 0.866) * 3.4));
                    col *= 1.0 - 0.3 * hatch * saturate(1.4 - lv);
                }

                if (knowledge < 0.25)
                {
                    // The fog: the atlas outlines only.
                    col = lerp(_FogColor.rgb, col, 0.1);
                    aMicro = 0;
                    aMeso *= 0.25;
                    aMacro *= 0.45;
                }
                else if (knowledge < 0.6)
                {
                    // Unknown wilderness: seen from afar, never walked. Dark and nearly grey.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, 0.3) * 0.55;
                }
                else if (knowledge < 0.7)
                {
                    // Glimpsed: a hex of known ground no unit has walked on or beside yet.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, 0.55) * 0.7;
                }
                else if (knowledge < 0.9)
                {
                    // Known: a scout passed over it. Its colours, a little muted until it is surveyed.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, 0.8) * 0.86;
                }

                float edge = max(max(eMicro * aMicro, eMeso * aMeso), eMacro * aMacro);
                col = lerp(col, _LineColor.rgb, edge);

                // The hex under the cursor (at the micro reading) and the selected unit's destination (always).
                float microLevel = saturate(1.3 - lv);
                if (_HoverMicro.z > 0.5 && all(micro == _HoverMicro.xy))
                    col = lerp(col, _SelectColor.rgb, (0.14 + eMicro * 0.7) * microLevel);
                if (_TargetMicro.z > 0.5 && all(micro == _TargetMicro.xy))
                    col = lerp(col, _ExpeditionColor.rgb, 0.22 + 0.18 * _Pulse + eMicro * 0.6 * microLevel);

                // Frontier, expeditions and the selection, drawn on the inner side of the cell's outline.
                float cellLevel = saturate(1.8 - lv);
                if (state.g > 0.5) col = lerp(col, _FrontierColor.rgb, eMeso * 0.8 * cellLevel);
                if (state.b > 0.5)
                {
                    col = lerp(col, _ExpeditionColor.rgb, 0.18 + 0.12 * _Pulse);
                    col = lerp(col, _ExpeditionColor.rgb, eMeso * cellLevel);
                }
                if (state.r > 0.5)
                {
                    col = lerp(col, _SelectColor.rgb, 0.16);
                    col = lerp(col, _SelectColor.rgb, eMeso * max(cellLevel, 0.6));
                }
                if (state.a > 0.5) col = lerp(col, _SelectColor.rgb, 0.12 + eMacro * 0.8);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
