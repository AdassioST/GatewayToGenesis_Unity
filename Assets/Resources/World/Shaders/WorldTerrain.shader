// The world's ground at all three scales (WorldRenderer, WORLD_GENERATION.md §3). One quad covers the world; each
// fragment finds its micro hex from its world position, walks the index-7 lattice up to its meso cell and macro
// aggregate (HexHierarchy, the same arithmetic as the C#), and reads their colours from point-sampled textures.
// _Level blends the readings (0 micro, 1 meso, 2 macro), so zooming never regenerates or moves anything. Borders
// are found by asking whether a point a pixel away belongs to another hex, cell or aggregate: a parent's outline is
// the true union of its children. At the micro reading, knowledge is read per micro hex (the hexes units walked and
// surveyed), crags (micro colour alpha 0.5) are hatched, and the hex under the cursor and a unit's destination glow.
//
// The look is dark fantasy: the unknown is a sea of drifting storm cloud, known land stands in raised hex plateaus lit
// from the north-west (a bright rim toward the light, a shadowed one away from it), coasts drop to the sea in dark
// cliff faces fringed with foam, slow cloud shadows pass over the land, and the frame darkens toward its edges.
// Colours arrive as sRGB (the colour textures are sRGB; constants go through Lin), so the project's linear colour
// space shows them as authored.
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
        _OwnerTex ("Holder colour (alpha: holder code) by micro hex", 2D) = "black" {}
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
            sampler2D _ReliefTex, _MicroFogTex, _OwnerTex;
            float _BorderAlpha;
            float4 _KnowledgeSaturation; // wilderness, glimpsed, known, surveyed
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
                float4 screen : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = v.vertex.xy;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            // An sRGB colour in the space the shader works in.
            float3 Lin(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
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
                if (any(uv < 0.0) || any(uv > 1.0)) return 0;
                return tex2Dlod(t, float4(uv, 0, 0));
            }

            float Differs(float2 a, float2 b)
            {
                return any(a != b) ? 1.0 : 0.0;
            }

            // Value noise and its sum over octaves (world-space, so clouds and shadows pan with the map).
            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p, int octaves)
            {
                float v = 0, a = 0.5;
                [loop] for (int k = 0; k < octaves; k++)
                {
                    v += a * Noise(p);
                    p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6) + 7.31;
                    a *= 0.5;
                }
                return v;
            }

            // The storm over the unknown: dark billows whose crowns catch a cold light and, here and there, gold.
            float3 Clouds(float2 p, float dim)
            {
                float t = _Time.y;
                float2 q = p * 0.018 + float2(t * 0.006, t * 0.0025);
                float warp = Fbm(q * 1.7 + 3.1, 3);
                float c = Fbm(q + warp * 0.9, 6);
                float3 ink = Lin(float3(0.028, 0.03, 0.045));
                float3 slate = Lin(float3(0.13, 0.13, 0.17));
                float3 crown = Lin(float3(0.34, 0.31, 0.3));
                float3 gold = Lin(float3(0.55, 0.42, 0.22));
                float3 col = lerp(ink, slate, smoothstep(0.3, 0.62, c));
                col = lerp(col, crown, smoothstep(0.58, 0.8, c) * 0.55);
                col = lerp(col, gold, smoothstep(0.7, 0.9, c) * smoothstep(0.45, 0.7, warp) * 0.45);
                return col * dim;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world;
                float2 sp = i.screen.xy / max(i.screen.w, 1e-5);
                float2 sd = (sp - 0.5) * float2(1.0, 0.85);
                float vignette = lerp(1.0, 0.42, smoothstep(0.18, 0.72, length(sd) * 1.25));

                float2 micro = MicroAt(p);
                float2 meso = Parent(micro);
                float2 macro = Parent(Parent(meso));

                float4 mesoColor = Cell(_MesoTex, meso, _MesoInfo);
                if (mesoColor.a < 0.3) return fixed4(Clouds(p, 1.0) * vignette, 1);
                bool water = mesoColor.a < 0.8;
                float4 microColor = Cell(_MicroTex, micro, _MicroInfo);
                float4 macroColor = Cell(_MacroTex, meso, _MesoInfo);

                float lv = _Level;
                float3 col = lerp(microColor.rgb, mesoColor.rgb, saturate(lv));
                col = lerp(col, macroColor.rgb, saturate(lv - 1.0));

                // Broad stone facets with finer mineral grain. Anchored to the world, filtered out at atlas
                // scale; no animated sparkle or screen-space noise when the camera moves.
                if (!water)
                {
                    float rock = Noise(p * 1.7 + Noise(p * 0.43) * 2.0);
                    float facets = floor(rock * 6.0) / 5.0;
                    float grain = Noise(p * 9.0);
                    float textureFade = saturate(1.7 - lv) * (1.0 - smoothstep(0.07, 0.25, _WorldPerPixel));
                    col *= lerp(1.0, 0.74 + facets * 0.48 + grain * 0.1, textureFade);
                }

                // Subtle strata and slow basin mist, before lenses and knowledge so they reveal nothing in fog.
                float3 relief = Cell(_ReliefTex, meso, _MesoInfo).rgb;
                float detail = saturate(1.6 - lv);
                float strata = smoothstep(0.72, 0.96, sin(p.y * 5.0 + sin(p.x * 0.7)) * 0.5 + 0.5);
                col *= 1.0 - strata * relief.r * detail * 0.22;
                float haze = sin(p.x * 0.12 + p.y * 0.24 + _Time.y * 0.035) *
                    sin(p.y * 0.09 - p.x * 0.17 - _Time.y * 0.022) * 0.5 + 0.5;
                col = lerp(col, Lin(float3(0.42, 0.46, 0.52)), relief.g * haze * detail * 0.14);
                float mote = frac(sin(dot(floor(p * 5.0), float2(12.9898, 78.233))) * 43758.5453);
                col += Lin(float3(0.3, 0.22, 0.07)) * step(0.975, mote) * relief.b * detail;

                // Water: a slow shimmer on the swell.
                if (water)
                {
                    float swell = Noise(p * 0.9 + float2(_Time.y * 0.12, _Time.y * 0.05)) * Noise(p * 0.45 - float2(_Time.y * 0.05, 0));
                    col += Lin(float3(0.1, 0.2, 0.2)) * smoothstep(0.35, 0.7, swell) * 0.5 * detail;
                }

                float4 overlay = Cell(_OverlayTex, meso, _MesoInfo);
                col = lerp(col, overlay.rgb, overlay.a * _OverlayAlpha);

                // Borders one pixel and a bit away, in six directions; a neighbouring cell of land seen from the
                // water marks the shore (foam).
                float w = _WorldPerPixel * 1.3;
                float eMicro = 0, eMeso = 0, eMacro = 0, shore = 0;
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
                        if (dme > 0)
                        {
                            eMacro = max(eMacro, Differs(Parent(Parent(me2)), macro));
                            if (water && Cell(_MesoTex, me2, _MesoInfo).a > 0.8) shore = 1.0;
                        }
                    }
                }

                // Raised plateaus: a cell's rim toward the light (north-west) is lit, the rim away from it shadowed.
                // The same at one or two pixels on each micro hex at the close readings: embossed tiles.
                float2 light = float2(-0.6, 0.8);
                float bevel = clamp(_WorldPerPixel * 3.5, 0.06, 0.55);
                float lit = Differs(Parent(MicroAt(p + light * bevel)), meso);
                float shade = Differs(Parent(MicroAt(p - light * bevel)), meso);
                float microLit = Differs(MicroAt(p + light * _WorldPerPixel * 2.2), micro);
                float microShade = Differs(MicroAt(p - light * _WorldPerPixel * 2.2), micro);
                float plateau = saturate(2.0 - lv);
                float closeup = saturate(1.2 - lv);
                if (!water)
                {
                    col *= 1.0 + (lit * 0.38 + microLit * 0.10 * closeup) * plateau;
                    col *= 1.0 - (shade * 0.55 + microShade * 0.16 * closeup) * plateau;
                }

                // Coasts: land drops to the sea in a dark cliff face (under the land, to the south), fringed with foam.
                float cliff = 0;
                if (water)
                {
                    float drop = clamp(_WorldPerPixel * 7.0, 0.12, 0.9);
                    float4 above = Cell(_MesoTex, Parent(MicroAt(p + float2(0, drop))), _MesoInfo);
                    float4 near = Cell(_MesoTex, Parent(MicroAt(p + float2(0, drop * 0.45))), _MesoInfo);
                    cliff = above.a > 0.8 ? (near.a > 0.8 ? 1.0 : 0.6) : 0.0;
                }

                // Knowledge: per micro hex at the micro reading (what units walked and surveyed), per cell beyond.
                float knowledge = lerp(Cell(_MicroFogTex, micro, _MicroInfo).r, Cell(_FogTex, meso, _MesoInfo).r, saturate(lv));
                float4 state = Cell(_StateTex, meso, _MesoInfo);
                float wet = water ? 0.3 : 1.0;
                float aMicro = 0.3 * saturate(1.0 - lv * 1.6) * wet;
                float aMeso = 0.55 * saturate(1.75 - lv) * wet;
                float aMacro = 0.4 * saturate(lv - 0.75) * (water ? 0.35 : 1.0);

                // Crags: broken rock hatched across the hex at the close readings.
                if (microColor.a < 0.75 && knowledge >= 0.25)
                {
                    float hatch = step(0.6, frac((p.x * 0.5 + p.y * 0.866) * 3.4));
                    col *= 1.0 - 0.35 * hatch * saturate(1.4 - lv);
                }

                // Light over the land: slow cloud shadows drifting across it, warmer where the sun breaks through.
                float sun = smoothstep(0.32, 0.7, Fbm(p * 0.03 + float2(_Time.y * 0.004, -_Time.y * 0.002), 4));
                col *= lerp(Lin(float3(0.7, 0.74, 0.86)), Lin(float3(1.06, 1.0, 0.9)), sun);

                // Cliff faces and foam, after the light (the cliff is always in shadow).
                if (cliff > 0)
                    col = lerp(col, Lin(float3(0.1, 0.075, 0.065)), 0.85 * cliff * saturate(2.2 - lv));
                if (shore > 0 && cliff < 0.5)
                {
                    float surf = 0.55 + 0.45 * sin(_Time.y * 1.3 + (p.x + p.y) * 2.1);
                    col = lerp(col, Lin(float3(0.72, 0.84, 0.82)), 0.55 * surf * saturate(1.9 - lv));
                }

                if (knowledge < 0.25)
                {
                    // The fog: the storm, with the atlas outlines only faintly beneath it.
                    col = Clouds(p, 1.0);
                    aMicro = 0;
                    aMeso = 0;
                    aMacro *= 0.3;
                }
                else if (knowledge < 0.6)
                {
                    // Unknown wilderness: seen from afar, never walked. Dark, moonlit, veiled in wisps of cloud.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, _KnowledgeSaturation.x) * Lin(float3(0.72, 0.76, 0.86));
                    col = lerp(col, Clouds(p, 1.0), 0.35 * smoothstep(0.3, 0.8, Fbm(p * 0.07 + _Time.y * 0.01, 3)));
                    aMicro *= 0.4;
                }
                else if (knowledge < 0.7)
                {
                    // Glimpsed: a hex of known ground no unit has walked on or beside yet.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, _KnowledgeSaturation.y) * 0.72;
                }
                else if (knowledge < 0.9)
                {
                    // Known: a scout passed over it. Its colours, a little muted until it is surveyed.
                    float grey = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(float3(grey, grey, grey), col, _KnowledgeSaturation.z) * 0.88;
                }

                float edge = max(max(eMicro * aMicro, eMeso * aMeso), eMacro * aMacro);
                col = lerp(col, _LineColor.rgb, edge);

                // Borders: where one holder's land meets another's or the wilderness, an outline in the holder's
                // colour on its own side, a crisp line with a faint glow inward (a few pixels at every zoom).
                // Land is held micro hex by micro hex, so the outline follows the hexes.
                float4 owner = Cell(_OwnerTex, micro, _MicroInfo);
                if (owner.a > 0.001 && knowledge >= 0.25)
                {
                    float border = 0;
                    [unroll] for (int b = 0; b < 6; b++)
                    {
                        float ang = b * 1.0471976 + 0.5235988;
                        float2 dir = float2(cos(ang), sin(ang));
                        float inner = Cell(_OwnerTex, MicroAt(p + dir * _WorldPerPixel * 1.6), _MicroInfo).a;
                        float outer = Cell(_OwnerTex, MicroAt(p + dir * _WorldPerPixel * 4.0), _MicroInfo).a;
                        if (abs(inner - owner.a) > 0.001) border = 1.0;
                        else if (abs(outer - owner.a) > 0.001) border = max(border, 0.4);
                    }
                    col = lerp(col, owner.rgb, border * _BorderAlpha);
                }

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

                return fixed4(col * vignette, 1);
            }
            ENDCG
        }
    }
}
