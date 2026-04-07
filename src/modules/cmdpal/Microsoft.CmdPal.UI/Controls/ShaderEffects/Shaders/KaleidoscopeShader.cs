// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using ComputeSharp;
using ComputeSharp.D2D1;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;

/// <summary>
/// Psychedelic MilkDrop-style kaleidoscope: mirrored segments, flowing warped
/// textures, smooth motion, neon bloom. Optimized for fluid 60fps rendering.
/// </summary>
[D2DInputCount(0)]
[D2DRequiresScenePosition]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct KaleidoscopeShader(
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

        // Stretch to fill the window — each axis normalized independently
        float2 uv = new float2(
            (pos.X / w - 0.5f) * 2f,
            (pos.Y / h - 0.5f) * 2f);

        float bass = (bands0.X + bands0.Y + bands0.Z) * 0.333f;
        float mid = (bands0.W + bands1.X + bands1.Y + bands1.Z + bands1.W) * 0.2f;
        float high = (bands2.X + bands2.Y + bands2.Z + bands2.W) * 0.25f;
        float energy = (bass + mid + high) * 0.333f;

        // Gentle bass zoom
        float zoom = 1f - bass * 0.08f;
        float px = uv.X * zoom;
        float py = uv.Y * zoom;

        // Slow smooth rotation
        float gRot = time * 0.08f;
        float gc = Hlsl.Cos(gRot);
        float gs = Hlsl.Sin(gRot);
        float rx = px * gc - py * gs;
        float ry = px * gs + py * gc;

        float3 color = new float3(0f, 0f, 0f);

        // === PASS 1: Primary kaleidoscope (fixed 8-fold, slowly rotating) ===
        float2 k1 = Kaleidoscope(rx, ry, 8f, time * 0.18f);
        float warpStr1 = 0.25f + mid * 0.35f;
        float2 w1 = Warp(k1.X, k1.Y, time * 0.4f, warpStr1);
        float f1 = FractalField(w1.X, w1.Y, time, bass);

        float shimmer = Hlsl.Sin(w1.X * 12f + time * 2f) * Hlsl.Sin(w1.Y * 10f - time * 1.5f);
        shimmer = shimmer * high * 0.08f;
        float intensity1 = f1 + shimmer;

        float hue1 = Hlsl.Frac(Hlsl.Atan2(w1.Y, w1.X) * 0.159f + time * 0.025f + f1 * 0.1f);
        float3 c1 = HueToRGB(hue1);
        color = new float3(
            color.X + c1.X * intensity1 * 0.5f,
            color.Y + c1.Y * intensity1 * 0.5f,
            color.Z + c1.Z * intensity1 * 0.5f);

        // === PASS 2: Second layer (fixed 6-fold, counter-rotate) ===
        float rot2 = -time * 0.05f;
        float c2r = Hlsl.Cos(rot2);
        float s2r = Hlsl.Sin(rot2);
        float rx2 = px * c2r - py * s2r;
        float ry2 = px * s2r + py * c2r;

        float2 k2 = Kaleidoscope(rx2, ry2, 6f, -time * 0.12f);
        float warpStr2 = 0.2f + bass * 0.3f;
        float2 w2 = Warp(k2.X, k2.Y, time * 0.3f + 3f, warpStr2);
        float f2 = FractalField(w2.X, w2.Y, time * 0.6f + 5f, mid);

        float hue2 = Hlsl.Frac(Hlsl.Atan2(w2.Y, w2.X) * 0.159f - time * 0.03f + f2 * 0.12f + 0.33f);
        float3 c2 = HueToRGB(hue2);
        color = new float3(
            color.X + c2.X * f2 * 0.3f,
            color.Y + c2.Y * f2 * 0.3f,
            color.Z + c2.Z * f2 * 0.3f);

        // === PASS 3: Flowing blob shapes ===
        float2 k3 = Kaleidoscope(rx * 1.1f, ry * 1.1f, 4f, time * 0.09f);
        float blobAngle = Hlsl.Atan2(k3.Y, k3.X);
        float blobR = Hlsl.Sqrt(k3.X * k3.X + k3.Y * k3.Y);
        float blobShape = 0.3f + 0.12f * Hlsl.Sin(blobAngle * 3f + time * 0.6f)
                              + 0.07f * Hlsl.Sin(blobAngle * 5f - time * 0.4f) * mid
                              + bass * 0.1f;

        float blob = 1f - Hlsl.SmoothStep(blobShape - 0.1f, blobShape + 0.1f, blobR);
        float blobEdge = Hlsl.Exp(-(blobR - blobShape) * (blobR - blobShape) * 40f) * 0.4f;

        float hue3 = Hlsl.Frac(time * 0.03f + blobAngle * 0.159f + 0.66f);
        float3 c3 = HueToRGB(hue3);
        color = new float3(
            color.X + c3.X * (blob * 0.15f + blobEdge),
            color.Y + c3.Y * (blob * 0.15f + blobEdge),
            color.Z + c3.Z * (blob * 0.15f + blobEdge));

        // === PASS 4: Spiral overlay ===
        float sR = Hlsl.Sqrt(rx * rx + ry * ry);
        float sTheta = Hlsl.Atan2(ry, rx);
        float spiral = Hlsl.Sin(sTheta * 5f + sR * (6f + bass * 3f) - time * 0.8f) * 0.5f + 0.5f;
        spiral *= Hlsl.Exp(-sR * 1.2f);

        float3 sCol = HueToRGB(Hlsl.Frac(sTheta * 0.159f + time * 0.02f));
        color = new float3(
            color.X + sCol.X * spiral * 0.15f,
            color.Y + sCol.Y * spiral * 0.15f,
            color.Z + sCol.Z * spiral * 0.15f);

        // === FEEDBACK: single lightweight ghost ===
        float fbA = time * 0.04f;
        float fbc = Hlsl.Cos(fbA);
        float fbs = Hlsl.Sin(fbA);
        float fbx = (rx * 0.9f) * fbc - (ry * 0.9f) * fbs;
        float fby = (rx * 0.9f) * fbs + (ry * 0.9f) * fbc;
        float2 fk = Kaleidoscope(fbx, fby, 8f, time * 0.18f);
        float2 fw = Warp(fk.X, fk.Y, time * 0.4f - 0.4f, warpStr1 * 0.5f);
        float ff = FractalField(fw.X, fw.Y, time - 0.4f, bass * 0.5f);
        float3 fc = HueToRGB(Hlsl.Frac(hue1 + 0.15f));
        color = new float3(
            color.X + fc.X * ff * 0.1f,
            color.Y + fc.Y * ff * 0.1f,
            color.Z + fc.Z * ff * 0.1f);

        // === BLOOM + VIGNETTE ===
        float r = Hlsl.Sqrt(px * px + py * py);
        float centerGlow = Hlsl.Exp(-r * r * 1.5f) * energy * 0.3f;
        float3 bloomCol = HueToRGB(Hlsl.Frac(time * 0.025f + 0.2f));
        color = new float3(
            color.X + bloomCol.X * centerGlow,
            color.Y + bloomCol.Y * centerGlow,
            color.Z + bloomCol.Z * centerGlow);

        float boost = 1f + energy * 0.8f;
        color = new float3(color.X * boost, color.Y * boost, color.Z * boost);

        float vig = 1f - Hlsl.Saturate((r - 0.7f) * 0.9f);
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

    private static float2 Kaleidoscope(float x, float y, float segments, float rotation)
    {
        float angle = Hlsl.Atan2(y, x) + 628.318f + rotation;
        float rad = Hlsl.Sqrt(x * x + y * y);
        float segAngle = 6.28318f / segments;
        float folded = angle - Hlsl.Floor(angle / segAngle) * segAngle;
        folded = Hlsl.Abs(folded - segAngle * 0.5f);
        return new float2(Hlsl.Cos(folded) * rad, Hlsl.Sin(folded) * rad);
    }

    private static float2 Warp(float x, float y, float t, float strength)
    {
        float wx = x + strength * Hlsl.Sin(y * 2.3f + t);
        float wy = y + strength * Hlsl.Cos(x * 1.9f + t * 0.8f);
        wx += strength * 0.4f * Hlsl.Sin(wy * 3.7f + t * 1.3f);
        wy += strength * 0.4f * Hlsl.Cos(wx * 3.1f + t * 1.1f);
        return new float2(wx, wy);
    }

    private static float FractalField(float px, float py, float t, float audioMod)
    {
        float val = 0f;
        float amp = 1f;
        float speed = 0.3f + audioMod * 0.15f;
        float x = px;
        float y = py;

        val += amp * (Hlsl.Sin(x * 1.5f + t * speed) * Hlsl.Cos(y * 1.3f + t * speed * 0.7f) * 0.5f + 0.5f);
        float nx = x * 0.866f - y * 0.5f;
        float ny = x * 0.5f + y * 0.866f;
        x = nx * 1.8f; y = ny * 1.8f;
        amp *= 0.5f;

        val += amp * (Hlsl.Sin(x * 1.2f + t * speed * 1.1f + 1.5f) * Hlsl.Cos(y * 1.4f - t * speed * 0.9f) * 0.5f + 0.5f);
        nx = x * 0.707f - y * 0.707f;
        ny = x * 0.707f + y * 0.707f;
        x = nx * 1.7f; y = ny * 1.7f;
        amp *= 0.45f;

        val += amp * (Hlsl.Sin(x * 1.1f - t * speed * 1.2f + 3f) * Hlsl.Cos(y * 0.9f + t * speed * 0.5f) * 0.5f + 0.5f);

        return val;
    }

    private static float3 HueToRGB(float hue)
    {
        float rv = Hlsl.Abs(Hlsl.Frac(hue) * 6f - 3f) - 1f;
        float g = Hlsl.Abs(Hlsl.Frac(hue + 0.6667f) * 6f - 3f) - 1f;
        float b = Hlsl.Abs(Hlsl.Frac(hue + 0.3333f) * 6f - 3f) - 1f;
        return new float3(Hlsl.Saturate(rv), Hlsl.Saturate(g), Hlsl.Saturate(b));
    }
}
