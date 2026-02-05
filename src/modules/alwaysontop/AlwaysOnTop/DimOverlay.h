#pragma once

#include <windows.h>
#include <vector>

#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>

struct DimOverlayHole
{
    RECT rect{};
    int radius = 0;
};

class DimOverlay
{
public:
    DimOverlay() = default;
    ~DimOverlay();

    bool Initialize(HINSTANCE hinstance);
    void Terminate();
    void Update(std::vector<DimOverlayHole> holes, bool visible);

    HWND Hwnd() const noexcept;

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    LRESULT MessageHandler(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

    bool CreateWindowAndVisuals();
    bool EnsureDispatcherQueue();
    bool EnsureCompositor();

    void UpdateWindowBounds();
    void UpdateRegion(const std::vector<DimOverlayHole>& holes);
    void SetVisible(bool visible);

    HINSTANCE m_hinstance{};
    HWND m_hwndOwner{};
    HWND m_hwnd{};
    RECT m_virtualBounds{};
    bool m_visible = false;
    bool m_destroyed = false;

    winrt::Windows::System::DispatcherQueueController m_dispatcherQueueController{ nullptr };
    winrt::Windows::UI::Composition::Compositor m_compositor{ nullptr };
    winrt::Windows::UI::Composition::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::Windows::UI::Composition::ContainerVisual m_root{ nullptr };
    winrt::Windows::UI::Composition::SpriteVisual m_dim{ nullptr };
    winrt::Windows::UI::Composition::CompositionColorBrush m_dimBrush{ nullptr };
    winrt::Windows::UI::Composition::ScalarKeyFrameAnimation m_opacityAnimation{ nullptr };

    std::vector<DimOverlayHole> m_lastHoles;
};
