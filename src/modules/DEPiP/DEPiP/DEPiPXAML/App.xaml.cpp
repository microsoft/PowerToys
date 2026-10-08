#include "pch.h"

#include "App.xaml.h"
#include "../DisplayCapture.h"
#include "DisplaySelectorWindow.xaml.h"
#include "MirrorWindow.xaml.h"

namespace winrt::DEPiP::implementation
{
    App::App()
    {
        InitializeComponent();
    }

    void App::OnLaunched(Microsoft::UI::Xaml::LaunchActivatedEventArgs const&)
    {
        try
        {
            auto displays = EnumerateDisplays();
            if (displays.empty())
            {
                Exit();
                return;
            }

            auto selector = winrt::make<DisplaySelectorWindow>();
            winrt::get_self<DisplaySelectorWindow>(selector)->Initialize(
                std::move(displays),
                [this](DisplayInfo const& source) {
                    auto mirror = winrt::make<MirrorWindow>();
                    winrt::get_self<MirrorWindow>(mirror)->Initialize(source);
                    m_window = mirror;
                    mirror.Activate();
                });
            m_window = selector;
            selector.Activate();
        }
        catch (winrt::hresult_error const& error)
        {
            MessageBoxW(nullptr, error.message().c_str(), L"DEPiP", MB_OK | MB_ICONERROR);
            Exit();
        }
    }
}
