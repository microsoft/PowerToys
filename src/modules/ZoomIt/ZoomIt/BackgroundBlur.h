//==============================================================================
//
// BackgroundBlur.h
//
// Performs person segmentation using Windows ML (Windows.AI.MachineLearning)
// and applies either a Gaussian blur or a custom background image to the
// background of a BGRA webcam frame.  The segmentation model runs on CPU
// via the Windows ML default device, keeping the GPU free for recording.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#pragma once

#include <vector>
#include <string>
#include <cstdint>
#include <winrt/Windows.AI.MachineLearning.h>

// Background processing mode for the webcam overlay.
enum class WebcamBackgroundMode : uint32_t
{
    None = 0,   // No background processing
    Blur = 1,   // Gaussian blur on the background
    Image = 2,  // Replace background with a user-chosen image
};

class BackgroundBlur
{
public:
    BackgroundBlur() = default;
    ~BackgroundBlur() = default;

    // Initialize the ONNX model.  modelPath must point to a valid .onnx
    // segmentation model file.  Returns true on success.
    bool Initialize( const wchar_t* modelPath );

    // Load a background replacement image from the given file path.
    // The image is decoded via WIC and stored as a BGRA buffer.
    // Returns true on success.
    bool SetBackgroundImage( const wchar_t* imagePath );

    // Returns true if a background image has been loaded.
    bool HasBackgroundImage() const { return !m_bgImage.empty(); }

    // Apply background blur to a BGRA pixel buffer in-place.
    // width/height are the frame dimensions.  blurRadius controls
    // the strength of the Gaussian blur (in pixels).
    // Returns true if segmentation + blur was applied successfully.
    bool Apply( uint8_t* bgraPixels, uint32_t width, uint32_t height, int blurRadius = 21 );

    // Apply background image replacement to a BGRA pixel buffer in-place.
    // Uses the previously loaded background image (via SetBackgroundImage).
    // Returns true if segmentation + image replacement was applied.
    bool ApplyImageReplacement( uint8_t* bgraPixels, uint32_t width, uint32_t height );

    // Returns true if the model has been loaded successfully.
    bool IsInitialized() const { return m_session != nullptr; }

    // Access the segmentation mask after Apply()/ApplyImageReplacement().
    // The mask has one float [0..1] per pixel at the processing resolution
    // (1.0 = person / foreground, 0.0 = background).
    const std::vector<float>& GetMask() const { return m_mask; }
    uint32_t GetMaskWidth() const { return m_lastMaskWidth; }
    uint32_t GetMaskHeight() const { return m_lastMaskHeight; }
    bool HasCachedMask() const { return m_hasCachedMask; }

    // Run segmentation only (no CPU blur or mask blend).  Use this when
    // the blur will be performed on the GPU via a compute shader.
    // Populates the mask (GetMask) but does NOT touch bgraPixels.
    bool RunSegmentationOnly( const uint8_t* bgraPixels, uint32_t width, uint32_t height );

    // Access the fully-blurred frame after Apply().
    // Contains all pixels blurred (before mask-based compositing).
    // Only valid after Apply() — NOT after ApplyImageReplacement().
    const std::vector<uint8_t>& GetBlurredFrame() const { return m_tempFrame; }

    // Access the model-resolution mask before upscaling (e.g. 256×256).
    // Useful when the GPU handles upscaling via hardware bilinear filtering.
    const std::vector<float>& GetModelMask() const { return m_erodeBuf; }
    int64_t GetModelMaskWidth() const { return m_modelOutputWidth; }
    int64_t GetModelMaskHeight() const { return m_modelOutputHeight; }

private:
    // Run the segmentation model and produce a float mask [0..1] per pixel.
    // When modelResOnly is true, stops after model-resolution post-processing
    // (feathering + temporal smoothing at 256×256) and skips the CPU upscale
    // to frame resolution — the GPU bilinear sampler handles that instead.
    bool RunSegmentation( const uint8_t* bgraPixels, uint32_t width, uint32_t height,
                          bool modelResOnly = false );

    // Apply box blur (iterated for Gaussian approximation) to bgraPixels
    // only where the mask indicates background.
    void ApplyBlurWithMask( uint8_t* bgraPixels, uint32_t width, uint32_t height, int blurRadius );

    // Replace background pixels with the loaded background image.
    void ApplyImageWithMask( uint8_t* bgraPixels, uint32_t width, uint32_t height );

    // Scale the loaded background image to the given dimensions (cached).
    void EnsureScaledBgImage( uint32_t width, uint32_t height );

    // Decide whether inference is needed this frame (periodic + motion-adaptive).
    bool ShouldRunInference( const uint8_t* bgraPixels, uint32_t width, uint32_t height );

    // Windows ML objects.
    winrt::Windows::AI::MachineLearning::LearningModel          m_model{ nullptr };
    winrt::Windows::AI::MachineLearning::LearningModelSession   m_session{ nullptr };
    winrt::Windows::AI::MachineLearning::LearningModelBinding   m_binding{ nullptr };
    winrt::hstring                                              m_inputName;
    winrt::hstring                                              m_outputName;

    // Model metadata (detected from the loaded model).
    int64_t                 m_modelInputWidth = 256;
    int64_t                 m_modelInputHeight = 256;
    int64_t                 m_modelInputChannels = 3;
    bool                    m_inputIsNchw = true; // true = [1,C,H,W], false = [1,H,W,C]
    bool                    m_usingGpu = false;   // true if DirectML session is active

    // Actual model output dimensions (may differ from input dimensions).
    int64_t                 m_modelOutputWidth = 256;
    int64_t                 m_modelOutputHeight = 256;

    // Reusable buffers to avoid per-frame allocations.
    std::vector<float>      m_inputTensor;      // RGB float [1,3,H,W] or [1,H,W,3]
    std::vector<float>      m_outputBuf;        // Raw copy of output tensor data
    std::vector<float>      m_mask;             // Segmentation mask [width*height]
    std::vector<float>      m_erodeBuf;         // Model-resolution mask buffer
    std::vector<float>      m_maskBlurBuf;      // Temp buffer for mask edge smoothing
    std::vector<uint8_t>    m_blurredFrame;     // Temporary blurred copy
    std::vector<uint8_t>    m_tempFrame;        // Second temp buffer for blur passes

    // Background image (original resolution, BGRA).
    std::vector<uint8_t>    m_bgImage;
    uint32_t                m_bgImageWidth = 0;
    uint32_t                m_bgImageHeight = 0;

    // Scaled background image (cached at overlay dimensions).
    std::vector<uint8_t>    m_scaledBgImage;
    uint32_t                m_scaledBgW = 0;
    uint32_t                m_scaledBgH = 0;

    // Frame-skipping: reuse the segmentation mask for N frames.
    int                     m_frameCounter = 0;
    uint32_t                m_lastMaskWidth = 0;
    uint32_t                m_lastMaskHeight = 0;
    bool                    m_hasCachedMask = false;

    // Motion detection: luminance samples from the previous frame.
    static constexpr int    MOTION_GRID_SIZE = 8; // 8×8 = 64 sample points
    float                   m_prevSamples[MOTION_GRID_SIZE * MOTION_GRID_SIZE] = {};
    bool                    m_hasPrevSamples = false;

    // Temporal smoothing: previous frame's mask blended with current
    // to stabilize edges and reduce flicker.
    std::vector<float>      m_prevMask;

    // Model-resolution previous mask for GPU path temporal smoothing.
    std::vector<float>      m_prevModelMask;
};
