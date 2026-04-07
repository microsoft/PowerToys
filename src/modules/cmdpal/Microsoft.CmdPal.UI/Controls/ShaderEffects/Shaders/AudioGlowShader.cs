// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Audio-reactive full-width glow along the top edge.
/// Replaces 5 CompositionRadialGradientBrush layers + 1 linear pulse
/// with a single GPU pixel shader.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct AudioGlowShader(
    float time,
    float hueOffset,
    float overallEnergy,
    float band0,
    float band1,
    float band2,
    float band3,
    float band4,
    int width,
    int height) : ID2D1PixelShader
{
    public float4 Execute()
    {
        float2 pos = D2D.GetScenePosition().XY;
        float w = (float)width;
        float h = (float)height;
        float glowH = Hlsl.Min(150f, h);

        // Use time for subtle shimmer on the pulse
        float shimmer = 1f + 0.02f * Hlsl.Sin(time * 3f);

        float4 result = new float4(0f, 0f, 0f, 0f);

        // Full-width pulse layer
        float pulseScaleY = 0.3f + 1.0f * overallEnergy;
        float pulseOpacity = 0.15f + 0.85f * overallEnergy;
        float3 pulseColor = HueToRGB(hueOffset);
        float pulseT = pos.Y / (glowH * pulseScaleY);
        float pulseA = SampleLinearGradient(pulseT) * pulseOpacity * shimmer * Hlsl.Step(0f, pulseT) * Hlsl.Step(pulseT, 1f);
        result += new float4(pulseColor.X * pulseA, pulseColor.Y * pulseA, pulseColor.Z * pulseA, pulseA);

        // Layer 0
        result += ComputeLayer(pos, w, glowH, 0, band0);

        // Layer 1
        result += ComputeLayer(pos, w, glowH, 1, band1);

        // Layer 2
        result += ComputeLayer(pos, w, glowH, 2, band2);

        // Layer 3
        result += ComputeLayer(pos, w, glowH, 3, band3);

        // Layer 4
        result += ComputeLayer(pos, w, glowH, 4, band4);

        // Boost with audio: baseline visible, audio intensifies
        float intensity = 0.35f + 1.5f * overallEnergy;
        return Hlsl.Saturate(result * intensity);
    }

    private float4 ComputeLayer(float2 pos, float w, float glowH, int layerIndex, float level)
    {
        float opacity = 0.1f + 0.9f * level;
        float scaleY = 0.3f + 1.2f * level;
        float effectiveH = glowH * scaleY;

        float inBounds = Hlsl.Step(0f, pos.Y) * Hlsl.Step(pos.Y, effectiveH);
        if (inBounds < 0.5f)
        {
            return new float4(0f, 0f, 0f, 0f);
        }

        float hue = Hlsl.Frac(layerIndex * 0.2f + hueOffset);
        float3 color = HueToRGB(hue);

        // Radial gradient — wide ellipse so glow spans most of the width
        float dx = (pos.X / w - 0.5f) / 1.5f;
        float dy = (pos.Y / effectiveH) / 0.9f;
        float dist = Hlsl.Sqrt(dx * dx + dy * dy);

        float alpha = SampleRadialGradient(dist);
        float a = alpha * opacity;
        return new float4(color.X * a, color.Y * a, color.Z * a, a);
    }

    private static float SampleRadialGradient(float dist)
    {

        // Steeper falloff: bright at top, fades fast
        float s1 = Hlsl.Step(0.1f, dist);
        float s2 = Hlsl.Step(0.25f, dist);
        float s3 = Hlsl.Step(1f, dist);

        float seg0 = Hlsl.Lerp(1f, 0.7f, Hlsl.Saturate(dist / 0.1f));
        float seg1 = Hlsl.Lerp(0.7f, 0.15f, Hlsl.Saturate((dist - 0.1f) / 0.15f));
        float seg2 = Hlsl.Lerp(0.15f, 0f, Hlsl.Saturate((dist - 0.25f) / 0.75f));

        float val = Hlsl.Lerp(seg0, seg1, s1);
        val = Hlsl.Lerp(val, seg2, s2);
        return val * (1f - s3);
    }

    private static float SampleLinearGradient(float t)
    {
        float s1 = Hlsl.Step(0.2f, t);
        float s2 = Hlsl.Step(0.5f, t);
        float s3 = Hlsl.Step(1f, t);

        float seg0 = Hlsl.Lerp(0.784f, 0.47f, Hlsl.Saturate(t / 0.2f));
        float seg1 = Hlsl.Lerp(0.47f, 0.157f, Hlsl.Saturate((t - 0.2f) / 0.3f));
        float seg2 = Hlsl.Lerp(0.157f, 0f, Hlsl.Saturate((t - 0.5f) / 0.5f));

        float val = Hlsl.Lerp(seg0, seg1, s1);
        val = Hlsl.Lerp(val, seg2, s2);
        return val * (1f - s3);
    }

    private static float3 HueToRGB(float hue)
    {
        float r = Hlsl.Abs(Hlsl.Frac(hue + 0f) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(r), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
