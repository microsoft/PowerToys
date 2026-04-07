// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// MilkDrop-inspired generative visualization: kaleidoscopic symmetry,
/// fractal-like domain warping, spiral vortices, audio-reactive ripples,
/// vibrant spectrum color cycling, and soft additive glow.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct WinampScopeShader(
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
        float aspect = w / h;
        float minDim = Hlsl.Min(w, h);

        // Scale by height so effect fills the vertical space

        // Stretch to fill the window
        float2 uv = new float2(
            (pos.X / w - 0.5f) * 2f,
            (pos.Y / h - 0.5f) * 2f);

        // Audio bands → energy zones
        float bass = (bands0.X + bands0.Y + bands0.Z) * 0.333f;
        float mid = (bands0.W + bands1.X + bands1.Y + bands1.Z + bands1.W) * 0.2f;
        float high = (bands2.X + bands2.Y + bands2.Z + bands2.W) * 0.25f;
        float energy = (bass + mid + high) * 0.333f;

        // Polar coordinates
        float r = Hlsl.Sqrt(uv.X * uv.X + uv.Y * uv.Y);
        float theta = Hlsl.Atan2(uv.Y, uv.X);

        // === KALEIDOSCOPE SYMMETRY ===

        // Audio-reactive segment count: 6-12 fold
        float segments = 6f + Hlsl.Floor(energy * 6f);
        float segAngle = 6.28318f / segments;
        float kAngle = theta + 628.318f;
        kAngle = kAngle - Hlsl.Floor(kAngle / segAngle) * segAngle;
        kAngle = Hlsl.Abs(kAngle - segAngle * 0.5f);
        float2 kp = new float2(Hlsl.Cos(kAngle) * r, Hlsl.Sin(kAngle) * r);

        // === DOMAIN WARPING (fractal-like distortion) ===
        float warpIntensity = 0.3f + 0.4f * bass;
        float2 wp = DomainWarp(kp, time, warpIntensity);

        // === LAYER 1: Flowing fractal field ===
        float fractal = FractalPattern(wp, time, bass);

        // === LAYER 2: Spiral vortex ===
        float spiralAngle = theta + r * (3f + 2f * bass) - time * (0.5f + 0.5f * mid);
        float spiral = Hlsl.Sin(spiralAngle * 5f) * 0.5f + 0.5f;
        spiral *= Hlsl.Exp(-r * 0.5f);
        float spiral2Angle = theta - r * (2f + 3f * high) + time * 0.3f;
        float spiral2 = Hlsl.Sin(spiral2Angle * 3f) * 0.5f + 0.5f;
        spiral2 *= Hlsl.Exp(-r * 0.7f);

        // === LAYER 3: Audio-reactive ripples ===
        float ripple = Hlsl.Sin((r - time * 0.8f - bass * 2f) * 12f);
        ripple *= Hlsl.Exp(-r * 1.5f);
        float ripple2 = Hlsl.Sin((r * 8f - time * 1.5f) * (1f + energy * 3f));
        ripple2 *= Hlsl.Exp(-r * 2f) * 0.5f;

        // === LAYER 4: Radial burst on bass hits ===
        float burstAngle = theta + time * 0.2f;
        float rays = Hlsl.Abs(Hlsl.Sin(burstAngle * 8f));
        float burst = rays * Hlsl.Exp(-r * (2f - bass * 1.5f)) * bass * 2f;

        // === LAYER 5: Morphing blob/flower ===
        float blobAngle = kAngle * 3f + time * 0.7f;
        float blobR = 0.4f + 0.3f * Hlsl.Sin(blobAngle) * (0.5f + bass);
        float blob = 1f - Hlsl.SmoothStep(blobR - 0.1f, blobR + 0.1f, r);

        // === COMPOSITE INTENSITY ===
        float intensity = 0f;
        intensity += fractal * 0.35f;
        intensity += spiral * 0.25f * (0.5f + mid);
        intensity += spiral2 * 0.15f;
        intensity += (ripple * 0.5f + 0.5f) * 0.2f * (0.3f + bass);
        intensity += (ripple2 * 0.5f + 0.5f) * 0.1f;
        intensity += burst;
        intensity += blob * 0.3f;

        // === VIBRANT SPECTRUM COLORING ===

        // Hue from angle + radius + time (creates rainbow kaleidoscope)
        float hue1 = Hlsl.Frac(theta * 0.159f + time * 0.05f + r * 0.3f);
        float hue2 = Hlsl.Frac(theta * 0.159f - time * 0.08f + fractal * 0.2f);
        float hue3 = Hlsl.Frac(time * 0.03f + r * 0.5f - theta * 0.1f);

        float3 col1 = HueToRGB(hue1) * fractal;
        float3 col2 = HueToRGB(hue2) * (spiral + spiral2);
        float3 col3 = HueToRGB(hue3) * (ripple * 0.5f + 0.5f);
        float3 colBurst = HueToRGB(Hlsl.Frac(time * 0.1f)) * burst;
        float3 colBlob = HueToRGB(Hlsl.Frac(time * 0.06f + 0.5f)) * blob;

        // Additive blending of colored layers
        float3 color = new float3(0f, 0f, 0f);
        color = new float3(
            color.X + col1.X * 0.4f, color.Y + col1.Y * 0.4f, color.Z + col1.Z * 0.4f);
        color = new float3(
            color.X + col2.X * 0.35f, color.Y + col2.Y * 0.35f, color.Z + col2.Z * 0.35f);
        color = new float3(
            color.X + col3.X * 0.2f * (0.3f + bass),
            color.Y + col3.Y * 0.2f * (0.3f + bass),
            color.Z + col3.Z * 0.2f * (0.3f + bass));
        color = new float3(
            color.X + colBurst.X, color.Y + colBurst.Y, color.Z + colBurst.Z);
        color = new float3(
            color.X + colBlob.X * 0.3f, color.Y + colBlob.Y * 0.3f, color.Z + colBlob.Z * 0.3f);

        // Energy-reactive brightness boost
        float boost = 1f + energy * 1.5f;
        color = new float3(color.X * boost, color.Y * boost, color.Z * boost);

        // === SOFT GLOW (simulated bloom) ===

        // Add a warm glow concentrated at the center
        float glowR = Hlsl.Exp(-r * r * 0.5f) * energy * 0.4f;
        float3 glowColor = HueToRGB(Hlsl.Frac(time * 0.04f));
        color = new float3(
            color.X + glowColor.X * glowR,
            color.Y + glowColor.Y * glowR,
            color.Z + glowColor.Z * glowR);

        // === FEEDBACK SIMULATION ===

        // Multiple warped copies at different scales create depth
        float2 fb1 = new float2(uv.X * 0.8f, uv.Y * 0.8f);
        float fbAngle = time * 0.02f;
        float2 fbRot = new float2(
            fb1.X * Hlsl.Cos(fbAngle) - fb1.Y * Hlsl.Sin(fbAngle),
            fb1.X * Hlsl.Sin(fbAngle) + fb1.Y * Hlsl.Cos(fbAngle));
        float fbR = Hlsl.Sqrt(fbRot.X * fbRot.X + fbRot.Y * fbRot.Y);
        float fbTheta = Hlsl.Atan2(fbRot.Y, fbRot.X) + 628.318f;
        float fbKA = fbTheta - Hlsl.Floor(fbTheta / segAngle) * segAngle;
        fbKA = Hlsl.Abs(fbKA - segAngle * 0.5f);
        float2 fbKP = new float2(Hlsl.Cos(fbKA) * fbR, Hlsl.Sin(fbKA) * fbR);
        float fbPattern = FractalPattern(DomainWarp(fbKP, time - 0.5f, warpIntensity * 0.7f), time - 0.5f, bass * 0.7f);
        float3 fbCol = HueToRGB(Hlsl.Frac(hue1 + 0.3f));
        color = new float3(
            color.X + fbCol.X * fbPattern * 0.15f,
            color.Y + fbCol.Y * fbPattern * 0.15f,
            color.Z + fbCol.Z * fbPattern * 0.15f);

        // === VIGNETTE ===
        float vignette = 1f - Hlsl.Saturate(r * 0.4f);
        vignette = vignette * vignette;
        color = new float3(color.X * vignette, color.Y * vignette, color.Z * vignette);

        float cr = Hlsl.Saturate(color.X);
        float cg = Hlsl.Saturate(color.Y);
        float cb = Hlsl.Saturate(color.Z);
        float ca = Hlsl.Max(cr, Hlsl.Max(cg, cb));

        // Boost with audio: baseline visible, audio intensifies
        float audioBoost = 0.35f + 1.5f * energy;
        return new float4(Hlsl.Saturate(cr * audioBoost), Hlsl.Saturate(cg * audioBoost), Hlsl.Saturate(cb * audioBoost), Hlsl.Saturate(ca * audioBoost));
    }

    private static float2 DomainWarp(float2 p, float t, float intensity)
    {
        float px = p.X + intensity * Hlsl.Sin(p.Y * 2.1f + t * 0.7f);
        float py = p.Y + intensity * Hlsl.Cos(p.X * 1.9f + t * 0.6f);
        px += intensity * 0.5f * Hlsl.Sin(py * 3.7f + t * 1.1f);
        py += intensity * 0.5f * Hlsl.Cos(px * 3.3f + t * 0.9f);
        px += intensity * 0.25f * Hlsl.Sin(py * 7.1f + t * 1.7f);
        py += intensity * 0.25f * Hlsl.Cos(px * 6.3f + t * 1.3f);
        return new float2(px, py);
    }

    /// <summary>
    /// Layered sine-based fractal pattern with rotation per octave.
    /// Creates flowing organic shapes similar to MilkDrop presets.
    /// </summary>
    private static float FractalPattern(float2 p, float t, float bassEnergy)
    {
        float val = 0f;
        float amp = 1f;
        float px = p.X;
        float py = p.Y;
        float rotSpeed = 0.5f + bassEnergy * 0.3f;

        // Octave 1
        val += amp * (Hlsl.Sin(px * 1.5f + t * 0.4f * rotSpeed) *
                      Hlsl.Cos(py * 1.3f + t * 0.3f) * 0.5f + 0.5f);
        float nx = px * 0.866f - py * 0.5f;
        float ny = px * 0.5f + py * 0.866f;
        px = nx * 1.8f;
        py = ny * 1.8f;
        amp *= 0.6f;

        // Octave 2
        val += amp * (Hlsl.Sin(px * 1.2f + t * 0.5f * rotSpeed + 1f) *
                      Hlsl.Cos(py * 1.4f - t * 0.35f) * 0.5f + 0.5f);
        nx = px * 0.707f - py * 0.707f;
        ny = px * 0.707f + py * 0.707f;
        px = nx * 1.7f;
        py = ny * 1.7f;
        amp *= 0.55f;

        // Octave 3
        val += amp * (Hlsl.Sin(px * 1.1f - t * 0.6f * rotSpeed + 2f) *
                      Hlsl.Cos(py * 0.9f + t * 0.4f) * 0.5f + 0.5f);
        nx = px * 0.94f - py * 0.342f;
        ny = px * 0.342f + py * 0.94f;
        px = nx * 1.6f;
        py = ny * 1.6f;
        amp *= 0.5f;

        // Octave 4
        val += amp * (Hlsl.Sin(px + t * 0.7f * rotSpeed) *
                      Hlsl.Cos(py - t * 0.5f) * 0.5f + 0.5f);

        return val;
    }

    /// <summary>
    /// Branchless HSV hue (S=1, V=1) to RGB.
    /// </summary>
    private static float3 HueToRGB(float hue)
    {
        float r = Hlsl.Abs(Hlsl.Frac(hue) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(r), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
