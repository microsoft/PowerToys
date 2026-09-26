#pragma once

struct DisplayInfo
{
    HMONITOR monitor = nullptr;
    MONITORINFOEXW info{ sizeof(MONITORINFOEXW) };
    size_t number = 0;
};

struct DEPiPSettings
{
    int inactiveTransparency = 14;
    bool lockAspectRatio = false;
    bool alwaysOnTop = false;
};

std::vector<DisplayInfo> EnumerateDisplays();
std::wstring GetDisplayLabel(DisplayInfo const& display);
DEPiPSettings LoadDEPiPSettings();
winrt::Windows::Graphics::Capture::GraphicsCaptureItem CreateCaptureItem(HMONITOR monitor);
winrt::Microsoft::UI::Xaml::Media::Imaging::WriteableBitmap CaptureDisplayPreview(DisplayInfo const& display);

template<typename T>
winrt::com_ptr<T> GetDXGIInterface(winrt::Windows::Foundation::IInspectable const& object)
{
    auto access = object.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<T> result;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<T>(), result.put_void()));
    return result;
}
