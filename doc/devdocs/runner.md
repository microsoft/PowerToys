#### [`main.cpp`](./main.cpp)
Contains the executable starting point, initialization code and the list of known PowerToys.

#### [`powertoy_module.h`](./powertoy_module.h) and [`powertoy_module.cpp`](./powertoy_module.cpp)
Contains code for initializing and managing the PowerToy modules.

#### [`powertoys_events.cpp`](./powertoys_events.cpp)
Contains code that handles the various events listeners, and forwards those events to the PowerToys modules.

#### [`lowlevel_keyboard_event.cpp`](./lowlevel_keyboard_event.cpp)
Contains code for registering the low level keyboard event hook that listens for keyboard events.

#### [`win_hook_event.cpp`](./win_hook_event.cpp)
Contains code for registering a Windows event hook through `SetWinEventHook`, that listens for various events raised when a window is interacted with.

#### [`tray_icon.cpp`](./tray_icon.cpp)
Contains code for managing the PowerToys tray icon and its menu commands.

#### [`settings_window.cpp`](./settings_window.cpp)
Contains code for starting the PowerToys settings window and communicating with it.

#### [`general_settings.cpp`](./general_settings.cpp)
Contains code for loading, saving and applying the general setings.

#### [`auto_start_helper.cpp`](./auto_start_helper.cpp)
Contains helper code for registering and unregistering PowerToys to run when the user logs in.

#### [`unhandled_exception_handler.cpp`](./unhandled_exception_handler.cpp)
Contains helper code to get stack traces in builds. Can be used by adding a call to `init_global_error_handlers` in [`WinMain`](./main.cpp).

#### [`trace.cpp`](./trace.cpp)
Contains code for telemetry.

#### [`svgs`](./svgs/)
Contains the SVG assets used by the PowerToys modules.
