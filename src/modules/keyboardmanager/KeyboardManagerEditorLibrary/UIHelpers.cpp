#include "pch.h"
#include "UIHelpers.h"

#include <common/monitor_utils.h>

using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::Xaml::Automation::Peers;

namespace UIHelpers
{
    // This method sets focus to the first "Select" button on the last row of the Grid of EditKeyboardWindow
    void SetFocusOnFirstSelectButtonInLastRowOfEditKeyboardWindow(StackPanel& parent, long colCount)
    {
        // First element in the last row (StackPanel)
        auto lastHotKeyLine = parent.Children().GetAt(parent.Children().Size() - 1).as<StackPanel>();

        // Get "From" Column
        auto fromColumn = lastHotKeyLine.Children().GetAt(0).as<StackPanel>();

        // Get "Select" Button from the "From" Column
        Button selectButton = fromColumn.Children().GetAt(0).as<Button>();
        if (selectButton != nullptr)
        {
            // Set programmatic focus on the button
            selectButton.Focus(FocusState::Programmatic);
        }
    }

    // This method sets focus to the first "Select" button on the last row of the Grid of EditShortcutsWindow
    void SetFocusOnFirstSelectButtonInLastRowOfEditShortcutsWindow(StackPanel& parent, long colCount)
    {
        // First element in the last row (StackPanel)
        auto lastHotKeyLine = parent.Children().GetAt(parent.Children().Size() - 1).as<StackPanel>();

        // Get "From" Column
        auto fromColumn = lastHotKeyLine.Children().GetAt(0).as<StackPanel>();

        StackPanel selectButtonTry = fromColumn.Children().GetAt(1).as<StackPanel>();
        Button selectButtonTry2 = selectButtonTry.Children().GetAt(1).as<Button>();
        if (selectButtonTry2 != nullptr)
        {
            // Set programmatic focus on the button
            selectButtonTry2.Focus(FocusState::Programmatic);
        }
    }

    RECT GetForegroundWindowDesktopRect()
    {
        HWND window = GetForegroundWindow();
        HMONITOR settingsMonitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        RECT desktopRect{};
        auto monitors = GetAllMonitorRects<&MONITORINFOEX::rcWork>();
        for (const auto& monitor : monitors)
        {
            if (settingsMonitor == monitor.first)
            {
                desktopRect = monitor.second;
                break;
            }
        }

        return desktopRect;
    }

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable GetSiblingElement(winrt::Windows::Foundation::IInspectable const& element)
    {
        FrameworkElement frameworkElement = element.as<FrameworkElement>();
        StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
        uint32_t index;

        parentElement.Children().IndexOf(frameworkElement, index);
        return parentElement.Children().GetAt(index + 1);
    }

    winrt::Windows::Foundation::IInspectable GetLabelWrapped(const winrt::Windows::Foundation::IInspectable& element, std::wstring label, double textWidth, HorizontalAlignment horizontalAlignment)
    {
        StackPanel sp = StackPanel();

        try
        {
            sp.Name(L"Wrapped_" + element.as<FrameworkElement>().Name());
        }
        catch (...)
        {
        }

        sp.Orientation(Orientation::Horizontal);
        sp.HorizontalAlignment(horizontalAlignment);
        TextBlock text;
        text.FontWeight(Text::FontWeights::Bold());
        text.Text(label);

        if (textWidth >= 0)
        {
            text.Width(textWidth);
        }

        sp.Children().Append(text);
        sp.Children().Append(element.as<FrameworkElement>());
        return sp;
    }

    winrt::Windows::Foundation::IInspectable GetWrapped(const winrt::Windows::Foundation::IInspectable& element, double width)
    {
        StackPanel sp = StackPanel();
        sp.Width(width);
        sp.Children().Append(element.as<FrameworkElement>());
        return sp;
    }

    winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> ToBoxValue(const std::vector<std::pair<DWORD, std::wstring>>& list)
    {
        winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> boxList = single_threaded_vector<winrt::Windows::Foundation::IInspectable>();
        for (auto& val : list)
        {
            auto comboBox = ComboBoxItem();
            comboBox.DataContext(winrt::box_value(std::to_wstring(val.first)));
            comboBox.Content(winrt::box_value(val.second));
            boxList.Append(winrt::box_value(comboBox));
        }

        return boxList;
    }

#ifndef NDEBUG
    std::vector<std::wstring> GetChildrenNames(StackPanel& s)
    {
        std::vector<std::wstring> result;
        for (auto child : s.Children())
        {
            std::wstring nameAndClass =
                child.as<IFrameworkElement>().Name().c_str();

            nameAndClass += L" ";
            nameAndClass += winrt::get_class_name(child.try_as<winrt::Windows::Foundation::IInspectable>()).c_str();
            result.push_back(nameAndClass);
        }

        return result;
    }
#endif
}
