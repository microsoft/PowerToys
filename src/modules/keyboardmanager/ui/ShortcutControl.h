#pragma once

#include <windows.h>
#include <stdlib.h>
#include <string.h>

#include <winrt/Windows.system.h>
#include <winrt/windows.ui.xaml.hosting.h>
#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>
#include <winrt/windows.ui.xaml.controls.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Windows.Foundation.Collections.h>
#include "winrt/Windows.Foundation.h"
#include "winrt/Windows.Foundation.Numerics.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"
#include "winrt/Windows.UI.Text.h"
#include "winrt/Windows.UI.Core.h"

#include <keyboardmanager/common/KeyboardManagerState.h>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;

extern TextBlock detectShortcutTextBlock;

extern std::vector<DWORD> detectedShortcuts;
__declspec(dllexport) void updateDetectShortcutTextBlock(std::vector<DWORD>& shortcutKeys);
class ShortcutControl
{
public:
    TextBlock shortcutText;
    Windows::UI::Xaml::Controls::Button bt;

public:
    std::unordered_map<DWORD, std::string> VKCodeToKeyName;
    static HWND _hWndEditShortcutsWindow;

    ShortcutControl() {}

    ShortcutControl(KeyboardManagerState& keyboardManagerState)
    {
        bt.Content(winrt::box_value(winrt::to_hstring("Type Shortcut")));
        bt.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
            keyboardManagerState.SetUIState(KeyboardManagerUIState::DetectShortcutWindowActivated, _hWndEditShortcutsWindow);
            // Using the XamlRoot of the bt to get the root of the XAML host
            createDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), keyboardManagerState);
        });
    }

    void AddToParent(StackPanel parent)
    {
        parent.Children().Append( bt);
        parent.Children().Append( shortcutText);
    }

    IInspectable getSiblingElement(IInspectable const& element)
    {
        FrameworkElement frameworkElement = element.as<FrameworkElement>();
        StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
        uint32_t index;

        parentElement.Children().IndexOf(frameworkElement, index);
        return parentElement.Children().GetAt(index + 1);
    }

    void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState)
    {
        ContentDialog detectShortcutBox;

        // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
        detectShortcutBox.XamlRoot(xamlRoot);
        detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
        detectShortcutBox.PrimaryButtonText(to_hstring(L"OK"));
        detectShortcutBox.IsSecondaryButtonEnabled(false);
        detectShortcutBox.CloseButtonText(to_hstring(L"Cancel"));
        detectShortcutBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

        TextBlock linkedShortcutText = getSiblingElement(sender).as<TextBlock>();

        detectShortcutBox.PrimaryButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
            hstring shortcutString;
            for (int i = 0; i < detectedShortcuts.size(); i++)
            {
                /*if (VKCodeToKeyName.find(detectedShortcuts[i]) != VKCodeToKeyName.end())
                {
                    shortcutString = shortcutString + to_hstring(VKCodeToKeyName[detectedShortcuts[i]]) + to_hstring(L" ");
                }
                else
                {*/
                    shortcutString = shortcutString + to_hstring((unsigned int)detectedShortcuts[i]) + to_hstring(L" ");
                //}
            }
            linkedShortcutText.Text(shortcutString);
            keyboardManagerState.ResetUIState();
        });
        detectShortcutBox.CloseButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
            keyboardManagerState.ResetUIState();
        });

        Windows::UI::Xaml::Controls::StackPanel stackPanel;
        stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

        TextBlock text;
        text.Text(winrt::to_hstring("Keys Pressed:"));
        text.Margin({ 0, 0, 0, 10 });
        TextBlock shortcutKeys;
        detectShortcutTextBlock = shortcutKeys;

        stackPanel.Children().Append(text);
        stackPanel.Children().Append(shortcutKeys);
        stackPanel.UpdateLayout();
        detectShortcutBox.Content(stackPanel);

        detectShortcutBox.ShowAsync();
    }
};
