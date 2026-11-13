#pragma once

#include <windows.h>

#include <memory>

#include "BorderGeometry.h"

namespace PowerToysWindowBorder
{
    struct Style
    {
        Placement placement = Placement::Outside;
        bool halo = true;
        bool highlight = true;
    };

    // A border around another app's window, drawn with Windows.UI.Composition shapes in a click-through,
    // never-activated layered window. Nothing is rasterized: resizing only updates shape geometry, and
    // corners stay concentric with the tracked window's own corners at any DPI.
    //
    // Must be used from a thread that pumps window messages. Create/Attach return nullptr when
    // Composition is unavailable so callers can keep a fallback renderer.
    class CompositionBorder
    {
    public:
        static constexpr DWORD WindowExStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP;
        static constexpr DWORD WindowStyle = WS_POPUP | WS_DISABLED;

        // Ensures this thread has a dispatcher queue and compositor. Returns false when unavailable.
        static bool EnsureCompositor() noexcept;

        // Attaches to a host window the caller created with WindowExStyle and WindowStyle. The caller's
        // window procedure should first offer messages to HandleMessage.
        static std::unique_ptr<CompositionBorder> Attach(HWND host, const Style& style) noexcept;

        // Creates and owns a host window of the given class (registered on first use).
        static std::unique_ptr<CompositionBorder> Create(HINSTANCE instance, const wchar_t* className, const Style& style) noexcept;

        // Keeps the host click-through and never activated. Returns true when the message was handled.
        static bool HandleMessage(UINT message, LRESULT& result) noexcept;

        ~CompositionBorder();
        CompositionBorder(const CompositionBorder&) = delete;
        CompositionBorder& operator=(const CompositionBorder&) = delete;
        CompositionBorder(CompositionBorder&&) = delete;
        CompositionBorder& operator=(CompositionBorder&&) = delete;

        HWND Window() const noexcept;

        // Moves the host around `frame` (the tracked window's visible frame, screen px) and updates the
        // band. `insertAfter` is the z-order anchor (nullptr keeps the current z-order). `cornerRadius`,
        // `thickness` are physical px; `opacity` is 0..1.
        void Update(HWND insertAfter, const RECT& frame, float scale, float cornerRadius, float thickness, COLORREF color, float opacity) noexcept;

        // Shows the host without activating it, fading in briefly when Windows animations are on.
        void Show() noexcept;
        void Hide() noexcept;

    private:
        struct Impl;
        explicit CompositionBorder(std::unique_ptr<Impl> impl) noexcept;
        std::unique_ptr<Impl> m_impl;
    };
}
