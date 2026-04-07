// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// WMP "Alchemy" starburst visualization — concentric geometric rays rotating
/// in opposite directions, pulsing with audio, plus a breathing ring and
/// bass-reactive center orb. All rendered in a single pixel shader.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct AlchemyShader(
    float time,
    float bassLevel,
    float overallEnergy,
    int width,
    int height,
    float4 outerBands0,
    float4 outerBands1,
    float4 innerBands) : ID2D1PixelShader
{
    public float4 Execute()
    {
        float2 pos = D2D.GetScenePosition().XY;
        float w = (float)width;
        float h = (float)height;
        float2 center = new float2(w * 0.5f, h * 0.5f);
        float dx = pos.X - center.X;
        float dy = pos.Y - center.Y;
        float r = Hlsl.Sqrt(dx * dx + dy * dy);
        float theta = Hlsl.Atan2(dy, dx);

        float span = Hlsl.Min(w, h);
        float outerLen = span * 0.45f;
        float innerLen = outerLen * 0.5f;

        float4 result = new float4(0f, 0f, 0f, 0f);

        // 1. Outer ring
        float ringRadius = outerLen * 1.25f;
        float ringScale = 0.85f + 0.4f * overallEnergy;
        float ringDist = r / (ringRadius * ringScale);
        float ringAlpha = SampleRingGradient(ringDist);
        float ringOp = (0.1f + 0.6f * overallEnergy) * ringAlpha;
        result += new float4(0.392f * ringOp, 0.706f * ringOp, 1f * ringOp, ringOp);

        // 2. Outer rays — 24 rays, clockwise rotation
        float cwAngle = time * 6.28318f / 30f;
        result += ComputeOuterRays(theta, r, cwAngle, outerLen);

        // 3. Inner rays — 14 rays, counter-clockwise
        float ccwAngle = -time * 6.28318f / 18f;
        result += ComputeInnerRays(theta, r, ccwAngle, innerLen);

        // 4. Center orb
        float orbRadius = span * 0.2f;
        float orbScale = Hlsl.Max(0.2f + 3.5f * bassLevel, 0.1f);
        float orbDist = r / (orbRadius * orbScale);
        float orbA = SampleOrbAlpha(orbDist) * (0.3f + 0.7f * bassLevel);
        float3 orbCol = SampleOrbColor(orbDist);
        result += new float4(orbCol.X * orbA, orbCol.Y * orbA, orbCol.Z * orbA, orbA);

        // Boost with audio: baseline visible, audio intensifies
        float intensityMul = 0.35f + 1.5f * overallEnergy;
        return Hlsl.Saturate(result * intensityMul);
    }

    private float4 ComputeOuterRays(float theta, float r, float rotAngle, float maxLength)
    {
        float sectorAngle = 6.28318f / 24f;
        float adjusted = theta - rotAngle + 628.318f;
        float rayIdxF = Hlsl.Floor(adjusted / sectorAngle);
        float inSector = adjusted - rayIdxF * sectorAngle;
        float dTheta = inSector - sectorAngle * 0.5f;

        float wrapped = rayIdxF - Hlsl.Floor(rayIdxF / 24f) * 24f;
        int nearestRay = (int)wrapped;
        int bandGroup = (int)Hlsl.Floor(wrapped / 3f);

        float level = GetOuterBand(bandGroup);

        float basePulse = 0.15f + 0.1f * Hlsl.Sin(time * 6.28318f / (3f + nearestRay * 0.15f));
        level = Hlsl.Max(level, basePulse);

        float rayWidth = 5f * (0.2f + 4f * level);
        float rayLength = maxLength * Hlsl.Max(level * 3f, 0.15f);

        float visible = Hlsl.Step(1f, r) * Hlsl.Step(r, rayLength);
        if (visible < 0.5f)
        {
            return new float4(0f, 0f, 0f, 0f);
        }

        float halfAngle = rayWidth / (2f * r);
        float angularFalloff = 1f - Hlsl.SmoothStep(halfAngle * 0.3f, halfAngle, Hlsl.Abs(dTheta));
        if (angularFalloff <= 0f)
        {
            return new float4(0f, 0f, 0f, 0f);
        }

        float t = r / rayLength;

        // Alternate colors: even=OuterColor(0.392, 0.706, 1.0), odd=AccentColor(0.392, 1.0, 0.706)
        float isEven = 1f - Hlsl.Step(0.5f, Hlsl.Frac(nearestRay * 0.5f));
        float3 primaryRGB = new float3(
            0.392f,
            Hlsl.Lerp(1f, 0.706f, isEven),
            Hlsl.Lerp(0.706f, 1f, isEven));

        float4 gradColor = SampleRayGradient(t, primaryRGB, 0.706f);
        return gradColor * angularFalloff * level;
    }

    private float4 ComputeInnerRays(float theta, float r, float rotAngle, float maxLength)
    {
        float sectorAngle = 6.28318f / 14f;
        float adjusted = theta - rotAngle + 628.318f;
        float rayIdxF = Hlsl.Floor(adjusted / sectorAngle);
        float inSector = adjusted - rayIdxF * sectorAngle;
        float dTheta = inSector - sectorAngle * 0.5f;

        float wrapped = rayIdxF - Hlsl.Floor(rayIdxF / 14f) * 14f;
        int nearestRay = (int)wrapped;
        int bandGroup = Hlsl.Min((int)Hlsl.Floor(wrapped / 4f), 3);

        float level = innerBands[bandGroup];

        float basePulse = 0.15f + 0.1f * Hlsl.Sin(time * 6.28318f / (3f + nearestRay * 0.2f));
        level = Hlsl.Max(level, basePulse);

        float rayWidth = 6f * (0.15f + 5f * level);
        float rayLength = maxLength * Hlsl.Max(level * 4f, 0.15f);

        float visible = Hlsl.Step(1f, r) * Hlsl.Step(r, rayLength);
        if (visible < 0.5f)
        {
            return new float4(0f, 0f, 0f, 0f);
        }

        float halfAngle = rayWidth / (2f * r);
        float angularFalloff = 1f - Hlsl.SmoothStep(halfAngle * 0.3f, halfAngle, Hlsl.Abs(dTheta));
        if (angularFalloff <= 0f)
        {
            return new float4(0f, 0f, 0f, 0f);
        }

        float t = r / rayLength;

        // Alternate InnerColor(1.0, 0.392, 0.863) / OuterColor(0.392, 0.706, 1.0)
        float isEven = 1f - Hlsl.Step(0.5f, Hlsl.Frac(nearestRay * 0.5f));
        float3 primaryRGB = new float3(
            Hlsl.Lerp(0.392f, 1f, isEven),
            Hlsl.Lerp(0.706f, 0.392f, isEven),
            Hlsl.Lerp(1f, 0.863f, isEven));
        float primaryAlpha = Hlsl.Lerp(0.706f, 0.784f, isEven);

        float4 gradColor = SampleRayGradient(t, primaryRGB, primaryAlpha);
        return gradColor * angularFalloff * level;
    }

    private float GetOuterBand(int group)
    {

        // 8 bands packed in 2 float4s
        float result = outerBands0.X;
        result = (group == 1) ? outerBands0.Y : result;
        result = (group == 2) ? outerBands0.Z : result;
        result = (group == 3) ? outerBands0.W : result;
        result = (group == 4) ? outerBands1.X : result;
        result = (group == 5) ? outerBands1.Y : result;
        result = (group == 6) ? outerBands1.Z : result;
        result = (group == 7) ? outerBands1.W : result;
        return result;
    }

    private static float SampleRingGradient(float dist)
    {
        float belowMin = Hlsl.Step(dist, 0.6f);
        float aboveMax = Hlsl.Step(1f, dist);
        float seg1 = Hlsl.Lerp(0f, 0.392f, Hlsl.Saturate((dist - 0.6f) / 0.2f));
        float seg2 = Hlsl.Lerp(0.392f, 0.196f, Hlsl.Saturate((dist - 0.8f) / 0.12f));
        float seg3 = Hlsl.Lerp(0.196f, 0f, Hlsl.Saturate((dist - 0.92f) / 0.08f));
        float s1 = Hlsl.Step(0.8f, dist);
        float s2 = Hlsl.Step(0.92f, dist);
        float val = Hlsl.Lerp(seg1, seg2, s1);
        val = Hlsl.Lerp(val, seg3, s2);
        return val * (1f - belowMin) * (1f - aboveMax);
    }

    private static float4 SampleRayGradient(float t, float3 primaryRGB, float primaryAlpha)
    {
        float s1 = Hlsl.Step(0.05f, t);
        float s2 = Hlsl.Step(0.2f, t);
        float s3 = Hlsl.Step(0.5f, t);

        float f0 = Hlsl.Saturate(t / 0.05f);
        float3 col0 = new float3(
            Hlsl.Lerp(1f, primaryRGB.X, f0),
            Hlsl.Lerp(1f, primaryRGB.Y, f0),
            Hlsl.Lerp(1f, primaryRGB.Z, f0));
        float a0 = Hlsl.Lerp(0.314f, primaryAlpha, f0);

        float3 col1 = primaryRGB;
        float a1 = Hlsl.Lerp(primaryAlpha, primaryAlpha * 0.7f, Hlsl.Saturate((t - 0.05f) / 0.15f));
        float a2 = Hlsl.Lerp(primaryAlpha * 0.7f, primaryAlpha * 0.3f, Hlsl.Saturate((t - 0.2f) / 0.3f));
        float a3 = Hlsl.Lerp(primaryAlpha * 0.3f, 0f, Hlsl.Saturate((t - 0.5f) / 0.5f));

        float3 color = new float3(
            Hlsl.Lerp(col0.X, col1.X, s1),
            Hlsl.Lerp(col0.Y, col1.Y, s1),
            Hlsl.Lerp(col0.Z, col1.Z, s1));
        float alpha = Hlsl.Lerp(a0, a1, s1);
        alpha = Hlsl.Lerp(alpha, a2, s2);
        alpha = Hlsl.Lerp(alpha, a3, s3);

        return new float4(color.X * alpha, color.Y * alpha, color.Z * alpha, alpha);
    }

    private static float SampleOrbAlpha(float dist)
    {
        float s1 = Hlsl.Step(0.15f, dist);
        float s2 = Hlsl.Step(0.4f, dist);
        float s3 = Hlsl.Step(0.7f, dist);
        float s4 = Hlsl.Step(1f, dist);

        float a0 = Hlsl.Lerp(0.784f, 0.627f, Hlsl.Saturate(dist / 0.15f));
        float a1 = Hlsl.Lerp(0.627f, 0.314f, Hlsl.Saturate((dist - 0.15f) / 0.25f));
        float a2 = Hlsl.Lerp(0.314f, 0.118f, Hlsl.Saturate((dist - 0.4f) / 0.3f));
        float a3 = Hlsl.Lerp(0.118f, 0f, Hlsl.Saturate((dist - 0.7f) / 0.3f));

        float val = Hlsl.Lerp(a0, a1, s1);
        val = Hlsl.Lerp(val, a2, s2);
        val = Hlsl.Lerp(val, a3, s3);
        return val * (1f - s4);
    }

    private static float3 SampleOrbColor(float dist)
    {
        float s1 = Hlsl.Step(0.15f, dist);
        float s2 = Hlsl.Step(0.4f, dist);
        float s3 = Hlsl.Step(0.7f, dist);

        float3 c0 = new float3(
            Hlsl.Lerp(1f, 0.863f, Hlsl.Saturate(dist / 0.15f)),
            Hlsl.Lerp(1f, 0.784f, Hlsl.Saturate(dist / 0.15f)),
            1f);
        float3 c1 = new float3(
            Hlsl.Lerp(0.863f, 0.627f, Hlsl.Saturate((dist - 0.15f) / 0.25f)),
            Hlsl.Lerp(0.784f, 0.47f, Hlsl.Saturate((dist - 0.15f) / 0.25f)),
            1f);
        float3 c2 = new float3(
            Hlsl.Lerp(0.627f, 0.392f, Hlsl.Saturate((dist - 0.4f) / 0.3f)),
            Hlsl.Lerp(0.47f, 0.314f, Hlsl.Saturate((dist - 0.4f) / 0.3f)),
            Hlsl.Lerp(1f, 0.863f, Hlsl.Saturate((dist - 0.4f) / 0.3f)));
        float3 c3 = new float3(
            Hlsl.Lerp(0.392f, 0.235f, Hlsl.Saturate((dist - 0.7f) / 0.3f)),
            Hlsl.Lerp(0.314f, 0.157f, Hlsl.Saturate((dist - 0.7f) / 0.3f)),
            Hlsl.Lerp(0.863f, 0.706f, Hlsl.Saturate((dist - 0.7f) / 0.3f)));

        float3 val = new float3(
            Hlsl.Lerp(c0.X, c1.X, s1), Hlsl.Lerp(c0.Y, c1.Y, s1), Hlsl.Lerp(c0.Z, c1.Z, s1));
        val = new float3(
            Hlsl.Lerp(val.X, c2.X, s2), Hlsl.Lerp(val.Y, c2.Y, s2), Hlsl.Lerp(val.Z, c2.Z, s2));
        val = new float3(
            Hlsl.Lerp(val.X, c3.X, s3), Hlsl.Lerp(val.Y, c3.Y, s3), Hlsl.Lerp(val.Z, c3.Z, s3));
        return val;
    }
}
