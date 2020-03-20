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

    StackPanel getShortcutControl();
    void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState);
};
