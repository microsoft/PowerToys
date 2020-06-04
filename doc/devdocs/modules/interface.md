# Interface definition

```cpp
class PowertoyModuleIface {
public:
  virtual const wchar_t* get_name() = 0;
  virtual const wchar_t** get_events() = 0;
  virtual bool get_config(wchar_t* buffer, int *buffer_size) = 0;
  virtual void set_config(const wchar_t* config) = 0;
  virtual void call_custom_action(const wchar_t* action) {};
  virtual void enable() = 0;
  virtual void disable() = 0;
  virtual bool is_enabled() = 0;
  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) = 0;
  virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) = 0;
  virtual void signal_system_menu_action(const wchar_t* name) = 0;
  virtual void destroy() = 0;
};

typedef PowertoyModuleIface* (__cdecl *powertoy_create_func)();
```

# Runtime logic

The PowerToys runner will, for each PowerToy DLL:
  - load the DLL,
  - call [`powertoy_create()`](#powertoy_create_func) to create the PowerToy.

On the received object, the runner will call:
  - [`get_name()`](#get_name) to get the name of the PowerToy,
  - [`get_events()`](#get_events) to get the list of the events the PowerToy wants to subscribe to,
  - [`enable()`](#enable) to initialize the PowerToy.

While running, the runner might call the following methods between create_powertoy()
and destroy():
  - [`disable()`](#disable)/[`enable()`](#enable)/[`is_enabled()`](#is_enabled) to change or get the PowerToy's enabled state,
  - [`get_config()`](#get_config) to get the available configuration settings,
  - [`set_config()`](#set_config) to set settings after they have been edited in the Settings editor,
  - [`call_custom_action()`](#call_custom_action) when the user selects a custom action in the Settings editor,
  - [`signal_event()`](#signal_event) to send an event the PowerToy registered to.
  - [`register_system_menu_helper()`](#register_system_menu_helper) to pass object, responsible for handling customized system menus, to module.
  - [`signal_system_menu_action()`](#signal_system_menu_action) to send an event when action is taken on system menu item.

When terminating, the runner will:
  - call [`disable()`](#disable),
  - call [`destroy()`](#destroy) which should free all the memory and delete the PowerToy object,
  - unload the DLL.


# Method definition

This section contains a more detailed description of each of the interface methods.

## powertoy_create_func

```cpp
typedef PowertoyModuleIface* (__cdecl *powertoy_create_func)()
```

Typedef of the factory function that creates the PowerToy object.
Must be exported by the DLL as `powertoy_create()`.

Called by the PowerToys runner to initialize each PowerToy.
It will be called only once before a call to [`destroy()`](#destroy) is made.

The returned PowerToy should be in the disabled state. The runner will call the [`enable()`](#enable) method to start the PowerToy.

In case of errors returns `nullptr`.

## get_name

```cpp
virtual const wchar_t* get_name()
```

Returns the name of the PowerToy, it will be cached by the runner.

## get_events

```cpp
virtual const wchar_t** get_events()
```

Returns a null-terminated table of the names of the events the PowerToy wants to subscribe to. Available events:
  * ll_keyboard
  * win_hook_event

A nullptr can be returned to signal that the PowerToy does not want to subscribe to any event.

## get_config

```
virtual bool get_config(wchar_t* buffer, int *buffer_size)
```

Fills a buffer with the available configuration settings.

If `buffer` is a null pointer or the buffer size is not large enough sets the required buffer size in 'buffer_size' and return false.

Returns true if successful.

## set_config

```cpp
virtual void set_config(const wchar_t* config)
```

After the user has changed the module settings in the Settings editor, the runner calls this method to pass to the module the updated values. It's a good place to save the settings as well.

## call_custom_action

```cpp
  virtual void call_custom_action(const wchar_t* action)
```

Calls a custom action in response to the user pressing the custom action button in the Settings editor.
This can be used to spawn custom editors defined by the PowerToy.

## enable

```cpp
  virtual void enable()
```

Enables the PowerToy.

## disable

```cpp
  virtual void disable()
```

Disables the PowerToy, should free as much memory as possible.

## is_enabled

```cpp
  virtual bool is_enabled() = 0;
```

Returns the PowerToy state.

## signal_event

```cpp
  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) = 0;
```

Handle event. Only the events the PowerToy subscribed to will be signaled.
The data argument and return value meaning are event-specific:
  * ll_keyboard: see [`lowlevel_keyboard_event_data.h`](./lowlevel_keyboard_event_data.h).
  * win_hook_event: see [`win_hook_event_data.h`](./win_hook_event_data.h)

Please note that some of the events are currently being signalled from a separate thread.

## register_system_menu_helper

```cpp
  virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) = 0;
```

Register helper class to handle all system menu items related actions. Creation, deletion
and all other actions taken on system menu item will be handled by provided class.
Module will be informed when action is taken on any item created on request of the module.

## signal_system_menu_action

```cpp
  virtual void signal_system_menu_action(const wchar_t* name) = 0;
```

Runner invokes this API when action is taken on item created on request from the module.
Item name is passed as an argument, so that module can distinguish between different menu items.

## destroy

```cpp
  virtual void destroy()
```
Destroy the PowerToy and free all memory.

## Powertoys system menu helper interface

Interface for helper class responsible for handling all system menu related actions.
```cpp
class PowertoySystemMenuIface {
public:
  struct ItemInfo {
    std::wstring name{};
    bool enable{ false };
    bool checkBox{ false };
  };
  virtual void SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config) = 0;
  virtual void ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) = 0;
};
```

## ItemInfo

```cpp
  struct ItemInfo {
    std::wstring name{};
    bool enable{ false };
    bool checkBox{ false };
  };
```

Structure containing all relevant information for system menu item: name (and hotkey if available), item
status at creation (enabled/disabled) and whether check box will appear next to item name when action is taken.

## SetConfiguration

```cpp
  virtual void SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config) = 0;
```

Module should use this interface to inform system menu helper class which custom system menu items to create.

## ProcessSelectedItem

```cpp
  virtual void ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) = 0;
```

Process action taken on specific system menu item.

# Code organization

### [`powertoy_module_interface.h`](/src/modules/interface/powertoy_module_interface.h)
Contains the PowerToys interface definition.

### [`powertoy_system_menu.h`](/src/modules/interface/powertoy_system_module.h)
Contains the PowerToys system menu helper interface definition.

### [`lowlevel_keyboard_event_data.h`](/src/modules/interface/lowlevel_keyboard_event_data.h)
Contains the `LowlevelKeyboardEvent` structure that's passed to `signal_event` for `ll_keyboard` events.

### [`win_hook_event_data.h`](/src/modules/interface/win_hook_event_data.h)
Contains the `WinHookEvent` structure that's passed to `signal_event` for `win_hook_event` events.

