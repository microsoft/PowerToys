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
    using namespace Windows::Media::Editing;
    using namespace Windows::Media::Playback;
    using namespace Windows::Storage::FileProperties;
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

namespace
{
    constexpr int kTimelinePadding = 12;
    constexpr int kTimelineTrackHeight = 24;
    constexpr int kTimelineTrackTopOffset = 18;
    constexpr int kTimelineHandleHalfWidth = 7;
    constexpr int kTimelineHandleHeight = 40;
    constexpr int kTimelineHandleHitRadius = 18;
    constexpr int64_t kJogStepTicks = 20'000'000;   // 2 seconds (or 1s for short videos)

    void CenterTrimDialog(HWND hDlg)
    {
        if (!hDlg)
        {
            return;
        }

        RECT rcDlg{};
        if (!GetWindowRect(hDlg, &rcDlg))
        {
            return;
        }

        const int dlgWidth = rcDlg.right - rcDlg.left;
        const int dlgHeight = rcDlg.bottom - rcDlg.top;

        RECT rcTarget{};
        HWND hParent = GetParent(hDlg);
        if (hParent && GetWindowRect(hParent, &rcTarget))
        {
            // Use parent window bounds when available.
        }
        else
        {
            HMONITOR monitor = MonitorFromWindow(hDlg, MONITOR_DEFAULTTONEAREST);
            MONITORINFO mi{ sizeof(mi) };
            if (GetMonitorInfo(monitor, &mi))
            {
                rcTarget = mi.rcWork;
            }
            else
            {
                rcTarget.left = 0;
                rcTarget.top = 0;
                rcTarget.right = GetSystemMetrics(SM_CXSCREEN);
                rcTarget.bottom = GetSystemMetrics(SM_CYSCREEN);
            }
        }

        const int targetWidth = rcTarget.right - rcTarget.left;
        const int targetHeight = rcTarget.bottom - rcTarget.top;

        int newX = rcTarget.left + (targetWidth - dlgWidth) / 2;
        int newY = rcTarget.top + (targetHeight - dlgHeight) / 2;

        if (dlgWidth >= targetWidth)
        {
            newX = rcTarget.left;
        }
        else
        {
            newX = static_cast<int>((std::clamp)(static_cast<LONG>(newX), rcTarget.left, rcTarget.right - dlgWidth));
        }

        if (dlgHeight >= targetHeight)
        {
            newY = rcTarget.top;
        }
        else
        {
            newY = static_cast<int>((std::clamp)(static_cast<LONG>(newY), rcTarget.top, rcTarget.bottom - dlgHeight));
        }

        SetWindowPos(hDlg, nullptr, newX, newY, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOZORDER);
    }

    std::wstring FormatTrimTime(const winrt::TimeSpan& value, bool includeMilliseconds)
    {
        const int64_t ticks = (std::max)(value.count(), int64_t{ 0 });
        const int64_t totalMilliseconds = ticks / 10000LL;
        const int milliseconds = static_cast<int>(totalMilliseconds % 1000);
        const int64_t totalSeconds = totalMilliseconds / 1000LL;
        const int seconds = static_cast<int>(totalSeconds % 60LL);
        const int64_t totalMinutes = totalSeconds / 60LL;
        const int minutes = static_cast<int>(totalMinutes % 60LL);
        const int hours = static_cast<int>(totalMinutes / 60LL);

        wchar_t buffer[32]{};
        if (hours > 0)
        {
            swprintf_s(buffer, L"%d:%02d:%02d", hours, minutes, seconds);
        }
        else
        {
            swprintf_s(buffer, L"%02d:%02d", minutes, seconds);
        }

        if (!includeMilliseconds)
        {
            return std::wstring(buffer);
        }

        wchar_t msBuffer[8]{};
        swprintf_s(msBuffer, L".%03d", milliseconds);
        return std::wstring(buffer) + msBuffer;
    }

    std::wstring FormatDurationString(const winrt::TimeSpan& duration)
    {
        return L"Selection: " + FormatTrimTime(duration, true);
    }

    void SetTimeText(HWND hDlg, int controlId, const winrt::TimeSpan& value, bool includeMilliseconds)
    {
        const std::wstring formatted = FormatTrimTime(value, includeMilliseconds);
        SetDlgItemText(hDlg, controlId, formatted.c_str());
    }

    int TimelineTimeToClientX(const VideoRecordingSession::TrimDialogData* pData, winrt::TimeSpan value, int clientWidth)
    {
        const int trackWidth = (std::max)(clientWidth - kTimelinePadding * 2, 1);
        return kTimelinePadding + pData->TimeToPixel(value, trackWidth);
    }

    winrt::TimeSpan TimelinePixelToTime(const VideoRecordingSession::TrimDialogData* pData, int x, int clientWidth)
    {
        const int trackWidth = (std::max)(clientWidth - kTimelinePadding * 2, 1);
        const int localX = std::clamp(x - kTimelinePadding, 0, trackWidth);
        return pData->PixelToTime(localX, trackWidth);
    }

    void UpdateDurationDisplay(HWND hDlg, VideoRecordingSession::TrimDialogData* pData)
    {
        if (!pData || !hDlg)
        {
            return;
        }

        const int64_t selectionTicks = (std::max)(pData->trimEnd.count() - pData->trimStart.count(), int64_t{ 0 });
        const std::wstring durationText = FormatDurationString(winrt::TimeSpan{ selectionTicks });
        SetDlgItemText(hDlg, IDC_TRIM_DURATION_LABEL, durationText.c_str());
    }

        RECT GetTimelineTrackRect(const RECT& clientRect)
        {
            const int trackLeft = clientRect.left + kTimelinePadding;
            const int trackRight = clientRect.right - kTimelinePadding;
            const int trackTop = clientRect.top + kTimelineTrackTopOffset;
            const int trackBottom = trackTop + kTimelineTrackHeight;
            RECT track{ trackLeft, trackTop, trackRight, trackBottom };
            return track;
        }

        RECT GetPlayheadBoundsRect(const RECT& clientRect, int x)
        {
            RECT track = GetTimelineTrackRect(clientRect);
            RECT lineRect{ x - 2, track.top - 12, x + 3, track.bottom + 22 };
            RECT circleRect{ x - 6, track.bottom + 12, x + 6, track.bottom + 24 };
            RECT combined{};
            UnionRect(&combined, &lineRect, &circleRect);
            return combined;
        }

        void InvalidatePlayheadRegion(HWND hTimeline, const RECT& clientRect, int previousX, int newX)
        {
            if (!hTimeline)
            {
                return;
            }

            RECT invalidRect{};
            bool hasRect = false;

            if (previousX >= 0)
            {
                RECT oldRect = GetPlayheadBoundsRect(clientRect, previousX);
                invalidRect = oldRect;
                hasRect = true;
            }

            if (newX >= 0)
            {
                RECT newRect = GetPlayheadBoundsRect(clientRect, newX);
                if (hasRect)
                {
                    RECT unionRect{};
                    UnionRect(&unionRect, &invalidRect, &newRect);
                    invalidRect = unionRect;
                }
                else
                {
                    invalidRect = newRect;
                    hasRect = true;
                }
            }

            if (hasRect)
            {
                InflateRect(&invalidRect, 2, 2);
                InvalidateRect(hTimeline, &invalidRect, FALSE);
            }
        }
}

static void StopPlayback(HWND hDlg, VideoRecordingSession::TrimDialogData* pData);
static winrt::fire_and_forget StartPlaybackAsync(HWND hDlg, VideoRecordingSession::TrimDialogData* pData);


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

//----------------------------------------------------------------------------
//
// Custom file dialog events handler for Trim button
//
//----------------------------------------------------------------------------
class CTrimFileDialogEvents : public IFileDialogEvents, public IFileDialogControlEvents
{
private:
    long m_cRef;
    HWND m_hParent;
    std::wstring m_videoPath;
    std::wstring* m_pTrimmedPath;
    winrt::TimeSpan* m_pTrimStart;
    winrt::TimeSpan* m_pTrimEnd;
    bool* m_pShouldTrim;

public:
    CTrimFileDialogEvents(HWND hParent, const std::wstring& videoPath, 
                          std::wstring* pTrimmedPath, winrt::TimeSpan* pTrimStart, 
                          winrt::TimeSpan* pTrimEnd, bool* pShouldTrim) 
        : m_cRef(1), m_hParent(hParent), m_videoPath(videoPath),
          m_pTrimmedPath(pTrimmedPath), m_pTrimStart(pTrimStart), 
          m_pTrimEnd(pTrimEnd), m_pShouldTrim(pShouldTrim)
    {
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CTrimFileDialogEvents, IFileDialogEvents),
            QITABENT(CTrimFileDialogEvents, IFileDialogControlEvents),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&m_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        long cRef = InterlockedDecrement(&m_cRef);
        if (!cRef)
            delete this;
        return cRef;
    }

    // IFileDialogEvents
    IFACEMETHODIMP OnFileOk(IFileDialog*) { return S_OK; }
    IFACEMETHODIMP OnFolderChange(IFileDialog*) { return S_OK; }
    IFACEMETHODIMP OnFolderChanging(IFileDialog*, IShellItem*) { return S_OK; }
    IFACEMETHODIMP OnSelectionChange(IFileDialog*) { return S_OK; }
    IFACEMETHODIMP OnShareViolation(IFileDialog*, IShellItem*, FDE_SHAREVIOLATION_RESPONSE*) { return S_OK; }
    IFACEMETHODIMP OnTypeChange(IFileDialog*) { return S_OK; }
    IFACEMETHODIMP OnOverwrite(IFileDialog*, IShellItem*, FDE_OVERWRITE_RESPONSE*) { return S_OK; }

    // IFileDialogControlEvents
    IFACEMETHODIMP OnItemSelected(IFileDialogCustomize*, DWORD, DWORD) { return S_OK; }
    IFACEMETHODIMP OnCheckButtonToggled(IFileDialogCustomize*, DWORD, BOOL) { return S_OK; }
    IFACEMETHODIMP OnControlActivating(IFileDialogCustomize*, DWORD) { return S_OK; }

    IFACEMETHODIMP OnButtonClicked(IFileDialogCustomize* pfdc, DWORD dwIDCtl)
    {
        if (dwIDCtl == 2000) // Trim button ID
        {
            // Get the file dialog's window handle to make trim dialog modal to it
            wil::com_ptr<IFileDialog> pfd;
            HWND hFileDlg = nullptr;
            if (SUCCEEDED(pfdc->QueryInterface(IID_PPV_ARGS(&pfd))))
            {
                wil::com_ptr<IOleWindow> pOleWnd;
                if (SUCCEEDED(pfd->QueryInterface(IID_PPV_ARGS(&pOleWnd))))
                {
                    pOleWnd->GetWindow(&hFileDlg);
                }
            }
            
            // Use file dialog window as parent if found, otherwise use original parent
            HWND hParent = hFileDlg ? hFileDlg : m_hParent;
            
            auto trimResult = VideoRecordingSession::ShowTrimDialog(hParent, m_videoPath, *m_pTrimStart, *m_pTrimEnd);
            if (trimResult == IDOK)
            {
                *m_pShouldTrim = true;
            }
        }
        return S_OK;
    }
};

//----------------------------------------------------------------------------
//
// VideoRecordingSession::ShowSaveDialogWithTrim
//
// Main entry point for trim+save workflow
//
//----------------------------------------------------------------------------
std::wstring VideoRecordingSession::ShowSaveDialogWithTrim(
    HWND hParent,
    const std::wstring& suggestedFileName,
    const std::wstring& originalVideoPath,
    std::wstring& trimmedVideoPath)
{
    trimmedVideoPath.clear();

    std::wstring videoPathToSave = originalVideoPath;
    winrt::TimeSpan trimStart{ 0 };
    winrt::TimeSpan trimEnd{ 0 };
    bool shouldTrim = false;

    // Create save dialog with custom Trim button
    auto saveDialog = wil::CoCreateInstance<::IFileSaveDialog>(CLSID_FileSaveDialog);

    FILEOPENDIALOGOPTIONS options;
    if (SUCCEEDED(saveDialog->GetOptions(&options)))
        saveDialog->SetOptions(options | FOS_FORCEFILESYSTEM);

    wil::com_ptr<::IShellItem> videosItem;
    if (SUCCEEDED(SHGetKnownFolderItem(FOLDERID_Videos, KF_FLAG_DEFAULT, nullptr,
        IID_IShellItem, (void**)videosItem.put())))
        saveDialog->SetDefaultFolder(videosItem.get());

    saveDialog->SetDefaultExtension(L".mp4");

    COMDLG_FILTERSPEC fileTypes[] = {
        { L"MP4 Video", L"*.mp4" }
    };
    saveDialog->SetFileTypes(_countof(fileTypes), fileTypes);
    saveDialog->SetFileName(suggestedFileName.c_str());

    // Add custom Trim button
    wil::com_ptr<IFileDialogCustomize> pfdCustomize;
    if (SUCCEEDED(saveDialog->QueryInterface(IID_PPV_ARGS(&pfdCustomize))))
    {
        pfdCustomize->AddPushButton(2000, L"Trim...");
    }

    // Set up event handler
    CTrimFileDialogEvents* pEvents = new CTrimFileDialogEvents(hParent, originalVideoPath, 
        &trimmedVideoPath, &trimStart, &trimEnd, &shouldTrim);
    DWORD dwCookie;
    saveDialog->Advise(pEvents, &dwCookie);

    HRESULT hr = saveDialog->Show(hParent);
    
    saveDialog->Unadvise(dwCookie);
    pEvents->Release();

    if (FAILED(hr))
    {
        return std::wstring(); // User cancelled save dialog
    }

    // If user clicked Trim button and confirmed, perform the trim
    if (shouldTrim)
    {
        try
        {
            auto trimOp = TrimVideoAsync(originalVideoPath, trimStart, trimEnd);
            
            // Pump messages while waiting for async operation
            while (trimOp.Status() == winrt::AsyncStatus::Started)
            {
                MSG msg;
                while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                }
                Sleep(10);
            }
            
            auto trimmedPath = std::wstring(trimOp.GetResults());

            if (trimmedPath.empty())
            {
                MessageBox(hParent, L"Failed to trim video", L"Error", MB_OK | MB_ICONERROR);
                return std::wstring();
            }

            trimmedVideoPath = trimmedPath;
            videoPathToSave = trimmedPath;
        }
        catch (...)
        {
            MessageBox(hParent, L"Failed to trim video", L"Error", MB_OK | MB_ICONERROR);
            return std::wstring();
        }
    }

    wil::com_ptr<::IShellItem> item;
    THROW_IF_FAILED(saveDialog->GetResult(item.put()));

    wil::unique_cotaskmem_string filePath;
    THROW_IF_FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, filePath.put()));

    return std::wstring(filePath.get());
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::ShowTrimDialog
//
// Shows the trim UI dialog
//
//----------------------------------------------------------------------------
INT_PTR VideoRecordingSession::ShowTrimDialog(
    HWND hParent,
    const std::wstring& videoPath,
    winrt::TimeSpan& trimStart,
    winrt::TimeSpan& trimEnd)
{
    std::promise<INT_PTR> resultPromise;
    auto resultFuture = resultPromise.get_future();

    std::thread staThread([hParent, videoPath, &trimStart, &trimEnd, promise = std::move(resultPromise)]() mutable
    {
        bool coInitialized = false;
        try
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
        }
        catch (const winrt::hresult_error&)
        {
            HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            if (SUCCEEDED(hr))
            {
                coInitialized = true;
            }
        }

        try
        {
            INT_PTR dlgResult = ShowTrimDialogInternal(hParent, videoPath, trimStart, trimEnd);
            promise.set_value(dlgResult);
        }
        catch (...)
        {
            promise.set_exception(std::current_exception());
        }

        if (coInitialized)
        {
            CoUninitialize();
        }
    });

    while (resultFuture.wait_for(std::chrono::milliseconds(20)) != std::future_status::ready)
    {
        MSG msg;
        while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    INT_PTR dialogResult = resultFuture.get();
    if (staThread.joinable())
    {
        staThread.join();
    }
    return dialogResult;
}

INT_PTR VideoRecordingSession::ShowTrimDialogInternal(
    HWND hParent,
    const std::wstring& videoPath,
    winrt::TimeSpan& trimStart,
    winrt::TimeSpan& trimEnd)
{
    TrimDialogData data;
    data.videoPath = videoPath;
    data.trimStart = winrt::TimeSpan{ 0 };
    data.trimEnd = winrt::TimeSpan{ 0 };

    // Get video duration - use simple file size estimation to avoid blocking
    // The actual trim operation will handle the real duration
    WIN32_FILE_ATTRIBUTE_DATA fileInfo;
    if (GetFileAttributesEx(videoPath.c_str(), GetFileExInfoStandard, &fileInfo))
    {
        ULARGE_INTEGER fileSize;
        fileSize.LowPart = fileInfo.nFileSizeLow;
        fileSize.HighPart = fileInfo.nFileSizeHigh;
        
        // Estimate: ~10MB per minute for typical 1080p recording
        // Duration in 100-nanosecond units (10,000,000 = 1 second)
        int64_t estimatedSeconds = fileSize.QuadPart / (10 * 1024 * 1024 / 60);
        if (estimatedSeconds < 1) estimatedSeconds = 10; // minimum 10 seconds
        if (estimatedSeconds > 3600) estimatedSeconds = 3600; // max 1 hour
        
        data.videoDuration = winrt::TimeSpan{ estimatedSeconds * 10000000LL };
        data.trimEnd = data.videoDuration;
    }
    else
    {
        // Default to 60 seconds if we can't get file size
        data.videoDuration = winrt::TimeSpan{ 600000000LL };
        data.trimEnd = data.videoDuration;
    }

    // Center dialog on the screen containing the parent window
    HMONITOR hMonitor = MonitorFromWindow(hParent, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi = { sizeof(mi) };
    GetMonitorInfo(hMonitor, &mi);

    // Calculate center position
    const int dialogWidth = 521;
    const int dialogHeight = 381;
    int x = mi.rcWork.left + (mi.rcWork.right - mi.rcWork.left - dialogWidth) / 2;
    int y = mi.rcWork.top + (mi.rcWork.bottom - mi.rcWork.top - dialogHeight) / 2;

    // Store position for use in dialog proc
    data.dialogX = x;
    data.dialogY = y;

    auto result = DialogBoxParam(
        GetModuleHandle(nullptr),
        MAKEINTRESOURCE(IDD_VIDEO_TRIM),
        hParent,
        TrimDialogProc,
        reinterpret_cast<LPARAM>(&data));

    if (result == IDOK)
    {
        trimStart = data.trimStart;
        trimEnd = data.trimEnd;
    }

    return result;
}

//----------------------------------------------------------------------------
//
// Helper: Update video frame preview
//
//----------------------------------------------------------------------------
static void UpdateVideoPreview(HWND hDlg, VideoRecordingSession::TrimDialogData* pData, bool invalidateTimeline = true)
{
    if (!pData)
    {
        return;
    }

    const auto previewTime = pData->previewOverrideActive ? pData->previewOverride : pData->currentPosition;

    // Update position label and timeline
    SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, previewTime, true);
    if (invalidateTimeline && hDlg)
    {
        InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_TIMELINE), nullptr, FALSE);
    }

    const int64_t requestTicks = previewTime.count();
    pData->latestPreviewRequest.store(requestTicks, std::memory_order_relaxed);

    if (pData->loadingPreview.exchange(true))
    {
        // A preview request is already running; we'll schedule the latest once it completes.
        return;
    }

    std::thread([](HWND hDlg, VideoRecordingSession::TrimDialogData* pData, int64_t requestTicks)
    {
        winrt::init_apartment(winrt::apartment_type::multi_threaded);

        const int64_t requestTicksRaw = requestTicks;
        bool updatedBitmap = false;

        bool durationChanged = false;

        try
        {
            if (!pData->composition)
            {
                auto file = winrt::StorageFile::GetFileFromPathAsync(pData->videoPath).get();
                auto clip = winrt::MediaClip::CreateFromFileAsync(file).get();

                pData->composition = winrt::MediaComposition();
                pData->composition.Clips().Append(clip);

                auto actualDuration = clip.OriginalDuration();
                if (actualDuration.count() > 0)
                {
                    if (pData->videoDuration.count() != actualDuration.count())
                    {
                        durationChanged = true;
                    }

                    pData->videoDuration = actualDuration;
                    pData->trimEnd = actualDuration;
                    if (pData->trimStart.count() > pData->trimEnd.count())
                    {
                        pData->trimStart = pData->trimEnd;
                    }
                    durationChanged = true;
                }
            }

            auto composition = pData->composition;
            if (composition)
            {
                auto durationTicks = composition.Duration().count();
                if (durationTicks > 0)
                {
                    requestTicks = std::clamp<int64_t>(requestTicks, 0, durationTicks);
                }

                auto stream = composition.GetThumbnailAsync(
                    winrt::TimeSpan{ requestTicks },
                    0, 0,
                    winrt::VideoFramePrecision::NearestFrame).get();

                if (stream)
                {
                    winrt::com_ptr<IWICImagingFactory> wicFactory;
                    if (SUCCEEDED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(wicFactory.put()))))
                    {
                        winrt::com_ptr<IStream> istream;
                        auto streamAsUnknown = static_cast<::IUnknown*>(winrt::get_abi(stream));
                        if (SUCCEEDED(CreateStreamOverRandomAccessStream(streamAsUnknown, IID_PPV_ARGS(istream.put()))) && istream)
                        {
                            winrt::com_ptr<IWICBitmapDecoder> decoder;
                            if (SUCCEEDED(wicFactory->CreateDecoderFromStream(istream.get(), nullptr, WICDecodeMetadataCacheOnDemand, decoder.put())))
                            {
                                winrt::com_ptr<IWICBitmapFrameDecode> frame;
                                if (SUCCEEDED(decoder->GetFrame(0, frame.put())))
                                {
                                    winrt::com_ptr<IWICFormatConverter> converter;
                                    if (SUCCEEDED(wicFactory->CreateFormatConverter(converter.put())))
                                    {
                                        if (SUCCEEDED(converter->Initialize(frame.get(), GUID_WICPixelFormat32bppBGRA,
                                                                             WICBitmapDitherTypeNone, nullptr, 0.0,
                                                                             WICBitmapPaletteTypeCustom)))
                                        {
                                            UINT width, height;
                                            converter->GetSize(&width, &height);

                                            BITMAPINFO bmi = {};
                                            bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
                                            bmi.bmiHeader.biWidth = width;
                                            bmi.bmiHeader.biHeight = -static_cast<LONG>(height);
                                            bmi.bmiHeader.biPlanes = 1;
                                            bmi.bmiHeader.biBitCount = 32;
                                            bmi.bmiHeader.biCompression = BI_RGB;

                                            void* bits = nullptr;
                                            HDC hdcScreen = GetDC(nullptr);
                                            HBITMAP hBitmap = CreateDIBSection(hdcScreen, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
                                            ReleaseDC(nullptr, hdcScreen);

                                            if (hBitmap && bits)
                                            {
                                                converter->CopyPixels(nullptr, width * 4, width * height * 4, static_cast<BYTE*>(bits));

                                                if (pData->hPreviewBitmap)
                                                {
                                                    DeleteObject(pData->hPreviewBitmap);
                                                }
                                                pData->hPreviewBitmap = hBitmap;
                                                updatedBitmap = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (...)
        {
        }

        pData->loadingPreview.store(false, std::memory_order_relaxed);

        if (updatedBitmap)
        {
            PostMessage(hDlg, WM_USER + 1, 0, 0);
        }

        if (pData->latestPreviewRequest.load(std::memory_order_relaxed) != requestTicksRaw)
        {
            PostMessage(hDlg, WM_USER + 2, 0, 0);
        }

        if (durationChanged)
        {
            PostMessage(hDlg, WM_USER + 3, 0, 0);
        }
    }, hDlg, pData, requestTicks).detach();
}

//----------------------------------------------------------------------------
//
// Helper: Draw custom timeline with handles
//
//----------------------------------------------------------------------------
static void DrawTimeline(HDC hdc, RECT rc, VideoRecordingSession::TrimDialogData* pData)
{
    const int width = rc.right - rc.left;
    const int height = rc.bottom - rc.top;

    // Create memory DC for double buffering
    HDC hdcMem = CreateCompatibleDC(hdc);
    HBITMAP hbmMem = CreateCompatibleBitmap(hdc, width, height);
    HBITMAP hbmOld = static_cast<HBITMAP>(SelectObject(hdcMem, hbmMem));

    // Draw to memory DC
    HBRUSH hBackground = CreateSolidBrush(GetSysColor(COLOR_BTNFACE));
    RECT rcMem = { 0, 0, width, height };
    FillRect(hdcMem, &rcMem, hBackground);
    DeleteObject(hBackground);

    const int trackLeft = kTimelinePadding;
    const int trackRight = width - kTimelinePadding;
    const int trackTop = kTimelineTrackTopOffset;
    const int trackBottom = trackTop + kTimelineTrackHeight;

    RECT rcTrack = { trackLeft, trackTop, trackRight, trackBottom };
    HBRUSH hTrackBase = CreateSolidBrush(RGB(214, 219, 224));
    FillRect(hdcMem, &rcTrack, hTrackBase);
    DeleteObject(hTrackBase);

    int startX = std::clamp(TimelineTimeToClientX(pData, pData->trimStart, width), trackLeft, trackRight);
    int endX = std::clamp(TimelineTimeToClientX(pData, pData->trimEnd, width), trackLeft, trackRight);
    if (endX < startX)
    {
        std::swap(startX, endX);
    }

    RECT rcBefore{ trackLeft, trackTop, startX, trackBottom };
    RECT rcAfter{ endX, trackTop, trackRight, trackBottom };
    HBRUSH hMuted = CreateSolidBrush(RGB(198, 202, 206));
    FillRect(hdcMem, &rcBefore, hMuted);
    FillRect(hdcMem, &rcAfter, hMuted);
    DeleteObject(hMuted);

    RECT rcActive{ startX, trackTop, endX, trackBottom };
    HBRUSH hActive = CreateSolidBrush(RGB(90, 147, 250));
    FillRect(hdcMem, &rcActive, hActive);
    DeleteObject(hActive);

    HPEN hOutline = CreatePen(PS_SOLID, 1, RGB(150, 150, 150));
    HPEN hOldPen = static_cast<HPEN>(SelectObject(hdcMem, hOutline));
    MoveToEx(hdcMem, trackLeft, trackTop, nullptr);
    LineTo(hdcMem, trackRight, trackTop);
    LineTo(hdcMem, trackRight, trackBottom);
    LineTo(hdcMem, trackLeft, trackBottom);
    LineTo(hdcMem, trackLeft, trackTop);
    SelectObject(hdcMem, hOldPen);
    DeleteObject(hOutline);

    const int trackWidth = trackRight - trackLeft;
    if (trackWidth > 0 && pData && pData->videoDuration.count() > 0)
    {
        const int tickTop = trackBottom + 2;
        const int tickMajorBottom = tickTop + 10;
        const int tickMinorBottom = tickTop + 6;

        const std::array<double, 5> fractions{ 0.0, 0.25, 0.5, 0.75, 1.0 };
        HPEN hTickPen = CreatePen(PS_SOLID, 1, RGB(150, 150, 150));
        HPEN hOldTickPen = static_cast<HPEN>(SelectObject(hdcMem, hTickPen));
        SetBkMode(hdcMem, TRANSPARENT);
        SetTextColor(hdcMem, RGB(80, 80, 80));
        
        // Use consistent font for all timeline text
        HFONT hTimelineFont = CreateFont(12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
        HFONT hOldTimelineFont = static_cast<HFONT>(SelectObject(hdcMem, hTimelineFont));

        for (size_t i = 0; i < fractions.size(); ++i)
        {
            const double fraction = fractions[i];
            const int x = trackLeft + static_cast<int>(std::round(fraction * trackWidth));
            const bool isMajor = (fraction == 0.0) || (fraction == 0.5) || (fraction == 1.0);
            MoveToEx(hdcMem, x, tickTop, nullptr);
            LineTo(hdcMem, x, isMajor ? tickMajorBottom : tickMinorBottom);

            if (fraction > 0.0 && fraction < 1.0)
            {
                // Calculate marker time within the selection range (trimStart to trimEnd)
                const int64_t selectionDuration = pData->trimEnd.count() - pData->trimStart.count();
                const auto markerTime = winrt::TimeSpan{ pData->trimStart.count() + static_cast<int64_t>(fraction * selectionDuration) };
                const std::wstring markerText = FormatTrimTime(markerTime, false);
                RECT rcMarker{ x - 35, tickMajorBottom + 2, x + 35, tickMajorBottom + 16 };
                DrawText(hdcMem, markerText.c_str(), -1, &rcMarker, DT_CENTER | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
            }
        }

        SelectObject(hdcMem, hOldTimelineFont);
        DeleteObject(hTimelineFont);
        SelectObject(hdcMem, hOldTickPen);
        DeleteObject(hTickPen);
    }

    auto drawHandle = [&](int x, COLORREF color, bool pointDown)
    {
        RECT handleRect{
            x - kTimelineHandleHalfWidth,
            trackTop - (kTimelineHandleHeight - kTimelineTrackHeight) / 2,
            x + kTimelineHandleHalfWidth,
            trackTop - (kTimelineHandleHeight - kTimelineTrackHeight) / 2 + kTimelineHandleHeight
        };

        HBRUSH hBrush = CreateSolidBrush(color);
        FillRect(hdcMem, &handleRect, hBrush);
        FrameRect(hdcMem, &handleRect, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));

        POINT triangle[3];
        if (pointDown)
        {
            triangle[0] = { x, handleRect.bottom + 6 };
            triangle[1] = { x - kTimelineHandleHalfWidth, handleRect.bottom - 4 };
            triangle[2] = { x + kTimelineHandleHalfWidth, handleRect.bottom - 4 };
        }
        else
        {
            triangle[0] = { x, handleRect.top - 6 };
            triangle[1] = { x - kTimelineHandleHalfWidth, handleRect.top + 4 };
            triangle[2] = { x + kTimelineHandleHalfWidth, handleRect.top + 4 };
        }

        HPEN hNullPen = static_cast<HPEN>(SelectObject(hdcMem, GetStockObject(NULL_PEN)));
        HBRUSH hPrevBrush = static_cast<HBRUSH>(SelectObject(hdcMem, hBrush));
        Polygon(hdcMem, triangle, 3);
        SelectObject(hdcMem, hPrevBrush);
        SelectObject(hdcMem, hNullPen);
        DeleteObject(hBrush);
    };

    drawHandle(startX, RGB(76, 175, 80), false);
    drawHandle(endX, RGB(244, 67, 54), true);

    const int posX = std::clamp(TimelineTimeToClientX(pData, pData->currentPosition, width), trackLeft, trackRight);
    HPEN hPositionPen = CreatePen(PS_SOLID, 2, RGB(33, 150, 243));
    hOldPen = static_cast<HPEN>(SelectObject(hdcMem, hPositionPen));
    MoveToEx(hdcMem, posX, trackTop - 12, nullptr);
    LineTo(hdcMem, posX, trackBottom + 22);
    SelectObject(hdcMem, hOldPen);
    DeleteObject(hPositionPen);

    HBRUSH hPositionBrush = CreateSolidBrush(RGB(33, 150, 243));
    HBRUSH hOldBrush = static_cast<HBRUSH>(SelectObject(hdcMem, hPositionBrush));
    Ellipse(hdcMem, posX - 6, trackBottom + 12, posX + 6, trackBottom + 24);
    SelectObject(hdcMem, hOldBrush);
    DeleteObject(hPositionBrush);

    // Set font for start/end labels (same font used for tick labels)
    SetBkMode(hdcMem, TRANSPARENT);
    SetTextColor(hdcMem, RGB(80, 80, 80));
    HFONT hFont = CreateFont(12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
        OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    HFONT hOldFont = static_cast<HFONT>(SelectObject(hdcMem, hFont));

    RECT rcStartLabel{ trackLeft, trackBottom + 16, trackLeft + 70, trackBottom + 32 };
    const std::wstring startLabel = FormatTrimTime(pData->trimStart, false);
    DrawText(hdcMem, startLabel.c_str(), -1, &rcStartLabel, DT_LEFT | DT_TOP | DT_SINGLELINE);

    RECT rcEndLabel{ trackRight - 70, trackBottom + 16, trackRight, trackBottom + 32 };
    const std::wstring endLabel = FormatTrimTime(pData->trimEnd, false);
    DrawText(hdcMem, endLabel.c_str(), -1, &rcEndLabel, DT_RIGHT | DT_TOP | DT_SINGLELINE);

    SelectObject(hdcMem, hOldFont);
    DeleteObject(hFont);

    // Copy the buffered image to the screen
    BitBlt(hdc, rc.left, rc.top, width, height, hdcMem, 0, 0, SRCCOPY);

    // Clean up
    SelectObject(hdcMem, hbmOld);
    DeleteObject(hbmMem);
    DeleteDC(hdcMem);
}

//----------------------------------------------------------------------------
//
// Helper: Mouse interaction for the trim timeline
//
//----------------------------------------------------------------------------
namespace
{
    constexpr UINT_PTR kPlaybackTimerId = 1;
    constexpr UINT kPlaybackTimerIntervalMs = 33;
    constexpr int64_t kPlaybackStepTicks = 330000; // ~33ms in 100-ns units
}

static void RefreshPlaybackButtons(HWND hDlg)
{
    if (!hDlg)
    {
        return;
    }

    InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_SKIP_START), nullptr, FALSE);
    InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_REWIND), nullptr, FALSE);
    InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_PLAY_PAUSE), nullptr, FALSE);
    InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_FORWARD), nullptr, FALSE);
    InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_SKIP_END), nullptr, FALSE);
}

static void HandlePlaybackCommand(int controlId, VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData || !pData->hDialog)
    {
        return;
    }

    HWND hDlg = pData->hDialog;

    switch (controlId)
    {
    case IDC_TRIM_PLAY_PAUSE:
        if (pData->isPlaying.load(std::memory_order_relaxed))
        {
            StopPlayback(hDlg, pData);
        }
        else
        {
            StartPlaybackAsync(hDlg, pData);
        }
        break;

    case IDC_TRIM_REWIND:
    {
        StopPlayback(hDlg, pData);
        // Use 1 second step for timelines < 20 seconds, 2 seconds otherwise
        const int64_t duration = pData->trimEnd.count() - pData->trimStart.count();
        const int64_t stepTicks = (duration < 200'000'000) ? 10'000'000 : kJogStepTicks;
        const int64_t newTicks = (std::max)(pData->trimStart.count(), pData->currentPosition.count() - stepTicks);
        pData->currentPosition = winrt::TimeSpan{ newTicks };
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    case IDC_TRIM_FORWARD:
    {
        StopPlayback(hDlg, pData);
        // Use 1 second step for timelines < 20 seconds, 2 seconds otherwise
        const int64_t duration = pData->trimEnd.count() - pData->trimStart.count();
        const int64_t stepTicks = (duration < 200'000'000) ? 10'000'000 : kJogStepTicks;
        const int64_t newTicks = (std::min)(pData->trimEnd.count(), pData->currentPosition.count() + stepTicks);
        pData->currentPosition = winrt::TimeSpan{ newTicks };
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    case IDC_TRIM_SKIP_START:
    {
        StopPlayback(hDlg, pData);
        pData->currentPosition = pData->trimStart;
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    case IDC_TRIM_SKIP_END:
    {
        StopPlayback(hDlg, pData);
        pData->currentPosition = pData->trimEnd;
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    default:
        StopPlayback(hDlg, pData);
        pData->currentPosition = pData->trimStart;
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    RefreshPlaybackButtons(hDlg);
}

static void StopPlayback(HWND hDlg, VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData)
    {
        return;
    }

    if (pData->isPlaying.exchange(false, std::memory_order_relaxed))
    {
        // Stop audio playback and align media position with UI state
        if (pData->mediaPlayer)
        {
            try
            {
                pData->mediaPlayer.Pause();
                pData->mediaPlayer.PlaybackSession().Position(pData->currentPosition);
                pData->mediaPlayer.Close();
            }
            catch (...) {}
            pData->mediaPlayer = nullptr;
        }

        if (hDlg)
        {
            KillTimer(hDlg, kPlaybackTimerId);
        }
        RefreshPlaybackButtons(hDlg);
    }
}

static winrt::fire_and_forget StartPlaybackAsync(HWND hDlg, VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData || !hDlg)
    {
        co_return;
    }

    if (pData->trimEnd.count() <= pData->trimStart.count())
    {
        co_return;
    }

    if (pData->currentPosition.count() < pData->trimStart.count() ||
        pData->currentPosition.count() >= pData->trimEnd.count())
    {
        pData->currentPosition = pData->trimStart;
        UpdateVideoPreview(hDlg, pData);
    }

    bool expected = false;
    if (!pData->isPlaying.compare_exchange_strong(expected, true, std::memory_order_relaxed))
    {
        co_return;
    }

    if (pData->mediaPlayer)
    {
        try
        {
            pData->mediaPlayer.Close();
        }
        catch (...) {}
        pData->mediaPlayer = nullptr;
    }

    winrt::MediaPlayer newPlayer{ nullptr };

    try
    {
        if (!pData->playbackFile)
        {
            auto file = co_await winrt::StorageFile::GetFileFromPathAsync(pData->videoPath);
            pData->playbackFile = file;
        }

        if (!pData->playbackFile)
        {
            throw winrt::hresult_error(E_FAIL);
        }

        newPlayer = winrt::MediaPlayer();
        newPlayer.AudioCategory(winrt::MediaPlayerAudioCategory::Media);
        newPlayer.IsVideoFrameServerEnabled(false);
        newPlayer.AutoPlay(false);
        newPlayer.Volume(1.0);

        auto mediaSource = winrt::MediaSource::CreateFromStorageFile(pData->playbackFile);
        VideoRecordingSession::TrimDialogData* dataPtr = pData;
        newPlayer.MediaOpened([dataPtr](auto const& sender, auto const&)
        {
            if (!dataPtr)
            {
                return;
            }
            try
            {
                sender.PlaybackSession().Position(dataPtr->currentPosition);
                sender.Play();
            }
            catch (...) {}
        });

        pData->mediaPlayer = newPlayer;
        pData->mediaPlayer.Source(mediaSource);
    }
    catch (...)
    {
        pData->isPlaying.store(false, std::memory_order_relaxed);
        if (newPlayer)
        {
            try
            {
                newPlayer.Close();
            }
            catch (...) {}
        }
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    if (!SetTimer(hDlg, kPlaybackTimerId, kPlaybackTimerIntervalMs, nullptr))
    {
        pData->isPlaying.store(false, std::memory_order_relaxed);
        if (pData->mediaPlayer)
        {
            try
            {
                pData->mediaPlayer.Close();
            }
            catch (...) {}
            pData->mediaPlayer = nullptr;
        }
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    RefreshPlaybackButtons(hDlg);
}

static LRESULT CALLBACK TimelineSubclassProc(
    HWND hWnd,
    UINT message,
    WPARAM wParam,
    LPARAM lParam,
    UINT_PTR uIdSubclass,
    DWORD_PTR dwRefData)
{
    auto* pData = reinterpret_cast<VideoRecordingSession::TrimDialogData*>(dwRefData);
    if (!pData)
    {
        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    auto restorePreviewIfNeeded = [&]()
    {
        if (pData->restorePreviewOnRelease && pData->hDialog)
        {
            const int64_t restoredTicks = std::clamp<int64_t>(
                pData->positionBeforeOverride.count(),
                0,
                pData->videoDuration.count());
            pData->currentPosition = winrt::TimeSpan{ restoredTicks };
            pData->previewOverrideActive = false;
            pData->restorePreviewOnRelease = false;
            UpdateVideoPreview(pData->hDialog, pData);
        }
    };

    switch (message)
    {
    case WM_NCDESTROY:
        RemoveWindowSubclass(hWnd, TimelineSubclassProc, uIdSubclass);
        break;

    case WM_LBUTTONDOWN:
    {
        StopPlayback(pData->hDialog, pData);

        RECT rcClient{};
        GetClientRect(hWnd, &rcClient);
        const int width = rcClient.right - rcClient.left;
        if (width <= 0)
        {
            break;
        }

        const int x = GET_X_LPARAM(lParam);
        const int clampedX = std::clamp(x, 0, width);
        const int startX = TimelineTimeToClientX(pData, pData->trimStart, width);
        const int posX = TimelineTimeToClientX(pData, pData->currentPosition, width);
        const int endX = TimelineTimeToClientX(pData, pData->trimEnd, width);

        pData->dragMode = VideoRecordingSession::TrimDialogData::None;
        pData->previewOverrideActive = false;
        pData->restorePreviewOnRelease = false;

        if (abs(clampedX - posX) <= kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::Position;
        }
        else if (abs(clampedX - startX) < kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::TrimStart;
        }
        else if (abs(clampedX - endX) < kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::TrimEnd;
        }

        if (pData->dragMode != VideoRecordingSession::TrimDialogData::None)
        {
            pData->isDragging = true;
            if (pData->dragMode == VideoRecordingSession::TrimDialogData::TrimStart ||
                pData->dragMode == VideoRecordingSession::TrimDialogData::TrimEnd)
            {
                pData->positionBeforeOverride = pData->currentPosition;
                pData->previewOverrideActive = true;
                pData->restorePreviewOnRelease = true;
                pData->previewOverride = (pData->dragMode == VideoRecordingSession::TrimDialogData::TrimStart) ?
                    pData->trimStart : pData->trimEnd;
                UpdateVideoPreview(pData->hDialog, pData);
            }
            SetCapture(hWnd);
            return 0;
        }
        break;
    }

    case WM_LBUTTONUP:
    {
        if (pData->isDragging)
        {
            pData->isDragging = false;
            ReleaseCapture();
            SetCursor(LoadCursor(nullptr, IDC_ARROW));
            restorePreviewIfNeeded();
            pData->dragMode = VideoRecordingSession::TrimDialogData::None;
            InvalidateRect(hWnd, nullptr, FALSE);
            return 0;
        }
        break;
    }

    case WM_MOUSEMOVE:
    {
        TRACKMOUSEEVENT tme{};
        tme.cbSize = sizeof(tme);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = hWnd;
        TrackMouseEvent(&tme);

        RECT rcClient{};
        GetClientRect(hWnd, &rcClient);
        const int width = rcClient.right - rcClient.left;
        if (width <= 0)
        {
            break;
        }

        const int rawX = GET_X_LPARAM(lParam);
        const int clampedX = std::clamp(rawX, 0, width);

        if (!pData->isDragging)
        {
            const int startX = TimelineTimeToClientX(pData, pData->trimStart, width);
            const int posX = TimelineTimeToClientX(pData, pData->currentPosition, width);
            const int endX = TimelineTimeToClientX(pData, pData->trimEnd, width);

            if (abs(clampedX - posX) <= kTimelineHandleHitRadius)
            {
                SetCursor(LoadCursor(nullptr, IDC_HAND));
            }
            else if (abs(clampedX - startX) < kTimelineHandleHitRadius || abs(clampedX - endX) < kTimelineHandleHitRadius)
            {
                SetCursor(LoadCursor(nullptr, IDC_SIZEWE));
            }
            else
            {
                SetCursor(LoadCursor(nullptr, IDC_ARROW));
            }
            return 0;
        }

        const auto newTime = TimelinePixelToTime(pData, clampedX, width);

        bool requestPreviewUpdate = false;
        bool applyOverride = false;
        winrt::TimeSpan overrideTime{ 0 };

        switch (pData->dragMode)
        {
        case VideoRecordingSession::TrimDialogData::TrimStart:
            if (newTime.count() < pData->trimEnd.count())
            {
                if (newTime.count() != pData->trimStart.count())
                {
                    pData->trimStart = newTime;
                    UpdateDurationDisplay(pData->hDialog, pData);
                }
                overrideTime = pData->trimStart;
                applyOverride = true;
                requestPreviewUpdate = true;
            }
            break;

        case VideoRecordingSession::TrimDialogData::Position:
        {
            const int previousPosX = TimelineTimeToClientX(pData, pData->currentPosition, width);
            pData->currentPosition = newTime;
            const int newPosX = TimelineTimeToClientX(pData, pData->currentPosition, width);
            RECT clientRect{};
            GetClientRect(hWnd, &clientRect);
            InvalidatePlayheadRegion(hWnd, clientRect, previousPosX, newPosX);
            pData->previewOverrideActive = false;
            UpdateVideoPreview(pData->hDialog, pData, false);
            break;
        }

        case VideoRecordingSession::TrimDialogData::TrimEnd:
            if (newTime.count() > pData->trimStart.count())
            {
                if (newTime.count() != pData->trimEnd.count())
                {
                    pData->trimEnd = newTime;
                    UpdateDurationDisplay(pData->hDialog, pData);
                }
                overrideTime = pData->trimEnd;
                applyOverride = true;
                requestPreviewUpdate = true;
            }
            break;

        default:
            break;
        }

        if (applyOverride)
        {
            pData->previewOverrideActive = true;
            pData->previewOverride = overrideTime;
        }

        if (requestPreviewUpdate)
        {
            UpdateVideoPreview(pData->hDialog, pData);
        }

        InvalidateRect(hWnd, nullptr, FALSE);
        return 0;
    }

    case WM_ERASEBKGND:
        return 1;

    case WM_MOUSELEAVE:
        if (!pData->isDragging)
        {
            SetCursor(LoadCursor(nullptr, IDC_ARROW));
        }
        break;

    case WM_CAPTURECHANGED:
        if (pData->isDragging)
        {
            pData->isDragging = false;
            pData->dragMode = VideoRecordingSession::TrimDialogData::None;
            restorePreviewIfNeeded();
        }
        break;
    }

    return DefSubclassProc(hWnd, message, wParam, lParam);
}

//----------------------------------------------------------------------------
//
// Helper: Draw custom playback buttons (play/pause and restart)
//
//----------------------------------------------------------------------------
static void DrawPlaybackButton(
    const DRAWITEMSTRUCT* pDIS,
    VideoRecordingSession::TrimDialogData* pData)
{
    if (!pDIS || !pData)
    {
        return;
    }

    const bool isPlayControl = (pDIS->CtlID == IDC_TRIM_PLAY_PAUSE);
    const bool isRewindControl = (pDIS->CtlID == IDC_TRIM_REWIND);
    const bool isForwardControl = (pDIS->CtlID == IDC_TRIM_FORWARD);
    const bool isSkipStartControl = (pDIS->CtlID == IDC_TRIM_SKIP_START);
    const bool isSkipEndControl = (pDIS->CtlID == IDC_TRIM_SKIP_END);
    
    // Check if skip buttons should be disabled based on position
    const bool atStart = (pData->currentPosition.count() <= pData->trimStart.count());
    const bool atEnd = (pData->currentPosition.count() >= pData->trimEnd.count());
    
    const bool isHover = isPlayControl ? pData->hoverPlay : 
                        (isRewindControl ? pData->hoverRewind : 
                        (isForwardControl ? pData->hoverForward :
                        (isSkipStartControl ? pData->hoverSkipStart : pData->hoverSkipEnd)));
    bool isDisabled = (pDIS->itemState & ODS_DISABLED) != 0;
    
    // Disable skip start when at start, skip end when at end
    if (isSkipStartControl && atStart) isDisabled = true;
    if (isSkipEndControl && atEnd) isDisabled = true;
    
    const bool isPressed = (pDIS->itemState & ODS_SELECTED) != 0;
    const bool isPlaying = pData->isPlaying.load(std::memory_order_relaxed);

    // Media Player color scheme - dark background with gradient
    COLORREF bgColorTop = RGB(45, 45, 50);
    COLORREF bgColorBottom = RGB(35, 35, 40);
    COLORREF iconColor = RGB(220, 220, 220);
    COLORREF borderColor = RGB(80, 80, 85);
    
    if (isHover && !isDisabled)
    {
        bgColorTop = RGB(60, 60, 65);
        bgColorBottom = RGB(50, 50, 55);
        iconColor = RGB(255, 255, 255);
    }
    if (isPressed && !isDisabled)
    {
        bgColorTop = RGB(30, 30, 35);
        bgColorBottom = RGB(25, 25, 30);
        iconColor = RGB(200, 200, 200);
    }
    if (isDisabled)
    {
        bgColorTop = RGB(40, 40, 45);
        bgColorBottom = RGB(35, 35, 40);
        iconColor = RGB(100, 100, 100);
    }

    int width = pDIS->rcItem.right - pDIS->rcItem.left;
    int height = pDIS->rcItem.bottom - pDIS->rcItem.top;
    float centerX = pDIS->rcItem.left + width / 2.0f;
    float centerY = pDIS->rcItem.top + height / 2.0f;
    float radius = min(width, height) / 2.0f - 1.0f;

    // Use GDI+ for antialiased rendering
    Gdiplus::Graphics graphics(pDIS->hDC);
    graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);

    // Draw gradient background circle
    Gdiplus::GraphicsPath path;
    path.AddEllipse(centerX - radius, centerY - radius, radius * 2, radius * 2);
    
    Gdiplus::PathGradientBrush gradientBrush(&path);
    Gdiplus::Color colors[] = {
        Gdiplus::Color(255, GetRValue(bgColorTop), GetGValue(bgColorTop), GetBValue(bgColorTop)),
        Gdiplus::Color(255, GetRValue(bgColorBottom), GetGValue(bgColorBottom), GetBValue(bgColorBottom))
    };
    
    // Create linear gradient from top to bottom
    Gdiplus::LinearGradientBrush linearGradient(
        Gdiplus::PointF(centerX, centerY - radius),
        Gdiplus::PointF(centerX, centerY + radius),
        Gdiplus::Color(255, GetRValue(bgColorTop), GetGValue(bgColorTop), GetBValue(bgColorTop)),
        Gdiplus::Color(255, GetRValue(bgColorBottom), GetGValue(bgColorBottom), GetBValue(bgColorBottom))
    );
    
    graphics.FillEllipse(&linearGradient, centerX - radius, centerY - radius, radius * 2, radius * 2);
    
    // Draw border
    Gdiplus::Pen borderPen(Gdiplus::Color(255, GetRValue(borderColor), GetGValue(borderColor), GetBValue(borderColor)), 1.0f);
    graphics.DrawEllipse(&borderPen, centerX - radius, centerY - radius, radius * 2, radius * 2);

    // Draw icons
    Gdiplus::SolidBrush iconBrush(Gdiplus::Color(255, GetRValue(iconColor), GetGValue(iconColor), GetBValue(iconColor)));
    float iconSize = radius * 2.0f / 3.0f;
    
    if (isPlayControl)
    {
        if (isPlaying)
        {
            // Draw pause icon (two vertical bars)
            float barWidth = iconSize / 4.0f;
            float barHeight = iconSize;
            float gap = iconSize / 5.0f;
            
            graphics.FillRectangle(&iconBrush,
                centerX - gap - barWidth, centerY - barHeight / 2.0f,
                barWidth, barHeight);
            graphics.FillRectangle(&iconBrush,
                centerX + gap, centerY - barHeight / 2.0f,
                barWidth, barHeight);
        }
        else
        {
            // Draw play triangle
            float triWidth = iconSize;
            float triHeight = iconSize;
            Gdiplus::PointF playTri[3] = {
                Gdiplus::PointF(centerX - triWidth / 3.0f, centerY - triHeight / 2.0f),
                Gdiplus::PointF(centerX + triWidth * 2.0f / 3.0f, centerY),
                Gdiplus::PointF(centerX - triWidth / 3.0f, centerY + triHeight / 2.0f)
            };
            graphics.FillPolygon(&iconBrush, playTri, 3);
        }
    }
    else if (isRewindControl || isForwardControl)
    {
        // Draw small play triangle in appropriate direction
        float triWidth = iconSize * 3.0f / 5.0f;
        float triHeight = iconSize * 3.0f / 5.0f;
        
        if (isRewindControl)
        {
            // Triangle pointing left
            Gdiplus::PointF tri[3] = {
                Gdiplus::PointF(centerX + triWidth / 3.0f, centerY - triHeight / 2.0f),
                Gdiplus::PointF(centerX - triWidth * 2.0f / 3.0f, centerY),
                Gdiplus::PointF(centerX + triWidth / 3.0f, centerY + triHeight / 2.0f)
            };
            graphics.FillPolygon(&iconBrush, tri, 3);
        }
        else
        {
            // Triangle pointing right
            Gdiplus::PointF tri[3] = {
                Gdiplus::PointF(centerX - triWidth / 3.0f, centerY - triHeight / 2.0f),
                Gdiplus::PointF(centerX + triWidth * 2.0f / 3.0f, centerY),
                Gdiplus::PointF(centerX - triWidth / 3.0f, centerY + triHeight / 2.0f)
            };
            graphics.FillPolygon(&iconBrush, tri, 3);
        }
    }
    else if (isSkipStartControl || isSkipEndControl)
    {
        // Draw skip to start/end icon (triangle + bar)
        float triWidth = iconSize * 2.0f / 3.0f;
        float triHeight = iconSize;
        float barWidth = iconSize / 6.0f;
        
        if (isSkipStartControl)
        {
            // Bar on left, triangle pointing left
            graphics.FillRectangle(&iconBrush,
                centerX - triWidth / 2.0f - barWidth, centerY - triHeight / 2.0f,
                barWidth, triHeight);
            
            Gdiplus::PointF tri[3] = {
                Gdiplus::PointF(centerX + triWidth / 2.0f, centerY - triHeight / 2.0f),
                Gdiplus::PointF(centerX - triWidth / 2.0f, centerY),
                Gdiplus::PointF(centerX + triWidth / 2.0f, centerY + triHeight / 2.0f)
            };
            graphics.FillPolygon(&iconBrush, tri, 3);
        }
        else
        {
            // Triangle pointing right, bar on right
            Gdiplus::PointF tri[3] = {
                Gdiplus::PointF(centerX - triWidth / 2.0f, centerY - triHeight / 2.0f),
                Gdiplus::PointF(centerX + triWidth / 2.0f, centerY),
                Gdiplus::PointF(centerX - triWidth / 2.0f, centerY + triHeight / 2.0f)
            };
            graphics.FillPolygon(&iconBrush, tri, 3);
            
            graphics.FillRectangle(&iconBrush,
                centerX + triWidth / 2.0f, centerY - triHeight / 2.0f,
                barWidth, triHeight);
        }
    }
}

//----------------------------------------------------------------------------
//
// Helper: Mouse interaction for playback controls
//
//----------------------------------------------------------------------------
static LRESULT CALLBACK PlaybackButtonSubclassProc(
    HWND hWnd,
    UINT message,
    WPARAM wParam,
    LPARAM lParam,
    UINT_PTR uIdSubclass,
    DWORD_PTR dwRefData)
{
    auto* pData = reinterpret_cast<VideoRecordingSession::TrimDialogData*>(dwRefData);
    if (!pData)
    {
        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    switch (message)
    {
    case WM_NCDESTROY:
        RemoveWindowSubclass(hWnd, PlaybackButtonSubclassProc, uIdSubclass);
        break;

    case WM_MOUSEMOVE:
    {
        TRACKMOUSEEVENT tme{ sizeof(tme), TME_LEAVE, hWnd, 0 };
        TrackMouseEvent(&tme);

        const int controlId = GetDlgCtrlID(hWnd);
        const bool isPlayControl = (controlId == IDC_TRIM_PLAY_PAUSE);
        const bool isRewindControl = (controlId == IDC_TRIM_REWIND);
        const bool isForwardControl = (controlId == IDC_TRIM_FORWARD);
        const bool isSkipStartControl = (controlId == IDC_TRIM_SKIP_START);
                
        bool& hoverFlag = isPlayControl ? pData->hoverPlay : 
                         (isRewindControl ? pData->hoverRewind : 
                         (isForwardControl ? pData->hoverForward :
                         (isSkipStartControl ? pData->hoverSkipStart : pData->hoverSkipEnd)));
        if (!hoverFlag)
        {
            hoverFlag = true;
            InvalidateRect(hWnd, nullptr, FALSE);
        }
        return 0;
    }

    case WM_MOUSELEAVE:
    {
        const int controlId = GetDlgCtrlID(hWnd);
        const bool isPlayControl = (controlId == IDC_TRIM_PLAY_PAUSE);
        const bool isRewindControl = (controlId == IDC_TRIM_REWIND);
        const bool isForwardControl = (controlId == IDC_TRIM_FORWARD);
        const bool isSkipStartControl = (controlId == IDC_TRIM_SKIP_START);
        
        bool& hoverFlag = isPlayControl ? pData->hoverPlay : 
                         (isRewindControl ? pData->hoverRewind : 
                         (isForwardControl ? pData->hoverForward :
                         (isSkipStartControl ? pData->hoverSkipStart : pData->hoverSkipEnd)));
        if (hoverFlag)
        {
            hoverFlag = false;
            InvalidateRect(hWnd, nullptr, FALSE);
        }
        return 0;
    }

    case WM_SETCURSOR:
        SetCursor(LoadCursor(nullptr, IDC_HAND));
        return TRUE;

    case WM_ERASEBKGND:
        return 1;

    }

    return DefSubclassProc(hWnd, message, wParam, lParam);
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::TrimDialogProc
//
// Dialog procedure for trim dialog
//
//----------------------------------------------------------------------------
INT_PTR CALLBACK VideoRecordingSession::TrimDialogProc(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    static TrimDialogData* pData = nullptr;

    switch (message)
    {
    case WM_INITDIALOG:
    {
        pData = reinterpret_cast<TrimDialogData*>(lParam);
        SetWindowLongPtr(hDlg, DWLP_USER, lParam);
        pData->hDialog = hDlg;
        pData->hoverPlay = false;
        pData->hoverRewind = false;
        pData->hoverForward = false;
        pData->hoverSkipStart = false;
        pData->hoverSkipEnd = false;
        pData->isPlaying.store(false, std::memory_order_relaxed);

        HWND hTimeline = GetDlgItem(hDlg, IDC_TRIM_TIMELINE);
        SetWindowSubclass(hTimeline, TimelineSubclassProc, 1, reinterpret_cast<DWORD_PTR>(pData));
        HWND hPlayPause = GetDlgItem(hDlg, IDC_TRIM_PLAY_PAUSE);
        SetWindowSubclass(hPlayPause, PlaybackButtonSubclassProc, 2, reinterpret_cast<DWORD_PTR>(pData));
        HWND hRewind = GetDlgItem(hDlg, IDC_TRIM_REWIND);
        if (hRewind)
        {
            SetWindowSubclass(hRewind, PlaybackButtonSubclassProc, 3, reinterpret_cast<DWORD_PTR>(pData));
        }
        HWND hForward = GetDlgItem(hDlg, IDC_TRIM_FORWARD);
        if (hForward)
        {
            SetWindowSubclass(hForward, PlaybackButtonSubclassProc, 4, reinterpret_cast<DWORD_PTR>(pData));
        }
        HWND hSkipStart = GetDlgItem(hDlg, IDC_TRIM_SKIP_START);
        if (hSkipStart)
        {
            SetWindowSubclass(hSkipStart, PlaybackButtonSubclassProc, 5, reinterpret_cast<DWORD_PTR>(pData));
        }
        HWND hSkipEnd = GetDlgItem(hDlg, IDC_TRIM_SKIP_END);
        if (hSkipEnd)
        {
            SetWindowSubclass(hSkipEnd, PlaybackButtonSubclassProc, 6, reinterpret_cast<DWORD_PTR>(pData));
        }

        // Initialize times
        pData->trimStart = winrt::TimeSpan{ 0 };
        pData->trimEnd = pData->videoDuration;
        pData->currentPosition = winrt::TimeSpan{ 0 };

        UpdateDurationDisplay(hDlg, pData);

        // Update labels and timeline (also starts async video load)
        UpdateVideoPreview(hDlg, pData);
        SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, pData->currentPosition, true);

        // Position dialog (use stored position if available)
        if (pData->dialogX != 0 || pData->dialogY != 0)
        {
            SetWindowPos(hDlg, nullptr, pData->dialogX, pData->dialogY, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOZORDER);
        }
        else
        {
            CenterTrimDialog(hDlg);
        }

        return TRUE;
    }

    case WM_USER + 1:
    {
        // Video preview loaded - refresh preview area
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_PREVIEW), nullptr, FALSE);
        }
        return TRUE;
    }

    case WM_USER + 2:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            UpdateVideoPreview(hDlg, pData);
        }
        return TRUE;
    }

    case WM_USER + 3:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            if (pData->currentPosition.count() > pData->trimEnd.count())
            {
                pData->currentPosition = pData->trimEnd;
            }
            UpdateDurationDisplay(hDlg, pData);
            SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, pData->currentPosition, true);
            InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_TIMELINE), nullptr, FALSE);
        }
        return TRUE;
    }

    case WM_DRAWITEM:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (!pData) break;

        DRAWITEMSTRUCT* pDIS = reinterpret_cast<DRAWITEMSTRUCT *> (lParam);

        if (pDIS->CtlID == IDC_TRIM_TIMELINE)
        {
            // Draw custom timeline
            DrawTimeline(pDIS->hDC, pDIS->rcItem, pData);
            return TRUE;
        }
        else if (pDIS->CtlID == IDC_TRIM_PREVIEW)
        {
            RECT rcFill = pDIS->rcItem;
            const int controlWidth = rcFill.right - rcFill.left;
            const int controlHeight = rcFill.bottom - rcFill.top;
            
            // Create memory DC for double buffering to eliminate flicker
            HDC hdcMem = CreateCompatibleDC(pDIS->hDC);
            HBITMAP hbmMem = CreateCompatibleBitmap(pDIS->hDC, controlWidth, controlHeight);
            HBITMAP hbmOld = static_cast<HBITMAP>(SelectObject(hdcMem, hbmMem));
            
            // Draw to memory DC
            RECT rcMem = { 0, 0, controlWidth, controlHeight };
            FillRect(hdcMem, &rcMem, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));

            if (pData->hPreviewBitmap)
            {
                HDC hdcBitmap = CreateCompatibleDC(hdcMem);
                HBITMAP hOldBitmap = static_cast<HBITMAP>(SelectObject(hdcBitmap, pData->hPreviewBitmap));

                BITMAP bm{};
                GetObject(pData->hPreviewBitmap, sizeof(bm), &bm);

                int destWidth = 0;
                int destHeight = 0;

                if (bm.bmWidth > 0 && bm.bmHeight > 0)
                {
                    const double scaleX = static_cast<double>(controlWidth) / static_cast<double>(bm.bmWidth);
                    const double scaleY = static_cast<double>(controlHeight) / static_cast<double>(bm.bmHeight);
                    const double scale = (std::max)(scaleX, scaleY);

                    destWidth = (std::max)(1, static_cast<int>(std::lround(static_cast<double>(bm.bmWidth) * scale)));
                    destHeight = (std::max)(1, static_cast<int>(std::lround(static_cast<double>(bm.bmHeight) * scale)));
                }
                else
                {
                    destWidth = controlWidth;
                    destHeight = controlHeight;
                }

                const int offsetX = (controlWidth - destWidth) / 2;
                const int offsetY = (controlHeight - destHeight) / 2;

                SetStretchBltMode(hdcMem, HALFTONE);
                SetBrushOrgEx(hdcMem, 0, 0, nullptr);
                StretchBlt(hdcMem,
                    offsetX,
                    offsetY,
                    destWidth,
                    destHeight,
                    hdcBitmap,
                    0,
                    0,
                    bm.bmWidth,
                    bm.bmHeight,
                    SRCCOPY);

                SelectObject(hdcBitmap, hOldBitmap);
                DeleteDC(hdcBitmap);
            }
            else
            {
                SetTextColor(hdcMem, RGB(200, 200, 200));
                SetBkMode(hdcMem, TRANSPARENT);
                DrawText(hdcMem, L"Preview not available", -1, &rcMem, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            }
            
            // Copy the buffered image to the screen
            BitBlt(pDIS->hDC, rcFill.left, rcFill.top, controlWidth, controlHeight, hdcMem, 0, 0, SRCCOPY);
            
            // Clean up
            SelectObject(hdcMem, hbmOld);
            DeleteObject(hbmMem);
            DeleteDC(hdcMem);
            
            return TRUE;
        }
        else if (pDIS->CtlID == IDC_TRIM_PLAY_PAUSE || pDIS->CtlID == IDC_TRIM_REWIND || 
                 pDIS->CtlID == IDC_TRIM_FORWARD || pDIS->CtlID == IDC_TRIM_SKIP_START || 
                 pDIS->CtlID == IDC_TRIM_SKIP_END)
        {
            DrawPlaybackButton(pDIS, pData);
            return TRUE;
        }
        break;
    }

    case WM_DESTROY:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            StopPlayback(hDlg, pData);
            
            // Clean up MediaPlayer
            if (pData->mediaPlayer)
            {
                try
                {
                    pData->mediaPlayer.Close();
                    pData->mediaPlayer = nullptr;
                }
                catch (...) {}
            }
            
            HWND hTimeline = GetDlgItem(hDlg, IDC_TRIM_TIMELINE);
            if (hTimeline)
            {
                RemoveWindowSubclass(hTimeline, TimelineSubclassProc, 1);
            }
            HWND hPlayPause = GetDlgItem(hDlg, IDC_TRIM_PLAY_PAUSE);
            if (hPlayPause)
            {
                RemoveWindowSubclass(hPlayPause, PlaybackButtonSubclassProc, 2);
            }
            HWND hRewind = GetDlgItem(hDlg, IDC_TRIM_REWIND);
            if (hRewind)
            {
                RemoveWindowSubclass(hRewind, PlaybackButtonSubclassProc, 3);
            }
            HWND hForward = GetDlgItem(hDlg, IDC_TRIM_FORWARD);
            if (hForward)
            {
                RemoveWindowSubclass(hForward, PlaybackButtonSubclassProc, 4);
            }
        }
        if (pData && pData->hPreviewBitmap)
        {
            DeleteObject(pData->hPreviewBitmap);
            pData->hPreviewBitmap = nullptr;
        }
        if (pData)
        {
            pData->playbackFile = nullptr;
        }
        break;
    }

    case WM_TIMER:
        if (wParam == kPlaybackTimerId)
        {
            pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
            if (!pData)
            {
                KillTimer(hDlg, kPlaybackTimerId);
                return TRUE;
            }

            if (!pData->isPlaying.load(std::memory_order_relaxed))
            {
                KillTimer(hDlg, kPlaybackTimerId);
                RefreshPlaybackButtons(hDlg);
                return TRUE;
            }

            const int64_t nextTicks = pData->currentPosition.count() + kPlaybackStepTicks;
            if (nextTicks >= pData->trimEnd.count())
            {
                pData->currentPosition = pData->trimStart;
                StopPlayback(hDlg, pData);
                UpdateVideoPreview(hDlg, pData);
            }
            else
            {
                pData->currentPosition = winrt::TimeSpan{ nextTicks };
                UpdateVideoPreview(hDlg, pData);
            }
            return TRUE;
        }
        break;

    case WM_COMMAND:
        switch (LOWORD(wParam))
        {
        case IDC_TRIM_REWIND:
        case IDC_TRIM_PLAY_PAUSE:
        case IDC_TRIM_FORWARD:
        case IDC_TRIM_SKIP_START:
        case IDC_TRIM_SKIP_END:
        {
            if (HIWORD(wParam) == BN_CLICKED)
            {
                pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
                HandlePlaybackCommand(static_cast<int>(LOWORD(wParam)), pData);
                return TRUE;
            }
            break;
        }

        case IDOK:
            pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
            StopPlayback(hDlg, pData);
            // Trim times are already set by mouse dragging
            EndDialog(hDlg, IDOK);
            return TRUE;

        case IDCANCEL:
            pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
            StopPlayback(hDlg, pData);
            EndDialog(hDlg, IDCANCEL);
            return TRUE;
        }
        break;
    }

    return FALSE;
}

//----------------------------------------------------------------------------
//
// VideoRecordingSession::TrimVideoAsync
//
// Performs the actual video trimming operation
//
//----------------------------------------------------------------------------
winrt::IAsyncOperation<winrt::hstring> VideoRecordingSession::TrimVideoAsync(
    const std::wstring& sourceVideoPath,
    winrt::TimeSpan trimTimeStart,
    winrt::TimeSpan trimTimeEnd)
{
    try
    {
        // Load the source video file
        auto sourceFile = co_await winrt::StorageFile::GetFileFromPathAsync(sourceVideoPath);

        // Create a media composition
        winrt::MediaComposition composition;
        auto clip = co_await winrt::MediaClip::CreateFromFileAsync(sourceFile);

        // Set the trim times
        clip.TrimTimeFromStart(trimTimeStart);
        clip.TrimTimeFromEnd(clip.OriginalDuration() - trimTimeEnd);

        // Add the trimmed clip to the composition
        composition.Clips().Append(clip);

        // Create output file in temp folder
        auto tempFolder = co_await winrt::StorageFolder::GetFolderFromPathAsync(
            std::filesystem::temp_directory_path().wstring());
        auto zoomitFolder = co_await tempFolder.CreateFolderAsync(
            L"ZoomIt", winrt::CreationCollisionOption::OpenIfExists);

        // Generate unique filename
        std::wstring filename = L"zoomit_trimmed_" +
            std::to_wstring(GetTickCount64()) + L".mp4";
        auto outputFile = co_await zoomitFolder.CreateFileAsync(
            filename, winrt::CreationCollisionOption::ReplaceExisting);

        // Render the composition to the output file with fast trimming (no re-encode)
        auto renderResult = co_await composition.RenderToFileAsync(
            outputFile, winrt::MediaTrimmingPreference::Fast);

        if (renderResult == winrt::TranscodeFailureReason::None)
        {
            co_return winrt::hstring(outputFile.Path());
        }
        else
        {
            co_return winrt::hstring();
        }
    }
    catch (...)
    {
        co_return winrt::hstring();
    }
}
