#include "theme_listener.h"

#define HKEY_WINDOWS_THEME L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"

// disabling warning 4702 - unreachable code
// prevent the warning after the call off a infinite loop function
#pragma warning(push)
#pragma warning(disable : 4702)
DWORD WINAPI _checkTheme(LPVOID lpParam)
{
    auto listener = static_cast<ThemeListener*>(lpParam);
    listener->CheckTheme();
    return 0;
}
#pragma warning(pop)

void ThemeListener::AddChangedHandler(THEME_HANDLE handle)
{
    handles.push_back(handle);
}

void ThemeListener::DelChangedHandler(THEME_HANDLE handle)
{
    auto it = std::find(handles.begin(), handles.end(), handle);
    handles.erase(it);
}

void ThemeListener::CheckTheme()
{
    HANDLE hEvent;
    HKEY hKey;

    // Open the Key to listen
    RegOpenKeyEx(HKEY_CURRENT_USER, HKEY_WINDOWS_THEME, 0, KEY_NOTIFY, &hKey);

    while (true)
    {
        // Create an event.
        hEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
        if (hEvent != 0)
        {
            // Watch the registry key for a change of value.
            RegNotifyChangeKeyValue(hKey,
                                    TRUE,
                                    REG_NOTIFY_CHANGE_LAST_SET,
                                    hEvent,
                                    TRUE);

            WaitForSingleObject(hEvent, INFINITE);

            auto _theme = ThemeHelpers::GetAppTheme();
            if (AppTheme != _theme)
            {
                AppTheme = _theme;
                for (int i = 0; i < handles.size(); i++)
                {
                    handles[i]();
                }
            }
        }
    }
}