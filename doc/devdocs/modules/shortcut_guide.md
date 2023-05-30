#### [`dllmain.cpp`](/src/modules/ShortcutGuide/ShortcutGuideModuleInterface/dllmain.cpp)
Contains DLL boilerplate code.

#### [`shortcut_guide.cpp`](/src/modules/ShortcutGuide/ShortcutGuide/shortcut_guide.cpp)
Contains the module interface code. It initializes the settings values and the keyboard event listener.

#### [`overlay_window.cpp`](/src/modules/ShortcutGuide/ShortcutGuide/overlay_window.cpp)
Contains the code for loading the SVGs, creating and rendering of the overlay window.

#### [`keyboard_state.cpp`]()
Contains helper methods for checking the current state of the keyboard.

#### [`target_state.cpp`](/src/modules/ShortcutGuide/ShortcutGuide/target_state.cpp)
State machine that handles the keyboard events. It’s responsible for deciding when to show the overlay, when to suppress the Start menu (if the overlay is displayed long enough), etc.

#### [`trace.cpp`](/src/modules/ShortcutGuide/ShortcutGuide/trace.cpp)
Contains code for telemetry.
