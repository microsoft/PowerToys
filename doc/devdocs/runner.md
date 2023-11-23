#### [`main.cpp`](/src/runner/main.cpp)
Contains the executable starting point, initialization code and the list of known PowerToys. All singletons are also initialized here at the start. Loads all the powertoys by scanning the `./modules` folder and `enable()`s those marked as enabled in `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` config. Then it runs [a message loop](https://learn.microsoft.com/windows/win32/winmsg/using-messages-and-message-queues) for the tray UI. Note that this message loop also [handles lowlevel_keyboard_hook events](https://github.com/microsoft/PowerToys/blob/1760af50c8803588cb575167baae0439af38a9c1/src/runner/lowlevel_keyboard_event.cpp#L24).

#### [`powertoy_module.h`](/src/runner/powertoy_module.h) and [`powertoy_module.cpp`](/src/runner/powertoy_module.cpp)
Contains code for initializing and managing the PowerToy modules. `PowertoyModule` is a RAII-style holder for the `PowertoyModuleIface` pointer, which we got by [invoking module DLL's `powertoy_create` function](https://github.com/microsoft/PowerToys/blob/1760af50c8803588cb575167baae0439af38a9c1/src/runner/powertoy_module.cpp#L13-L24).

#### [`powertoys_events.cpp`](/src/runner/powertoys_events.cpp)
Contains code that handles the various events listeners, and forwards those events to the PowerToys modules. You can learn more about the current event architecture [here](/doc/devdocs/shared-hooks.md).

#### [`lowlevel_keyboard_event.cpp`](/src/runner/lowlevel_keyboard_event.cpp)
Contains code for registering the low level keyboard event hook that listens for keyboard events. Please note that `signal_event` is called from the main thread for this event.

#### [`win_hook_event.cpp`](/src/runner/win_hook_event.cpp)
Contains code for registering a Windows event hook through `SetWinEventHook`, that listens for various events raised when a window is interacted with. Please note, that `signal_event` is called from a separate `dispatch_thread_proc` worker thread, so you must provide thread-safety for your `signal_event` if you intend to receive it. This is a subject to change.

#### [`tray_icon.cpp`](/src/runner/tray_icon.cpp)
Contains code for managing the PowerToys tray icon and its menu commands. Note that `dispatch_run_on_main_ui_thread` is used to 
transfer received json message from the [Settings window](/doc/devdocs/settings.md) to the main thread, since we're communicating with it from [a dedicated thread](https://github.com/microsoft/PowerToys/blob/7357e40d3f54de51176efe54fda6d57028837b8c/src/runner/settings_window.cpp#L267-L271).
#### [`settings_window.cpp`](/src/runner/settings_window.cpp)
Contains code for starting the PowerToys settings window and communicating with it. Settings window is a separate process, so we're using [Windows pipes](https://learn.microsoft.com/windows/win32/ipc/pipes) as a transport for json messages.

#### [`general_settings.cpp`](/src/runner/general_settings.cpp)
Contains code for loading, saving and applying the general settings.

#### [`auto_start_helper.cpp`](/src/runner/auto_start_helper.cpp)
Contains helper code for registering and unregistering PowerToys to run when the user logs in.

#### [`unhandled_exception_handler.cpp`](/src/runner/unhandled_exception_handler.cpp)
Contains helper code to get stack traces in builds. Can be used by adding a call to `init_global_error_handlers` in [`WinMain`](./main.cpp).

#### [`trace.cpp`](/src/runner/trace.cpp)
Contains code for telemetry.

#### [`svgs`](/src/runner/svgs/)
Contains the SVG assets used by the PowerToys modules.
