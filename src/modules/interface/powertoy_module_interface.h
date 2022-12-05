#pragma once

#include <compare>
#include <common/utils/gpo.h>

/*
  DLL Interface for PowerToys. The powertoy_create() (see below) must return
  an object that implements this interface.

  See tools/project_template/ModuleTemplate for simple, noop, PowerToy implementation.

  The PowerToys runner will, for each PowerToy DLL:
    - load the DLL,
    - call powertoy_create() to create the PowerToy.

  On the received object, the runner will call:
    - get_key() to get the non localized ID of the PowerToy,
    - enable() to initialize the PowerToy.
    - get_hotkeys() to register the hotkeys the PowerToy uses.

  While running, the runner might call the following methods between create_powertoy()
  and destroy():
    - disable()/enable()/is_enabled() to change or get the PowerToy's enabled state,
    - get_config() to get the available configuration settings,
    - set_config() to set various settings,
    - call_custom_action() when the user selects clicks a custom action in settings,
    - get_hotkeys() when the settings change, to make sure the hotkey(s) are up to date.
    - on_hotkey() when the corresponding hotkey is pressed.

  When terminating, the runner will:
    - call destroy() which should free all the memory and delete the PowerToy object,
    - unload the DLL.

  The runner will call on_hotkey() even if the module is disabled.
 */

class PowertoyModuleIface
{
public:
    /* Describes a hotkey which can trigger an action in the PowerToy */
    struct Hotkey
    {
        bool win = false;
        bool ctrl = false;
        bool shift = false;
        bool alt = false;
        unsigned char key = 0;

        std::strong_ordering operator<=>(const Hotkey&) const = default;
    };

    struct HotkeyEx
    {
        WORD modifiersMask = 0;
        WORD vkCode = 0;
    };

    /* Returns the localized name of the PowerToy*/
    virtual const wchar_t* get_name() = 0;
    /* Returns non localized name of the PowerToy, this will be cached by the runner. */
    virtual const wchar_t* get_key() = 0;
    /* Fills a buffer with the available configuration settings.
    * If 'buffer' is a null ptr or the buffer size is not large enough
    * sets the required buffer size in 'buffer_size' and return false.
    * Returns true if successful.
    */
    virtual bool get_config(wchar_t* buffer, int* buffer_size) = 0;
    /* Sets the configuration values. */
    virtual void set_config(const wchar_t* config) = 0;
    /* Call custom action from settings screen. */
    virtual void call_custom_action(const wchar_t* /*action*/){};
    /* Enables the PowerToy. */
    virtual void enable() = 0;
    /* Disables the PowerToy, should free as much memory as possible. */
    virtual void disable() = 0;
    /* Should return if the PowerToys is enabled or disabled. */
    virtual bool is_enabled() = 0;
    /* Destroy the PowerToy and free all memory. */
    virtual void destroy() = 0;

    /* Get the list of hotkeys. Should return the number of available hotkeys and
     * fill up the buffer to the minimum of the number of hotkeys and its size.
     * Modules do not need to override this method, it will return zero by default.
     * This method is called even when the module is disabled.
     */
    virtual size_t get_hotkeys(Hotkey* /*buffer*/, size_t /*buffer_size*/)
    {
        return 0;
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx()
    {
        return std::nullopt;
    }

    virtual void OnHotkeyEx()
    {
    }

    /* Called when one of the registered hotkeys is pressed. Should return true
     * if the key press is to be swallowed.
     */
    virtual bool on_hotkey(size_t /*hotkeyId*/)
    {
        return false;
    }

    /* These are for enabling the legacy behavior of showing the shortcut guide after pressing the win key.
     * keep_track_of_pressed_win_key returns true if the module wants to keep track of the win key being pressed.
     * milliseconds_win_key_must_be_pressed returns the number of milliseconds the win key should be pressed before triggering the module.
     * Don't use these for new modules.
     */
    virtual bool keep_track_of_pressed_win_key() { return false; }
    virtual UINT milliseconds_win_key_must_be_pressed() { return 0; }

    virtual void send_settings_telemetry()
    {
    }

    virtual bool is_enabled_by_default() const { return true; }

    /* Provides the GPO configuration value for the module. This should be overridden by the module interface to get the proper gpo policy setting. */
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration()
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

protected:
    HANDLE CreateDefaultEvent(const wchar_t* eventName)
    {
        SECURITY_ATTRIBUTES sa;
        sa.nLength = sizeof(sa);
        sa.bInheritHandle = false;
        sa.lpSecurityDescriptor = NULL;
        return CreateEventW(&sa, FALSE, FALSE, eventName);
    }
};

/*
  Typedef of the factory function that creates the PowerToy object.

  Must be exported by the DLL as powertoy_create(), e.g.:

  extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
  
  Called by the PowerToys runner to initialize each PowerToy.
  It will be called only once before a call to destroy() method is made.

  Returned PowerToy should be in disabled state. The runner will call
  the enable() method to start the PowerToy.

  In case of errors return nullptr.
*/
typedef PowertoyModuleIface*(__cdecl* powertoy_create_func)();
