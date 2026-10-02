#include "pch.h"

#include "DisplaySelectorWindow.xaml.h"
#include "../resource.h"

namespace winrt::DEPiP::implementation
{
    namespace
    {
        std::wstring LoadStringResource(UINT id)
        {
            wchar_t const* value = nullptr;
            int length = LoadStringW(
                GetModuleHandleW(nullptr),
                id,
                reinterpret_cast<wchar_t*>(&value),
                0);
            winrt::check_bool(length > 0);
            return { value, static_cast<size_t>(length) };
        }
    }

    DisplaySelectorWindow::DisplaySelectorWindow()
    {
        InitializeComponent();
        Title(LoadStringResource(IDS_DISPLAY_SELECTOR_TITLE));
        InstructionText().Text(LoadStringResource(IDS_DISPLAY_SELECTOR_INSTRUCTION));
        DescriptionText().Text(LoadStringResource(IDS_DISPLAY_SELECTOR_DESCRIPTION));
        CancelButton().Content(winrt::box_value(LoadStringResource(IDS_CANCEL)));
    }

    void DisplaySelectorWindow::Initialize(
        std::vector<DisplayInfo> displays,
        std::function<void(DisplayInfo const&)> selected)
    {
        using namespace winrt::Microsoft::UI::Xaml;
        using namespace winrt::Microsoft::UI::Xaml::Controls;

        m_displays = std::move(displays);
        m_selected = std::move(selected);
        auto primaryLabel = LoadStringResource(IDS_PRIMARY_DISPLAY);

        auto primary = std::find_if(m_displays.begin(), m_displays.end(), [](auto const& display) {
            return (display.info.dwFlags & MONITORINFOF_PRIMARY) != 0;
        });
        RECT workArea =
            primary != m_displays.end() ? primary->info.rcWork : m_displays.front().info.rcWork;
        auto native = this->try_as<::IWindowNative>();
        HWND window = nullptr;
        winrt::check_hresult(native->get_WindowHandle(&window));
        AppWindow().SetIcon(L"Assets\\DEPiP.ico");
        UINT dpi = GetDpiForWindow(window);
        RECT windowRect{
            0,
            0,
            MulDiv(760, dpi, USER_DEFAULT_SCREEN_DPI),
            MulDiv(440, dpi, USER_DEFAULT_SCREEN_DPI),
        };
        winrt::check_bool(AdjustWindowRectExForDpi(
            &windowRect,
            WS_OVERLAPPEDWINDOW,
            FALSE,
            0,
            dpi));
        int windowWidth = windowRect.right - windowRect.left;
        int windowHeight = windowRect.bottom - windowRect.top;
        AppWindow().MoveAndResize({
            workArea.left + (workArea.right - workArea.left - windowWidth) / 2,
            workArea.top + (workArea.bottom - workArea.top - windowHeight) / 2,
            windowWidth,
            windowHeight,
        });

        for (size_t index = 0; index < m_displays.size(); ++index)
        {
            auto const& display = m_displays[index];
            auto image = Image();
            image.Width(256);
            image.Height(144);
            image.Stretch(Media::Stretch::Uniform);
            image.Source(CaptureDisplayPreview(display));

            RECT bounds = display.info.rcMonitor;
            std::wstring label = GetDisplayLabel(display);
            if ((display.info.dwFlags & MONITORINFOF_PRIMARY) != 0)
            {
                label += L" (" + primaryLabel + L")";
            }
            label += L"\n" + std::to_wstring(bounds.right - bounds.left) + L" x " +
                     std::to_wstring(bounds.bottom - bounds.top);

            auto text = TextBlock();
            text.Text(label);
            text.TextAlignment(TextAlignment::Center);

            auto content = StackPanel();
            content.Spacing(8);
            content.Children().Append(image);
            content.Children().Append(text);

            auto button = Button();
            button.Padding({ 10, 10, 10, 10 });
            button.CornerRadius({ 8, 8, 8, 8 });
            button.Content(content);
            Automation::AutomationProperties::SetName(button, label);
            Automation::AutomationProperties::SetAutomationId(
                button,
                L"DisplayCard" + std::to_wstring(index + 1));
            button.Click([this, index](auto&&, auto&&) {
                m_selected(m_displays[index]);
                Close();
            });
            button.PointerEntered([](auto&&, auto&&) {
                SetCursor(LoadCursorW(nullptr, IDC_HAND));
            });
            DisplayCards().Children().Append(button);
        }
    }

    void DisplaySelectorWindow::Cancel_Click(
        Windows::Foundation::IInspectable const&,
        Microsoft::UI::Xaml::RoutedEventArgs const&)
    {
        Close();
    }
}
