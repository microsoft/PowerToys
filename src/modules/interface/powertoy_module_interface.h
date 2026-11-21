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
    - get_hotkeys() to register the hotkeys that the PowerToy uses.

  While running, the runner might call the following methods between create_powertoy()
  and destroy():
    - disable()/enable()/is_enabled() to change or get the PowerToy's enabled state,
    - get_config() to get the available configuration settings,
    - set_config() to set various settings,
    - call_custom_action() when the user selects clicks a custom action in settings,
    - get_hotkeys() when the settings change, to make sure the hotkey(s) are up to date.
    - should_handle_hotkey() and invoke_hotkey() when the corresponding hotkey is pressed.

  When terminating, the runner will:
    - call destroy() which should free all the memory and delete the PowerToy object,
    - unload the DLL.

  The runner will call should_handle_hotkey() even if the module is disabled.
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
        // The id is used to identify the hotkey in the module. The order in module interface should be the same as in the settings.
        int id = 0;
        // Currently, this is only used by AdvancedPaste to determine if the hotkey is shown in the settings.
        bool isShown = true;

        std::strong_ordering operator<=>(const Hotkey& other) const
        {
            // Compare bool fields first
            if (auto cmp = (win <=> other.win); cmp != 0)
                return cmp;
            if (auto cmp = (ctrl <=> other.ctrl); cmp != 0)
                return cmp;
            if (auto cmp = (shift <=> other.shift); cmp != 0)
                return cmp;
            if (auto cmp = (alt <=> other.alt); cmp != 0)
                return cmp;

            // Compare key value only
            return key <=> other.key;

            // Note: Deliberately NOT comparing 'name' field
        }

        bool operator==(const Hotkey& other) const
        {
            return win == other.win &&
                   ctrl == other.ctrl &&
                   shift == other.shift &&
                   alt == other.alt &&
                   key == other.key;
        }
    };

    struct HotkeyEx
    {
        WORD modifiersMask = 0;
        WORD vkCode = 0;
        int id = 0;
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

    /* Called on the runner's main thread (via WM_HOTKEY). This is the same thread
     * that runs invoke_hotkey(), so it is serialized with the other module calls.
     * A slow implementation delays other hotkeys, the tray icon and Settings, so
     * keep it short. The runner logs a warning when it takes longer than 50 ms.
     */
    virtual void OnHotkeyEx()
    {
    }

    /* Legacy single-phase hotkey handler. Should return true if the key press is
     * to be swallowed.
     *
     * The runner no longer calls this from the low-level keyboard hook. It calls
     * should_handle_hotkey() on the hook thread and invoke_hotkey() on the
     * runner's main thread. The default invoke_hotkey() calls on_hotkey() and
     * ignores the return value, so modules that only implement on_hotkey() keep
     * working. See should_handle_hotkey() for how the swallow decision is made.
     */
    virtual bool on_hotkey(size_t /*hotkeyId*/)
    {
        return false;
    }

    // Reserved ID used when the legacy Windows-key hold path invokes on_hotkey.
    static constexpr size_t WIN_KEY_HOLD_HOTKEY_ID = static_cast<size_t>(-1);

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

    /* How the runner handles a press of a registered hotkey. */
    enum class HotkeyDisposition
    {
        // Don't invoke the module and let the key reach the foreground app.
        PassThrough,
        // Invoke the module and also let the key reach the foreground app.
        InvokeAndPassThrough,
        // Invoke the module and swallow the key press.
        InvokeAndSwallow,
    };

    /* Two-phase hotkey contract, phase 1. Called on the runner's low-level
     * keyboard hook thread when one of the hotkeys from get_hotkeys() is pressed,
     * to decide whether the key press is swallowed and whether invoke_hotkey()
     * runs. Keyboard input for the whole machine waits for this call, so it must
     * finish in microseconds: no I/O, COM, process launches, waits or locks that
     * other threads may hold. Reading plain fields that other module methods
     * write is acceptable here.
     *
     * The default returns InvokeAndSwallow when the module is enabled and
     * PassThrough otherwise, which matches modules whose on_hotkey() returns
     * their enabled state. Override it when the swallow decision depends on more
     * than that (for example, on the foreground window).
     *
     * Appended at the end of the interface so that the existing vtable layout
     * is unchanged.
     */
    virtual HotkeyDisposition should_handle_hotkey(size_t /*hotkeyId*/)
    {
        return is_enabled() ? HotkeyDisposition::InvokeAndSwallow : HotkeyDisposition::PassThrough;
    }

    /* Two-phase hotkey contract, phase 2. Called in FIFO order on the runner's
     * main thread, the same thread that calls enable(), disable(), set_config()
     * and the other module methods, after should_handle_hotkey() returned one of
     * the Invoke dispositions. It is also called for the legacy Windows-key hold
     * (WIN_KEY_HOLD_HOTKEY_ID) when the module is enabled.
     *
     * Keyboard input is not blocked while this runs, but the hook waits up to
     * 50 ms for it to finish before releasing the key press. That keeps the
     * foreground activation rights from the key press available to fast actions
     * (SetForegroundWindow, AllowSetForegroundWindow). Slow actions still delay
     * later hotkeys, the tray icon and Settings, and the runner logs a warning
     * when one takes longer than 50 ms.
     *
     * The default calls on_hotkey() and ignores the return value.
     */
    virtual void invoke_hotkey(size_t hotkeyId)
    {
        on_hotkey(hotkeyId);
    }

    // Some actions like AdvancedPaste generate new inputs, which we don't want to catch again.
    // The flag was purposefully chose to not collide with other keyboard manager flags.
    const static inline ULONG_PTR CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG = 0x110;

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
