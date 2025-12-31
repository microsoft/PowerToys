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
#include <ppltasks.h>
#include <atomic>
#include <algorithm>
#include <chrono>
#include <mutex>
#include <vector>

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

    // Trim and save functionality
    static std::wstring ShowSaveDialogWithTrim(
        HWND hWnd,
        const std::wstring& suggestedFileName,
        const std::wstring& originalVideoPath,
        std::wstring& trimmedVideoPath);

    struct TrimDialogData
    {
        struct GifFrame
        {
            HBITMAP hBitmap{ nullptr };
            winrt::Windows::Foundation::TimeSpan start{ 0 };
            winrt::Windows::Foundation::TimeSpan duration{ 0 };
            UINT width{ 0 };
            UINT height{ 0 };
        };

        std::wstring videoPath;
        winrt::Windows::Foundation::TimeSpan videoDuration{ 0 };
        winrt::Windows::Foundation::TimeSpan trimStart{ 0 };
        winrt::Windows::Foundation::TimeSpan trimEnd{ 0 };
        winrt::Windows::Foundation::TimeSpan currentPosition{ 0 };
        winrt::Windows::Media::Editing::MediaComposition composition{ nullptr };
        winrt::Windows::Media::Playback::MediaPlayer mediaPlayer{ nullptr };
        winrt::Windows::Storage::StorageFile playbackFile{ nullptr };
        HBITMAP hPreviewBitmap{ nullptr };
        HWND hDialog{ nullptr };
        std::atomic<bool> loadingPreview{ false };
        std::atomic<int64_t> latestPreviewRequest{ 0 };
        std::atomic<int64_t> lastRenderedPreview{ -1 };
        std::atomic<bool> isPlaying{ false };
        std::atomic<bool> frameCopyInProgress{ false };
        std::atomic<bool> smoothActive{ false };
        std::atomic<int64_t> smoothBaseTicks{ 0 };
        std::atomic<int64_t> smoothLastSyncMicroseconds{ 0 };
        std::atomic<bool> smoothHasNonZeroSample{ false };
        std::mutex previewBitmapMutex;
        winrt::event_token frameAvailableToken{};
        winrt::event_token positionChangedToken{};
        winrt::event_token stateChangedToken{};
        winrt::com_ptr<ID3D11Device> previewD3DDevice;
        winrt::com_ptr<ID3D11DeviceContext> previewD3DContext;
        winrt::com_ptr<ID3D11Texture2D> previewFrameTexture;
        winrt::com_ptr<ID3D11Texture2D> previewFrameStaging;
        bool hoverPlay{ false };
        bool hoverRewind{ false };
        bool hoverForward{ false };
        bool hoverSkipStart{ false };
        bool hoverSkipEnd{ false };
        winrt::Windows::Foundation::TimeSpan previewOverride{ 0 };
        winrt::Windows::Foundation::TimeSpan positionBeforeOverride{ 0 };
        bool previewOverrideActive{ false };
        bool restorePreviewOnRelease{ false };
        bool playheadPushed{ false };
        int dialogX{ 0 };
        int dialogY{ 0 };
        bool isGif{ false };
        bool previewBitmapOwned{ true };
        std::vector<GifFrame> gifFrames;
        bool gifFramesLoaded{ false };
        size_t gifLastFrameIndex{ 0 };
        std::chrono::steady_clock::time_point gifFrameStartTime{}; // When the current GIF frame started displaying

        // Mouse tracking for timeline
        enum DragMode { None, TrimStart, Position, TrimEnd };
        DragMode dragMode{ None };
        bool isDragging{ false };
        int lastPlayheadX{ -1 }; // Track last playhead pixel position for efficient invalidation
        MMRESULT mmTimerId{ 0 }; // Multimedia timer for smooth MP4 playback

        // Helper to convert time to pixel position
        int TimeToPixel(winrt::Windows::Foundation::TimeSpan time, int timelineWidth) const
        {
            if (timelineWidth <= 0 || videoDuration.count() <= 0)
            {
                return 0;
            }
            double ratio = static_cast<double>(time.count()) / static_cast<double>(videoDuration.count());
            ratio = std::clamp(ratio, 0.0, 1.0);
            return static_cast<int>(ratio * timelineWidth);
        }

        // Helper to convert pixel to time
        winrt::Windows::Foundation::TimeSpan PixelToTime(int pixel, int timelineWidth) const
        {
            if (timelineWidth <= 0 || videoDuration.count() <= 0)
            {
                return winrt::Windows::Foundation::TimeSpan{ 0 };
            }
            int clampedPixel = std::clamp(pixel, 0, timelineWidth);
            double ratio = static_cast<double>(clampedPixel) / static_cast<double>(timelineWidth);
            return winrt::Windows::Foundation::TimeSpan{ static_cast<int64_t>(ratio * videoDuration.count()) };
        }
    };

    static INT_PTR ShowTrimDialog(
        HWND hParent,
        const std::wstring& videoPath,
        winrt::Windows::Foundation::TimeSpan& trimStart,
        winrt::Windows::Foundation::TimeSpan& trimEnd);

private:
    static INT_PTR CALLBACK TrimDialogProc(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam);

    static winrt::Windows::Foundation::IAsyncOperation<winrt::hstring> TrimVideoAsync(
        const std::wstring& sourceVideoPath,
        winrt::Windows::Foundation::TimeSpan trimTimeStart,
        winrt::Windows::Foundation::TimeSpan trimTimeEnd);
    static winrt::Windows::Foundation::IAsyncOperation<winrt::hstring> TrimGifAsync(
        const std::wstring& sourceGifPath,
        winrt::Windows::Foundation::TimeSpan trimTimeStart,
        winrt::Windows::Foundation::TimeSpan trimTimeEnd);
    static INT_PTR ShowTrimDialogInternal(
        HWND hParent,
        const std::wstring& videoPath,
        winrt::Windows::Foundation::TimeSpan& trimStart,
        winrt::Windows::Foundation::TimeSpan& trimEnd);

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