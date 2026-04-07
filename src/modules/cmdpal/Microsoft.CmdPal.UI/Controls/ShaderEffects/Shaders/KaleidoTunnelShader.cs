// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Kaleidoscope tunnel: infinite zoom into a mirrored fractal corridor.
/// Combines tunnel depth mapping (1/r) with kaleidoscope folds so the
/// pattern repeats infinitely as you fly forward. Audio drives the
/// wall patterns and color intensity while movement stays smooth.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct KaleidoTunnelShader(
    float time,
    int width,
    int height,
    float4 bands0,
    float4 bands1,
    float4 bands2) : ID2D1PixelShader
{
    public float4 Execute()
    {
        float2 pos = D2D.GetScenePosition().XY;
        float w = (float)width;
        float h = (float)height;
        float minDim = Hlsl.Min(w, h);

        // Stretch to fill the window
        float2 uv = new float2(
            (pos.X / w - 0.5f) * 2f,
            (pos.Y / h - 0.5f) * 2f);

        float bass = (bands0.X + bands0.Y + bands0.Z) * 0.333f;
        float mid = (bands0.W + bands1.X + bands1.Y + bands1.Z + bands1.W) * 0.2f;
        float high = (bands2.X + bands2.Y + bands2.Z + bands2.W) * 0.25f;
        float energy = (bass + mid + high) * 0.333f;

        // Slow rotation of the view
        float viewRot = time * 0.1f;
        float vc = Hlsl.Cos(viewRot);
        float vs = Hlsl.Sin(viewRot);
        float rx = uv.X * vc - uv.Y * vs;
        float ry = uv.X * vs + uv.Y * vc;

        float radius = Hlsl.Sqrt(rx * rx + ry * ry);
        radius = Hlsl.Max(radius, 0.001f);
        float angle = Hlsl.Atan2(ry, rx);

        // === TUNNEL DEPTH ===
        float depth = 1f / radius;
        float tunnelZ = depth + time * 2.5f;

        // === KALEIDOSCOPE FOLD on the angular coordinate ===
        float kAngle = angle + tunnelZ * 0.15f + time * 0.12f;
        float2 kp = KaleidoFold(kAngle, radius, 8f);

        // Second fold at different depth rate for layering
        float kAngle2 = angle - tunnelZ * 0.1f + time * 0.08f;
        float2 kp2 = KaleidoFold(kAngle2, radius, 6f);

        // === WARPED PATTERN in kaleidoscope space ===
        float warpStr = 0.2f + mid * 0.25f;
        float wpx = kp.X + warpStr * Hlsl.Sin(kp.Y * 2.3f + tunnelZ * 0.3f + time * 0.3f);
        float wpy = kp.Y + warpStr * Hlsl.Cos(kp.X * 1.9f + tunnelZ * 0.2f + time * 0.25f);
        wpx += warpStr * 0.4f * Hlsl.Sin(wpy * 3.5f + time * 0.4f);
        wpy += warpStr * 0.4f * Hlsl.Cos(wpx * 3.1f + time * 0.35f);

        // === FRACTAL TEXTURE (depth-tiled) ===
        float tileZ = Hlsl.Frac(tunnelZ * 0.08f);
        float pattern1 = Hlsl.Sin(wpx * 1.5f + tileZ * 12f + time * 0.2f)
                        * Hlsl.Cos(wpy * 1.3f + tileZ * 10f + time * 0.15f) * 0.5f + 0.5f;

        float nx = wpx * 0.866f - wpy * 0.5f;
        float ny = wpx * 0.5f + wpy * 0.866f;
        float pattern2 = Hlsl.Sin(nx * 2.4f + tileZ * 8f + time * 0.25f + 1.5f)
                        * Hlsl.Cos(ny * 2.1f - tileZ * 6f + time * 0.2f) * 0.5f + 0.5f;

        float pattern = pattern1 + pattern2 * 0.5f;

        // === SECOND LAYER PATTERN ===
        float wp2x = kp2.X + warpStr * 0.6f * Hlsl.Sin(kp2.Y * 2f + tunnelZ * 0.2f + 2f);
        float wp2y = kp2.Y + warpStr * 0.6f * Hlsl.Cos(kp2.X * 1.7f + tunnelZ * 0.15f + 2f);
        float pattern2nd = Hlsl.Sin(wp2x * 1.8f + tileZ * 9f + time * 0.3f)
                         * Hlsl.Cos(wp2y * 1.5f + tileZ * 7f) * 0.5f + 0.5f;

        // === TUNNEL RINGS (depth markers) ===
        float ringPhase = Hlsl.Frac(tunnelZ * 0.15f);
        float ring = Hlsl.Exp(-ringPhase * ringPhase * 120f)
                   + Hlsl.Exp(-(1f - ringPhase) * (1f - ringPhase) * 120f);
        ring *= 0.5f + 0.5f * energy;

        // === RADIAL LINES in kaleidoscope space ===
        float foldedAngle = kp.X;
        float ribPhase = Hlsl.Frac(Hlsl.Atan2(kp.Y, kp.X) * 2.387f + 0.5f);
        float rib = Hlsl.Exp(-(ribPhase - 0.5f) * (ribPhase - 0.5f) * 50f) * 0.4f;
        rib *= 0.3f + 0.7f * mid;

        // === DEPTH FOG ===
        float fog = Hlsl.Exp(-depth * 0.06f);
        float fogNear = Hlsl.Lerp(0.3f, 1f, fog);

        // === COLORING — hue from kaleidoscope angle + depth ===
        float hue1 = Hlsl.Frac(Hlsl.Atan2(kp.Y, kp.X) * 0.159f + tunnelZ * 0.02f + time * 0.02f);
        float hue2 = Hlsl.Frac(Hlsl.Atan2(kp2.Y, kp2.X) * 0.159f - tunnelZ * 0.015f + time * 0.03f + 0.33f);
        float hueRing = Hlsl.Frac(tunnelZ * 0.03f + time * 0.015f + 0.6f);

        float3 col1 = HueToRGB(hue1);
        float3 col2 = HueToRGB(hue2);
        float3 colRing = HueToRGB(hueRing);

        // Composite color
        float i1 = pattern * 0.45f * (0.4f + bass * 0.6f);
        float i2 = pattern2nd * 0.25f;
        float iRing = ring * 0.5f;
        float iRib = rib;

        float3 color = new float3(
            col1.X * i1 + col2.X * i2 + colRing.X * iRing + col1.X * iRib,
            col1.Y * i1 + col2.Y * i2 + colRing.Y * iRing + col1.Y * iRib,
            col1.Z * i1 + col2.Z * i2 + colRing.Z * iRing + col1.Z * iRib);

        // Apply fog
        color = new float3(color.X * fogNear, color.Y * fogNear, color.Z * fogNear);

        // === CENTER LIGHT (vanishing point glow) ===
        float centerGlow = Hlsl.Exp(-radius * radius * 2f) * (0.3f + energy * 0.5f);
        float3 glowCol = HueToRGB(Hlsl.Frac(time * 0.02f + 0.5f));
        color = new float3(
            color.X + glowCol.X * centerGlow,
            color.Y + glowCol.Y * centerGlow,
            color.Z + glowCol.Z * centerGlow);

        // === SHIMMER on high frequencies ===
        float shimmer = Hlsl.Sin(tunnelZ * 15f + angle * 8f + time * 1.5f) * 0.5f + 0.5f;
        shimmer = Hlsl.Step(0.88f, shimmer) * high * 0.3f;
        float3 shimCol = HueToRGB(Hlsl.Frac(hue1 + 0.2f));
        color = new float3(
            color.X + shimCol.X * shimmer * fog,
            color.Y + shimCol.Y * shimmer * fog,
            color.Z + shimCol.Z * shimmer * fog);

        // Energy boost
        float boost = 1f + energy * 0.6f;
        color = new float3(color.X * boost, color.Y * boost, color.Z * boost);

        // Vignette
        float vig = 1f - Hlsl.Saturate((radius - 0.6f) * 0.8f);
        vig = vig * vig;
        color = new float3(color.X * vig, color.Y * vig, color.Z * vig);

        float cr = Hlsl.Saturate(color.X);
        float cg = Hlsl.Saturate(color.Y);
        float cb = Hlsl.Saturate(color.Z);
        float ca = Hlsl.Max(cr, Hlsl.Max(cg, cb));

        // Boost with audio: baseline visible, audio intensifies
        float intensity = 0.35f + 1.5f * energy;
        return new float4(Hlsl.Saturate(cr * intensity), Hlsl.Saturate(cg * intensity), Hlsl.Saturate(cb * intensity), Hlsl.Saturate(ca * intensity));
    }

    /// <summary>
    /// Kaleidoscope fold: mirrors angle into a segment, returns
    /// new coordinates in the folded space at the given radius.
    /// </summary>
    private static float2 KaleidoFold(float angle, float radius, float segments)
    {
        float a = angle + 628.318f;
        float segAngle = 6.28318f / segments;
        float folded = a - Hlsl.Floor(a / segAngle) * segAngle;
        folded = Hlsl.Abs(folded - segAngle * 0.5f);
        return new float2(Hlsl.Cos(folded) * radius, Hlsl.Sin(folded) * radius);
    }

    private static float3 HueToRGB(float hue)
    {
        float rv = Hlsl.Abs(Hlsl.Frac(hue) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(rv), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
