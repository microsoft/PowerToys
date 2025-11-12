//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// GIF recording support using Windows Imaging Component (WIC)
//
//==============================================================================
#pragma once

#include "CaptureFrameWait.h"
#include <d3d11_4.h>
#include <vector>

class GifRecordingSession : public std::enable_shared_from_this<GifRecordingSession>
{
public:
    [[nodiscard]] static std::shared_ptr<GifRecordingSession> Create(
        winrt::Direct3D11::IDirect3DDevice const& device,
        winrt::GraphicsCaptureItem const& item,
        RECT const& cropRect,
        uint32_t frameRate,
        winrt::Streams::IRandomAccessStream const& stream);
    ~GifRecordingSession();

    winrt::IAsyncAction StartAsync();
    void EnableCursorCapture(bool enable = true) { m_frameWait->EnableCursorCapture(enable); }
    void Close();

private:
    GifRecordingSession(
        winrt::Direct3D11::IDirect3DDevice const& device,
        winrt::Capture::GraphicsCaptureItem const& item,
        RECT const cropRect,
        uint32_t frameRate,
        winrt::Streams::IRandomAccessStream const& stream);
    void CloseInternal();
    HRESULT EncodeFrame(ID3D11Texture2D* texture);

private:
    winrt::Direct3D11::IDirect3DDevice m_device{ nullptr };
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;
    RECT m_rcCrop;
    uint32_t m_frameRate;

    winrt::GraphicsCaptureItem m_item{ nullptr };
    winrt::GraphicsCaptureItem::Closed_revoker m_itemClosed;
    std::shared_ptr<CaptureFrameWait> m_frameWait;

    winrt::Streams::IRandomAccessStream m_stream{ nullptr };

    // WIC components for GIF encoding
    winrt::com_ptr<IWICImagingFactory> m_wicFactory;
    winrt::com_ptr<IWICStream> m_wicStream;
    winrt::com_ptr<IWICBitmapEncoder> m_gifEncoder;
    winrt::com_ptr<IWICMetadataQueryWriter> m_encoderMetadataWriter;

    std::atomic<bool> m_isRecording = false;
    std::atomic<bool> m_closed = false;

    uint32_t m_frameWidth=0;
    uint32_t m_frameHeight=0;
    uint32_t m_frameDelay=0;
    uint32_t m_frameCount = 0;

    int32_t m_width=0;
    int32_t m_height=0;
};
