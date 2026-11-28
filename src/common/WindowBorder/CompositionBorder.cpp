#include "CompositionBorder.h"

#include <DispatcherQueue.h>
#include <dwmapi.h>
#include <windows.ui.composition.interop.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>

#include <algorithm>
#include <chrono>

#pragma comment(lib, "CoreMessaging.lib")
#pragma comment(lib, "dwmapi.lib")

namespace wuc = winrt::Windows::UI::Composition;

namespace
{
    constexpr std::chrono::milliseconds FadeInDuration{ 150 };

    bool EnsureDispatcherQueue()
    {
        if (winrt::Windows::System::DispatcherQueue::GetForCurrentThread())
        {
            return true;
        }

        const DispatcherQueueOptions options{ sizeof(DispatcherQueueOptions), DQTYPE_THREAD_CURRENT, DQTAT_COM_NONE };
        winrt::Windows::System::DispatcherQueueController controller{ nullptr };
        if (FAILED(CreateDispatcherQueueController(options, reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(winrt::put_abi(controller)))))
        {
            return false;
        }

        // Intentionally leaked: the queue must outlive every compositor created on this thread, and
        // shutting it down during process teardown is not needed.
        winrt::detach_abi(controller);
        return true;
    }

    // One compositor per UI thread, intentionally leaked for the same reason as the dispatcher queue.
    // (Projected WinRT types can't be heap-allocated directly, hence the holder.)
    struct CompositorHolder
    {
        wuc::Compositor compositor{ nullptr };
    };

    wuc::Compositor* ThreadCompositor() noexcept
    {
        static thread_local CompositorHolder* holder = nullptr;
        static thread_local bool failed = false;
        if (holder || failed)
        {
            return holder ? &holder->compositor : nullptr;
        }

        try
        {
            if (EnsureDispatcherQueue())
            {
                auto created = std::make_unique<CompositorHolder>();
                created->compositor = wuc::Compositor();
                holder = created.release();
            }
        }
        catch (...)
        {
            holder = nullptr;
        }

        failed = holder == nullptr;
        return holder ? &holder->compositor : nullptr;
    }

    bool AnimationsEnabled() noexcept
    {
        BOOL enabled = TRUE;
        SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0);
        return enabled != FALSE;
    }

    LRESULT CALLBACK OwnedWindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        LRESULT result = 0;
        if (PowerToysWindowBorder::CompositionBorder::HandleMessage(message, result))
        {
            return result;
        }

        return DefWindowProcW(window, message, wparam, lparam);
    }

    constexpr uint8_t ToByte(float value) noexcept
    {
        const float clamped = value < 0.f ? 0.f : (value > 1.f ? 1.f : value);
        return static_cast<uint8_t>((clamped * 255.f) + 0.5f);
    }
}

namespace PowerToysWindowBorder
{
    struct CompositionBorder::Impl
    {
        // One stroked rounded rectangle.
        struct Item
        {
            wuc::CompositionRoundedRectangleGeometry geometry{ nullptr };
            wuc::CompositionColorBrush brush{ nullptr };
            wuc::CompositionSpriteShape shape{ nullptr };

            void Create(const wuc::Compositor& compositor, const wuc::ShapeVisual& visual)
            {
                geometry = compositor.CreateRoundedRectangleGeometry();
                brush = compositor.CreateColorBrush();
                shape = compositor.CreateSpriteShape(geometry);
                shape.StrokeBrush(brush);
                shape.StrokeThickness(0.f);
                visual.Shapes().Append(shape);
            }

            void Apply(const Stroke& stroke, uint8_t r, uint8_t g, uint8_t b) const
            {
                if (!stroke.IsVisible())
                {
                    shape.StrokeThickness(0.f);
                    return;
                }

                geometry.Offset({ stroke.rect.left, stroke.rect.top });
                geometry.Size({ stroke.rect.width, stroke.rect.height });
                geometry.CornerRadius({ stroke.rect.radius, stroke.rect.radius });
                brush.Color(winrt::Windows::UI::Color{ ToByte(stroke.alpha), r, g, b });
                shape.StrokeThickness(stroke.thickness);
            }
        };

        // Inputs of the last applied layout; geometry is only touched when one of these changes.
        struct Key
        {
            int frameWidth = -1;
            int frameHeight = -1;
            float scale = 0.f;
            float cornerRadius = -1.f;
            float thickness = -1.f;
            COLORREF color = CLR_INVALID;

            bool operator==(const Key&) const = default;
        };

        HWND window = nullptr;
        bool ownsWindow = false;
        Style style;
        wuc::Desktop::DesktopWindowTarget target{ nullptr };
        wuc::ContainerVisual root{ nullptr };
        wuc::ShapeVisual shapes{ nullptr };
        std::array<Item, HaloSteps> halo;
        Item band;
        Item highlight;
        Key key;
        float opacity = -1.f;
        bool visible = false;

        ~Impl()
        {
            shapes = nullptr;
            root = nullptr;
            target = nullptr;
            if (ownsWindow && window)
            {
                DestroyWindow(window);
            }
        }

        void Build(const wuc::Compositor& compositor)
        {
            SetLayeredWindowAttributes(window, 0, 255, LWA_ALPHA);

            BOOL excludedFromPeek = TRUE;
            DwmSetWindowAttribute(window, DWMWA_EXCLUDED_FROM_PEEK, &excludedFromPeek, sizeof(excludedFromPeek));

            auto interop = compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
            winrt::check_hresult(interop->CreateDesktopWindowTarget(
                window,
                TRUE,
                reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(winrt::put_abi(target))));

            root = compositor.CreateContainerVisual();
            shapes = compositor.CreateShapeVisual();
            root.Children().InsertAtTop(shapes);
            target.Root(root);

            // Back to front: halo, band, edge highlight.
            for (auto& item : halo)
            {
                item.Create(compositor, shapes);
            }
            band.Create(compositor, shapes);
            highlight.Create(compositor, shapes);
        }
    };

    bool CompositionBorder::EnsureCompositor() noexcept
    {
        return ThreadCompositor() != nullptr;
    }

    bool CompositionBorder::HandleMessage(UINT message, LRESULT& result) noexcept
    {
        switch (message)
        {
        case WM_NCHITTEST:
            result = HTTRANSPARENT;
            return true;
        case WM_MOUSEACTIVATE:
            result = MA_NOACTIVATE;
            return true;
        default:
            return false;
        }
    }

    std::unique_ptr<CompositionBorder> CompositionBorder::Attach(HWND host, const Style& style) noexcept
    {
        const auto compositor = ThreadCompositor();
        if (!host || !compositor)
        {
            return nullptr;
        }

        try
        {
            auto impl = std::make_unique<Impl>();
            impl->window = host;
            impl->style = style;
            impl->Build(*compositor);
            return std::unique_ptr<CompositionBorder>(new CompositionBorder(std::move(impl)));
        }
        catch (...)
        {
            return nullptr;
        }
    }

    std::unique_ptr<CompositionBorder> CompositionBorder::Create(HINSTANCE instance, const wchar_t* className, const Style& style) noexcept
    {
        if (!className || !EnsureCompositor())
        {
            return nullptr;
        }

        WNDCLASSEXW wcex{};
        wcex.cbSize = sizeof(wcex);
        wcex.lpfnWndProc = OwnedWindowProc;
        wcex.hInstance = instance;
        wcex.lpszClassName = className;
        wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        RegisterClassExW(&wcex);

        HWND window = CreateWindowExW(WindowExStyle, className, L"", WindowStyle, 0, 0, 1, 1, nullptr, nullptr, instance, nullptr);
        if (!window)
        {
            return nullptr;
        }

        auto border = Attach(window, style);
        if (!border)
        {
            DestroyWindow(window);
            return nullptr;
        }

        border->m_impl->ownsWindow = true;
        return border;
    }

    CompositionBorder::CompositionBorder(std::unique_ptr<Impl> impl) noexcept :
        m_impl(std::move(impl))
    {
    }

    CompositionBorder::~CompositionBorder() = default;

    HWND CompositionBorder::Window() const noexcept
    {
        return m_impl->window;
    }

    void CompositionBorder::Update(HWND insertAfter, const RECT& frame, float scale, float cornerRadius, float thickness, COLORREF color, float opacity) noexcept
    {
        auto& impl = *m_impl;
        const int frameWidth = frame.right - frame.left;
        const int frameHeight = frame.bottom - frame.top;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        const Layout layout = ComputeLayout(LayoutInput{
            .frameWidth = static_cast<float>(frameWidth),
            .frameHeight = static_cast<float>(frameHeight),
            .cornerRadius = cornerRadius,
            .thickness = thickness,
            .scale = scale,
            .placement = impl.style.placement,
            .halo = impl.style.halo,
            .highlight = impl.style.highlight,
        });

        UINT flags = SWP_NOACTIVATE;
        if (!insertAfter)
        {
            flags |= SWP_NOZORDER;
        }
        SetWindowPos(impl.window, insertAfter, frame.left - layout.inflate, frame.top - layout.inflate, layout.hostWidth, layout.hostHeight, flags);

        try
        {
            const Impl::Key key{ frameWidth, frameHeight, scale, cornerRadius, thickness, color };
            if (!(key == impl.key))
            {
                impl.key = key;
                impl.shapes.Size({ static_cast<float>(layout.hostWidth), static_cast<float>(layout.hostHeight) });

                const uint8_t r = GetRValue(color);
                const uint8_t g = GetGValue(color);
                const uint8_t b = GetBValue(color);
                for (size_t i = 0; i < impl.halo.size(); ++i)
                {
                    impl.halo.at(i).Apply(layout.halo.at(i), r, g, b);
                }
                impl.band.Apply(layout.band, r, g, b);
                impl.highlight.Apply(layout.highlight, Lighten(r, HighlightLighten), Lighten(g, HighlightLighten), Lighten(b, HighlightLighten));
            }

            const float clampedOpacity = std::clamp(opacity, 0.f, 1.f);
            if (clampedOpacity != impl.opacity)
            {
                impl.opacity = clampedOpacity;
                impl.root.Opacity(clampedOpacity);
            }
        }
        catch (...)
        {
            // A lost compositor leaves the border stale until the next update; never take down the host.
        }
    }

    void CompositionBorder::Show() noexcept
    {
        auto& impl = *m_impl;
        if (impl.visible)
        {
            return;
        }

        impl.visible = true;
        try
        {
            impl.shapes.StopAnimation(L"Opacity");
            impl.shapes.Opacity(1.f);
            if (AnimationsEnabled())
            {
                const auto compositor = impl.shapes.Compositor();
                auto fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0.f, 0.f);
                fade.InsertKeyFrame(1.f, 1.f);
                fade.Duration(FadeInDuration);
                impl.shapes.StartAnimation(L"Opacity", fade);
            }
        }
        catch (...)
        {
        }

        ShowWindow(impl.window, SW_SHOWNA);
    }

    void CompositionBorder::Hide() noexcept
    {
        auto& impl = *m_impl;
        impl.visible = false;
        ShowWindow(impl.window, SW_HIDE);
        try
        {
            impl.shapes.StopAnimation(L"Opacity");
            impl.shapes.Opacity(1.f);
        }
        catch (...)
        {
        }
    }
}
