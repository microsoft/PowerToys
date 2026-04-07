// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Audio-reactive waveform tunnel: the camera flies through a glowing
/// corridor whose walls oscillate with audio. Bass drives forward movement,
/// mids shape the wall geometry, and highs tint the neon highlights.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct WaveformTunnelShader(
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

        // Centered UV in [-1, 1] with aspect correction

        // Stretch to fill the window
        float2 uv = new float2(
            (pos.X / w - 0.5f) * 2f,
            (pos.Y / h - 0.5f) * 2f);

        // Audio
        float bass = (bands0.X + bands0.Y + bands0.Z) * 0.333f;
        float mid = (bands0.W + bands1.X + bands1.Y + bands1.Z + bands1.W) * 0.2f;
        float high = (bands2.X + bands2.Y + bands2.Z + bands2.W) * 0.25f;
        float energy = (bass + mid + high) * 0.333f;

        // Gentle continuous camera rotation — purely time-driven, no audio jitter
        float rotAngle = time * 0.15f;
        float cosR = Hlsl.Cos(rotAngle);
        float sinR = Hlsl.Sin(rotAngle);
        float2 ruv = new float2(
            uv.X * cosR - uv.Y * sinR,
            uv.X * sinR + uv.Y * cosR);

        // Polar coords of rotated UV
        float angle = Hlsl.Atan2(ruv.Y, ruv.X);
        float radius = Hlsl.Sqrt(ruv.X * ruv.X + ruv.Y * ruv.Y);

        // Prevent division by zero at exact center
        radius = Hlsl.Max(radius, 0.001f);

        // === TUNNEL MAPPING ===

        // Inverse radius gives depth (z); angle gives position around the tunnel
        float depth = 1f / radius;

        // Constant forward speed — audio drives visuals, not camera position
        float speed = time * 3f;
        float tunnelZ = depth + speed;
        float tunnelAngle = angle;

        // === TUNNEL WALL WAVEFORMS ===

        // Main wall oscillation — driven by mid frequencies
        float wallWave1 = Hlsl.Sin(tunnelZ * 4f + tunnelAngle * 3f + time * 0.8f) * (0.3f + mid * 0.7f);
        float wallWave2 = Hlsl.Sin(tunnelZ * 7f - tunnelAngle * 2f + time * 1.2f) * (0.15f + mid * 0.4f);
        float wallWave3 = Hlsl.Sin(tunnelZ * 13f + time * 2f) * (0.08f + high * 0.3f);
        float wallPattern = wallWave1 + wallWave2 + wallWave3;

        // Bass-reactive tunnel pulsing — walls breathe inward on hits
        float tunnelPulse = 1f + 0.15f * Hlsl.Sin(tunnelZ * 2f + time) * bass;

        // === RING LINES (depth markers) ===
        float ringFreq = 8f;
        float ringZ = Hlsl.Frac(tunnelZ * ringFreq * 0.1f);
        float ring = Hlsl.Exp(-ringZ * ringZ * 200f) + Hlsl.Exp(-(1f - ringZ) * (1f - ringZ) * 200f);
        ring *= 0.6f + 0.4f * energy;

        // === LONGITUDINAL LINES (ribs along tunnel) ===
        float ribCount = 12f;
        float ribAngle = Hlsl.Frac(tunnelAngle * ribCount * 0.159f + 0.5f);
        float rib = Hlsl.Exp(-(ribAngle - 0.5f) * (ribAngle - 0.5f) * 80f);
        rib *= 0.3f + 0.7f * mid;

        // === WALL SURFACE PATTERN (waveform texture) ===
        float surface = wallPattern * 0.5f + 0.5f;
        surface = surface * surface;

        // === EDGE GLOW (brighter at tunnel edges, simulates lighting) ===
        float edgeGlow = Hlsl.Exp(-radius * 1.2f);
        float wallGlow = (1f - edgeGlow) * 0.8f;

        // === NEON STRIPE EFFECT ===
        float stripe = Hlsl.Sin(tunnelZ * 20f + tunnelAngle * 4f) * 0.5f + 0.5f;
        stripe = Hlsl.Step(0.92f, stripe) * (0.5f + bass);

        // === COMPOSITE INTENSITY ===
        float intensity = 0f;
        intensity += surface * wallGlow * 0.5f;
        intensity += ring * 0.6f;
        intensity += rib * wallGlow * 0.3f;
        intensity += stripe * 0.4f;

        // Center light shaft
        intensity += edgeGlow * 0.2f * (0.5f + energy);

        // === DEPTH FOG (fade distant parts) ===
        float fog = Hlsl.Exp(-depth * 0.08f);
        intensity *= Hlsl.Lerp(0.2f, 1f, fog);

        // === COLORING ===

        // Base hue shifts with depth and angle — creates flowing neon ribbons
        float hue1 = Hlsl.Frac(tunnelZ * 0.05f + time * 0.02f);
        float hue2 = Hlsl.Frac(tunnelAngle * 0.159f + time * 0.05f + 0.3f);
        float hue3 = Hlsl.Frac(time * 0.03f + depth * 0.02f + 0.6f);

        float3 col1 = HueToRGB(hue1);
        float3 col2 = HueToRGB(hue2);
        float3 col3 = HueToRGB(hue3);

        // Blend colors based on what element is dominant
        float3 wallCol = new float3(
            col1.X * surface + col2.X * rib * 0.5f,
            col1.Y * surface + col2.Y * rib * 0.5f,
            col1.Z * surface + col2.Z * rib * 0.5f);
        float3 ringCol = new float3(col3.X * 1.2f, col3.Y * 1.2f, col3.Z * 1.2f);
        float3 stripeCol = new float3(
            Hlsl.Lerp(col2.X, 1f, 0.5f),
            Hlsl.Lerp(col2.Y, 1f, 0.5f),
            Hlsl.Lerp(col2.Z, 1f, 0.5f));

        float3 color = new float3(
            wallCol.X * wallGlow * 0.5f + ringCol.X * ring * 0.6f + stripeCol.X * stripe * 0.4f,
            wallCol.Y * wallGlow * 0.5f + ringCol.Y * ring * 0.6f + stripeCol.Y * stripe * 0.4f,
            wallCol.Z * wallGlow * 0.5f + ringCol.Z * ring * 0.6f + stripeCol.Z * stripe * 0.4f);

        // Central light shaft — white/cyan glow at the center (light at end of tunnel)
        float3 shaftCol = HueToRGB(Hlsl.Frac(time * 0.04f + 0.5f));
        float shaftIntensity = edgeGlow * edgeGlow * (0.3f + energy * 0.7f);
        color = new float3(
            color.X + shaftCol.X * shaftIntensity,
            color.Y + shaftCol.Y * shaftIntensity,
            color.Z + shaftCol.Z * shaftIntensity);

        // Apply depth fog
        color = new float3(color.X * fog, color.Y * fog, color.Z * fog);

        // Energy flash — bass hits cause brief bright flash
        float flash = bass * bass * 0.3f;
        float3 flashCol = HueToRGB(Hlsl.Frac(time * 0.08f));
        color = new float3(
            color.X + flashCol.X * flash,
            color.Y + flashCol.Y * flash,
            color.Z + flashCol.Z * flash);

        // === SECOND TUNNEL LAYER (ghostly inner tube) ===
        float innerRadius = radius * 1.5f + 0.05f;
        float innerDepth = 1f / Hlsl.Max(innerRadius, 0.001f);
        float innerZ = innerDepth + speed * 0.7f;
        float innerWave = Hlsl.Sin(innerZ * 6f + tunnelAngle * 5f + time) * 0.5f + 0.5f;
        float innerRing = Hlsl.Exp(-Hlsl.Frac(innerZ * 0.8f) * Hlsl.Frac(innerZ * 0.8f) * 150f);
        float innerFog = Hlsl.Exp(-innerDepth * 0.1f);
        float innerIntensity = (innerWave * 0.3f + innerRing * 0.4f) * innerFog * 0.25f;
        float3 innerCol = HueToRGB(Hlsl.Frac(hue1 + 0.5f));
        color = new float3(
            color.X + innerCol.X * innerIntensity,
            color.Y + innerCol.Y * innerIntensity,
            color.Z + innerCol.Z * innerIntensity);

        // === AUDIO WAVEFORM ON WALLS ===

        // Project band levels as glowing bumps along the tunnel
        float bandPos = Hlsl.Frac(tunnelAngle * 1.91f);
        float bandIdx = bandPos * 11f;
        float bFloor = Hlsl.Floor(bandIdx);
        float bFrac = bandIdx - bFloor;
        float bSmooth = bFrac * bFrac * (3f - 2f * bFrac);
        float bVal0 = GetBand((int)bFloor);
        float bVal1 = GetBand(Hlsl.Min((int)bFloor + 1, 11));
        float bandLevel = Hlsl.Lerp(bVal0, bVal1, bSmooth);
        float bandGlow = bandLevel * Hlsl.Exp(-radius * 2f) * fog * 0.5f;
        float3 bandCol = HueToRGB(Hlsl.Frac(bandPos + time * 0.06f));
        color = new float3(
            color.X + bandCol.X * bandGlow,
            color.Y + bandCol.Y * bandGlow,
            color.Z + bandCol.Z * bandGlow);

        // === VIGNETTE ===
        float vignette = 1f - radius * 0.25f;
        vignette = Hlsl.Saturate(vignette * vignette);
        color = new float3(color.X * vignette, color.Y * vignette, color.Z * vignette);

        float cr = Hlsl.Saturate(color.X);
        float cg = Hlsl.Saturate(color.Y);
        float cb = Hlsl.Saturate(color.Z);
        float ca = Hlsl.Max(cr, Hlsl.Max(cg, cb));

        // Boost with audio: baseline visible, audio intensifies
        float audioBoost = 0.35f + 1.5f * energy;
        return new float4(Hlsl.Saturate(cr * audioBoost), Hlsl.Saturate(cg * audioBoost), Hlsl.Saturate(cb * audioBoost), Hlsl.Saturate(ca * audioBoost));
    }

    private float GetBand(int i)
    {
        float r = bands0.X;
        r = (i >= 1) ? bands0.Y : r;
        r = (i >= 2) ? bands0.Z : r;
        r = (i >= 3) ? bands0.W : r;
        r = (i >= 4) ? bands1.X : r;
        r = (i >= 5) ? bands1.Y : r;
        r = (i >= 6) ? bands1.Z : r;
        r = (i >= 7) ? bands1.W : r;
        r = (i >= 8) ? bands2.X : r;
        r = (i >= 9) ? bands2.Y : r;
        r = (i >= 10) ? bands2.Z : r;
        r = (i >= 11) ? bands2.W : r;
        return r;
    }

    private static float3 HueToRGB(float hue)
    {
        float r = Hlsl.Abs(Hlsl.Frac(hue) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(r), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
