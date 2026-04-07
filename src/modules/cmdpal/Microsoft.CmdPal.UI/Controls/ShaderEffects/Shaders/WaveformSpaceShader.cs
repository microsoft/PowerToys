// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Retro MilkDrop 3D waveform space: glowing sine waves and dotted lines
/// drift through dark space forming a tunnel of oscillating geometry.
/// Deep neon purples/magentas, soft glow, dreamy motion trails.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct WaveformSpaceShader(
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

        float2 uv = new float2(
            (pos.X / w - 0.5f) * 2f,
            (pos.Y / h - 0.5f) * 2f);

        float bass = (bands0.X + bands0.Y + bands0.Z) * 0.333f;
        float mid = (bands0.W + bands1.X + bands1.Y + bands1.Z + bands1.W) * 0.2f;
        float high = (bands2.X + bands2.Y + bands2.Z + bands2.W) * 0.25f;
        float energy = (bass + mid + high) * 0.333f;

        // === CAMERA: wander in random-feeling directions using layered sine ===
        float camX = 0.3f * Hlsl.Sin(time * 0.17f) + 0.15f * Hlsl.Sin(time * 0.41f + 2f)
                   + 0.08f * Hlsl.Sin(time * 0.73f + 5f);
        float camY = 0.3f * Hlsl.Sin(time * 0.13f + 1f) + 0.15f * Hlsl.Sin(time * 0.37f + 4f)
                   + 0.08f * Hlsl.Sin(time * 0.67f + 3f);

        // Camera also rotates
        float camRot = time * 0.12f + 0.2f * Hlsl.Sin(time * 0.09f);
        float cc = Hlsl.Cos(camRot);
        float cs = Hlsl.Sin(camRot);
        float rx = (uv.X - camX) * cc - (uv.Y - camY) * cs;
        float ry = (uv.X - camX) * cs + (uv.Y - camY) * cc;

        float3 color = new float3(0f, 0f, 0f);

        // === TUNNEL DEPTH from polar mapping ===
        float radius = Hlsl.Sqrt(rx * rx + ry * ry);
        radius = Hlsl.Max(radius, 0.001f);
        float angle = Hlsl.Atan2(ry, rx);
        float depth = 1f / radius;
        float z = depth + time * 2.5f;

        // === LAYER 1: Horizontal sine wave layers at different depths ===

        // Multiple stacked waveforms flowing through z-space
        float waveAccum = 0f;

        // Wave layer 1 — slow, wide, BRIGHT
        float wz1 = z * 0.8f;
        float wave1Y = Hlsl.Sin(wz1 * 3f + angle * 2f + time * 0.3f) * (0.4f + mid * 0.6f);
        float wave1Dist = Hlsl.Abs(ry - wave1Y * (1f - radius * 0.5f));
        float wave1 = Hlsl.Exp(-wave1Dist * wave1Dist * 60f * depth) * Hlsl.Exp(-depth * 0.04f);
        waveAccum += wave1 * 1.3f;

        // Wave layer 2 — faster, narrower
        float wz2 = z * 1.2f;
        float wave2Y = Hlsl.Sin(wz2 * 5f - angle * 3f + time * 0.5f) * (0.3f + bass * 0.5f);
        float wave2Dist = Hlsl.Abs(ry - wave2Y * (1f - radius * 0.4f));
        float wave2 = Hlsl.Exp(-wave2Dist * wave2Dist * 90f * depth) * Hlsl.Exp(-depth * 0.05f);
        waveAccum += wave2;

        // Wave layer 3 — high-frequency detail
        float wz3 = z * 1.6f;
        float wave3Y = Hlsl.Sin(wz3 * 8f + angle + time * 0.7f) * (0.15f + high * 0.35f);
        float wave3Dist = Hlsl.Abs(ry - wave3Y * (1f - radius * 0.3f));
        float wave3 = Hlsl.Exp(-wave3Dist * wave3Dist * 150f * depth) * Hlsl.Exp(-depth * 0.06f);
        waveAccum += wave3 * 0.6f;

        // Wave layer 4 — horizontal cross-wave
        float wave4Y = Hlsl.Sin(z * 4f + angle * 4f - time * 0.4f) * (0.2f + mid * 0.3f);
        float wave4Dist = Hlsl.Abs(rx - wave4Y * (1f - radius * 0.5f));
        float wave4 = Hlsl.Exp(-wave4Dist * wave4Dist * 80f * depth) * Hlsl.Exp(-depth * 0.05f);
        waveAccum += wave4 * 0.8f;

        // Wave layer 5 — diagonal slash
        float wave5Val = Hlsl.Sin(z * 6f + angle * 6f + time * 0.6f) * (0.15f + energy * 0.3f);
        float wave5Dist = Hlsl.Abs((rx + ry) * 0.707f - wave5Val * (1f - radius * 0.4f));
        float wave5 = Hlsl.Exp(-wave5Dist * wave5Dist * 100f * depth) * Hlsl.Exp(-depth * 0.05f);
        waveAccum += wave5 * 0.5f;

        // Color the waves — deep purple to magenta gradient
        float waveHue = Hlsl.Frac(0.8f + depth * 0.01f + time * 0.015f);
        float3 waveCol = PurplePalette(waveHue, energy);
        color = new float3(
            color.X + waveCol.X * waveAccum,
            color.Y + waveCol.Y * waveAccum,
            color.Z + waveCol.Z * waveAccum);

        // === LAYER 2: Dotted particle lines ===

        // Rings of dots flowing through the tunnel
        float dotRingZ = Hlsl.Frac(z * 0.12f);
        float dotRing = Hlsl.Exp(-dotRingZ * dotRingZ * 300f)
                      + Hlsl.Exp(-(1f - dotRingZ) * (1f - dotRingZ) * 300f);

        // Angular dots around each ring
        float dotAngle = angle * 12f + z * 2f;
        float dotPhase = Hlsl.Frac(dotAngle * 0.159f);
        float dot = Hlsl.Exp(-(dotPhase - 0.5f) * (dotPhase - 0.5f) * 200f);
        float dotIntensity = dotRing * dot * 0.5f * Hlsl.Exp(-depth * 0.04f);
        dotIntensity *= 0.4f + energy * 0.6f;

        float3 dotCol = PurplePalette(Hlsl.Frac(0.85f + angle * 0.05f + time * 0.02f), energy);
        color = new float3(
            color.X + dotCol.X * dotIntensity,
            color.Y + dotCol.Y * dotIntensity,
            color.Z + dotCol.Z * dotIntensity);

        // === LAYER 3: Flowing wireframe grid in tunnel space ===
        float gridU = Hlsl.Frac(angle * 4f * 0.159f + time * 0.05f);
        float gridV = Hlsl.Frac(z * 0.06f);
        float gridLineU = Hlsl.Exp(-(gridU - 0.5f) * (gridU - 0.5f) * 400f);
        float gridLineV = Hlsl.Exp(-gridV * gridV * 200f) + Hlsl.Exp(-(1f - gridV) * (1f - gridV) * 200f);
        float grid = (gridLineU + gridLineV * 0.6f) * Hlsl.Exp(-depth * 0.06f) * 0.15f;
        grid *= 0.3f + mid * 0.7f;

        float3 gridCol = PurplePalette(Hlsl.Frac(0.75f + time * 0.01f), 0.3f);
        color = new float3(
            color.X + gridCol.X * grid,
            color.Y + gridCol.Y * grid,
            color.Z + gridCol.Z * grid);

        // === LAYER 4: Central morphing shape ===

        // Warped blob at center that morphs with audio
        float blobAngle = angle + time * 0.2f;
        float blobShape = 0.15f + 0.06f * Hlsl.Sin(blobAngle * 3f + time * 0.5f)
                               + 0.04f * Hlsl.Sin(blobAngle * 5f - time * 0.3f) * mid
                               + 0.03f * Hlsl.Sin(blobAngle * 7f + time * 0.8f) * high
                               + bass * 0.05f;
        float blobDist = radius - blobShape;
        float blobGlow = Hlsl.Exp(-blobDist * blobDist * 60f) * 0.6f;
        float blobCore = Hlsl.Exp(-blobDist * blobDist * 300f) * 0.4f;

        float3 blobCol = PurplePalette(Hlsl.Frac(0.9f + blobAngle * 0.05f + time * 0.025f), energy);
        color = new float3(
            color.X + blobCol.X * blobGlow + 0.8f * blobCore,
            color.Y + blobCol.Y * blobGlow + 0.6f * blobCore,
            color.Z + blobCol.Z * blobGlow + 1f * blobCore);

        // === LAYER 5: Floating particle sparkle ===
        float sparkleAngle = angle * 8f + z * 5f + time * 1.2f;
        float sparkleHash = Hlsl.Frac(Hlsl.Sin(sparkleAngle * 43758.5453f + Hlsl.Floor(depth * 20f) * 12.9898f) * 43758.5453f);
        float sparkle = Hlsl.Step(0.93f, sparkleHash) * Hlsl.Exp(-depth * 0.04f) * 0.6f;
        sparkle *= 0.4f + energy * 0.8f;
        color = new float3(
            color.X + sparkle * 0.7f,
            color.Y + sparkle * 0.4f,
            color.Z + sparkle * 1f);

        // === FEEDBACK TRAIL — ghost copy zoomed out ===
        float2 fbUV = new float2(uv.X * 0.92f, uv.Y * 0.92f);
        float fbCos = Hlsl.Cos(time * 0.03f);
        float fbSin = Hlsl.Sin(time * 0.03f);
        float2 fbRot = new float2(
            fbUV.X * fbCos - fbUV.Y * fbSin,
            fbUV.X * fbSin + fbUV.Y * fbCos);
        float fbR = Hlsl.Sqrt(fbRot.X * fbRot.X + fbRot.Y * fbRot.Y);
        fbR = Hlsl.Max(fbR, 0.001f);
        float fbAngle = Hlsl.Atan2(fbRot.Y, fbRot.X);
        float fbDepth = 1f / fbR;
        float fbZ = fbDepth + time * 2f - 0.3f;

        float fbWave1 = Hlsl.Sin(fbZ * 0.8f * 3f + fbAngle * 2f + (time - 0.3f) * 0.3f) * 0.3f;
        float fbWaveDist = Hlsl.Abs(fbRot.Y - fbWave1 * (1f - fbR * 0.5f));
        float fbWaveInt = Hlsl.Exp(-fbWaveDist * fbWaveDist * 60f * fbDepth) * Hlsl.Exp(-fbDepth * 0.06f) * 0.12f;

        float3 fbCol = PurplePalette(Hlsl.Frac(0.82f + time * 0.01f), 0.5f);
        color = new float3(
            color.X + fbCol.X * fbWaveInt,
            color.Y + fbCol.Y * fbWaveInt,
            color.Z + fbCol.Z * fbWaveInt);

        // === POST: Energy boost — cranked up ===
        float boost = 1.2f + energy * 1.2f;
        color = new float3(color.X * boost, color.Y * boost, color.Z * boost);

        // === POST: Center glow — stronger ===
        float centerGlow = Hlsl.Exp(-radius * radius * 2f) * (0.25f + bass * 0.5f);
        color = new float3(
            color.X + 0.6f * centerGlow,
            color.Y + 0.2f * centerGlow,
            color.Z + 0.9f * centerGlow);

        // === POST: Vignette ===
        float edgeDist = Hlsl.Max(Hlsl.Abs(uv.X), Hlsl.Abs(uv.Y));
        float vig = 1f - Hlsl.Saturate((edgeDist - 0.7f) * 1.5f);
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
    /// Deep purple/magenta neon palette. hue shifts within the purple range,
    /// energy brightens and adds pink/white highlights.
    /// </summary>
    private static float3 PurplePalette(float t, float energy)
    {

        // Base: cycle through deep purple → magenta → blue-purple
        float phase = t * 6.28318f;
        float r = 0.4f + 0.3f * Hlsl.Sin(phase + 0f);
        float g = 0.1f + 0.15f * Hlsl.Sin(phase + 2.5f);
        float b = 0.5f + 0.35f * Hlsl.Sin(phase + 1.2f);

        // Energy pushes toward bright pink/white
        r = Hlsl.Lerp(r, 1f, energy * 0.3f);
        g = Hlsl.Lerp(g, 0.5f, energy * 0.2f);
        b = Hlsl.Lerp(b, 1f, energy * 0.25f);

        return new float3(
            Hlsl.Saturate(r),
            Hlsl.Saturate(g),
            Hlsl.Saturate(b));
    }
}
