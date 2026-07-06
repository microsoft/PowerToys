// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// KeystrokeRenderer: draws the captured keystrokes as horizontally stacked
// "key pill" visuals on the existing Mouse/Input Highlighter Composition overlay.
//
// Text is rasterized with Direct2D + DirectWrite onto CompositionDrawingSurfaces,
// which are hosted by SpriteVisuals under a dedicated container in the overlay's
// visual tree. All public methods must be called on the composition (overlay UI)
// thread.
#pragma once

#include <windows.h>

#include <deque>
#include <string>

#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Numerics.h>

#include <d2d1_1.h>
#include <dwrite.h>
#include <d3d11.h>

#include "KeystrokeTypes.h"

namespace InputHighlighter
{
    // Anchor position of the keystroke stack, matching the settings enum (0-5).
    enum class KeystrokePosition
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        BottomLeft = 3,
        BottomCenter = 4,
        BottomRight = 5,
    };

    struct KeystrokeRendererSettings
    {
        DisplayMode displayMode = DisplayMode::Last5;
        KeystrokePosition position = KeystrokePosition::BottomCenter;
        int timeoutMs = 2000;
        float textSize = 24.0f; // DIP font size
        winrt::Windows::UI::Color textColor{ 255, 255, 255, 255 };
        winrt::Windows::UI::Color backgroundColor{ 200, 32, 32, 32 };
        winrt::Windows::UI::Color strokeColor{ 0, 255, 255, 255 };
        int strokeThickness = 0; // DIP; 0 = no border
    };

    class KeystrokeRenderer
    {
    public:
        KeystrokeRenderer() = default;
        ~KeystrokeRenderer();

        KeystrokeRenderer(const KeystrokeRenderer&) = delete;
        KeystrokeRenderer& operator=(const KeystrokeRenderer&) = delete;

        // Creates the D2D/DWrite devices and the Composition graphics device, and
        // adds a container visual to parentRoot. hwnd is used for DPI queries and
        // to size/anchor the stack. Returns false on failure (rendering disabled).
        bool Initialize(const winrt::Windows::UI::Composition::Compositor& compositor,
                        const winrt::Windows::UI::Composition::ContainerVisual& parentRoot,
                        HWND hwnd);

        void Uninitialize();

        bool IsInitialized() const noexcept { return m_initialized; }

        void ApplySettings(const KeystrokeRendererSettings& settings);

        // The client-space rectangle the stack is anchored within (typically the
        // work area of the active monitor, translated to overlay client coords).
        void SetAnchorRect(const D2D1_RECT_F& clientRect);

        // Applies a processor result (Add / ReplaceLast / RemoveLast / None).
        void OnResult(const KeystrokeResult& result);

        // Called periodically to fade/remove expired pills. Returns true if the
        // set of visible pills changed (so the caller may stop the timer when empty).
        bool Tick();

        bool HasVisiblePills() const noexcept { return !m_pills.empty(); }

        // Removes all pills immediately (e.g. when drawing stops).
        void Clear();

    private:
        struct Pill
        {
            winrt::Windows::UI::Composition::SpriteVisual visual{ nullptr };
            winrt::Windows::UI::Composition::CompositionSurfaceBrush brush{ nullptr };
            winrt::Windows::UI::Composition::CompositionDrawingSurface surface{ nullptr };
            std::wstring text;
            ULONGLONG expireAt = 0; // GetTickCount64 deadline; 0 = never
            float width = 0.0f; // DIP
            float height = 0.0f; // DIP
        };

        bool CreateDevices();
        float DpiScale() const;
        void DrawPill(Pill& pill, const std::wstring& text);
        void Relayout();
        void EnforceCap();
        size_t MaxPills() const;
        void AnimateEntrance(const Pill& pill, float targetOpacity);

        bool m_initialized = false;
        HWND m_hwnd = nullptr;

        KeystrokeRendererSettings m_settings;
        D2D1_RECT_F m_anchorRect{ 0, 0, 0, 0 };

        winrt::Windows::UI::Composition::Compositor m_compositor{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual m_container{ nullptr };
        winrt::Windows::UI::Composition::CompositionGraphicsDevice m_graphicsDevice{ nullptr };

        winrt::com_ptr<ID3D11Device> m_d3dDevice;
        winrt::com_ptr<ID2D1Device> m_d2dDevice;
        winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
        winrt::com_ptr<IDWriteFactory> m_dwriteFactory;

        std::deque<Pill> m_pills;
    };
}
