// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Circular audio spectrum: glowing bars radiate outward from a center ring,
/// layered with neon glow, motion trails, soft chaos, and rotating rings.
/// Early 2000s WMP / Winamp aesthetic with additive blending.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct RadialSpectrumShader(
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

        float r = Hlsl.Sqrt(uv.X * uv.X + uv.Y * uv.Y);
        float theta = Hlsl.Atan2(uv.Y, uv.X);

        // Bass-reactive global scale — circle breathes
        float globalScale = 1f + bass * 0.12f;
        r = r / globalScale;

        float3 color = new float3(0f, 0f, 0f);

        // ================================================================

        // LAYER 1 — PRIMARY SPECTRUM BARS (48 segments)

        // ================================================================
        float rot1 = time * 0.2f;
        float adj1 = theta - rot1 + 628.318f;
        float barCount = 48f;
        float sectorAngle = 6.28318f / barCount;
        float barIdxF = Hlsl.Floor(adj1 / sectorAngle);
        float inSector = adj1 - barIdxF * sectorAngle;
        float dAngle = Hlsl.Abs(inSector - sectorAngle * 0.5f);

        // Map bar index to audio band (48 bars → 12 bands, 4 bars each)
        float wrapped = barIdxF - Hlsl.Floor(barIdxF / barCount) * barCount;
        int bandGroup = (int)Hlsl.Floor(wrapped * 12f / barCount);
        float bandVal = GetBand(bandGroup);

        // Bar dimensions
        float baseRadius = 0.28f;
        float barLength = 0.15f + 0.55f * bandVal;
        float barInner = baseRadius;
        float barOuter = baseRadius + barLength;
        float barHalfWidth = sectorAngle * 0.35f;

        // Soft angular falloff
        float angularMask = 1f - Hlsl.SmoothStep(barHalfWidth * 0.6f, barHalfWidth, dAngle);

        // Radial mask — inside the bar
        float radialMask = Hlsl.Step(barInner, r) * (1f - Hlsl.SmoothStep(barOuter - 0.02f, barOuter, r));

        // Gradient along bar: bright at tip
        float barGrad = Hlsl.Saturate((r - barInner) / Hlsl.Max(barLength, 0.01f));
        float barBright = Hlsl.Lerp(0.4f, 1f, barGrad);

        // Glow extending beyond bar tip
        float tipDist = Hlsl.Max(r - barOuter, 0f);
        float tipGlow = Hlsl.Exp(-tipDist * tipDist * 60f) * bandVal * 0.6f;

        float barIntensity = (angularMask * radialMask * barBright + tipGlow * angularMask) * (0.3f + bandVal * 0.7f);

        // Color — hue from angular position + time cycling
        float barHue = Hlsl.Frac(wrapped / barCount + time * 0.03f);
        float3 barCol = HueToRGB(barHue);
        color = new float3(
            color.X + barCol.X * barIntensity,
            color.Y + barCol.Y * barIntensity,
            color.Z + barCol.Z * barIntensity);

        // ================================================================

        // LAYER 2 — INNER RING GLOW (base circle outline)

        // ================================================================
        float ringDist = Hlsl.Abs(r - baseRadius);
        float ringGlow = Hlsl.Exp(-ringDist * ringDist * 400f) * (0.4f + energy * 0.6f);
        float ringBloom = Hlsl.Exp(-ringDist * ringDist * 30f) * 0.2f * (0.3f + bass);
        float3 ringCol = HueToRGB(Hlsl.Frac(time * 0.05f));
        color = new float3(
            color.X + ringCol.X * (ringGlow + ringBloom),
            color.Y + ringCol.Y * (ringGlow + ringBloom),
            color.Z + ringCol.Z * (ringGlow + ringBloom));

        // ================================================================

        // LAYER 3 — OUTER ECHO RING (larger, dimmer, delayed hue)

        // ================================================================
        float outerRingR = baseRadius + 0.15f + 0.55f * energy;
        float outerDist = Hlsl.Abs(r - outerRingR);
        float outerRing = Hlsl.Exp(-outerDist * outerDist * 120f) * 0.35f;
        float outerBloom = Hlsl.Exp(-outerDist * outerDist * 15f) * 0.1f;
        float3 outerCol = HueToRGB(Hlsl.Frac(time * 0.04f + 0.33f));
        color = new float3(
            color.X + outerCol.X * (outerRing + outerBloom),
            color.Y + outerCol.Y * (outerRing + outerBloom),
            color.Z + outerCol.Z * (outerRing + outerBloom));

        // ================================================================

        // LAYER 4 — SECOND SPECTRUM (mirrored inward, dimmer)

        // ================================================================
        float innerBarOuter = baseRadius;
        float innerBarLength = 0.08f + 0.2f * bandVal;
        float innerBarInner = Hlsl.Max(baseRadius - innerBarLength, 0.02f);
        float innerRadialMask = Hlsl.Step(innerBarInner, r) * Hlsl.Step(r, innerBarOuter);
        float innerBarGrad = Hlsl.Saturate((innerBarOuter - r) / Hlsl.Max(innerBarLength, 0.01f));
        float innerIntensity = angularMask * innerRadialMask * innerBarGrad * 0.3f * bandVal;
        float3 innerCol = HueToRGB(Hlsl.Frac(barHue + 0.5f));
        color = new float3(
            color.X + innerCol.X * innerIntensity,
            color.Y + innerCol.Y * innerIntensity,
            color.Z + innerCol.Z * innerIntensity);

        // ================================================================

        // LAYER 5 — SECONDARY THIN BARS (24 bars, counter-rotating)

        // ================================================================
        float rot2 = -time * 0.35f;
        float adj2 = theta - rot2 + 628.318f;
        float bar2Count = 24f;
        float sector2 = 6.28318f / bar2Count;
        float bar2IdxF = Hlsl.Floor(adj2 / sector2);
        float inSec2 = adj2 - bar2IdxF * sector2;
        float dAngle2 = Hlsl.Abs(inSec2 - sector2 * 0.5f);

        float wrap2 = bar2IdxF - Hlsl.Floor(bar2IdxF / bar2Count) * bar2Count;
        int band2 = (int)Hlsl.Floor(wrap2 * 12f / bar2Count);
        float bv2 = GetBand(band2);

        float bar2Inner = baseRadius + 0.05f;
        float bar2Length = 0.1f + 0.3f * bv2;
        float bar2Outer = bar2Inner + bar2Length;
        float bar2HalfW = sector2 * 0.2f;

        float ang2 = 1f - Hlsl.SmoothStep(bar2HalfW * 0.5f, bar2HalfW, dAngle2);
        float rad2 = Hlsl.Step(bar2Inner, r) * (1f - Hlsl.SmoothStep(bar2Outer - 0.01f, bar2Outer, r));
        float grad2 = Hlsl.Saturate((r - bar2Inner) / Hlsl.Max(bar2Length, 0.01f));
        float int2 = ang2 * rad2 * grad2 * 0.25f * bv2;

        float3 col2 = HueToRGB(Hlsl.Frac(wrap2 / bar2Count + time * 0.06f + 0.15f));
        color = new float3(
            color.X + col2.X * int2,
            color.Y + col2.Y * int2,
            color.Z + col2.Z * int2);

        // ================================================================

        // LAYER 6 — FEEDBACK TRAIL SIMULATION (smeared ghost at larger scale)

        // ================================================================
        float2 fbUV = new float2(uv.X * 0.92f, uv.Y * 0.92f);
        float fbCos = Hlsl.Cos(time * 0.08f);
        float fbSin = Hlsl.Sin(time * 0.08f);
        float2 fbRot = new float2(
            fbUV.X * fbCos - fbUV.Y * fbSin,
            fbUV.X * fbSin + fbUV.Y * fbCos);
        float fbR = Hlsl.Sqrt(fbRot.X * fbRot.X + fbRot.Y * fbRot.Y) / globalScale;
        float fbTheta = Hlsl.Atan2(fbRot.Y, fbRot.X);

        float fbAdj = fbTheta - rot1 + 0.1f + 628.318f;
        float fbBarIdx = Hlsl.Floor(fbAdj / sectorAngle);
        float fbInSec = fbAdj - fbBarIdx * sectorAngle;
        float fbDAngle = Hlsl.Abs(fbInSec - sectorAngle * 0.5f);
        float fbWrapped = fbBarIdx - Hlsl.Floor(fbBarIdx / barCount) * barCount;
        int fbBand = (int)Hlsl.Floor(fbWrapped * 12f / barCount);
        float fbBandVal = GetBand(fbBand);

        float fbBarOuter = baseRadius + 0.15f + 0.55f * fbBandVal;
        float fbAngular = 1f - Hlsl.SmoothStep(barHalfWidth * 0.8f, barHalfWidth * 1.4f, fbDAngle);
        float fbRadial = Hlsl.Step(baseRadius * 0.9f, fbR) * (1f - Hlsl.SmoothStep(fbBarOuter, fbBarOuter + 0.05f, fbR));
        float fbIntensity = fbAngular * fbRadial * 0.12f * fbBandVal;

        float3 fbCol = HueToRGB(Hlsl.Frac(fbWrapped / barCount + time * 0.03f - 0.1f));
        color = new float3(
            color.X + fbCol.X * fbIntensity,
            color.Y + fbCol.Y * fbIntensity,
            color.Z + fbCol.Z * fbIntensity);

        // ================================================================

        // LAYER 7 — CENTER ORB (pulsing with bass)

        // ================================================================
        float orbScale = 0.08f + 0.12f * bass;
        float orbDist = r / orbScale;
        float orbGlow = Hlsl.Exp(-orbDist * orbDist * 2f);
        float orbSharp = Hlsl.Exp(-orbDist * orbDist * 8f);
        float3 orbCol = HueToRGB(Hlsl.Frac(time * 0.07f + 0.5f));
        float3 orbWhite = new float3(0.9f, 0.95f, 1f);
        color = new float3(
            color.X + Hlsl.Lerp(orbCol.X, orbWhite.X, orbSharp) * (orbGlow * 0.4f + orbSharp * 0.6f),
            color.Y + Hlsl.Lerp(orbCol.Y, orbWhite.Y, orbSharp) * (orbGlow * 0.4f + orbSharp * 0.6f),
            color.Z + Hlsl.Lerp(orbCol.Z, orbWhite.Z, orbSharp) * (orbGlow * 0.4f + orbSharp * 0.6f));

        // ================================================================

        // LAYER 8 — CHAOTIC PARTICLE NOISE (subtle sparkle)

        // ================================================================
        float noiseAngle = theta * 7f + time * 2f + r * 20f;
        float noise = Hlsl.Frac(Hlsl.Sin(noiseAngle * 43758.5453f + Hlsl.Floor(r * 30f) * 12.9898f) * 43758.5453f);
        float sparkle = Hlsl.Step(0.97f, noise) * energy * 0.5f;
        float sparkleR = Hlsl.Step(baseRadius * 0.5f, r) * Hlsl.Step(r, baseRadius + barLength + 0.1f);
        color = new float3(
            color.X + sparkle * sparkleR,
            color.Y + sparkle * sparkleR,
            color.Z + sparkle * sparkleR * 0.8f);

        // ================================================================

        // POST — VIGNETTE

        // ================================================================
        float vig = 1f - Hlsl.Saturate((r - 0.8f) * 1.2f);
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

    private float GetBand(int i)
    {
        float v = bands0.X;
        v = (i >= 2) ? bands0.Z : v;
        v = (i >= 3) ? bands0.W : v;
        v = (i >= 4) ? bands1.X : v;
        v = (i >= 5) ? bands1.Y : v;
        v = (i >= 6) ? bands1.Z : v;
        v = (i >= 7) ? bands1.W : v;
        v = (i >= 8) ? bands2.X : v;
        v = (i >= 9) ? bands2.Y : v;
        v = (i >= 10) ? bands2.Z : v;
        v = (i >= 11) ? bands2.W : v;
        return v;
    }

    private static float3 HueToRGB(float hue)
    {
        float rv = Hlsl.Abs(Hlsl.Frac(hue) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(rv), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
