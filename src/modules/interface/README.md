# PowerToys Interface

The PowerToys interface that each PowerToy must implement.
See [`the example PowerToy`](/src/modules/example_powertoy) for a PowerToys module example that uses this interface.

## Interface definition

This is the interface definition:

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
  virtual void destroy() = 0;
};

typedef PowertoyModuleIface* (__cdecl *powertoy_create_func)();
```

### Runtime logic

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

When terminating, the runner will:
  - call [`disable()`](#disable),
  - call [`destroy()`](#destroy) which should free all the memory and delete the PowerToy object,
  - unload the DLL.


### Method definition

This section contains a more detailed description of each of the interface methods.

#### powertoy_create_func

```cpp
typedef PowertoyModuleIface* (__cdecl *powertoy_create_func)()
```

Typedef of the factory function that creates the PowerToy object.
Must be exported by the DLL as `powertoy_create()`.

Called by the PowerToys runner to initialize each PowerToy.
It will be called only once before a call to [`destroy()`](#destroy) is made.

The returned PowerToy should be in the disabled state. The runner will call the [`enable()`](#enable) method to start the PowerToy.

In case of errors returns `nullptr`.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
extern "C" __declspec(dllexport) PowertoyModuleIface*  __cdecl powertoy_create() {
  return new ExamplePowertoy();
}

ExamplePowertoy::ExamplePowertoy() {
  init_settings();
}
```

#### get_name

```cpp
virtual const wchar_t* get_name()
```

Returns the name of the PowerToy, it will be cached by the runner.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):
```cpp
  virtual const wchar_t* get_name() override {
    return L"Example Powertoy";
  }
```

#### get_events

```cpp
virtual const wchar_t** get_events()
```

Returns a null-terminated table of the names of the events the PowerToy wants to subscribe to. Available events:
  * ll_keyboard
  * win_hook_event

A nullptr can be returned to signal that the PowerToy does not want to subscribe to any event.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual const wchar_t** get_events() override {
    static const wchar_t* events[] = { ll_keyboard,
                                       win_hook_event,
                                       nullptr };
    return events;
  }
```

#### get_config

```
virtual bool get_config(wchar_t* buffer, int *buffer_size)
```

Fills a buffer with the available configuration settings.

If `buffer` is a null pointer or the buffer size is not large enough sets the required buffer size in 'buffer_size' and return false.

Returns true if successful.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual bool get_config(wchar_t* buffer, int* buffer_size) override {
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    // Create a Settings object.
    PowerToysSettings::Settings settings(hinstance, get_name());
    settings.set_description(L"Serves as an example powertoy, with example settings.");

    // Add an overview link to show in the Settings.
    settings.set_overview_link(L"https://github.com/microsoft/PowerToys");

    // Add a video link to show in the Settings.
    settings.set_video_link(L"https://www.youtube.com/watch?v=d3LHo2yXKoY&t=21462");

    // Add a bool property with a toggle editor.
    settings.add_bool_toogle(
      L"test_bool_toggle", // property name.
      L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
      test_bool_prop // property value.
    );

// More settings
...

    // Add a custom action property. When using this settings type, the "call_custom_action()" method should be overriden as well.
    settings.add_custom_action(
      L"test_custom_action", // action name.
      L"This is what a CustomAction property looks like", // label above the field.
      L"Call a custom action", // button text.
      L"Press the button to call a custom action in the Example PowerToy" // display values / extended info.
    );

    return settings.serialize_to_buffer(buffer, buffer_size);
  }
```

#### set_config

```cpp
virtual void set_config(const wchar_t* config)
```

After the user has changed the module settings in the Settings editor, the runner calls this method to pass to the module the updated values. It's a good place to save the settings as well.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual void set_config(const wchar_t* config) override {
    try {
      // Parse the PowerToysValues object from the received json string.
      PowerToysSettings::PowerToyValues _values =
        PowerToysSettings::PowerToyValues::from_json_string(config);

      // Update the bool property.
      if (_values.is_bool_value(L"test bool_toggle")) {
        test_bool_prop = _values.get_bool_value(L"test bool_toggle");
      }

// More settings
...

      save_settings();
    }
    catch (std::exception ex) {
      // Improper JSON.
    }
  }
```

#### call_custom_action

```cpp
  virtual void call_custom_action(const wchar_t* action)
```

Calls a custom action in response to the user pressing the custom action button in the Settings editor.
This can be used to spawn custom editors defined by the PowerToy.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual void call_custom_action(const wchar_t* action) override {
    try {
      // Parse the action values, including name.
      PowerToysSettings::CustomActionObject action_object =
        PowerToysSettings::CustomActionObject::from_json_string(action);

      if (action_object.get_name() == L"test_custom_action") {

        // Custom action code to increase and show a counter.
        ++this->test_custom_action_num_calls;
        std::wstring msg(L"I have been called ");
        msg += std::to_wstring(this->test_custom_action_num_calls);
        msg += L" time(s).";
        MessageBox(NULL, msg.c_str(), L"Custom action call.", MB_OK | MB_TOPMOST);
      }
    }
    catch (std::exception ex) {
      // Improper JSON.
    }
  }
```

#### enable

```cpp
  virtual void enable()
```

Enables the PowerToy.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual void enable() {
    m_enabled = true;
  }
```

#### disable

```cpp
  virtual void disable()
```

Disables the PowerToy, should free as much memory as possible.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual void disable() {
    m_enabled = false;
  }
```

#### is_enabled

```cpp
  virtual bool is_enabled() = 0;
```

Returns the PowerToy state.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual bool is_enabled() override {
    return m_enabled;
  }
```
#### signal_event

```cpp
  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) = 0;
```

Handle event. Only the events the PowerToy subscribed to will be signaled.
The data argument and return value meaning are event-specific:
  * ll_keyboard: see [`lowlevel_keyboard_event_data.h`](./lowlevel_keyboard_event_data.h).
  * win_hook_event: see [`win_hook_event_data.h`](./win_hook_event_data.h)

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual intptr_t signal_event(const wchar_t* name, intptr_t data)  override {
    if (wcscmp(name, ll_keyboard) == 0) {
      auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
      // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
      // otherwise return 0.
      return 0;
    } else if (wcscmp(name, win_hook_event) == 0) {
      auto& event = *(reinterpret_cast<WinHookEvent*>(data));
      // Return value is ignored
      return 0;
    }
    return 0;
  }
```

#### destroy

```cpp
  virtual void destroy()
```
Destroy the PowerToy and free all memory.

Sample code from [`the example PowerToy`](/src/modules/example_powertoy/dllmain.cpp):

```cpp
  virtual void destroy() override {
    delete this;
  }
```

## Code organization

#### [`powertoy_module_interface.h`](./powertoy_module_interface.h)
Contains the PowerToys interface definition.

#### [`lowlevel_keyboard_event_data.h`](./lowlevel_keyboard_event_data.h)
Contains the `LowlevelKeyboardEvent` structure that's passed to `signal_event` for `ll_keyboard` events.

#### [`win_hook_event_data.h`](./win_hook_event_data.h)
Contains the `WinHookEvent` structure that's passed to `signal_event` for `win_hook_event` events.

