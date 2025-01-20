//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Video capture code derived from https://github.com/robmikh/capturevideosample
//
//==============================================================================
#pragma once

#include "CaptureFrameWait.h"
#include "AudioSampleGenerator.h"
#include <d3d11_4.h>

class VideoRecordingSession : public std::enable_shared_from_this<VideoRecordingSession>
{
public:
    [[nodiscard]] static std::shared_ptr<VideoRecordingSession> Create(
        winrt::Direct3D11::IDirect3DDevice const& device,
        winrt::GraphicsCaptureItem const& item,
        RECT const& cropRect,
        uint32_t frameRate,
        bool captureAudio,
        winrt::Streams::IRandomAccessStream const& stream);
    ~VideoRecordingSession();

    winrt::IAsyncAction StartAsync();
    void EnableCursorCapture(bool enable = true) { m_frameWait->EnableCursorCapture(enable); }
    void Close();

private:
    VideoRecordingSession(
        winrt::Direct3D11::IDirect3DDevice const& device,
        winrt::Capture::GraphicsCaptureItem const& item,
        RECT const cropRect,
        uint32_t frameRate,
        bool captureAudio,
        winrt::Streams::IRandomAccessStream const& stream);
    void CloseInternal();

    void OnMediaStreamSourceStarting(
        winrt::MediaStreamSource const& sender,
        winrt::MediaStreamSourceStartingEventArgs const& args);
    void OnMediaStreamSourceSampleRequested(
        winrt::MediaStreamSource const& sender,
        winrt::MediaStreamSourceSampleRequestedEventArgs const& args);

private:
    winrt::Direct3D11::IDirect3DDevice m_device{ nullptr };
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;
    RECT m_rcCrop;

    winrt::GraphicsCaptureItem m_item{ nullptr };
    winrt::GraphicsCaptureItem::Closed_revoker m_itemClosed;
    std::shared_ptr<CaptureFrameWait> m_frameWait;

    winrt::Streams::IRandomAccessStream m_stream{ nullptr };
    winrt::MediaEncodingProfile m_encodingProfile{ nullptr };
    winrt::VideoStreamDescriptor m_videoDescriptor{ nullptr };
    winrt::MediaStreamSource m_streamSource{ nullptr };
    winrt::MediaTranscoder m_transcoder{ nullptr };

    std::unique_ptr<AudioSampleGenerator> m_audioGenerator;

    winrt::com_ptr<IDXGISwapChain1> m_previewSwapChain;
    winrt::com_ptr<ID3D11RenderTargetView> m_renderTargetView;

    std::atomic<bool> m_isRecording = false;
    std::atomic<bool> m_closed = false;
};