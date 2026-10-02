#include "pch.h"

#include "DisplayCapture.h"
#include "resource.h"

#include <common/SettingsAPI/settings_helpers.h>

namespace
{
    constexpr int PreviewWidth = 320;
    constexpr int PreviewHeight = 180;

    BOOL CALLBACK AddMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM context)
    {
        auto displays = reinterpret_cast<std::vector<DisplayInfo>*>(context);
        MONITORINFOEXW info{ sizeof(MONITORINFOEXW) };
        if (GetMonitorInfoW(monitor, &info))
        {
            displays->push_back({ monitor, info, displays->size() + 1 });
        }
        return TRUE;
    }
}

std::vector<DisplayInfo> EnumerateDisplays()
{
    std::vector<DisplayInfo> displays;
    EnumDisplayMonitors(nullptr, nullptr, AddMonitor, reinterpret_cast<LPARAM>(&displays));
    return displays;
}

std::wstring GetDisplayLabel(DisplayInfo const& display)
{
    wchar_t const* value = nullptr;
    int length = LoadStringW(
        GetModuleHandleW(nullptr),
        IDS_DISPLAY_LABEL,
        reinterpret_cast<wchar_t*>(&value),
        0);
    winrt::check_bool(length > 0);
    return std::wstring(value, static_cast<size_t>(length)) + L" " + std::to_wstring(display.number);
}

DEPiPSettings LoadDEPiPSettings()
{
    DEPiPSettings settings;
    try
    {
        auto properties = PTSettingsHelper::load_module_settings(L"DEPiP").GetNamedObject(L"properties");
        settings.inactiveTransparency = std::clamp(
            static_cast<int>(properties.GetNamedObject(L"inactiveTransparency").GetNamedNumber(L"value")),
            0,
            90);
        settings.lockAspectRatio =
            properties.GetNamedObject(L"lockAspectRatio").GetNamedBoolean(L"value");
        settings.alwaysOnTop =
            properties.GetNamedObject(L"alwaysOnTop").GetNamedBoolean(L"value");
    }
    catch (...)
    {
    }
    return settings;
}

winrt::Windows::Graphics::Capture::GraphicsCaptureItem CreateCaptureItem(HMONITOR monitor)
{
    using namespace winrt::Windows::Graphics::Capture;
    auto interop = winrt::get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    GraphicsCaptureItem item{ nullptr };
    winrt::check_hresult(interop->CreateForMonitor(
        monitor,
        winrt::guid_of<GraphicsCaptureItem>(),
        winrt::put_abi(item)));
    return item;
}

winrt::Microsoft::UI::Xaml::Media::Imaging::WriteableBitmap CaptureDisplayPreview(
    DisplayInfo const& display)
{
    HDC screen = GetDC(nullptr);
    winrt::check_pointer(screen);
    HDC memory = CreateCompatibleDC(screen);
    winrt::check_pointer(memory);

    BITMAPINFO bitmapInfo{};
    bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmapInfo.bmiHeader.biWidth = PreviewWidth;
    bitmapInfo.bmiHeader.biHeight = -PreviewHeight;
    bitmapInfo.bmiHeader.biPlanes = 1;
    bitmapInfo.bmiHeader.biBitCount = 32;
    bitmapInfo.bmiHeader.biCompression = BI_RGB;
    void* pixels = nullptr;
    HBITMAP bitmap = CreateDIBSection(screen, &bitmapInfo, DIB_RGB_COLORS, &pixels, nullptr, 0);
    winrt::check_pointer(bitmap);
    HGDIOBJ previous = SelectObject(memory, bitmap);

    RECT bounds = display.info.rcMonitor;
    int sourceWidth = bounds.right - bounds.left;
    int sourceHeight = bounds.bottom - bounds.top;
    int targetWidth = PreviewWidth;
    int targetHeight = MulDiv(sourceHeight, targetWidth, sourceWidth);
    if (targetHeight > PreviewHeight)
    {
        targetHeight = PreviewHeight;
        targetWidth = MulDiv(sourceWidth, targetHeight, sourceHeight);
    }
    PatBlt(memory, 0, 0, PreviewWidth, PreviewHeight, BLACKNESS);
    SetStretchBltMode(memory, HALFTONE);
    StretchBlt(
        memory,
        (PreviewWidth - targetWidth) / 2,
        (PreviewHeight - targetHeight) / 2,
        targetWidth,
        targetHeight,
        screen,
        bounds.left,
        bounds.top,
        sourceWidth,
        sourceHeight,
        SRCCOPY | CAPTUREBLT);

    auto sourcePixels = static_cast<byte*>(pixels);
    for (size_t offset = 3; offset < PreviewWidth * PreviewHeight * 4; offset += 4)
    {
        sourcePixels[offset] = 0xFF;
    }

    winrt::Microsoft::UI::Xaml::Media::Imaging::WriteableBitmap source(
        PreviewWidth,
        PreviewHeight);
    auto buffer = source.PixelBuffer();
    auto access = buffer.as<::Windows::Storage::Streams::IBufferByteAccess>();
    byte* destination = nullptr;
    winrt::check_hresult(access->Buffer(&destination));
    memcpy(destination, pixels, PreviewWidth * PreviewHeight * 4);

    SelectObject(memory, previous);
    DeleteObject(bitmap);
    DeleteDC(memory);
    ReleaseDC(nullptr, screen);

    source.Invalidate();
    return source;
}
