#include "ShortcutControl.h"

HWND ShortcutControl::_hWndEditShortcutsWindow = nullptr;

TextBlock detectShortcutTextBlock = nullptr;

std::vector<DWORD> detectedShortcuts;
void updateDetectShortcutTextBlock(std::vector<DWORD>& shortcutKeys)
{
    if (detectShortcutTextBlock == nullptr)
    {
        return;
    }

    detectedShortcuts = shortcutKeys;

    hstring shortcutString;
    for (int i = 0; i < shortcutKeys.size(); i++)
    {
        //{
        shortcutString = shortcutString + to_hstring((unsigned int)shortcutKeys[i]) + to_hstring(L" ");
        //}
    }

    detectShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        detectShortcutTextBlock.Text(shortcutString);
    });
}