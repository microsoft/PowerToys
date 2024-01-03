# Classes and structures

> This document is outdated and will soon be renewed.

#### class Animation: [header](/src/common/animation.h) [source](/src/common/animation.cpp)
Animation helper class with two easing-in animations: linear and exponential.

#### class AsyncMessageQueue: [header](/src/common/async_message_queue.h)
Header-only asynchronous message queue. Used by `TwoWayPipeMessageIPC`.

#### class TwoWayPipeMessageIPC: [header](/src/common/two_way_pipe_message_ipc.h)
Header-only asynchronous IPC messaging class. Used by the runner to communicate with the settings window.

#### class DPIAware: [header](/src/common/dpi_aware.h) [source](/src/common/dpi_aware.cpp)
Helper class for creating DPI-aware applications.

#### struct MonitorInfo: [header](/src/common/monitors.h) [source](/src/common/monitors.cpp)
Class for obtaining information about physical displays connected to the machine.

#### class Settings, class PowerToyValues, class CustomActionObject: [header](/src/common/settings_objects.h) [source](/src/common/settings_objects.cpp)
Classes used to define settings screens for the PowerToys modules.

#### class Tasklist: [header](/src/common/tasklist_positions.h) [source](/src/common/tasklist_positions.cpp)
Class that can detect the position of the windows buttons on the taskbar. It also detects which window will react to pressing `WinKey + number`.

#### struct WindowsColors: [header](/src/common/windows_colors.h) [source](/src/common/windows_colors.cpp)
Class for detecting the current Windows color scheme.

# Helpers

#### Common helpers: [header](/src/common/common.h) [source](/src/common/common.cpp)
Various helper functions.

#### Settings helpers: [header](/src/common/settings_helpers.h)
Helper methods for the settings.

#### Start visible helper: [header](/src/common/start_visible.h) [source](/src/common/start_visible.cpp)
Contains function to test if the Start menu is visible.

# Toast Notifications

#### Notifications API [header](/src/common/notifications.h) [source](/src/common/notifications.cpp)
To use UWP-style toast notifications, simply include the header and call one of these functions:

```cpp
    void show_toast(std::wstring_view message);       // #1
    
    void show_toast_background_activated(             // #2
      std::wstring_view message,
      std::wstring_view background_handler_id,
      std::vector<std::wstring_view> button_labels);
```
We might add more functions in the future if the need arises, e.g. `show_toast_xml` which will accept raw XML for rich customization.

Description:
- `#1` is for sending simple notifications without any callbacks or buttons
- `#2` is capable of showing a toast with multiple buttons and background activation
- `message` is a plain-text argument

Implement a toast activation handler/callback as a function in [handler_functions.cpp](/src/common/notifications_winrt/handler_functions.cpp) and register its `background_handler_id` via `handlers_map`, e.g.:

```cpp
// Your .cpp where you'd like to show a toast

#include <common/notifications.h>

void some_func() {
// ...
  notifications::show_toast_background_activated(
    L"Toast message!",                                                  // text displayed in a toast
    L"awesome_toast",                                                   // activation handler id
    {L"Press me!", L"Also could press me!", L"I'm here to be pressed!"} // buttons in a toast
  );
```

```cpp
// handler_functions.cpp
void awesome_toast_handler(IBackgroundTaskInstance, const size_t button_id)
{
  switch(button_id)
  {
    case 0:
      // handle "Press me!" button click
    case 1:
      // handle "Also could press me!" button click
    case 2:
      // handle "I'm here to be pressed!" button click
  }
}

namespace
{
  const std::unordered_map<std::wstring_view, handler_function_t> handlers_map = {
    // ...other handlers...
    {L"awesome_toast", awesome_toast_handler}
  };}

```

Note: since _background activation_ implies that your toast handler will be invoked in a separate process, you can't share data directly from within a handler and your PT process. Also, since PT is currently a Desktop Bridge app, _foreground activation_ is [handled the same as background](https://learn.microsoft.com/windows/uwp/design/shell/tiles-and-notifications/send-local-toast-desktop-cpp-wrl#foreground-vs-background-activation), therefore we don't make a dedicated API for it. You can read more on the rationale of the current design [here](https://github.com/microsoft/PowerToys/pull/1178#issue-368768337).


