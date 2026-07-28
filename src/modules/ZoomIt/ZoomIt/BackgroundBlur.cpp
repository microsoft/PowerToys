//==============================================================================
//
// BackgroundBlur.cpp
//
// Windows ML-based person segmentation and background blur for the
// webcam overlay.  Uses the built-in Windows.AI.MachineLearning API
// to load an ONNX segmentation model (e.g. MediaPipe SelfieSegmentation)
// and produce a per-pixel person mask, then blurs or replaces the
// background using an iterated box blur or a user-chosen image.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#include "pch.h"
#include "BackgroundBlur.h"
#include <algorithm>
#include <cstring>
#include <wincodec.h>
#include <wil/com.h>

namespace winml = winrt::Windows::AI::MachineLearning;
namespace wf    = winrt::Windows::Foundation::Collections;

// Defined in Zoomit.cpp; compiles to nothing in Release builds.
void OutputDebug(const TCHAR* format, ...);

//----------------------------------------------------------------------------
// BackgroundBlur::Initialize
//
// Loads the ONNX segmentation model via Windows ML and inspects its
// input/output tensor shapes to auto-configure preprocessing.
//----------------------------------------------------------------------------
bool BackgroundBlur::Initialize( const wchar_t* modelPath )
{
    try
    {
        // Load the model from file.
        m_model = winml::LearningModel::LoadFromFilePath( modelPath );

        // Try GPU (DirectML) first for faster inference; fall back to CPU
        // if no suitable GPU is available.
        try
        {
            winml::LearningModelDevice gpuDevice( winml::LearningModelDeviceKind::DirectXHighPerformance );
            m_session = winml::LearningModelSession( m_model, gpuDevice );
            m_usingGpu = true;
            OutputDebug( L"[BackgroundBlur] Using DirectML (GPU) for inference\n" );
        }
        catch( ... )
        {
            winml::LearningModelDevice cpuDevice( winml::LearningModelDeviceKind::Cpu );
            m_session = winml::LearningModelSession( m_model, cpuDevice );
            m_usingGpu = false;
            OutputDebug( L"[BackgroundBlur] GPU unavailable, falling back to CPU\n" );
        }
        m_binding = winml::LearningModelBinding( m_session );

        // Get input feature descriptor.
        auto inputFeatures = m_model.InputFeatures();
        if( inputFeatures.Size() == 0 )
        {
            OutputDebug( L"[BackgroundBlur] Model has no input features\n" );
            return false;
        }
        auto inputDesc = inputFeatures.GetAt( 0 );
        m_inputName = inputDesc.Name();

        // Inspect input tensor shape.
        auto tensorDesc = inputDesc.as<winml::ITensorFeatureDescriptor>();
        auto shape = tensorDesc.Shape();
        if( shape.Size() == 4 )
        {
            if( shape.GetAt( 1 ) == 3 || shape.GetAt( 1 ) == 1 )
            {
                // NCHW layout.
                m_inputIsNchw = true;
                m_modelInputChannels = shape.GetAt( 1 );
                m_modelInputHeight = shape.GetAt( 2 ) > 0 ? shape.GetAt( 2 ) : 256;
                m_modelInputWidth  = shape.GetAt( 3 ) > 0 ? shape.GetAt( 3 ) : 256;
            }
            else
            {
                // NHWC layout.
                m_inputIsNchw = false;
                m_modelInputHeight   = shape.GetAt( 1 ) > 0 ? shape.GetAt( 1 ) : 256;
                m_modelInputWidth    = shape.GetAt( 2 ) > 0 ? shape.GetAt( 2 ) : 256;
                m_modelInputChannels = shape.GetAt( 3 );
            }
        }

        // Get output feature name.
        auto outputFeatures = m_model.OutputFeatures();
        if( outputFeatures.Size() == 0 )
        {
            OutputDebug( L"[BackgroundBlur] Model has no output features\n" );
            return false;
        }
        m_outputName = outputFeatures.GetAt( 0 ).Name();

        OutputDebug( L"[BackgroundBlur] Model loaded: input=%s %lldx%lld (ch=%lld, %s)\n",
                     m_inputName.c_str(), m_modelInputWidth, m_modelInputHeight,
                     m_modelInputChannels, m_inputIsNchw ? L"NCHW" : L"NHWC" );

        // Pre-allocate input tensor buffer.
        m_inputTensor.resize( static_cast<size_t>( m_modelInputChannels * m_modelInputHeight * m_modelInputWidth ) );

        return true;
    }
    catch( winrt::hresult_error const& ex )
    {
        OutputDebug( L"[BackgroundBlur] Initialize failed: %s (0x%08X)\n", ex.message().c_str(), ex.code().value );
        m_session = nullptr;
        m_model = nullptr;
        return false;
    }
}

//----------------------------------------------------------------------------
// BackgroundBlur::RunSegmentation
//
// Resizes the BGRA frame to the model's expected input size, converts
// to float RGB, runs inference via Windows ML, and produces a float mask
// in m_mask where 1.0 = person, 0.0 = background.
//----------------------------------------------------------------------------
bool BackgroundBlur::RunSegmentation( const uint8_t* bgraPixels, uint32_t width, uint32_t height,
                                      bool modelResOnly )
{
    const int64_t mW = m_modelInputWidth;
    const int64_t mH = m_modelInputHeight;
    const int64_t mC = m_modelInputChannels;

    // Resize BGRA → model-sized float RGB using nearest-neighbor.
    for( int64_t y = 0; y < mH; y++ )
    {
        uint32_t srcY = static_cast<uint32_t>( y * height / mH );
        for( int64_t x = 0; x < mW; x++ )
        {
            uint32_t srcX = static_cast<uint32_t>( x * width / mW );
            const uint8_t* px = bgraPixels + ( static_cast<size_t>( srcY ) * width + srcX ) * 4;
            float b = px[0] / 255.0f;
            float g = px[1] / 255.0f;
            float r = px[2] / 255.0f;

            if( m_inputIsNchw )
            {
                m_inputTensor[static_cast<size_t>(0ll * mH * mW + y * mW + x)] = r;
                if( mC > 1 ) m_inputTensor[static_cast<size_t>(1ll * mH * mW + y * mW + x)] = g;
                if( mC > 2 ) m_inputTensor[static_cast<size_t>(2ll * mH * mW + y * mW + x)] = b;
            }
            else
            {
                size_t idx = static_cast<size_t>( (y * mW + x) * mC );
                m_inputTensor[idx + 0] = r;
                if( mC > 1 ) m_inputTensor[idx + 1] = g;
                if( mC > 2 ) m_inputTensor[idx + 2] = b;
            }
        }
    }

    try
    {
        // Create the input tensor shape.
        std::vector<int64_t> inputShape;
        if( m_inputIsNchw )
            inputShape = { 1, mC, mH, mW };
        else
            inputShape = { 1, mH, mW, mC };

        // Create a TensorFloat from our data.
        auto inputTensor = winml::TensorFloat::CreateFromArray(
            inputShape, winrt::array_view<const float>( m_inputTensor.data(),
                                                         m_inputTensor.data() + m_inputTensor.size() ) );

        // Bind input and evaluate.
        m_binding.Clear();
        m_binding.Bind( m_inputName, inputTensor );

        auto result = m_session.Evaluate( m_binding, L"" );

        // Extract output tensor — bulk-copy to a raw float array so we
        // avoid per-element WinRT/COM dispatch in the hot loop.
        auto outputTensor = result.Outputs().Lookup( m_outputName ).as<winml::TensorFloat>();
        auto outputShape = outputTensor.Shape();
        auto outputView = outputTensor.GetAsVectorView();
        const uint32_t outputSize = outputView.Size();
        m_outputBuf.resize( outputSize );
        outputView.GetMany( 0, m_outputBuf );
        const float* outData = m_outputBuf.data();

        // Determine output mask dimensions.
        int64_t outH = mH, outW = mW;
        int64_t numClasses = 1;
        if( outputShape.Size() == 4 )
        {
            if( outputShape.GetAt( 1 ) <= 2 && outputShape.GetAt( 2 ) > 2 )
            {
                // [1, classes, H, W]
                numClasses = outputShape.GetAt( 1 );
                outH = outputShape.GetAt( 2 );
                outW = outputShape.GetAt( 3 );
            }
            else
            {
                // [1, H, W, classes]
                outH = outputShape.GetAt( 1 );
                outW = outputShape.GetAt( 2 );
                numClasses = outputShape.GetAt( 3 );
            }
        }
        else if( outputShape.Size() == 3 )
        {
            outH = outputShape.GetAt( 1 );
            outW = outputShape.GetAt( 2 );
        }

        // Store actual output dimensions for GetModelMaskWidth/Height.
        m_modelOutputWidth = outW;
        m_modelOutputHeight = outH;

        // Build model-resolution mask first, apply sigmoid sharpening
        // at model resolution (e.g. 256×256 = 65K pixels), then upscale
        // to frame resolution.
        const size_t modelPixels = static_cast<size_t>( outH * outW );
        m_erodeBuf.resize( modelPixels );

        // Extract person scores at model resolution from the raw array.
        // Apply a hard threshold to produce a binary mask.  This is much
        // faster than a sigmoid (no expf) and eliminates the partial-blur
        // halo that was bleeding onto body/head edges.
        for( int64_t y = 0; y < outH; y++ )
        {
            for( int64_t x = 0; x < outW; x++ )
            {
                float personScore;
                if( numClasses == 1 )
                {
                    personScore = outData[y * outW + x];
                }
                else
                {
                    float bg = outData[0 * outH * outW + y * outW + x];
                    float fg = outData[1 * outH * outW + y * outW + x];
                    personScore = ( fg > bg ) ? 1.0f : 0.0f;
                }

                m_erodeBuf[static_cast<size_t>( y * outW + x )] = ( personScore > 0.5f ) ? 1.0f : 0.0f;
            }
        }

        // ── GPU path: model-resolution post-processing only ────────
        // When modelResOnly is true, apply feathering and temporal
        // smoothing at model resolution (e.g. 256×256 = 65K pixels)
        // and return early.  The GPU's hardware bilinear sampler will
        // handle upscaling to frame resolution for free.
        if( modelResOnly )
        {
            // Small box blur on m_erodeBuf for edge feathering.
            // Radius 1 at 256×256 provides similar smoothing to
            // radius 3 at 960×540 after bilinear upscale.
            const int modelBlurRadius = 1;
            const int modelDiam = modelBlurRadius * 2 + 1;
            m_maskBlurBuf.resize( modelPixels );

            // Horizontal pass.
            for( int64_t y = 0; y < outH; y++ )
            {
                const float* srcRow = m_erodeBuf.data() + y * outW;
                float* dstRow = m_maskBlurBuf.data() + y * outW;
                float sum = 0.0f;
                for( int i = -modelBlurRadius; i <= modelBlurRadius; i++ )
                    sum += srcRow[(std::max)( static_cast<int64_t>(0), (std::min)( outW - 1, static_cast<int64_t>( i ) ) )];
                for( int64_t x = 0; x < outW; x++ )
                {
                    dstRow[x] = sum / modelDiam;
                    int64_t remX = (std::max)( static_cast<int64_t>(0), x - modelBlurRadius );
                    int64_t addX = (std::min)( outW - 1, x + modelBlurRadius + 1 );
                    sum += srcRow[addX] - srcRow[remX];
                }
            }

            // Vertical pass.
            for( int64_t x = 0; x < outW; x++ )
            {
                float sum = 0.0f;
                for( int i = -modelBlurRadius; i <= modelBlurRadius; i++ )
                {
                    int64_t iy = (std::max)( static_cast<int64_t>(0), (std::min)( outH - 1, static_cast<int64_t>( i ) ) );
                    sum += m_maskBlurBuf[static_cast<size_t>( iy * outW + x )];
                }
                for( int64_t y = 0; y < outH; y++ )
                {
                    m_erodeBuf[static_cast<size_t>( y * outW + x )] = sum / modelDiam;
                    int64_t remY = (std::max)( static_cast<int64_t>(0), y - modelBlurRadius );
                    int64_t addY = (std::min)( outH - 1, y + modelBlurRadius + 1 );
                    sum += m_maskBlurBuf[static_cast<size_t>( addY * outW + x )] -
                           m_maskBlurBuf[static_cast<size_t>( remY * outW + x )];
                }
            }

            // Temporal smoothing at model resolution.
            if( m_prevModelMask.size() == modelPixels )
            {
                constexpr float alpha = 0.6f;
                constexpr float beta  = 0.4f;
                for( size_t i = 0; i < modelPixels; i++ )
                    m_erodeBuf[i] = alpha * m_erodeBuf[i] + beta * m_prevModelMask[i];
            }
            m_prevModelMask = m_erodeBuf;

            return true;
        }

        // Upscale processed mask to frame dimensions via bilinear interpolation
        // to produce smooth edges instead of staircase artifacts.
        const size_t maskPixels = static_cast<size_t>( width ) * height;
        m_mask.resize( maskPixels );
        for( uint32_t y = 0; y < height; y++ )
        {
            float srcYf = ( y + 0.5f ) * outH / static_cast<float>( height ) - 0.5f;
            srcYf = (std::max)( 0.0f, (std::min)( srcYf, static_cast<float>( outH - 1 ) ) );
            int64_t y0 = static_cast<int64_t>( srcYf );
            int64_t y1 = (std::min)( y0 + 1, outH - 1 );
            float fy = srcYf - y0;

            for( uint32_t x = 0; x < width; x++ )
            {
                float srcXf = ( x + 0.5f ) * outW / static_cast<float>( width ) - 0.5f;
                srcXf = (std::max)( 0.0f, (std::min)( srcXf, static_cast<float>( outW - 1 ) ) );
                int64_t x0 = static_cast<int64_t>( srcXf );
                int64_t x1 = (std::min)( x0 + 1, outW - 1 );
                float fx = srcXf - x0;

                float v00 = m_erodeBuf[static_cast<size_t>(y0 * outW + x0)];
                float v01 = m_erodeBuf[static_cast<size_t>(y0 * outW + x1)];
                float v10 = m_erodeBuf[static_cast<size_t>(y1 * outW + x0)];
                float v11 = m_erodeBuf[static_cast<size_t>(y1 * outW + x1)];

                m_mask[static_cast<size_t>( y ) * width + x] =
                    v00 * ( 1.0f - fx ) * ( 1.0f - fy ) +
                    v01 * fx * ( 1.0f - fy ) +
                    v10 * ( 1.0f - fx ) * fy +
                    v11 * fx * fy;
            }
        }

        // Apply a small box blur to the upscaled mask to feather edges.
        const int maskBlurRadius = 3;
        const int maskDiam = maskBlurRadius * 2 + 1;
        m_maskBlurBuf.resize( maskPixels );

        // Horizontal pass.
        for( uint32_t y = 0; y < height; y++ )
        {
            const float* srcRow = m_mask.data() + static_cast<size_t>( y ) * width;
            float* dstRow = m_maskBlurBuf.data() + static_cast<size_t>( y ) * width;
            float sum = 0.0f;

            for( int i = -maskBlurRadius; i <= maskBlurRadius; i++ )
                sum += srcRow[(std::max)( 0, (std::min)( static_cast<int>( width ) - 1, i ) )];

            for( uint32_t x = 0; x < width; x++ )
            {
                dstRow[x] = sum / maskDiam;
                int remX = (std::max)( 0, static_cast<int>( x ) - maskBlurRadius );
                int addX = (std::min)( static_cast<int>( width ) - 1, static_cast<int>( x ) + maskBlurRadius + 1 );
                sum += srcRow[addX] - srcRow[remX];
            }
        }

        // Vertical pass.
        for( uint32_t x = 0; x < width; x++ )
        {
            float sum = 0.0f;

            for( int i = -maskBlurRadius; i <= maskBlurRadius; i++ )
            {
                int iy = (std::max)( 0, (std::min)( static_cast<int>( height ) - 1, i ) );
                sum += m_maskBlurBuf[static_cast<size_t>( iy ) * width + x];
            }

            for( uint32_t y = 0; y < height; y++ )
            {
                m_mask[static_cast<size_t>( y ) * width + x] = sum / maskDiam;
                int remY = (std::max)( 0, static_cast<int>( y ) - maskBlurRadius );
                int addY = (std::min)( static_cast<int>( height ) - 1, static_cast<int>( y ) + maskBlurRadius + 1 );
                sum += m_maskBlurBuf[static_cast<size_t>( addY ) * width + x] -
                       m_maskBlurBuf[static_cast<size_t>( remY ) * width + x];
            }
        }

        // Temporal smoothing: blend the current mask with the previous
        // frame's mask to stabilize edges and reduce flicker.  A weight
        // of 0.6 current + 0.4 previous keeps edges responsive while
        // dampening the frame-to-frame jitter around fine details like
        // ears, hair, and fingers.
        {
            const size_t maskPixelsInner = static_cast<size_t>( width ) * height;
            if( m_prevMask.size() == maskPixelsInner )
            {
                constexpr float alpha = 0.6f;  // current frame weight
                constexpr float beta  = 0.4f;  // previous frame weight
                for( size_t i = 0; i < maskPixelsInner; i++ )
                {
                    m_mask[i] = alpha * m_mask[i] + beta * m_prevMask[i];
                }
            }
            m_prevMask = m_mask;
        }

        return true;
    }
    catch( winrt::hresult_error const& ex )
    {
        OutputDebug( L"[BackgroundBlur] Evaluate failed: %s (0x%08X)\n", ex.message().c_str(), ex.code().value );
        return false;
    }
}

//----------------------------------------------------------------------------
// HorizontalBoxBlur / VerticalBoxBlur
//
// Separable box blur passes used to build an approximate Gaussian.
//----------------------------------------------------------------------------
static void HorizontalBoxBlur(
    const uint8_t* src, uint8_t* dst, uint32_t width, uint32_t height, int radius )
{
    const int diameter = radius * 2 + 1;
    for( uint32_t y = 0; y < height; y++ )
    {
        int rSum = 0, gSum = 0, bSum = 0;
        const uint8_t* row = src + static_cast<size_t>( y ) * width * 4;

        // Initialize window with clamped left edge.
        for( int i = -radius; i <= radius; i++ )
        {
            int ix = (std::max)( 0, (std::min)( static_cast<int>( width ) - 1, i ) );
            const uint8_t* px = row + ix * 4;
            bSum += px[0];
            gSum += px[1];
            rSum += px[2];
        }

        uint8_t* dstRow = dst + static_cast<size_t>( y ) * width * 4;
        for( uint32_t x = 0; x < width; x++ )
        {
            dstRow[x * 4 + 0] = static_cast<uint8_t>( bSum / diameter );
            dstRow[x * 4 + 1] = static_cast<uint8_t>( gSum / diameter );
            dstRow[x * 4 + 2] = static_cast<uint8_t>( rSum / diameter );
            dstRow[x * 4 + 3] = 0xFF;

            // Slide window: add right, remove left.
            int removeX = (std::max)( 0, static_cast<int>( x ) - radius );
            int addX = (std::min)( static_cast<int>( width ) - 1, static_cast<int>( x ) + radius + 1 );
            const uint8_t* remPx = row + removeX * 4;
            const uint8_t* addPx = row + addX * 4;
            bSum += addPx[0] - remPx[0];
            gSum += addPx[1] - remPx[1];
            rSum += addPx[2] - remPx[2];
        }
    }
}

static void VerticalBoxBlur(
    const uint8_t* src, uint8_t* dst, uint32_t width, uint32_t height, int radius )
{
    const int diameter = radius * 2 + 1;
    for( uint32_t x = 0; x < width; x++ )
    {
        int rSum = 0, gSum = 0, bSum = 0;

        // Initialize window with clamped top edge.
        for( int i = -radius; i <= radius; i++ )
        {
            int iy = (std::max)( 0, (std::min)( static_cast<int>( height ) - 1, i ) );
            const uint8_t* px = src + ( static_cast<size_t>( iy ) * width + x ) * 4;
            bSum += px[0];
            gSum += px[1];
            rSum += px[2];
        }

        for( uint32_t y = 0; y < height; y++ )
        {
            uint8_t* dstPx = dst + ( static_cast<size_t>( y ) * width + x ) * 4;
            dstPx[0] = static_cast<uint8_t>( bSum / diameter );
            dstPx[1] = static_cast<uint8_t>( gSum / diameter );
            dstPx[2] = static_cast<uint8_t>( rSum / diameter );
            dstPx[3] = 0xFF;

            int removeY = (std::max)( 0, static_cast<int>( y ) - radius );
            int addY = (std::min)( static_cast<int>( height ) - 1, static_cast<int>( y ) + radius + 1 );
            const uint8_t* remPx = src + ( static_cast<size_t>( removeY ) * width + x ) * 4;
            const uint8_t* addPx = src + ( static_cast<size_t>( addY ) * width + x ) * 4;
            bSum += addPx[0] - remPx[0];
            gSum += addPx[1] - remPx[1];
            rSum += addPx[2] - remPx[2];
        }
    }
}

//----------------------------------------------------------------------------
// BackgroundBlur::ApplyBlurWithMask
//
// Downscales the frame to a small working size, blurs there, then
// performs a single full-resolution pass that blends the original
// pixels with the upscaled blurred pixels according to the mask.
//----------------------------------------------------------------------------
void BackgroundBlur::ApplyBlurWithMask( uint8_t* bgraPixels, uint32_t width, uint32_t height, int blurRadius )
{
    const size_t frameBytes = static_cast<size_t>( width ) * height * 4;
    m_blurredFrame.resize( frameBytes );
    m_tempFrame.resize( frameBytes );

    // The input is already capped at 960×540 by WebcamCapture, so blur
    // directly — no need for a secondary downscale.
    int effectiveRadius = (std::max)( 3, blurRadius );

    // 2 iterations of box blur → approximate Gaussian.
    HorizontalBoxBlur( bgraPixels, m_blurredFrame.data(), width, height, effectiveRadius );
    VerticalBoxBlur( m_blurredFrame.data(), m_tempFrame.data(), width, height, effectiveRadius );
    HorizontalBoxBlur( m_tempFrame.data(), m_blurredFrame.data(), width, height, effectiveRadius );
    VerticalBoxBlur( m_blurredFrame.data(), m_tempFrame.data(), width, height, effectiveRadius );

    // Blend pass with alpha support for smooth mask edges.
    const uint8_t* blurData = m_tempFrame.data();
    for( uint32_t y = 0; y < height; y++ )
    {
        uint8_t* dstRow = bgraPixels + static_cast<size_t>( y ) * width * 4;
        const uint8_t* blurRow = blurData + static_cast<size_t>( y ) * width * 4;
        const float* maskRow = m_mask.data() + static_cast<size_t>( y ) * width;

        for( uint32_t x = 0; x < width; x++ )
        {
            float maskVal = maskRow[x];

            // Fast path: fully person → keep original pixel untouched.
            if( maskVal >= 1.0f )
                continue;

            uint8_t* dp = dstRow + x * 4;
            const uint8_t* bp = blurRow + x * 4;

            // Fast path: fully background → copy blurred pixel.
            if( maskVal <= 0.0f )
            {
                *reinterpret_cast<uint32_t*>( dp ) = *reinterpret_cast<const uint32_t*>( bp );
                continue;
            }

            // Edge pixel → alpha blend original and blurred.
            float inv = 1.0f - maskVal;
            dp[0] = static_cast<uint8_t>( dp[0] * maskVal + bp[0] * inv + 0.5f );
            dp[1] = static_cast<uint8_t>( dp[1] * maskVal + bp[1] * inv + 0.5f );
            dp[2] = static_cast<uint8_t>( dp[2] * maskVal + bp[2] * inv + 0.5f );
        }
    }
}

//----------------------------------------------------------------------------
// BackgroundBlur::SetBackgroundImage
//
// Loads an image file via WIC and stores it as a BGRA pixel buffer.
//----------------------------------------------------------------------------
bool BackgroundBlur::SetBackgroundImage( const wchar_t* imagePath )
{
    m_bgImage.clear();
    m_bgImageWidth = 0;
    m_bgImageHeight = 0;
    m_scaledBgImage.clear();
    m_scaledBgW = 0;
    m_scaledBgH = 0;

    if( !imagePath || !*imagePath )
        return false;

    auto factory = wil::CoCreateInstance<IWICImagingFactory>( CLSID_WICImagingFactory );
    if( !factory )
        return false;

    wil::com_ptr<IWICBitmapDecoder> decoder;
    HRESULT hr = factory->CreateDecoderFromFilename(
        imagePath, nullptr, GENERIC_READ, WICDecodeMetadataCacheOnDemand, &decoder );
    if( FAILED( hr ) )
    {
        OutputDebug( L"[BackgroundBlur] Failed to decode image: %s (hr=0x%08X)\n", imagePath, hr );
        return false;
    }

    wil::com_ptr<IWICBitmapFrameDecode> frame;
    hr = decoder->GetFrame( 0, &frame );
    if( FAILED( hr ) )
        return false;

    // Convert to BGRA 32bpp.
    wil::com_ptr<IWICFormatConverter> converter;
    hr = factory->CreateFormatConverter( &converter );
    if( FAILED( hr ) )
        return false;

    hr = converter->Initialize(
        frame.get(), GUID_WICPixelFormat32bppBGRA,
        WICBitmapDitherTypeNone, nullptr, 0.0, WICBitmapPaletteTypeCustom );
    if( FAILED( hr ) )
        return false;

    UINT w = 0, h = 0;
    converter->GetSize( &w, &h );
    if( w == 0 || h == 0 )
        return false;

    m_bgImage.resize( static_cast<size_t>( w ) * h * 4 );
    hr = converter->CopyPixels( nullptr, w * 4, static_cast<UINT>( m_bgImage.size() ), m_bgImage.data() );
    if( FAILED( hr ) )
    {
        m_bgImage.clear();
        return false;
    }

    m_bgImageWidth = w;
    m_bgImageHeight = h;

    OutputDebug( L"[BackgroundBlur] Background image loaded: %ux%u from %s\n", w, h, imagePath );
    return true;
}

//----------------------------------------------------------------------------
// BackgroundBlur::EnsureScaledBgImage
//
// Scales the loaded background image to the specified dimensions using
// nearest-neighbor.  The result is cached and only recomputed when the
// target dimensions change.  The image is center-cropped to preserve
// aspect ratio (like "cover" scaling).
//----------------------------------------------------------------------------
void BackgroundBlur::EnsureScaledBgImage( uint32_t width, uint32_t height )
{
    if( m_scaledBgW == width && m_scaledBgH == height && !m_scaledBgImage.empty() )
        return;

    m_scaledBgImage.resize( static_cast<size_t>( width ) * height * 4 );
    m_scaledBgW = width;
    m_scaledBgH = height;

    // Compute center-crop of the source image to match the target aspect ratio.
    double targetAspect = static_cast<double>( width ) / height;
    double srcAspect = static_cast<double>( m_bgImageWidth ) / m_bgImageHeight;

    uint32_t cropW, cropH, cropX, cropY;
    if( srcAspect > targetAspect )
    {
        // Source is wider — crop horizontally.
        cropH = m_bgImageHeight;
        cropW = static_cast<uint32_t>( m_bgImageHeight * targetAspect + 0.5 );
        cropX = ( m_bgImageWidth - cropW ) / 2;
        cropY = 0;
    }
    else
    {
        // Source is taller — crop vertically.
        cropW = m_bgImageWidth;
        cropH = static_cast<uint32_t>( m_bgImageWidth / targetAspect + 0.5 );
        cropX = 0;
        cropY = ( m_bgImageHeight - cropH ) / 2;
    }

    for( uint32_t y = 0; y < height; y++ )
    {
        uint32_t srcY = cropY + y * cropH / height;
        for( uint32_t x = 0; x < width; x++ )
        {
            uint32_t srcX = cropX + x * cropW / width;
            size_t srcIdx = ( static_cast<size_t>( srcY ) * m_bgImageWidth + srcX ) * 4;
            size_t dstIdx = ( static_cast<size_t>( y ) * width + x ) * 4;
            m_scaledBgImage[dstIdx + 0] = m_bgImage[srcIdx + 0];
            m_scaledBgImage[dstIdx + 1] = m_bgImage[srcIdx + 1];
            m_scaledBgImage[dstIdx + 2] = m_bgImage[srcIdx + 2];
            m_scaledBgImage[dstIdx + 3] = 0xFF;
        }
    }
}

//----------------------------------------------------------------------------
// BackgroundBlur::ApplyImageWithMask
//
// Replaces background pixels with the loaded background image using the
// segmentation mask.  Person pixels are preserved, background pixels come
// from the scaled image.
//----------------------------------------------------------------------------
void BackgroundBlur::ApplyImageWithMask( uint8_t* bgraPixels, uint32_t width, uint32_t height )
{
    EnsureScaledBgImage( width, height );

    const uint8_t* bgData = m_scaledBgImage.data();

    for( uint32_t y = 0; y < height; y++ )
    {
        uint8_t* dstRow = bgraPixels + static_cast<size_t>( y ) * width * 4;
        const uint8_t* bgRow = bgData + static_cast<size_t>( y ) * width * 4;
        const float* maskRow = m_mask.data() + static_cast<size_t>( y ) * width;

        for( uint32_t x = 0; x < width; x++ )
        {
            float maskVal = maskRow[x];

            // Fully person → keep original pixel.
            if( maskVal >= 1.0f )
                continue;

            uint8_t* dp = dstRow + x * 4;
            const uint8_t* bp = bgRow + x * 4;

            // Fully background → copy background image pixel.
            if( maskVal <= 0.0f )
            {
                *reinterpret_cast<uint32_t*>( dp ) = *reinterpret_cast<const uint32_t*>( bp );
                continue;
            }

            // Edge pixel → alpha blend person and background image.
            float inv = 1.0f - maskVal;
            dp[0] = static_cast<uint8_t>( dp[0] * maskVal + bp[0] * inv + 0.5f );
            dp[1] = static_cast<uint8_t>( dp[1] * maskVal + bp[1] * inv + 0.5f );
            dp[2] = static_cast<uint8_t>( dp[2] * maskVal + bp[2] * inv + 0.5f );
        }
    }
}

//----------------------------------------------------------------------------
// BackgroundBlur::ShouldRunInference
//
// Decides whether segmentation inference should run this frame.
// Uses a combination of periodic fallback and motion detection:
// motion is estimated by comparing luminance at a sparse grid of
// sample points with the previous frame.  When the scene changes
// quickly (fast head movement), inference runs every frame.
//----------------------------------------------------------------------------
bool BackgroundBlur::ShouldRunInference( const uint8_t* bgraPixels, uint32_t width, uint32_t height )
{
    // Always run if no cached mask or dimensions changed.
    if( !m_hasCachedMask || m_lastMaskWidth != width || m_lastMaskHeight != height )
        return true;

    // Periodic fallback: run at least every N frames.
    const uint32_t pixels = width * height;
    const int inferenceInterval = ( pixels > 500000 ) ? 6 : 3;
    if( ( m_frameCounter % inferenceInterval ) == 0 )
        return true;

    // Motion detection: sample luminance at a sparse grid and compare
    // with the previous frame.
    constexpr int gridSize = MOTION_GRID_SIZE;
    constexpr int numSamples = gridSize * gridSize;
    float curSamples[numSamples];

    for( int gy = 0; gy < gridSize; gy++ )
    {
        uint32_t sy = ( gy * 2 + 1 ) * height / ( gridSize * 2 );
        for( int gx = 0; gx < gridSize; gx++ )
        {
            uint32_t sx = ( gx * 2 + 1 ) * width / ( gridSize * 2 );
            const uint8_t* px = bgraPixels + ( static_cast<size_t>( sy ) * width + sx ) * 4;
            curSamples[gy * gridSize + gx] = 0.299f * px[2] + 0.587f * px[1] + 0.114f * px[0];
        }
    }

    float motionScore = 0.0f;
    if( m_hasPrevSamples )
    {
        for( int i = 0; i < numSamples; i++ )
        {
            float diff = curSamples[i] - m_prevSamples[i];
            motionScore += diff > 0.0f ? diff : -diff;
        }
        motionScore /= numSamples;
    }

    memcpy( m_prevSamples, curSamples, sizeof( curSamples ) );
    m_hasPrevSamples = true;

    // Average per-sample luminance change > 5/255 indicates significant motion.
    return motionScore > 5.0f;
}

//----------------------------------------------------------------------------
// BackgroundBlur::ApplyImageReplacement
//
// Main entry point for background image replacement mode.
//----------------------------------------------------------------------------
bool BackgroundBlur::ApplyImageReplacement( uint8_t* bgraPixels, uint32_t width, uint32_t height )
{
    if( !m_session || !bgraPixels || width == 0 || height == 0 )
        return false;

    if( m_bgImage.empty() )
        return false;

    if( ShouldRunInference( bgraPixels, width, height ) )
    {
        if( !RunSegmentation( bgraPixels, width, height ) )
            return false;
        m_lastMaskWidth = width;
        m_lastMaskHeight = height;
        m_hasCachedMask = true;
    }
    m_frameCounter++;

    ApplyImageWithMask( bgraPixels, width, height );
    return true;
}

//----------------------------------------------------------------------------
// BackgroundBlur::Apply
//
// Main entry point: runs segmentation and applies blur to the background.
//----------------------------------------------------------------------------
bool BackgroundBlur::Apply( uint8_t* bgraPixels, uint32_t width, uint32_t height, int blurRadius )
{
    if( !m_session || !bgraPixels || width == 0 || height == 0 )
        return false;

    if( ShouldRunInference( bgraPixels, width, height ) )
    {
        if( !RunSegmentation( bgraPixels, width, height ) )
            return false;
        m_lastMaskWidth = width;
        m_lastMaskHeight = height;
        m_hasCachedMask = true;
    }
    m_frameCounter++;

    ApplyBlurWithMask( bgraPixels, width, height, blurRadius );
    return true;
}

//----------------------------------------------------------------------------
// BackgroundBlur::RunSegmentationOnly
//
// Runs the segmentation model and produces the mask, but does NOT blur
// or modify the pixel buffer.  Used when the GPU compute shader will
// perform the box blur instead of the CPU.
//----------------------------------------------------------------------------
bool BackgroundBlur::RunSegmentationOnly( const uint8_t* bgraPixels, uint32_t width, uint32_t height )
{
    if( !m_session || !bgraPixels || width == 0 || height == 0 )
        return false;

    if( ShouldRunInference( bgraPixels, width, height ) )
    {
        // Model-resolution only: skip CPU upscale+feather at frame
        // resolution — the GPU bilinear sampler handles that.
        if( !RunSegmentation( bgraPixels, width, height, /*modelResOnly=*/ true ) )
            return false;
        m_lastMaskWidth = width;
        m_lastMaskHeight = height;
        m_hasCachedMask = true;
    }
    m_frameCounter++;

    return m_hasCachedMask;
}
