#include "pch.h"
#include <common/settings_objects.h>
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <lib/ZoneSet.h>
#include <lib/RegistryHelpers.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            Trace::RegisterProvider();
            break;

        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
            break;

        case DLL_PROCESS_DETACH:
            Trace::UnregisterProvider();
            break;
    }
    return TRUE;
}

// This function is exported and called from FancyZonesEditor.exe to save a layout from the editor.
STDAPI PersistZoneSet(
    PCWSTR activeKey, // Registry key holding ActiveZoneSet
    PCWSTR resolutionKey, // Registry key to persist ZoneSet to
    HMONITOR monitor,
    WORD layoutId, // LayoutModel Id
    int zoneCount, // Number of zones in zones
    int zones[]) // Array of zones serialized in left/top/right/bottom chunks
{
    // See if we have already persisted this layout we can update.
    UUID id{GUID_NULL};
    if (wil::unique_hkey key{ RegistryHelpers::OpenKey(resolutionKey) })
    {
        ZoneSetPersistedData data{};
        DWORD dataSize = sizeof(data);
        wchar_t value[256]{};
        DWORD valueLength = ARRAYSIZE(value);
        DWORD i = 0;
        while (RegEnumValueW(key.get(), i++, value, &valueLength, nullptr, nullptr, reinterpret_cast<BYTE*>(&data), &dataSize) == ERROR_SUCCESS)
        {
            if (data.LayoutId == layoutId)
            {
                if (data.ZoneCount == zoneCount)
                {
                    CLSIDFromString(value, &id);
                    break;
                }
            }
            valueLength = ARRAYSIZE(value);
            dataSize = sizeof(data);
        }
    }

    if (id == GUID_NULL)
    {
        // No existing layout found so let's create a new one.
        UuidCreate(&id);
    }

    if (id != GUID_NULL)
    {
        winrt::com_ptr<IZoneSet> zoneSet = MakeZoneSet(
            ZoneSetConfig(
                id,
                layoutId,
                reinterpret_cast<HMONITOR>(monitor),
                resolutionKey,
                ZoneSetLayout::Custom,
                0, 0, 0));

        for (int i = 0; i < zoneCount; i++)
        {
            const int baseIndex = i * 4;
            const int left = zones[baseIndex];
            const int top = zones[baseIndex+1];
            const int right = zones[baseIndex+2];
            const int bottom = zones[baseIndex+3];
            zoneSet->AddZone(MakeZone({ left, top, right, bottom }), false);
        }
        zoneSet->Save();

        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED_LOG(StringFromCLSID(id, &zoneSetId)))
        {
            RegistryHelpers::SetString(activeKey, L"ActiveZoneSetId", zoneSetId.get());
        }

        return S_OK;
    }
    return E_FAIL;
}

class FancyZonesModule : public PowertoyModuleIface
{
public:
    // Return the display name of the powertoy, this will be cached
    virtual PCWSTR get_name() override
    {
        return L"FancyZones";
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty list.
    virtual PCWSTR* get_events() override
    {
        static PCWSTR events[] = { ll_keyboard, win_hook_event, nullptr };
        return events;
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int *buffer_size) override
    {
        return m_settings->GetConfig(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        m_settings->SetConfig(config);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        m_settings->CallCustomAction(action);
    }

    // Enable the powertoy
    virtual void enable()
    {
        if (!m_app)
        {
            Trace::FancyZones::EnableFancyZones(true);
            m_app = MakeFancyZones(reinterpret_cast<HINSTANCE>(&__ImageBase), m_settings.get());
            if (m_app)
            {
                m_app->Run();
            }
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return (m_app != nullptr);
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        if (m_app)
        {
            if (wcscmp(name, ll_keyboard) == 0)
            {
                // Return 1 if the keypress is to be suppressed (not forwarded to Windows), otherwise return 0.
                return HandleKeyboardHookEvent(reinterpret_cast<LowlevelKeyboardEvent*>(data));
            }
            else if (wcscmp(name, win_hook_event) == 0)
            {
                // Return value is ignored
                HandleWinHookEvent(reinterpret_cast<WinHookEvent*>(data));
            }
        }
        return 0;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    FancyZonesModule()
    {
        m_settings = MakeFancyZonesSettings(reinterpret_cast<HINSTANCE>(&__ImageBase), FancyZonesModule::get_name());
    }

private:
    static bool IsInterestingWindow(HWND window)
    {
        auto style = GetWindowLongPtr(window, GWL_STYLE);
        auto exStyle = GetWindowLongPtr(window, GWL_EXSTYLE);
        return WI_IsFlagSet(style, WS_MAXIMIZEBOX) && WI_IsFlagClear(style, WS_CHILD) && WI_IsFlagClear(exStyle, WS_EX_TOOLWINDOW);
    }

    void Disable(bool const traceEvent)
    {
        if (m_app) {
            if (traceEvent) 
            {
                Trace::FancyZones::EnableFancyZones(false);
            }
            m_app->Destroy();
            m_app = nullptr;
        }
    }

    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
    void HandleWinHookEvent(WinHookEvent* data) noexcept;
    void MoveSizeStart(HWND window, POINT const& ptScreen) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    void MoveSizeUpdate(POINT const& ptScreen) noexcept;

    winrt::com_ptr<IFancyZones> m_app;
    winrt::com_ptr<IFancyZonesSettings> m_settings;
};

intptr_t FancyZonesModule::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (data->wParam == WM_KEYDOWN)
    {
        return m_app.as<IFancyZonesCallback>()->OnKeyDown(data->lParam) ? 1 : 0;
    }
    return 0;
}

void FancyZonesModule::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    POINT ptScreen;
    GetPhysicalCursorPos(&ptScreen);

    switch (data->event)
    {
    case EVENT_SYSTEM_MOVESIZESTART:
    {
        MoveSizeStart(data->hwnd, ptScreen);
    }
    break;

    case EVENT_SYSTEM_MOVESIZEEND:
    {
        MoveSizeEnd(data->hwnd, ptScreen);
    }
    break;

    case EVENT_OBJECT_LOCATIONCHANGE:
    {
        if (m_app.as<IFancyZonesCallback>()->InMoveSize())
        {
            MoveSizeUpdate(ptScreen);
        }
    }
    break;

    case EVENT_OBJECT_NAMECHANGE:
    {
        // The accessibility name of the desktop window changes whenever the user
        // switches virtual desktops.
        if (data->hwnd == GetDesktopWindow())
        {
            Trace::VirtualDesktopChanged();
            m_app.as<IFancyZonesCallback>()->VirtualDesktopChanged();
        }
    }
    break;

    case EVENT_OBJECT_UNCLOAKED:
    case EVENT_OBJECT_SHOW:
    case EVENT_OBJECT_CREATE:
    {
        if (data->idObject == OBJID_WINDOW)
        {
            if (IsInterestingWindow(data->hwnd))
            {
                m_app.as<IFancyZonesCallback>()->WindowCreated(data->hwnd);
            }
        }
    }
    break;

    default:
        break;
    }
}

void FancyZonesModule::MoveSizeStart(HWND window, POINT const& ptScreen) noexcept
{
    if (IsInterestingWindow(window))
    {
        if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
        {
            m_app.as<IFancyZonesCallback>()->MoveSizeStart(window, monitor, ptScreen);
        }
    }
}

void FancyZonesModule::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (IsInterestingWindow(window))
    {
        m_app.as<IFancyZonesCallback>()->MoveSizeEnd(window, ptScreen);
    }
}

void FancyZonesModule::MoveSizeUpdate(POINT const& ptScreen) noexcept
{
    if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
    {
        m_app.as<IFancyZonesCallback>()->MoveSizeUpdate(monitor, ptScreen);
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface*  __cdecl powertoy_create()
{
  return new FancyZonesModule();
}


