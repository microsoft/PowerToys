#pragma once
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <map>
#include <vector>
#include <string>
#include <unordered_map>
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

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;

using namespace Windows::UI::Xaml::Controls;

enum class KeyboardManagerUIState
{
    // If set to this value then there is no keyboard manager window currently active that requires a hook
    Deactivated,
    // If set to this value then the detect key window is currently active and it requires a hook
    DetectKeyWindowActivated,
    // If set to this value then the detect shortcut window is currently active and it requires a hook
    DetectShortcutWindowActivated
};

class KeyboardManagerState
{
private:
    // State variable used to store which UI window is currently active that requires interaction with the hook
    KeyboardManagerUIState uiState;
    // Window handle for the current UI window which is active. Should be set to nullptr if UI is deactivated
    HWND currentUIWindow;
    // Vector to store the shortcut detected in the detect shortcut UI window. This is used in both the backend and the UI.
    std::vector<DWORD> detectedShortcut;
    // Stores the UI element which is to be updated based on the shortcut entered
    TextBlock currentShortcutTextBlock = nullptr;

public:   
    // Maps which store the remappings for each of the features. The bool fields should be initalised to false. They are used to check the current state of the shortcut (i.e is that particular shortcut currently pressed down or not).
    // Stores single key remappings
    std::unordered_map<DWORD, WORD> singleKeyReMap;

    // Stores keys which need to be changed from toggle behaviour to modifier behaviour. Eg. Caps Lock
    std::unordered_map<DWORD, bool> singleKeyToggleToMod;

    // Stores the os level shortcut remappings
    std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>> osLevelShortcutReMap;

    // Stores the app-specific shortcut remappings. Maps application name to the shortcut map
    std::map<std::wstring, std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>>> appSpecificShortcutReMap;

    KeyboardManagerState() :
        uiState(KeyboardManagerUIState::Deactivated), currentUIWindow(nullptr)
    {
    }

    void ResetUIState();
    bool CheckUIState(KeyboardManagerUIState state);
    void SetCurrentUIWindow(HWND windowHandle);
    void SetUIState(KeyboardManagerUIState state, HWND windowHandle = nullptr);
    void ConfigureDetectShortcutUI(TextBlock& textBlock);
    void UpdateDetectShortcutUI(std::vector<DWORD>& shortcutKeys);
    std::vector<DWORD> GetDetectedShortcut();
};