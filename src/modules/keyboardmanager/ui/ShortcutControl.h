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
#include <keyboardManager/common/Helpers.h>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;

class ShortcutControl
{
private:
    TextBlock shortcutText;
    Button bt;
    StackPanel shortcutControlLayout;

public:
    static HWND _hWndEditShortcutsWindow;
    static KeyboardManagerState* keyboardManagerState;

    ShortcutControl()
    {
        bt.Content(winrt::box_value(winrt::to_hstring("Type Shortcut")));
        bt.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
            keyboardManagerState->SetUIState(KeyboardManagerUIState::DetectShortcutWindowActivated, _hWndEditShortcutsWindow);
            // Using the XamlRoot of the bt to get the root of the XAML host
            createDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState);
        });

        shortcutControlLayout.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
        shortcutControlLayout.Margin({ 0, 0, 0, 10 });
        shortcutControlLayout.Spacing(10);

        shortcutControlLayout.Children().Append(bt);
        shortcutControlLayout.Children().Append(shortcutText);
    }

    static void AddNewShortcutControlRow(StackPanel& parent, std::vector<DWORD> originalKeys = std::vector<DWORD>(), std::vector<WORD> newKeys = std::vector<WORD>())
    {
        Windows::UI::Xaml::Controls::StackPanel tableRow;
        tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
        tableRow.Spacing(100);
        tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
        ShortcutControl originalSC;
        tableRow.Children().Append(originalSC.getShortcutControl());
        ShortcutControl newSC;
        tableRow.Children().Append(newSC.getShortcutControl());
        if (!originalKeys.empty() && !newKeys.empty())
        {
            originalSC.shortcutText.Text(convertVectorToHstring<DWORD>(originalKeys));
            newSC.shortcutText.Text(convertVectorToHstring<WORD>(newKeys));
        }
        Windows::UI::Xaml::Controls::Button deleteShortcut;
        FontIcon deleteSymbol;
        deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
        deleteSymbol.Glyph(L"\xE74D");
        deleteShortcut.Content(deleteSymbol);
        deleteShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
            StackPanel currentRow = sender.as<Button>().Parent().as<StackPanel>();
            uint32_t index;
            parent.Children().IndexOf(currentRow, index);
            parent.Children().RemoveAt(index);
        });
        tableRow.Children().Append(deleteShortcut);
        parent.Children().Append(tableRow);
    }

    StackPanel getShortcutControl();
    void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState);
};
