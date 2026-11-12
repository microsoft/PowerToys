//==============================================================================
//
// WebcamComposite.hlsl
//
// GPU composite shader for webcam overlay.
// Composites sharp foreground from full-resolution camera with
// blurred background from processing-resolution blur buffer, using
// a segmentation mask to blend between the two sources.
//
// The GPU's hardware texture sampler provides free bilinear filtering,
// making this orders of magnitude faster than the equivalent CPU loop.
//
// Entry points:
//   VSMain  (vs_5_0)  — full-screen triangle, no vertex buffer needed
//   PSMain  (ps_5_0)  — composite camera + blur + mask + shape mask
//
// Recompile with:
//   fxc /T vs_5_0 /E VSMain /Fh WebcamCompositeVS.h /Vn g_WebcamCompositeVS WebcamComposite.hlsl
//   fxc /T ps_5_0 /E PSMain /Fh WebcamCompositePS.h /Vn g_WebcamCompositePS WebcamComposite.hlsl
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================

// Camera frame at full resolution (e.g. 1920x1080), B8G8R8A8_UNORM.
// Shader sees RGBA due to hardware swizzle.
Texture2D    CameraTex : register(t0);

// Blurred processing buffer at reduced resolution (e.g. 960x540), B8G8R8A8_UNORM.
// Already gamma-corrected from CPU downsample.
Texture2D    BlurTex   : register(t1);

// Segmentation mask from ONNX model, R32_FLOAT.
// 1.0 = foreground (person), 0.0 = background.
Texture2D    MaskTex   : register(t2);

// Bilinear sampler with clamp addressing — used for all textures.
SamplerState BilinearSamp : register(s0);

cbuffer CompositeConstants : register(b0)
{
    float2 CropOffset;      // Camera crop UV offset (srcCropX/camW, srcCropY/camH)
    float2 CropScale;       // Camera crop UV scale  (srcCropW/camW, srcCropH/camH)
    float  Gamma;           // Gamma correction exponent (< 1 brightens)
    float  CornerRadius;    // Corner radius in output pixels
    float  OutputW;         // Output width in pixels
    float  OutputH;         // Output height in pixels
    uint   ShapeType;       // 0=Square, 1=RoundedRect, 2=RoundedSquare, 3=Circle
    uint   HasMask;         // 1 if segmentation mask is valid
    float2 Pad;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

//----------------------------------------------------------------------------
// Vertex shader: full-screen triangle from SV_VertexID (no vertex buffer).
//   Draw(3, 0) to invoke.  The triangle covers [-1,1] clip space.
//----------------------------------------------------------------------------
VSOutput VSMain( uint vertexId : SV_VertexID )
{
    VSOutput output;
    output.TexCoord = float2( (vertexId << 1) & 2, vertexId & 2 );
    output.Position = float4( output.TexCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0 );
    return output;
}

//----------------------------------------------------------------------------
// Pixel shader: composite camera foreground with blurred background.
//   - Shape masking (circle, rounded rect) produces alpha = 0 outside.
//   - Segmentation mask blends camera (foreground) with blur (background).
//   - Gamma correction applied to camera samples only (blur already corrected).
//   - Hardware bilinear filtering on all texture samples (free).
//----------------------------------------------------------------------------
float4 PSMain( VSOutput input ) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float px = uv.x * OutputW;
    float py = uv.y * OutputH;

    // ── Shape mask ─────────────────────────────────────────────────────
    if( ShapeType == 3 )  // Circle
    {
        float halfW  = OutputW * 0.5;
        float halfH  = OutputH * 0.5;
        float radius = min( halfW, halfH );
        float dx = ( px - halfW ) / radius;
        float dy = ( py - halfH ) / radius;
        if( dx * dx + dy * dy > 1.0 )
            return float4( 0, 0, 0, 0 );
    }
    else if( ShapeType >= 1 )  // RoundedRect or RoundedSquare
    {
        float cx = 0, cy = 0;
        bool inCorner = false;

        if( px < CornerRadius && py < CornerRadius )
        { cx = CornerRadius; cy = CornerRadius; inCorner = true; }
        else if( px > OutputW - CornerRadius && py < CornerRadius )
        { cx = OutputW - CornerRadius; cy = CornerRadius; inCorner = true; }
        else if( px < CornerRadius && py > OutputH - CornerRadius )
        { cx = CornerRadius; cy = OutputH - CornerRadius; inCorner = true; }
        else if( px > OutputW - CornerRadius && py > OutputH - CornerRadius )
        { cx = OutputW - CornerRadius; cy = OutputH - CornerRadius; inCorner = true; }

        if( inCorner )
        {
            float ddx = px - cx;
            float ddy = py - cy;
            if( ddx * ddx + ddy * ddy > CornerRadius * CornerRadius )
                return float4( 0, 0, 0, 0 );
        }
    }

    // ── Composite ──────────────────────────────────────────────────────
    if( HasMask )
    {
        // Segmentation mask (bilinear-filtered for smooth edges).
        float mask = saturate( MaskTex.Sample( BilinearSamp, uv ).r );

        // Camera: crop-to-fill UV mapping + gamma correction.
        float2 camUV = CropOffset + uv * CropScale;
        float4 cam   = CameraTex.Sample( BilinearSamp, camUV );
        cam.rgb = pow( max( cam.rgb, 0.001 ), Gamma );

        // Blur: already gamma-corrected from CPU downsample.
        float4 blur = BlurTex.Sample( BilinearSamp, uv );

        // Blend: mask=1 → camera (foreground), mask=0 → blur (background).
        float3 result = lerp( blur.rgb, cam.rgb, mask );
        return float4( result, 1.0 );
    }
    else
    {
        // No segmentation mask — just display the processing buffer.
        float4 blur = BlurTex.Sample( BilinearSamp, uv );
        return float4( blur.rgb, 1.0 );
    }
}
