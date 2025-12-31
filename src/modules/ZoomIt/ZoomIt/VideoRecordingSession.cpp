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
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Media.h>
#include <cstdlib>
#include <filesystem>
#include <shlwapi.h>   // For SHCreateStreamOnFileEx
#include <mmsystem.h>   // For timeBeginPeriod/timeEndPeriod

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "winmm.lib")

extern DWORD g_RecordScaling;

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Graphics;
    using namespace Windows::Graphics::Capture;
    using namespace Windows::Graphics::DirectX;
    using namespace Windows::Graphics::DirectX::Direct3D11;
    using namespace Windows::Graphics::Imaging;
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
constexpr UINT kGifDefaultDelayCs = 10;       // 100ms (~10 FPS) when metadata delay is missing
constexpr UINT kGifMinDelayCs = 2;            // 20ms minimum; browsers treat <2cs as 10cs (100ms)
constexpr UINT kGifBrowserFixupThreshold = 2; // Delays < this are treated as 10cs by browsers
constexpr UINT kGifMaxPreviewDimension = 1280; // cap decoded GIF preview size to keep playback smooth

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

static bool IsGifPath(const std::wstring& path)
{
    try
    {
        auto ext = std::filesystem::path(path).extension().wstring();
        return _wcsicmp(ext.c_str(), L".gif") == 0;
    }
    catch (...)
    {
        return false;
    }
}

static void CleanupGifFrames(VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData)
    {
        return;
    }

    for (auto& frame : pData->gifFrames)
    {
        if (frame.hBitmap)
        {
            DeleteObject(frame.hBitmap);
            frame.hBitmap = nullptr;
        }
    }
    pData->gifFrames.clear();
}

static size_t FindGifFrameIndex(const std::vector<VideoRecordingSession::TrimDialogData::GifFrame>& frames, int64_t ticks)
{
    if (frames.empty())
    {
        return 0;
    }

    // Linear scan is fine for typical GIF counts; keeps logic simple and predictable
    for (size_t i = 0; i < frames.size(); ++i)
    {
        const auto start = frames[i].start.count();
        const auto end = start + frames[i].duration.count();
        if (ticks >= start && ticks < end)
        {
            return i;
        }
    }

    // If we fall through, clamp to last frame
    return frames.size() - 1;
}

static bool LoadGifFrames(const std::wstring& gifPath, VideoRecordingSession::TrimDialogData* pData)
{
    OutputDebugStringW((L"[GIF Trim] LoadGifFrames called for: " + gifPath + L"\n").c_str());
    
    if (!pData)
    {
        OutputDebugStringW(L"[GIF Trim] pData is null\n");
        return false;
    }

    try
    {
        CleanupGifFrames(pData);

        winrt::com_ptr<IWICImagingFactory> factory;
        HRESULT hrFactory = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(factory.put()));
        if (FAILED(hrFactory))
        {
            OutputDebugStringW((L"[GIF Trim] CoCreateInstance WICImagingFactory failed hr=0x" + std::to_wstring(hrFactory) + L"\n").c_str());
            return false;
        }

        winrt::com_ptr<IWICBitmapDecoder> decoder;

        auto logHr = [&](const wchar_t* step, HRESULT hr)
        {
            wchar_t buf[512]{};
            swprintf_s(buf, L"[GIF Trim] %s failed hr=0x%08X path=%s\n", step, static_cast<unsigned>(hr), gifPath.c_str());
            OutputDebugStringW(buf);
        };

        auto tryCreateDecoder = [&]() -> bool
        {
            OutputDebugStringW(L"[GIF Trim] Trying CreateDecoderFromFilename...\n");
            HRESULT hr = factory->CreateDecoderFromFilename(gifPath.c_str(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, decoder.put());
            if (SUCCEEDED(hr))
            {
                OutputDebugStringW(L"[GIF Trim] CreateDecoderFromFilename succeeded\n");
                return true;
            }

            logHr(L"CreateDecoderFromFilename", hr);

            // Fallback: try opening with FILE_SHARE_READ | FILE_SHARE_WRITE to handle locked files
            OutputDebugStringW(L"[GIF Trim] Trying CreateStreamOnFile fallback...\n");
            HANDLE hFile = CreateFileW(gifPath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (hFile != INVALID_HANDLE_VALUE)
            {
                winrt::com_ptr<IStream> fileStream;
                // Create an IStream over the file handle using SHCreateStreamOnFileEx
                CloseHandle(hFile);
                hr = SHCreateStreamOnFileEx(gifPath.c_str(), STGM_READ | STGM_SHARE_DENY_NONE, 0, FALSE, nullptr, fileStream.put());
                if (SUCCEEDED(hr) && fileStream)
                {
                    hr = factory->CreateDecoderFromStream(fileStream.get(), nullptr, WICDecodeMetadataCacheOnLoad, decoder.put());
                    if (SUCCEEDED(hr))
                    {
                        OutputDebugStringW(L"[GIF Trim] CreateDecoderFromStream (SHCreateStreamOnFileEx) succeeded\n");
                        return true;
                    }
                    logHr(L"CreateDecoderFromStream(SHCreateStreamOnFileEx)", hr);
                }
                else
                {
                    logHr(L"SHCreateStreamOnFileEx", hr);
                }
            }

            return false;
        };

    auto tryCopyAndDecode = [&]() -> bool
    {
        OutputDebugStringW(L"[GIF Trim] Trying temp file copy fallback...\n");
        // Copy file to temp using Win32 APIs (no WinRT async)
        wchar_t tempDir[MAX_PATH];
        if (GetTempPathW(MAX_PATH, tempDir) == 0)
        {
            return false;
        }

        std::wstring tempPath = std::wstring(tempDir) + L"ZoomIt\\";
        CreateDirectoryW(tempPath.c_str(), nullptr);
        
        std::wstring tempName = L"gif_trim_cache_" + std::to_wstring(GetTickCount64()) + L".gif";
        tempPath += tempName;

        if (!CopyFileW(gifPath.c_str(), tempPath.c_str(), FALSE))
        {
            logHr(L"CopyFileW", HRESULT_FROM_WIN32(GetLastError()));
            return false;
        }

        HRESULT hr = factory->CreateDecoderFromFilename(tempPath.c_str(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, decoder.put());
        if (SUCCEEDED(hr))
        {
            OutputDebugStringW(L"[GIF Trim] CreateDecoderFromFilename(temp copy) succeeded\n");
            return true;
        }
        logHr(L"CreateDecoderFromFilename(temp copy)", hr);
        
        // Clean up temp file on failure
        DeleteFileW(tempPath.c_str());
        return false;
    };

    if (!tryCreateDecoder())
    {
        if (!tryCopyAndDecode())
        {
            return false;
        }
    }

    UINT frameCount = 0;
    if (FAILED(decoder->GetFrameCount(&frameCount)) || frameCount == 0)
    {
        return false;
    }

    int64_t cumulativeTicks = 0;
    UINT frameWidth = 0;
    UINT frameHeight = 0;

    for (UINT i = 0; i < frameCount; ++i)
    {
        winrt::com_ptr<IWICBitmapFrameDecode> frame;
        if (FAILED(decoder->GetFrame(i, frame.put())))
        {
            continue;
        }

        if (i == 0)
        {
            frame->GetSize(&frameWidth, &frameHeight);
        }

        UINT delayCs = kGifDefaultDelayCs;
        try
        {
            winrt::com_ptr<IWICMetadataQueryReader> metadata;
            if (SUCCEEDED(frame->GetMetadataQueryReader(metadata.put())) && metadata)
            {
                PROPVARIANT prop{};
                PropVariantInit(&prop);
                if (SUCCEEDED(metadata->GetMetadataByName(L"/grctlext/Delay", &prop)))
                {
                    if (prop.vt == VT_UI2)
                    {
                        delayCs = prop.uiVal;
                    }
                    else if (prop.vt == VT_UI1)
                    {
                        delayCs = prop.bVal;
                    }
                }
                PropVariantClear(&prop);
            }
        }
        catch (...)
        {
            // Keep fallback delay
        }

        if (delayCs == 0)
        {
            // GIF spec: delay of 0 means "as fast as possible"; browsers use ~10ms
            delayCs = kGifDefaultDelayCs;
        }
        else if (delayCs < kGifBrowserFixupThreshold)
        {
            // Browsers treat delays < 2cs (20ms) as 10cs (100ms) to prevent CPU-hogging GIFs
            delayCs = kGifDefaultDelayCs;
        }

        // Log the first few frame delays for debugging
        if (i < 3)
        {
            OutputDebugStringW((L"[GIF Trim] Frame " + std::to_wstring(i) + L" delay: " + std::to_wstring(delayCs) + L" cs (" + std::to_wstring(delayCs * 10) + L" ms)\n").c_str());
        }

        // Respect a max preview size to avoid huge allocations on large GIFs
        UINT targetWidth = frameWidth;
        UINT targetHeight = frameHeight;
        if (targetWidth > kGifMaxPreviewDimension || targetHeight > kGifMaxPreviewDimension)
        {
            const double scaleX = static_cast<double>(kGifMaxPreviewDimension) / static_cast<double>(targetWidth);
            const double scaleY = static_cast<double>(kGifMaxPreviewDimension) / static_cast<double>(targetHeight);
            const double scale = (std::min)(scaleX, scaleY);
            targetWidth = static_cast<UINT>(std::lround(static_cast<double>(targetWidth) * scale));
            targetHeight = static_cast<UINT>(std::lround(static_cast<double>(targetHeight) * scale));
            targetWidth = (std::max)(1u, targetWidth);
            targetHeight = (std::max)(1u, targetHeight);
        }

        winrt::com_ptr<IWICBitmapSource> source = frame;
        if (targetWidth != frameWidth || targetHeight != frameHeight)
        {
            winrt::com_ptr<IWICBitmapScaler> scaler;
            if (SUCCEEDED(factory->CreateBitmapScaler(scaler.put())))
            {
                if (SUCCEEDED(scaler->Initialize(frame.get(), targetWidth, targetHeight, WICBitmapInterpolationModeFant)))
                {
                    source = scaler;
                }
            }
        }

        winrt::com_ptr<IWICFormatConverter> converter;
        if (FAILED(factory->CreateFormatConverter(converter.put())))
        {
            continue;
        }

        if (FAILED(converter->Initialize(source.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.0, WICBitmapPaletteTypeCustom)))
        {
            continue;
        }

        UINT convertedWidth = 0;
        UINT convertedHeight = 0;
        converter->GetSize(&convertedWidth, &convertedHeight);
        if (convertedWidth == 0 || convertedHeight == 0)
        {
            continue;
        }

        const UINT stride = convertedWidth * 4;
        std::vector<BYTE> buffer(static_cast<size_t>(stride) * convertedHeight);
        if (FAILED(converter->CopyPixels(nullptr, stride, static_cast<UINT>(buffer.size()), buffer.data())))
        {
            continue;
        }

        BITMAPINFO bmi{};
        bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bmi.bmiHeader.biWidth = static_cast<LONG>(convertedWidth);
        bmi.bmiHeader.biHeight = -static_cast<LONG>(convertedHeight);
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = BI_RGB;

        void* bits = nullptr;
        HDC hdcScreen = GetDC(nullptr);
        HBITMAP hBitmap = CreateDIBSection(hdcScreen, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
        ReleaseDC(nullptr, hdcScreen);

        if (!hBitmap || !bits)
        {
            if (hBitmap)
            {
                DeleteObject(hBitmap);
            }
            continue;
        }

        for (UINT row = 0; row < convertedHeight; ++row)
        {
            memcpy(static_cast<BYTE*>(bits) + static_cast<size_t>(row) * stride,
                   buffer.data() + static_cast<size_t>(row) * stride,
                   stride);
        }

        VideoRecordingSession::TrimDialogData::GifFrame gifFrame;
        gifFrame.hBitmap = hBitmap;
        gifFrame.start = winrt::TimeSpan{ cumulativeTicks };
        gifFrame.duration = winrt::TimeSpan{ static_cast<int64_t>(delayCs) * 100'000 }; // centiseconds to 100ns
        gifFrame.width = convertedWidth;
        gifFrame.height = convertedHeight;

        cumulativeTicks += gifFrame.duration.count();
        pData->gifFrames.push_back(gifFrame);
    }

    if (pData->gifFrames.empty())
    {
        OutputDebugStringW(L"[GIF Trim] No frames loaded\n");
        return false;
    }

    const auto& lastFrame = pData->gifFrames.back();
    pData->videoDuration = winrt::TimeSpan{ lastFrame.start.count() + lastFrame.duration.count() };
    pData->trimEnd = pData->videoDuration;
    pData->gifFramesLoaded = true;
    pData->gifLastFrameIndex = 0;

    OutputDebugStringW((L"[GIF Trim] Successfully loaded " + std::to_wstring(pData->gifFrames.size()) + L" frames\n").c_str());
    return true;
    }
    catch (const winrt::hresult_error& e)
    {
        OutputDebugStringW((L"[GIF Trim] Exception in LoadGifFrames: " + e.message() + L"\n").c_str());
        return false;
    }
    catch (const std::exception& e)
    {
        OutputDebugStringA("[GIF Trim] std::exception in LoadGifFrames: ");
        OutputDebugStringA(e.what());
        OutputDebugStringA("\n");
        return false;
    }
    catch (...)
    {
        OutputDebugStringW(L"[GIF Trim] Unknown exception in LoadGifFrames\n");
        return false;
    }
}

namespace
{
    struct __declspec(uuid("5b0d3235-4dba-4d44-8657-1f1d0f83e9a3")) IMemoryBufferByteAccess : IUnknown
    {
        virtual HRESULT STDMETHODCALLTYPE GetBuffer(BYTE** value, UINT32* capacity) = 0;
    };

    constexpr int kTimelinePadding = 12;
    constexpr int kTimelineTrackHeight = 24;
    constexpr int kTimelineTrackTopOffset = 18;
    constexpr int kTimelineHandleHalfWidth = 7;
    constexpr int kTimelineHandleHeight = 40;
    constexpr int kTimelineHandleHitRadius = 18;
    constexpr int64_t kJogStepTicks = 20'000'000;   // 2 seconds (or 1s for short videos)
    constexpr int64_t kPreviewMinDeltaTicks = 2'000'000; // 20ms between thumbnails while playing
    constexpr UINT32 kPreviewRequestWidthPlaying = 320;
    constexpr UINT32 kPreviewRequestHeightPlaying = 180;
    constexpr int64_t kTicksPerMicrosecond = 10; // 100ns units per microsecond
    constexpr int64_t kPlaybackSyncIntervalMs = 40;             // refresh baseline frequently for smoother prediction
    constexpr int64_t kPlaybackDriftCheckMs = 40;              // sample MediaPlayer at least every 40ms (overridden to every tick currently)
    constexpr int64_t kPlaybackDriftSnapTicks = 2'000'000;     // snap if drift exceeds 200ms
    constexpr int kPlaybackDriftBlendNumerator = 1;            // blend 20% toward real position
    constexpr int kPlaybackDriftBlendDenominator = 5;
    constexpr UINT WMU_PREVIEW_READY = WM_USER + 1;
    constexpr UINT WMU_PREVIEW_SCHEDULED = WM_USER + 2;
    constexpr UINT WMU_DURATION_CHANGED = WM_USER + 3;
    constexpr UINT WMU_PLAYBACK_POSITION = WM_USER + 4;
    constexpr UINT WMU_PLAYBACK_STOP = WM_USER + 5;

    std::atomic<int> g_highResTimerRefs{ 0 };

    void AcquireHighResTimer()
    {
        if (g_highResTimerRefs.fetch_add(1, std::memory_order_relaxed) == 0)
        {
            timeBeginPeriod(1);
        }
    }

    void ReleaseHighResTimer()
    {
        const int prev = g_highResTimerRefs.fetch_sub(1, std::memory_order_relaxed);
        if (prev == 1)
        {
            timeEndPeriod(1);
        }
    }

    bool EnsurePlaybackDevice(VideoRecordingSession::TrimDialogData* pData)
    {
        if (!pData)
        {
            return false;
        }

        if (pData->previewD3DDevice && pData->previewD3DContext)
        {
            return true;
        }

        UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(_DEBUG)
        creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

        D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0 };
        D3D_FEATURE_LEVEL levelCreated = D3D_FEATURE_LEVEL_11_0;

        winrt::com_ptr<ID3D11Device> device;
        winrt::com_ptr<ID3D11DeviceContext> context;
        if (SUCCEEDED(D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            creationFlags,
            levels,
            ARRAYSIZE(levels),
            D3D11_SDK_VERSION,
            device.put(),
            &levelCreated,
            context.put())))
        {
            pData->previewD3DDevice = device;
            pData->previewD3DContext = context;
            return true;
        }

        return false;
    }

    bool EnsureFrameTextures(VideoRecordingSession::TrimDialogData* pData, UINT width, UINT height)
    {
        if (!pData || !pData->previewD3DDevice)
        {
            return false;
        }

        auto recreate = [&]()
        {
            pData->previewFrameTexture = nullptr;
            pData->previewFrameStaging = nullptr;

            D3D11_TEXTURE2D_DESC desc{};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_DEFAULT;
            desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

            winrt::com_ptr<ID3D11Texture2D> frameTex;
            if (FAILED(pData->previewD3DDevice->CreateTexture2D(&desc, nullptr, frameTex.put())))
            {
                return false;
            }

            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            desc.Usage = D3D11_USAGE_STAGING;

            winrt::com_ptr<ID3D11Texture2D> staging;
            if (FAILED(pData->previewD3DDevice->CreateTexture2D(&desc, nullptr, staging.put())))
            {
                return false;
            }

            pData->previewFrameTexture = frameTex;
            pData->previewFrameStaging = staging;
            return true;
        };

        if (!pData->previewFrameTexture || !pData->previewFrameStaging)
        {
            return recreate();
        }

        D3D11_TEXTURE2D_DESC existing{};
        pData->previewFrameTexture->GetDesc(&existing);
        if (existing.Width != width || existing.Height != height)
        {
            return recreate();
        }

        return true;
    }

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

        // Enable OK button only if grippers have moved (trim is needed)
        const bool trimChanged = (pData->trimStart.count() != pData->originalTrimStart.count()) ||
                                 (pData->trimEnd.count() != pData->originalTrimEnd.count());
        EnableWindow(GetDlgItem(hDlg, IDOK), trimChanged);
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

static int64_t SteadyClockMicros()
{
    return std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

static void ResetSmoothPlayback(VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData)
    {
        return;
    }

    pData->smoothActive.store(false, std::memory_order_relaxed);
    pData->smoothBaseTicks.store(0, std::memory_order_relaxed);
    pData->smoothLastSyncMicroseconds.store(0, std::memory_order_relaxed);
    pData->smoothHasNonZeroSample.store(false, std::memory_order_relaxed);
}

static void LogSmoothingEvent(const wchar_t* label, int64_t predictedTicks, int64_t mediaTicks, int64_t driftTicks);

static int64_t GetSmoothedPositionTicks(
    VideoRecordingSession::TrimDialogData* pData,
    int64_t minTicks,
    int64_t maxTicks);

static void BeginSmoothPlayback(VideoRecordingSession::TrimDialogData* pData, int64_t mediaTicks)
{
    if (!pData)
    {
        return;
    }

    const int64_t nowUs = SteadyClockMicros();
    pData->smoothBaseTicks.store(mediaTicks, std::memory_order_relaxed);
    pData->smoothLastSyncMicroseconds.store(nowUs, std::memory_order_relaxed);
    pData->smoothActive.store(true, std::memory_order_relaxed);
    pData->smoothHasNonZeroSample.store(mediaTicks > 0, std::memory_order_relaxed);
}

static void SyncSmoothPlayback(VideoRecordingSession::TrimDialogData* pData, int64_t mediaTicks, int64_t /*minTicks*/, int64_t /*maxTicks*/)
{
    if (!pData)
    {
        return;
    }

    const int64_t nowUs = SteadyClockMicros();
    pData->smoothBaseTicks.store(mediaTicks, std::memory_order_relaxed);
    pData->smoothLastSyncMicroseconds.store(nowUs, std::memory_order_relaxed);
    pData->smoothActive.store(true, std::memory_order_relaxed);
    pData->smoothHasNonZeroSample.store(mediaTicks > 0, std::memory_order_relaxed);

    LogSmoothingEvent(L"setBase", mediaTicks, mediaTicks, 0);
}

static int64_t GetSmoothedPositionTicks(
    VideoRecordingSession::TrimDialogData* pData,
    int64_t minTicks,
    int64_t maxTicks)
{
    if (!pData)
    {
        return minTicks;
    }

    if (!pData->smoothActive.load(std::memory_order_relaxed))
    {
        const int64_t rawTicks = pData->currentPosition.count();
        return std::clamp<int64_t>(rawTicks, minTicks, maxTicks);
    }

    const int64_t lastSyncUs = pData->smoothLastSyncMicroseconds.load(std::memory_order_relaxed);
    const int64_t baseTicks = pData->smoothBaseTicks.load(std::memory_order_relaxed);

    if (lastSyncUs == 0)
    {
        return std::clamp<int64_t>(baseTicks, minTicks, maxTicks);
    }

    const int64_t nowUs = SteadyClockMicros();
    const int64_t deltaUs = (std::max<int64_t>)(0, nowUs - lastSyncUs);
    const int64_t predicted = baseTicks + deltaUs * kTicksPerMicrosecond;
    return std::clamp<int64_t>(predicted, minTicks, maxTicks);
}

static void LogSmoothingEvent(const wchar_t* label, int64_t predictedTicks, int64_t mediaTicks, int64_t driftTicks)
{
    wchar_t buf[256]{};
    swprintf_s(buf, L"[TrimSmooth] %s pred=%lld media=%lld drift=%lld\n",
        label ? label : L"", static_cast<long long>(predictedTicks), static_cast<long long>(mediaTicks), static_cast<long long>(driftTicks));
    OutputDebugStringW(buf);
}

static void StopPlayback(HWND hDlg, VideoRecordingSession::TrimDialogData* pData, bool capturePosition = true);
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
            try
            {
                OutputDebugStringW(L"[Trim] Trim button clicked\n");
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
                OutputDebugStringW((L"[Trim] Calling ShowTrimDialog with path: " + m_videoPath + L"\n").c_str());

                auto trimResult = VideoRecordingSession::ShowTrimDialog(hParent, m_videoPath, *m_pTrimStart, *m_pTrimEnd);
                OutputDebugStringW((L"[Trim] ShowTrimDialog returned: " + std::to_wstring(trimResult) + L"\n").c_str());
                if (trimResult == IDOK)
                {
                    *m_pShouldTrim = true;
                }
            }
            catch (const std::exception& e)
            {
                OutputDebugStringA("[Trim] Exception in OnButtonClicked: ");
                OutputDebugStringA(e.what());
                OutputDebugStringA("\n");
            }
            catch (...)
            {
                OutputDebugStringW(L"[Trim] Unknown exception in OnButtonClicked\n");
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

    const bool isGif = IsGifPath(originalVideoPath);

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

    if (isGif)
    {
        saveDialog->SetDefaultExtension(L".gif");
        COMDLG_FILTERSPEC fileTypes[] = {
            { L"GIF Animation", L"*.gif" }
        };
        saveDialog->SetFileTypes(_countof(fileTypes), fileTypes);
    }
    else
    {
        saveDialog->SetDefaultExtension(L".mp4");
        COMDLG_FILTERSPEC fileTypes[] = {
            { L"MP4 Video", L"*.mp4" }
        };
        saveDialog->SetFileTypes(_countof(fileTypes), fileTypes);
    }
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
            auto trimOp = isGif ? TrimGifAsync(originalVideoPath, trimStart, trimEnd)
                                : TrimVideoAsync(originalVideoPath, trimStart, trimEnd);

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
            OutputDebugStringW(L"[Trim] Calling ShowTrimDialogInternal...\n");
            INT_PTR dlgResult = ShowTrimDialogInternal(hParent, videoPath, trimStart, trimEnd);
            OutputDebugStringW((L"[Trim] ShowTrimDialogInternal returned: " + std::to_wstring(dlgResult) + L"\n").c_str());
            promise.set_value(dlgResult);
        }
        catch (const winrt::hresult_error& e)
        {
            OutputDebugStringW((L"[Trim] winrt exception: " + e.message() + L"\n").c_str());
            promise.set_exception(std::current_exception());
        }
        catch (const std::exception& e)
        {
            OutputDebugStringA("[Trim] std::exception: ");
            OutputDebugStringA(e.what());
            OutputDebugStringA("\n");
            promise.set_exception(std::current_exception());
        }
        catch (...)
        {
            OutputDebugStringW(L"[Trim] Unknown exception\n");
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
    data.isGif = IsGifPath(videoPath);

    if (data.isGif)
    {
        if (!LoadGifFrames(videoPath, &data))
        {
            MessageBox(hParent, L"Unable to load the GIF for trimming. The file may be locked or unreadable.", L"Error", MB_OK | MB_ICONERROR);
            return IDCANCEL;
        }
    }
    else
    {
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

    OutputDebugStringW(L"[Trim] About to call DialogBoxParam...\n");
    auto result = DialogBoxParam(
        GetModuleHandle(nullptr),
        MAKEINTRESOURCE(IDD_VIDEO_TRIM),
        hParent,
        TrimDialogProc,
        reinterpret_cast<LPARAM>(&data));
    OutputDebugStringW((L"[Trim] DialogBoxParam returned: " + std::to_wstring(result) + L"\n").c_str());

    if (result == IDOK)
    {
        trimStart = data.trimStart;
        trimEnd = data.trimEnd;
    }

    return result;
}

static void UpdatePositionUI(HWND hDlg, VideoRecordingSession::TrimDialogData* pData, bool invalidateTimeline = true)
{
    if (!pData || !hDlg)
    {
        return;
    }

    const auto previewTime = pData->previewOverrideActive ? pData->previewOverride : pData->currentPosition;
    SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, previewTime, true);
    if (invalidateTimeline)
    {
        InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_TIMELINE), nullptr, FALSE);
    }
}

static void SyncMediaPlayerPosition(VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData || !pData->mediaPlayer)
    {
        return;
    }

    try
    {
        auto session = pData->mediaPlayer.PlaybackSession();
        if (session)
        {
            const int64_t clampedTicks = std::clamp<int64_t>(
                pData->currentPosition.count(),
                pData->trimStart.count(),
                pData->trimEnd.count());
            session.Position(winrt::TimeSpan{ clampedTicks });
        }
    }
    catch (...)
    {
    }
}

static HBITMAP CreateBitmapFromSoftwareBitmap(winrt::SoftwareBitmap const& sourceBitmap)
{
    if (!sourceBitmap)
    {
        return nullptr;
    }

    winrt::SoftwareBitmap bitmap = sourceBitmap;
    if (bitmap.BitmapPixelFormat() != winrt::BitmapPixelFormat::Bgra8 ||
        bitmap.BitmapAlphaMode() != winrt::BitmapAlphaMode::Premultiplied)
    {
        bitmap = winrt::SoftwareBitmap::Convert(bitmap, winrt::BitmapPixelFormat::Bgra8, winrt::BitmapAlphaMode::Premultiplied);
    }

    auto buffer = bitmap.LockBuffer(winrt::BitmapBufferAccessMode::Read);
    auto reference = buffer.CreateReference();
    auto byteAccess = reference.as<IMemoryBufferByteAccess>();

    BYTE* data = nullptr;
    UINT32 capacity = 0;
    if (FAILED(byteAccess->GetBuffer(&data, &capacity)))
    {
        return nullptr;
    }

    auto desc = buffer.GetPlaneDescription(0);
    if (desc.Width <= 0 || desc.Height <= 0)
    {
        return nullptr;
    }

    const UINT32 requiredBytes = static_cast<UINT32>(desc.Width * 4) * static_cast<UINT32>(desc.Height);
    if (capacity < requiredBytes + desc.StartIndex)
    {
        return nullptr;
    }

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = desc.Width;
    bmi.bmiHeader.biHeight = -desc.Height; // Top-down bitmap
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HDC hdcScreen = GetDC(nullptr);
    HBITMAP hBitmap = CreateDIBSection(hdcScreen, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    ReleaseDC(nullptr, hdcScreen);

    if (!hBitmap || !bits)
    {
        if (hBitmap)
        {
            DeleteObject(hBitmap);
        }
        return nullptr;
    }

    const LONG srcStride = desc.Stride;
    BYTE* destBytes = static_cast<BYTE*>(bits);
    const size_t destStride = static_cast<size_t>(desc.Width) * 4;
    const BYTE* srcBase = data + desc.StartIndex;

    for (int row = 0; row < desc.Height; ++row)
    {
        memcpy(destBytes + row * destStride, srcBase + row * srcStride, destStride);
    }

    return hBitmap;
}

static void CleanupMediaPlayer(VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData || !pData->mediaPlayer)
    {
        return;
    }

    try
    {
        auto session = pData->mediaPlayer.PlaybackSession();
        if (session)
        {
            if (pData->positionChangedToken.value)
            {
                session.PositionChanged(pData->positionChangedToken);
                pData->positionChangedToken = {};
            }
            if (pData->stateChangedToken.value)
            {
                session.PlaybackStateChanged(pData->stateChangedToken);
                pData->stateChangedToken = {};
            }
        }

        if (pData->frameAvailableToken.value)
        {
            pData->mediaPlayer.VideoFrameAvailable(pData->frameAvailableToken);
            pData->frameAvailableToken = {};
        }

        pData->mediaPlayer.Close();
    }
    catch (...)
    {
    }

    pData->mediaPlayer = nullptr;
    pData->frameCopyInProgress.store(false, std::memory_order_relaxed);
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
    UpdatePositionUI(hDlg, pData, invalidateTimeline);

    // When playing with the frame server, frames arrive via VideoFrameAvailable; avoid extra thumbnails.
    if (pData->isPlaying.load(std::memory_order_relaxed) && pData->mediaPlayer)
    {
        return;
    }

    const int64_t requestTicks = previewTime.count();
    pData->latestPreviewRequest.store(requestTicks, std::memory_order_relaxed);

    if (pData->loadingPreview.exchange(true))
    {
        // A preview request is already running; we'll schedule the latest once it completes.
        return;
    }

    if (pData->isGif)
    {
        // Use request time directly (don't clamp to trim bounds) so thumbnail updates outside trim region
        const int64_t clampedTicks = std::clamp<int64_t>(requestTicks, 0, pData->videoDuration.count());
        if (!pData->gifFrames.empty())
        {
            const size_t frameIndex = FindGifFrameIndex(pData->gifFrames, clampedTicks);
            pData->gifLastFrameIndex = frameIndex;
            {
                std::lock_guard<std::mutex> lock(pData->previewBitmapMutex);
                if (pData->hPreviewBitmap && pData->previewBitmapOwned)
                {
                    DeleteObject(pData->hPreviewBitmap);
                }
                pData->hPreviewBitmap = pData->gifFrames[frameIndex].hBitmap;
                pData->previewBitmapOwned = false;
            }

            pData->lastRenderedPreview.store(clampedTicks, std::memory_order_relaxed);
            pData->loadingPreview.store(false, std::memory_order_relaxed);

            if (hDlg)
            {
                InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_PREVIEW), nullptr, FALSE);
            }
            return;
        }

        pData->loadingPreview.store(false, std::memory_order_relaxed);
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
                    // Also update original values so OK button state stays correct
                    pData->originalTrimEnd = actualDuration;
                    if (pData->originalTrimStart.count() > pData->originalTrimEnd.count())
                    {
                        pData->originalTrimStart = pData->originalTrimEnd;
                    }
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

                const bool isPlaying = pData->isPlaying.load(std::memory_order_relaxed);
                const UINT32 reqW = isPlaying ? kPreviewRequestWidthPlaying : 0;
                const UINT32 reqH = isPlaying ? kPreviewRequestHeightPlaying : 0;

                auto stream = composition.GetThumbnailAsync(
                    winrt::TimeSpan{ requestTicks },
                    reqW,
                    reqH,
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

                                                {
                                                    std::lock_guard<std::mutex> lock(pData->previewBitmapMutex);
                                                    if (pData->hPreviewBitmap && pData->previewBitmapOwned)
                                                    {
                                                        DeleteObject(pData->hPreviewBitmap);
                                                    }
                                                    pData->hPreviewBitmap = hBitmap;
                                                    pData->previewBitmapOwned = true;
                                                }
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
            pData->lastRenderedPreview.store(requestTicks, std::memory_order_relaxed);
            PostMessage(hDlg, WMU_PREVIEW_READY, 0, 0);
        }

        if (pData->latestPreviewRequest.load(std::memory_order_relaxed) != requestTicksRaw)
        {
            PostMessage(hDlg, WMU_PREVIEW_SCHEDULED, 0, 0);
        }

        if (durationChanged)
        {
            PostMessage(hDlg, WMU_DURATION_CHANGED, 0, 0);
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
    constexpr UINT kPlaybackTimerIntervalMs = 16;  // Fallback for GIF; MP4 uses multimedia timer
    constexpr int64_t kPlaybackStepTicks = static_cast<int64_t>(kPlaybackTimerIntervalMs) * 10'000;
    constexpr UINT WMU_MM_TIMER_TICK = WM_USER + 10;  // Posted by multimedia timer callback
    constexpr UINT kMMTimerIntervalMs = 8;  // 8ms for ~120Hz update rate
}

// Multimedia timer callback - runs in a separate thread, just posts a message
static void CALLBACK MMTimerCallback(UINT /*uTimerID*/, UINT /*uMsg*/, DWORD_PTR dwUser, DWORD_PTR /*dw1*/, DWORD_PTR /*dw2*/)
{
    HWND hDlg = reinterpret_cast<HWND>(dwUser);
    if (hDlg && IsWindow(hDlg))
    {
        PostMessage(hDlg, WMU_MM_TIMER_TICK, 0, 0);
    }
}

static void StopMMTimer(VideoRecordingSession::TrimDialogData* pData)
{
    if (pData && pData->mmTimerId != 0)
    {
        timeKillEvent(pData->mmTimerId);
        pData->mmTimerId = 0;
    }
}

static bool StartMMTimer(HWND hDlg, VideoRecordingSession::TrimDialogData* pData)
{
    if (!pData || !hDlg)
    {
        return false;
    }
    
    StopMMTimer(pData);
    
    pData->mmTimerId = timeSetEvent(
        kMMTimerIntervalMs,
        1,  // 1ms resolution
        MMTimerCallback,
        reinterpret_cast<DWORD_PTR>(hDlg),
        TIME_PERIODIC | TIME_KILL_SYNCHRONOUS);
    
    return pData->mmTimerId != 0;
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
            StopPlayback(hDlg, pData, true);
        }
        else
        {
            StartPlaybackAsync(hDlg, pData);
        }
        break;

    case IDC_TRIM_REWIND:
    {
        StopPlayback(hDlg, pData, false);
        // Use 1 second step for timelines < 20 seconds, 2 seconds otherwise
        const int64_t duration = pData->trimEnd.count() - pData->trimStart.count();
        const int64_t stepTicks = (duration < 200'000'000) ? 10'000'000 : kJogStepTicks;
        const int64_t newTicks = (std::max)(pData->trimStart.count(), pData->currentPosition.count() - stepTicks);
        pData->currentPosition = winrt::TimeSpan{ newTicks };
        SyncMediaPlayerPosition(pData);
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    case IDC_TRIM_FORWARD:
    {
        StopPlayback(hDlg, pData, false);
        // Use 1 second step for timelines < 20 seconds, 2 seconds otherwise
        const int64_t duration = pData->trimEnd.count() - pData->trimStart.count();
        const int64_t stepTicks = (duration < 200'000'000) ? 10'000'000 : kJogStepTicks;
        const int64_t newTicks = (std::min)(pData->trimEnd.count(), pData->currentPosition.count() + stepTicks);
        pData->currentPosition = winrt::TimeSpan{ newTicks };
        SyncMediaPlayerPosition(pData);
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    case IDC_TRIM_SKIP_END:
    {
        StopPlayback(hDlg, pData, false);
        pData->currentPosition = pData->trimEnd;
        SyncMediaPlayerPosition(pData);
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    default:
        StopPlayback(hDlg, pData, false);
        pData->currentPosition = pData->trimStart;
        SyncMediaPlayerPosition(pData);
        UpdateVideoPreview(hDlg, pData);
        break;
    }

    RefreshPlaybackButtons(hDlg);
}

static void StopPlayback(HWND hDlg, VideoRecordingSession::TrimDialogData* pData, bool capturePosition)
{
    if (!pData)
    {
        return;
    }

    const bool wasPlaying = pData->isPlaying.exchange(false, std::memory_order_relaxed);
    ResetSmoothPlayback(pData);

    // Stop audio playback and align media position with UI state, but keep player alive for resume
    if (pData->mediaPlayer)
    {
        try
        {
            auto session = pData->mediaPlayer.PlaybackSession();
            if (session)
            {
                if (capturePosition)
                {
                    pData->currentPosition = session.Position();
                }
                session.Position(pData->currentPosition);
            }
            pData->mediaPlayer.Pause();
        }
        catch (...)
        {
        }
    }

    if (hDlg)
    {
        if (wasPlaying)
        {
            StopMMTimer(pData);  // Stop multimedia timer for MP4
            KillTimer(hDlg, kPlaybackTimerId);  // Stop regular timer for GIF
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

    ResetSmoothPlayback(pData);

    if (pData->currentPosition.count() < pData->trimStart.count() ||
        pData->currentPosition.count() >= pData->trimEnd.count())
    {
        pData->currentPosition = pData->trimStart;
        UpdateVideoPreview(hDlg, pData);
    }

    // Store the position where playback started so we can loop back to it
    pData->playbackStartPosition = pData->currentPosition;

    bool expected = false;
    if (!pData->isPlaying.compare_exchange_strong(expected, true, std::memory_order_relaxed))
    {
        co_return;
    }

    if (pData->isGif)
    {
        // Initialize the frame start time for proper timing
        pData->gifFrameStartTime = std::chrono::steady_clock::now();
        
        // Use multimedia timer for smooth GIF playback
        if (!StartMMTimer(hDlg, pData))
        {
            pData->isPlaying.store(false, std::memory_order_relaxed);
            RefreshPlaybackButtons(hDlg);
            co_return;
        }

        PostMessage(hDlg, WMU_PLAYBACK_POSITION, 0, 0);
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    // If a player already exists (paused), just resume from currentPosition
    if (pData->mediaPlayer)
    {
        try
        {
            auto session = pData->mediaPlayer.PlaybackSession();
            if (session)
            {
                const int64_t clampedTicks = std::clamp<int64_t>(
                    pData->currentPosition.count(),
                    pData->trimStart.count(),
                    pData->trimEnd.count());
                session.Position(winrt::TimeSpan{ clampedTicks });
                pData->currentPosition = winrt::TimeSpan{ clampedTicks };
                // Defer smoothing until the first real media sample to avoid extrapolating from zero
                pData->smoothActive.store(false, std::memory_order_relaxed);
                pData->smoothHasNonZeroSample.store(false, std::memory_order_relaxed);
            }
            pData->mediaPlayer.Play();
        }
        catch (...)
        {
        }

        // Use multimedia timer for smooth updates
        if (!StartMMTimer(hDlg, pData))
        {
            pData->isPlaying.store(false, std::memory_order_relaxed);
            ResetSmoothPlayback(pData);
            RefreshPlaybackButtons(hDlg);
            co_return;
        }

        PostMessage(hDlg, WMU_PLAYBACK_POSITION, 0, 0);
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    CleanupMediaPlayer(pData);

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
        newPlayer.IsVideoFrameServerEnabled(true);
        newPlayer.AutoPlay(false);
        newPlayer.Volume(1.0);

        pData->frameCopyInProgress.store(false, std::memory_order_relaxed);
        pData->mediaPlayer = newPlayer;

        auto mediaSource = winrt::MediaSource::CreateFromStorageFile(pData->playbackFile);
        VideoRecordingSession::TrimDialogData* dataPtr = pData;

        pData->frameAvailableToken = pData->mediaPlayer.VideoFrameAvailable([hDlg, dataPtr](auto const& sender, auto const&)
        {
            if (!dataPtr)
            {
                return;
            }

            if (dataPtr->frameCopyInProgress.exchange(true, std::memory_order_relaxed))
            {
                return;
            }

            try
            {
                if (!EnsurePlaybackDevice(dataPtr))
                {
                    dataPtr->frameCopyInProgress.store(false, std::memory_order_relaxed);
                    return;
                }

                auto session = sender.PlaybackSession();
                UINT width = session.NaturalVideoWidth();
                UINT height = session.NaturalVideoHeight();
                if (width == 0 || height == 0)
                {
                    width = 640;
                    height = 360;
                }

                if (!EnsureFrameTextures(dataPtr, width, height))
                {
                    dataPtr->frameCopyInProgress.store(false, std::memory_order_relaxed);
                    return;
                }

                winrt::com_ptr<IDXGISurface> dxgiSurface;
                if (dataPtr->previewFrameTexture)
                {
                    dxgiSurface = dataPtr->previewFrameTexture.as<IDXGISurface>();
                }

                if (dxgiSurface)
                {
                    winrt::com_ptr<IInspectable> inspectableSurface;
                    if (SUCCEEDED(CreateDirect3D11SurfaceFromDXGISurface(dxgiSurface.get(), inspectableSurface.put())))
                    {
                        auto surface = inspectableSurface.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface>();
                        sender.CopyFrameToVideoSurface(surface);

                        if (dataPtr->previewD3DContext && dataPtr->previewFrameStaging)
                        {
                            dataPtr->previewD3DContext->CopyResource(dataPtr->previewFrameStaging.get(), dataPtr->previewFrameTexture.get());

                            D3D11_MAPPED_SUBRESOURCE mapped{};
                            if (SUCCEEDED(dataPtr->previewD3DContext->Map(dataPtr->previewFrameStaging.get(), 0, D3D11_MAP_READ, 0, &mapped)))
                            {
                                const UINT rowPitch = mapped.RowPitch;
                                const UINT bytesPerPixel = 4;
                                const UINT destStride = width * bytesPerPixel;

                                BITMAPINFO bmi{};
                                bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
                                bmi.bmiHeader.biWidth = static_cast<LONG>(width);
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
                                    BYTE* dest = static_cast<BYTE*>(bits);
                                    const BYTE* src = static_cast<const BYTE*>(mapped.pData);
                                    for (UINT y = 0; y < height; ++y)
                                    {
                                        memcpy(dest + y * destStride, src + y * rowPitch, destStride);
                                    }

                                    {
                                        std::lock_guard<std::mutex> lock(dataPtr->previewBitmapMutex);
                                        if (dataPtr->hPreviewBitmap && dataPtr->previewBitmapOwned)
                                        {
                                            DeleteObject(dataPtr->hPreviewBitmap);
                                        }
                                        dataPtr->hPreviewBitmap = hBitmap;
                                        dataPtr->previewBitmapOwned = true;
                                    }

                                    PostMessage(hDlg, WMU_PREVIEW_READY, 0, 0);
                                }
                                else if (hBitmap)
                                {
                                    DeleteObject(hBitmap);
                                }

                                dataPtr->previewD3DContext->Unmap(dataPtr->previewFrameStaging.get(), 0);
                            }
                        }
                    }
                }
            }
            catch (...)
            {
            }

            dataPtr->frameCopyInProgress.store(false, std::memory_order_relaxed);
        });

        auto session = pData->mediaPlayer.PlaybackSession();
        pData->positionChangedToken = session.PositionChanged([hDlg, dataPtr](auto const& sender, auto const&)
        {
            if (!dataPtr)
            {
                return;
            }

            try
            {
                auto pos = sender.Position();
                dataPtr->currentPosition = pos;

                if (dataPtr->isPlaying.load(std::memory_order_relaxed) &&
                    !dataPtr->smoothHasNonZeroSample.load(std::memory_order_relaxed) &&
                    pos.count() > 0)
                {
                    // Seed smoothing on first real position, but keep baseline exact to avoid a jump
                    dataPtr->smoothHasNonZeroSample.store(true, std::memory_order_relaxed);
                    SyncSmoothPlayback(dataPtr, pos.count(), dataPtr->trimStart.count(), dataPtr->trimEnd.count());
                    LogSmoothingEvent(L"eventFirst", pos.count(), pos.count(), 0);
                }

                if (pos >= dataPtr->trimEnd)
                {
                    dataPtr->currentPosition = dataPtr->trimStart;
                    PostMessage(hDlg, WMU_PLAYBACK_STOP, 0, 0);
                }
                else
                {
                    PostMessage(hDlg, WMU_PLAYBACK_POSITION, 0, 0);
                }
            }
            catch (...)
            {
            }
        });

        pData->stateChangedToken = session.PlaybackStateChanged([hDlg](auto const&, auto const&)
        {
            PostMessage(hDlg, WMU_PLAYBACK_POSITION, 0, 0);
        });

        pData->mediaPlayer.MediaOpened([dataPtr, hDlg](auto const& sender, auto const&)
        {
            if (!dataPtr)
            {
                return;
            }
            try
            {
                const int64_t clampedTicks = std::clamp<int64_t>(
                    dataPtr->currentPosition.count(),
                    dataPtr->trimStart.count(),
                    dataPtr->trimEnd.count());
                sender.PlaybackSession().Position(winrt::TimeSpan{ clampedTicks });
                sender.Play();
            }
            catch (...)
            {
            }
        });

        pData->mediaPlayer.Source(mediaSource);
    }
    catch (...)
    {
        pData->isPlaying.store(false, std::memory_order_relaxed);
        CleanupMediaPlayer(pData);
        if (newPlayer)
        {
            try
            {
                newPlayer.Close();
            }
            catch (...)
            {
            }
        }
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    // Use multimedia timer for smooth updates
    if (!StartMMTimer(hDlg, pData))
    {
        pData->isPlaying.store(false, std::memory_order_relaxed);
        CleanupMediaPlayer(pData);
        ResetSmoothPlayback(pData);
        RefreshPlaybackButtons(hDlg);
        co_return;
    }

    // Defer smoothing until first real playback position is reported to prevent early extrapolation
    pData->smoothActive.store(false, std::memory_order_relaxed);
    pData->smoothHasNonZeroSample.store(false, std::memory_order_relaxed);
    PostMessage(hDlg, WMU_PLAYBACK_POSITION, 0, 0);
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
        if (!pData->restorePreviewOnRelease)
        {
            pData->previewOverrideActive = false;
            pData->playheadPushed = false;
            return;
        }

        if (pData->playheadPushed)
        {
            // Keep pushed playhead; just clear override flags
            pData->previewOverrideActive = false;
            pData->restorePreviewOnRelease = false;
            pData->playheadPushed = false;
            return;
        }

        if (pData->hDialog)
        {
            const int64_t restoredTicks = std::clamp<int64_t>(
                pData->positionBeforeOverride.count(),
                pData->trimStart.count(),
                pData->trimEnd.count());
            pData->currentPosition = winrt::TimeSpan{ restoredTicks };
            pData->previewOverrideActive = false;
            pData->restorePreviewOnRelease = false;
            pData->playheadPushed = false;
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
        // Pause without recapturing position; we might be parked on a handle
        StopPlayback(pData->hDialog, pData, false);

        RECT rcClient{};
        GetClientRect(hWnd, &rcClient);
        const int width = rcClient.right - rcClient.left;
        if (width <= 0)
        {
            break;
        }

        const int x = GET_X_LPARAM(lParam);
        const int y = GET_Y_LPARAM(lParam);
        const int clampedX = std::clamp(x, 0, width);
        const int trackTop = kTimelineTrackTopOffset;
        const int trackBottom = trackTop + kTimelineTrackHeight;
        const int knobTop = trackBottom + 10; // aligns with knob ellipse band
        const int knobBottom = trackBottom + 26;
        const bool inKnobBand = (y >= knobTop && y <= knobBottom);

        const int startX = TimelineTimeToClientX(pData, pData->trimStart, width);
        const int posX = TimelineTimeToClientX(pData, pData->currentPosition, width);
        const int endX = TimelineTimeToClientX(pData, pData->trimEnd, width);

        pData->dragMode = VideoRecordingSession::TrimDialogData::None;
        pData->previewOverrideActive = false;
        pData->restorePreviewOnRelease = false;

        // If grabbing the knob band, prefer the playhead even if it overlaps a grip
        if (inKnobBand && abs(clampedX - posX) <= kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::Position;
        }
        else if (abs(clampedX - startX) <= kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::TrimStart;
        }
        else if (abs(clampedX - endX) <= kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::TrimEnd;
        }
        else if (abs(clampedX - posX) <= kTimelineHandleHitRadius)
        {
            pData->dragMode = VideoRecordingSession::TrimDialogData::Position;
        }

        if (pData->dragMode != VideoRecordingSession::TrimDialogData::None)
        {
            pData->isDragging = true;
            pData->playheadPushed = false;
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
                // Keep playhead inside selection when left grip passes it
                if (pData->currentPosition.count() < pData->trimStart.count())
                {
                    pData->playheadPushed = true;
                    pData->currentPosition = pData->trimStart;
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
                // Keep playhead inside selection when right grip passes it
                if (pData->currentPosition.count() > pData->trimEnd.count())
                {
                    pData->playheadPushed = true;
                    pData->currentPosition = pData->trimEnd;
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
    float iconSize = radius * 0.8f; // slightly larger icons

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
        OutputDebugStringW(L"[Trim] WM_INITDIALOG received\n");
        pData = reinterpret_cast<TrimDialogData*>(lParam);
        if (!pData)
        {
            OutputDebugStringW(L"[Trim] ERROR: pData is null in WM_INITDIALOG\n");
            EndDialog(hDlg, IDCANCEL);
            return FALSE;
        }
        SetWindowLongPtr(hDlg, DWLP_USER, lParam);
        pData->hDialog = hDlg;
        pData->hoverPlay = false;
        pData->hoverRewind = false;
        pData->hoverForward = false;
        pData->hoverSkipStart = false;
        pData->hoverSkipEnd = false;
        pData->isPlaying.store(false, std::memory_order_relaxed);
        pData->lastRenderedPreview.store(-1, std::memory_order_relaxed);

        AcquireHighResTimer();

        // Make OK the default button
        SendMessage(hDlg, DM_SETDEFID, IDOK, 0);

        HWND hTimeline = GetDlgItem(hDlg, IDC_TRIM_TIMELINE);
        if (hTimeline)
        {
            SetWindowSubclass(hTimeline, TimelineSubclassProc, 1, reinterpret_cast<DWORD_PTR>(pData));
        }
        HWND hPlayPause = GetDlgItem(hDlg, IDC_TRIM_PLAY_PAUSE);
        if (hPlayPause)
        {
            SetWindowSubclass(hPlayPause, PlaybackButtonSubclassProc, 2, reinterpret_cast<DWORD_PTR>(pData));
        }
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
        pData->originalTrimStart = pData->trimStart;
        pData->originalTrimEnd = pData->trimEnd;
        pData->currentPosition = winrt::TimeSpan{ 0 };

        // Initially disable OK button since no trim has been made yet
        EnableWindow(GetDlgItem(hDlg, IDOK), FALSE);

        OutputDebugStringW((L"[Trim] isGif=" + std::to_wstring(pData->isGif) + L" duration=" + std::to_wstring(pData->videoDuration.count()) + L"\n").c_str());

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

        OutputDebugStringW(L"[Trim] WM_INITDIALOG completed\n");
        return TRUE;
    }

    case WMU_PREVIEW_READY:
    {
        // Video preview loaded - refresh preview area
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
                KillTimer(hDlg, kPlaybackTimerId);
            InvalidateRect(GetDlgItem(hDlg, IDC_TRIM_PREVIEW), nullptr, FALSE);
        }
        return TRUE;
    }

    case WMU_PREVIEW_SCHEDULED:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            UpdateVideoPreview(hDlg, pData);
        }
        return TRUE;
    }

    case WMU_DURATION_CHANGED:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            if (pData->currentPosition.count() > pData->trimEnd.count())
            {
                pData->currentPosition = pData->trimEnd;
            }
            UpdateDurationDisplay(hDlg, pData);
            UpdatePositionUI(hDlg, pData);
        }
        return TRUE;
    }

    case WMU_PLAYBACK_POSITION:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (pData)
        {
            // Always move the playhead smoothly
            UpdatePositionUI(hDlg, pData);

            // Throttle expensive thumbnail generation while playing
            const int64_t currentTicks = pData->currentPosition.count();
            const int64_t lastTicks = pData->lastRenderedPreview.load(std::memory_order_relaxed);
            if (!pData->loadingPreview.load(std::memory_order_relaxed))
            {
                const int64_t delta = (lastTicks < 0) ? kPreviewMinDeltaTicks : std::llabs(currentTicks - lastTicks);
                if (delta >= kPreviewMinDeltaTicks)
                {
                    UpdateVideoPreview(hDlg, pData, false);
                }
            }
        }
        return TRUE;
    }

    case WMU_PLAYBACK_STOP:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        StopPlayback(hDlg, pData, false);
        UpdateVideoPreview(hDlg, pData);
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

            std::unique_lock<std::mutex> previewLock(pData->previewBitmapMutex);

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

            // Ensure MediaPlayer and event handlers are fully released
            CleanupMediaPlayer(pData);

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
            std::lock_guard<std::mutex> lock(pData->previewBitmapMutex);
            if (pData->previewBitmapOwned)
            {
                DeleteObject(pData->hPreviewBitmap);
            }
            pData->hPreviewBitmap = nullptr;
        }
        if (pData)
        {
            StopMMTimer(pData);  // Stop multimedia timer if running
            pData->playbackFile = nullptr;
            CleanupGifFrames(pData);
        }

        ReleaseHighResTimer();
        break;
    }

    // Multimedia timer tick - handles MP4 and GIF playback with high precision
    case WMU_MM_TIMER_TICK:
    {
        pData = reinterpret_cast<TrimDialogData*>(GetWindowLongPtr(hDlg, DWLP_USER));
        if (!pData)
        {
            return TRUE;
        }

        if (!pData->isPlaying.load(std::memory_order_relaxed))
        {
            StopMMTimer(pData);
            RefreshPlaybackButtons(hDlg);
            return TRUE;
        }

        // Handle GIF playback
        if (pData->isGif && !pData->gifFrames.empty())
        {
            const int64_t clampedTicks = std::clamp<int64_t>(
                pData->currentPosition.count(),
                pData->trimStart.count(),
                pData->trimEnd.count());
            const size_t frameIndex = FindGifFrameIndex(pData->gifFrames, clampedTicks);
            const auto& frame = pData->gifFrames[frameIndex];
            
            // Check if enough real time has passed to advance to the next frame
            auto now = std::chrono::steady_clock::now();
            auto elapsedMs = std::chrono::duration_cast<std::chrono::milliseconds>(now - pData->gifFrameStartTime).count();
            auto frameDurationMs = frame.duration.count() / 10'000; // Convert 100-ns ticks to ms
            
            // Update playhead position smoothly based on elapsed time within current frame
            const int64_t frameElapsedTicks = static_cast<int64_t>(elapsedMs) * 10'000;
            const int64_t smoothPosition = frame.start.count() + (std::min)(frameElapsedTicks, frame.duration.count());
            pData->currentPosition = winrt::TimeSpan{ std::clamp<int64_t>(smoothPosition, pData->trimStart.count(), pData->trimEnd.count()) };
            
            // Update playhead
            HWND hTimeline = GetDlgItem(hDlg, IDC_TRIM_TIMELINE);
            if (hTimeline)
            {
                RECT rc;
                GetClientRect(hTimeline, &rc);
                const int newX = TimelineTimeToClientX(pData, pData->currentPosition, rc.right - rc.left);
                if (newX != pData->lastPlayheadX)
                {
                    InvalidatePlayheadRegion(hTimeline, rc, pData->lastPlayheadX, newX);
                    pData->lastPlayheadX = newX;
                    UpdateWindow(hTimeline);
                }
            }
            SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, pData->currentPosition, true);
            
            if (elapsedMs >= frameDurationMs)
            {
                // Time to advance to next frame
                const int64_t nextTicks = frame.start.count() + frame.duration.count();

                if (nextTicks >= pData->trimEnd.count())
                {
                    // Loop back to where playback started
                    pData->currentPosition = pData->playbackStartPosition;
                    StopPlayback(hDlg, pData, false);
                    UpdateVideoPreview(hDlg, pData);
                }
                else
                {
                    pData->currentPosition = winrt::TimeSpan{ nextTicks };
                    pData->gifFrameStartTime = now; // Reset timer for new frame
                    UpdateVideoPreview(hDlg, pData);
                }
            }
            return TRUE;
        }

        // Handle MP4 playback
        if (pData->mediaPlayer)
        {
            try
            {
                auto session = pData->mediaPlayer.PlaybackSession();
                if (!session)
                {
                    StopPlayback(hDlg, pData, false);
                    UpdateVideoPreview(hDlg, pData);
                    return TRUE;
                }

                // Simply use MediaPlayer position directly
                auto position = session.Position();
                const int64_t mediaTicks = position.count();
                
                // Clamp to trim range
                const int64_t clampedTicks = std::clamp<int64_t>(
                    mediaTicks,
                    pData->trimStart.count(),
                    pData->trimEnd.count());
                pData->currentPosition = winrt::TimeSpan{ clampedTicks };

                // Invalidate only the old and new playhead regions for efficiency
                HWND hTimeline = GetDlgItem(hDlg, IDC_TRIM_TIMELINE);
                if (hTimeline)
                {
                    RECT rc;
                    GetClientRect(hTimeline, &rc);
                    const int newX = TimelineTimeToClientX(pData, pData->currentPosition, rc.right - rc.left);
                    // Only repaint if position actually changed
                    if (newX != pData->lastPlayheadX)
                    {
                        InvalidatePlayheadRegion(hTimeline, rc, pData->lastPlayheadX, newX);
                        pData->lastPlayheadX = newX;
                        UpdateWindow(hTimeline);
                    }
                }
                SetTimeText(hDlg, IDC_TRIM_POSITION_LABEL, pData->currentPosition, true);

                if (clampedTicks >= pData->trimEnd.count())
                {
                    // Loop back to where playback started
                    pData->currentPosition = pData->playbackStartPosition;
                    StopPlayback(hDlg, pData, false);
                    UpdateVideoPreview(hDlg, pData);
                }
            }
            catch (...)
            {
            }
        }
        return TRUE;
    }

    case WM_TIMER:
        // WM_TIMER is no longer used for playback; both MP4 and GIF use multimedia timer (WMU_MM_TIMER_TICK)
        // This handler is kept for any other timers that might be added in the future
        if (wParam == kPlaybackTimerId)
        {
            // Legacy timer - should not fire anymore, but clean up if it does
            KillTimer(hDlg, kPlaybackTimerId);
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

winrt::IAsyncOperation<winrt::hstring> VideoRecordingSession::TrimGifAsync(
    const std::wstring& sourceGifPath,
    winrt::TimeSpan trimTimeStart,
    winrt::TimeSpan trimTimeEnd)
{
    co_await winrt::resume_background();

    try
    {
        if (trimTimeEnd.count() <= trimTimeStart.count())
        {
            co_return winrt::hstring();
        }

        winrt::com_ptr<IWICImagingFactory> factory;
        winrt::check_hresult(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(factory.put())));

        auto sourceFile = co_await winrt::StorageFile::GetFileFromPathAsync(sourceGifPath);
        auto sourceStream = co_await sourceFile.OpenAsync(winrt::FileAccessMode::Read);

        winrt::com_ptr<IStream> sourceIStream;
        winrt::check_hresult(CreateStreamOverRandomAccessStream(winrt::get_unknown(sourceStream), IID_PPV_ARGS(sourceIStream.put())));

        winrt::com_ptr<IWICBitmapDecoder> decoder;
        winrt::check_hresult(factory->CreateDecoderFromStream(sourceIStream.get(), nullptr, WICDecodeMetadataCacheOnLoad, decoder.put()));

        UINT frameCount = 0;
        winrt::check_hresult(decoder->GetFrameCount(&frameCount));
        if (frameCount == 0)
        {
            co_return winrt::hstring();
        }

        // Prepare output file
        auto tempFolder = co_await winrt::StorageFolder::GetFolderFromPathAsync(std::filesystem::temp_directory_path().wstring());
        auto zoomitFolder = co_await tempFolder.CreateFolderAsync(L"ZoomIt", winrt::CreationCollisionOption::OpenIfExists);
        std::wstring filename = L"zoomit_trimmed_" + std::to_wstring(GetTickCount64()) + L".gif";
        auto outputFile = co_await zoomitFolder.CreateFileAsync(filename, winrt::CreationCollisionOption::ReplaceExisting);
        auto outputStream = co_await outputFile.OpenAsync(winrt::FileAccessMode::ReadWrite);

        winrt::com_ptr<IStream> outputIStream;
        winrt::check_hresult(CreateStreamOverRandomAccessStream(winrt::get_unknown(outputStream), IID_PPV_ARGS(outputIStream.put())));

        winrt::com_ptr<IWICBitmapEncoder> encoder;
        winrt::check_hresult(factory->CreateEncoder(GUID_ContainerFormatGif, nullptr, encoder.put()));
        winrt::check_hresult(encoder->Initialize(outputIStream.get(), WICBitmapEncoderNoCache));

        // Try to set looping metadata
        try
        {
            winrt::com_ptr<IWICMetadataQueryWriter> encoderMetadataWriter;
            if (SUCCEEDED(encoder->GetMetadataQueryWriter(encoderMetadataWriter.put())) && encoderMetadataWriter)
            {
                PROPVARIANT prop{};
                PropVariantInit(&prop);
                prop.vt = VT_UI1 | VT_VECTOR;
                prop.caub.cElems = 11;
                prop.caub.pElems = static_cast<UCHAR*>(CoTaskMemAlloc(11));
                if (prop.caub.pElems)
                {
                    memcpy(prop.caub.pElems, "NETSCAPE2.0", 11);
                    encoderMetadataWriter->SetMetadataByName(L"/appext/application", &prop);
                }
                PropVariantClear(&prop);

                PropVariantInit(&prop);
                prop.vt = VT_UI1 | VT_VECTOR;
                prop.caub.cElems = 5;
                prop.caub.pElems = static_cast<UCHAR*>(CoTaskMemAlloc(5));
                if (prop.caub.pElems)
                {
                    prop.caub.pElems[0] = 3;
                    prop.caub.pElems[1] = 1;
                    prop.caub.pElems[2] = 0;
                    prop.caub.pElems[3] = 0;
                    prop.caub.pElems[4] = 0;
                    encoderMetadataWriter->SetMetadataByName(L"/appext/data", &prop);
                }
                PropVariantClear(&prop);
            }
        }
        catch (...)
        {
            // Loop metadata is optional; continue without failing
        }

        int64_t cumulativeTicks = 0;
        bool wroteFrame = false;

        for (UINT i = 0; i < frameCount; ++i)
        {
            winrt::com_ptr<IWICBitmapFrameDecode> frame;
            if (FAILED(decoder->GetFrame(i, frame.put())))
            {
                continue;
            }

            UINT delayCs = kGifDefaultDelayCs;
            try
            {
                winrt::com_ptr<IWICMetadataQueryReader> metadata;
                if (SUCCEEDED(frame->GetMetadataQueryReader(metadata.put())) && metadata)
                {
                    PROPVARIANT prop{};
                    PropVariantInit(&prop);
                    if (SUCCEEDED(metadata->GetMetadataByName(L"/grctlext/Delay", &prop)))
                    {
                        if (prop.vt == VT_UI2)
                        {
                            delayCs = prop.uiVal;
                        }
                        else if (prop.vt == VT_UI1)
                        {
                            delayCs = prop.bVal;
                        }
                    }
                    PropVariantClear(&prop);
                }
            }
            catch (...)
            {
            }

            if (delayCs == 0)
            {
                delayCs = kGifDefaultDelayCs;
            }

            const int64_t frameStart = cumulativeTicks;
            const int64_t frameEnd = frameStart + static_cast<int64_t>(delayCs) * 100'000;
            cumulativeTicks = frameEnd;

            if (frameEnd <= trimTimeStart.count() || frameStart >= trimTimeEnd.count())
            {
                continue;
            }

            const int64_t visibleStart = (std::max)(frameStart, trimTimeStart.count());
            const int64_t visibleEnd = (std::min)(frameEnd, trimTimeEnd.count());
            const int64_t visibleTicks = visibleEnd - visibleStart;
            if (visibleTicks <= 0)
            {
                continue;
            }

            UINT width = 0;
            UINT height = 0;
            frame->GetSize(&width, &height);

            winrt::com_ptr<IWICBitmapFrameEncode> frameEncode;
            winrt::com_ptr<IPropertyBag2> propertyBag;
            winrt::check_hresult(encoder->CreateNewFrame(frameEncode.put(), propertyBag.put()));
            winrt::check_hresult(frameEncode->Initialize(propertyBag.get()));
            winrt::check_hresult(frameEncode->SetSize(width, height));

            WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat8bppIndexed;
            winrt::check_hresult(frameEncode->SetPixelFormat(&pixelFormat));

            winrt::com_ptr<IWICFormatConverter> converter;
            winrt::check_hresult(factory->CreateFormatConverter(converter.put()));
            winrt::check_hresult(converter->Initialize(frame.get(), GUID_WICPixelFormat32bppBGRA, WICBitmapDitherTypeNone, nullptr, 0.0, WICBitmapPaletteTypeCustom));

            winrt::check_hresult(frameEncode->WriteSource(converter.get(), nullptr));

            try
            {
                winrt::com_ptr<IWICMetadataQueryWriter> frameMetadataWriter;
                if (SUCCEEDED(frameEncode->GetMetadataQueryWriter(frameMetadataWriter.put())) && frameMetadataWriter)
                {
                    PROPVARIANT prop{};
                    PropVariantInit(&prop);
                    prop.vt = VT_UI2;
                    // Convert ticks (100ns) to centiseconds with rounding and minimum 1
                    const int64_t roundedCs = (visibleTicks + 50'000) / 100'000;
                    prop.uiVal = static_cast<USHORT>((std::max<int64_t>)(1, roundedCs));
                    frameMetadataWriter->SetMetadataByName(L"/grctlext/Delay", &prop);
                    PropVariantClear(&prop);

                    PropVariantInit(&prop);
                    prop.vt = VT_UI1;
                    prop.bVal = 2; // restore to background
                    frameMetadataWriter->SetMetadataByName(L"/grctlext/Disposal", &prop);
                    PropVariantClear(&prop);
                }
            }
            catch (...)
            {
            }

            winrt::check_hresult(frameEncode->Commit());
            wroteFrame = true;
        }

        winrt::check_hresult(encoder->Commit());

        if (!wroteFrame)
        {
            co_return winrt::hstring();
        }

        co_return winrt::hstring(outputFile.Path());
    }
    catch (...)
    {
        co_return winrt::hstring();
    }
}
