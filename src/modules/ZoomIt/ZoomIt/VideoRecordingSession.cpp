//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Video capture code derived from https://github.com/robmikh/capturevideosample
//
//==============================================================================
#include "pch.h"
#include "VideoRecordingSession.h"
#include "CaptureFrameWait.h"

extern DWORD g_RecordScaling;

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Graphics;
    using namespace Windows::Graphics::Capture;
    using namespace Windows::Graphics::DirectX;
    using namespace Windows::Graphics::DirectX::Direct3D11;
    using namespace Windows::Storage;
    using namespace Windows::UI::Composition;
    using namespace Windows::Media::Core;
    using namespace Windows::Media::Transcoding;
    using namespace Windows::Media::MediaProperties;
}

namespace util
{
    using namespace robmikh::common::uwp;
}

const float CLEAR_COLOR[] = { 0.0f, 0.0f, 0.0f, 1.0f };

int32_t EnsureEven(int32_t value)
{
    if (value % 2 == 0)
    {
        return value;
    }
    else
    {
        return value + 1;
    }
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::VideoRecordingSession
//
//----------------------------------------------------------------------------
VideoRecordingSession::VideoRecordingSession(
    winrt::IDirect3DDevice const& device,
    winrt::GraphicsCaptureItem const& item,
    RECT const cropRect,
    uint32_t frameRate,
    bool captureAudio,
    winrt::Streams::IRandomAccessStream const& stream)
{
    m_device = device;
    m_d3dDevice = GetDXGIInterfaceFromObject<ID3D11Device>(m_device);
    m_d3dDevice->GetImmediateContext(m_d3dContext.put());
    m_item = item;
    auto itemSize = item.Size();
    auto inputWidth = EnsureEven(itemSize.Width);
    auto inputHeight = EnsureEven(itemSize.Height);
    m_frameWait = std::make_shared<CaptureFrameWait>(m_device, m_item, winrt::SizeInt32{ inputWidth, inputHeight });
    auto weakPointer{ std::weak_ptr{ m_frameWait } };
    m_itemClosed = item.Closed(winrt::auto_revoke, [weakPointer](auto&, auto&)
        {
            auto sharedPointer{ weakPointer.lock() };

            if (sharedPointer)
            {
                sharedPointer->StopCapture();
            }
        });

    // Get crop dimension
    if( (cropRect.right - cropRect.left) != 0 )
    {
        m_rcCrop = cropRect;
        m_frameWait->ShowCaptureBorder( false );
    }
    else
    {
        m_rcCrop.left = 0;
        m_rcCrop.top = 0;
        m_rcCrop.right = inputWidth;
        m_rcCrop.bottom = inputHeight;
    }

    // Ensure the video is not too small and try to maintain the aspect ratio
    constexpr int c_minimumSize = 34;
    auto scaledWidth = MulDiv(m_rcCrop.right - m_rcCrop.left, g_RecordScaling, 100);
    auto scaledHeight = MulDiv(m_rcCrop.bottom - m_rcCrop.top, g_RecordScaling, 100);
    auto outputWidth = scaledWidth;
    auto outputHeight = scaledHeight;
    if (outputWidth < c_minimumSize)
    {
        outputWidth = c_minimumSize;
        outputHeight = MulDiv(outputHeight, outputWidth, scaledWidth);
    }
    if (outputHeight < c_minimumSize)
    {
        outputHeight = c_minimumSize;
        outputWidth = MulDiv(outputWidth, outputHeight, scaledHeight);
    }
    if (outputWidth > inputWidth)
    {
        outputWidth = inputWidth;
        outputHeight = c_minimumSize, MulDiv(outputHeight, scaledWidth, outputWidth);
    }
    if (outputHeight > inputHeight)
    {
        outputHeight = inputHeight;
        outputWidth = c_minimumSize, MulDiv(outputWidth, scaledHeight, outputHeight);
    }
    outputWidth = EnsureEven(outputWidth);
    outputHeight = EnsureEven(outputHeight);

    // Describe out output: H264 video with an MP4 container
    m_encodingProfile = winrt::MediaEncodingProfile();
    m_encodingProfile.Container().Subtype(L"MPEG4");
    auto video = m_encodingProfile.Video();
    video.Subtype(L"H264");
    video.Width(outputWidth);
    video.Height(outputHeight);
    video.Bitrate(static_cast<uint32_t>(outputWidth * outputHeight * frameRate * 2 * 0.07));
    video.FrameRate().Numerator(frameRate);
    video.FrameRate().Denominator(1);
    video.PixelAspectRatio().Numerator(1);
    video.PixelAspectRatio().Denominator(1);
    m_encodingProfile.Video(video);

    // if audio capture, set up audio profile
    if (captureAudio)
    {
        auto audio = m_encodingProfile.Audio();
        audio = winrt::AudioEncodingProperties::CreateAac(48000, 1, 16);
        m_encodingProfile.Audio(audio);
	}

    // Describe our input: uncompressed BGRA8 buffers
    auto properties = winrt::VideoEncodingProperties::CreateUncompressed(
        winrt::MediaEncodingSubtypes::Bgra8(),
        static_cast<uint32_t>(m_rcCrop.right - m_rcCrop.left),
        static_cast<uint32_t>(m_rcCrop.bottom - m_rcCrop.top));
    m_videoDescriptor = winrt::VideoStreamDescriptor(properties);

    m_stream = stream;

    m_previewSwapChain = util::CreateDXGISwapChain(
        m_d3dDevice,
        static_cast<uint32_t>(m_rcCrop.right - m_rcCrop.left),
        static_cast<uint32_t>(m_rcCrop.bottom - m_rcCrop.top),
        DXGI_FORMAT_B8G8R8A8_UNORM,
        2);
    winrt::com_ptr<ID3D11Texture2D> backBuffer;
    winrt::check_hresult(m_previewSwapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void()));
    winrt::check_hresult(m_d3dDevice->CreateRenderTargetView(backBuffer.get(), nullptr, m_renderTargetView.put()));

    if( captureAudio ) {

        m_audioGenerator = std::make_unique<AudioSampleGenerator>();
    }
    else {

        m_audioGenerator = nullptr;
    }
}


//----------------------------------------------------------------------------
//
// VideoRecordingSession::~VideoRecordingSession
//
//----------------------------------------------------------------------------
VideoRecordingSession::~VideoRecordingSession()
{
    Close();
}


//----------------------------------------------------------------------------
//
// VideoRecordingSession::StartAsync
//
//----------------------------------------------------------------------------
winrt::IAsyncAction VideoRecordingSession::StartAsync()
{
    auto expected = false;
    if (m_isRecording.compare_exchange_strong(expected, true))
    {

        // Create our MediaStreamSource
        if(m_audioGenerator) {

            co_await m_audioGenerator->InitializeAsync();
            m_streamSource = winrt::MediaStreamSource(m_videoDescriptor, winrt::AudioStreamDescriptor(m_audioGenerator->GetEncodingProperties()));
        }
        else {

            m_streamSource = winrt::MediaStreamSource(m_videoDescriptor);
        }
        m_streamSource.BufferTime(std::chrono::seconds(0));
        m_streamSource.Starting({ this, &VideoRecordingSession::OnMediaStreamSourceStarting });
        m_streamSource.SampleRequested({ this, &VideoRecordingSession::OnMediaStreamSourceSampleRequested });

        // Create our transcoder
        m_transcoder = winrt::MediaTranscoder();
        m_transcoder.HardwareAccelerationEnabled(true);

        auto self = shared_from_this();

        // Start encoding
        auto transcode = co_await m_transcoder.PrepareMediaStreamSourceTranscodeAsync(m_streamSource, m_stream, m_encodingProfile);
        co_await transcode.TranscodeAsync();
    }
    co_return;
}


//----------------------------------------------------------------------------
//
// VideoRecordingSession::Close
//
//----------------------------------------------------------------------------
void VideoRecordingSession::Close()
{
    auto expected = false;
    if (m_closed.compare_exchange_strong(expected, true))
    {
        expected = true;
        if (!m_isRecording.compare_exchange_strong(expected, false))
        {
            CloseInternal();
        }
        else
        {
            m_frameWait->StopCapture();
        }
    }
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::CloseInternal
//
//----------------------------------------------------------------------------
void VideoRecordingSession::CloseInternal()
{
    if(m_audioGenerator) {
        m_audioGenerator->Stop();
    }
    m_frameWait->StopCapture();
    m_itemClosed.revoke();
}


//----------------------------------------------------------------------------
//
// VideoRecordingSession::OnMediaStreamSourceStarting
//
//----------------------------------------------------------------------------
void VideoRecordingSession::OnMediaStreamSourceStarting(
    winrt::MediaStreamSource const&,
    winrt::MediaStreamSourceStartingEventArgs const& args)
{
    auto frame = m_frameWait->TryGetNextFrame();
    if (frame) {
        args.Request().SetActualStartPosition(frame->SystemRelativeTime);
        if (m_audioGenerator) {

            m_audioGenerator->Start();
        }
    }
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::Create
//
//----------------------------------------------------------------------------
std::shared_ptr<VideoRecordingSession> VideoRecordingSession::Create(
    winrt::IDirect3DDevice const& device,
    winrt::GraphicsCaptureItem const& item,
    RECT const& crop,
    uint32_t frameRate,
    bool captureAudio,
    winrt::Streams::IRandomAccessStream const& stream)
{
    return std::shared_ptr<VideoRecordingSession>(new VideoRecordingSession(device, item, crop, frameRate, captureAudio, stream));
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::OnMediaStreamSourceSampleRequested
//
//----------------------------------------------------------------------------
void VideoRecordingSession::OnMediaStreamSourceSampleRequested(
    winrt::MediaStreamSource const&,
    winrt::MediaStreamSourceSampleRequestedEventArgs const& args)
{
    auto request = args.Request();
    auto streamDescriptor = request.StreamDescriptor();
    if (auto videoStreamDescriptor = streamDescriptor.try_as<winrt::VideoStreamDescriptor>())
    {
        if (auto frame = m_frameWait->TryGetNextFrame())
        {
            try
            {
                auto timeStamp = frame->SystemRelativeTime;
                auto contentSize = frame->ContentSize;
                auto frameTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(frame->FrameTexture);
                D3D11_TEXTURE2D_DESC desc = {};
                frameTexture->GetDesc(&desc);

                winrt::com_ptr<ID3D11Texture2D> backBuffer;
                winrt::check_hresult(m_previewSwapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void()));

                // Use the smaller of the crop size or content size. The content
                // size can change while recording, for example by resizing the
                // window. This ensures that only valid content is copied.
                auto width = min(m_rcCrop.right - m_rcCrop.left, contentSize.Width);
                auto height = min(m_rcCrop.bottom - m_rcCrop.top, contentSize.Height);

                // Set the content region to copy and clamp the coordinates to the
                // texture surface.
                D3D11_BOX region = {};
                region.left = std::clamp(m_rcCrop.left, static_cast<LONG>(0), static_cast<LONG>(desc.Width));
                region.right = std::clamp(m_rcCrop.left + width, static_cast<LONG>(0), static_cast<LONG>(desc.Width));
                region.top = std::clamp(m_rcCrop.top, static_cast<LONG>(0), static_cast<LONG>(desc.Height));
                region.bottom = std::clamp(m_rcCrop.top + height, static_cast<LONG>(0), static_cast<LONG>(desc.Height));
                region.back = 1;

                m_d3dContext->ClearRenderTargetView(m_renderTargetView.get(), CLEAR_COLOR);
                m_d3dContext->CopySubresourceRegion(
                    backBuffer.get(),
                    0,
                    0, 0, 0,
                    frameTexture.get(),
                    0,
                    &region);

                desc = {};
                backBuffer->GetDesc(&desc);

                desc.Usage = D3D11_USAGE_DEFAULT;
                desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
                desc.CPUAccessFlags = 0;
                desc.MiscFlags = 0;
                winrt::com_ptr<ID3D11Texture2D> sampleTexture;
                winrt::check_hresult(m_d3dDevice->CreateTexture2D(&desc, nullptr, sampleTexture.put()));
                m_d3dContext->CopyResource(sampleTexture.get(), backBuffer.get());
                auto dxgiSurface = sampleTexture.as<IDXGISurface>();
                auto sampleSurface = CreateDirect3DSurface(dxgiSurface.get());

                DXGI_PRESENT_PARAMETERS presentParameters{};
                winrt::check_hresult(m_previewSwapChain->Present1(0, 0, &presentParameters));

                auto sample = winrt::MediaStreamSample::CreateFromDirect3D11Surface(sampleSurface, timeStamp);
                request.Sample(sample);
            }
            catch (winrt::hresult_error const& error)
            {
                OutputDebugStringW(error.message().c_str());
                request.Sample(nullptr);
                CloseInternal();
                return;
            }
        }
        else
        {
            request.Sample(nullptr);
            CloseInternal();
        }
    } 
    else if (auto audioStreamDescriptor = streamDescriptor.try_as<winrt::AudioStreamDescriptor>())
    {
        if (auto sample = m_audioGenerator->TryGetNextSample())
        {
            request.Sample(sample.value());
        }
        else
        {
            request.Sample(nullptr);
        }
    }
}
