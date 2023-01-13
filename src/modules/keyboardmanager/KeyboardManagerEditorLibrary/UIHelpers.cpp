#include "pch.h"
#include "UIHelpers.h"

#include <common/monitor_utils.h>

namespace UIHelpers
{
    // This method sets focus to the first Type button on the last row of the Grid
    void SetFocusOnTypeButtonInLastRow(StackPanel& parent, long colCount)
    {
        // First element in the last row (StackPanel)
        StackPanel firstElementInLastRow = parent.Children().GetAt(parent.Children().Size() - 1).as<StackPanel>().Children().GetAt(0).as<StackPanel>();

        // Type button is the first child in the StackPanel
        Button firstTypeButtonInLastRow = firstElementInLastRow.Children().GetAt(0).as<Button>();

        // Set programmatic focus on the button
        firstTypeButtonInLastRow.Focus(FocusState::Programmatic);
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
}
