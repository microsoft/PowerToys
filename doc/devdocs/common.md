# Classes and structures

#### class Animation: [header](/src/common/animation.h) [source](/src/common/animation.cpp)
Animation helper class with two easing-in animations: linear and exponential.

#### class AsyncMessageQueue: [header](/src/common/async_message_queue.h)
Header-only asynchronous message queue. Used by `TwoWayPipeMessageIPC`.

#### class TwoWayPipeMessageIPC: [header](/src/common/two_way_pipe_message_ipc.h)
Header-only asynchronous IPC messaging class. Used by the runner to communicate with the settings window.

#### class D2DSVG: [header](/src/common/d2d_svg.h) [source](/src/common/d2d_svg.cpp)
Class for loading, rendering and for some basic modifications of SVG graphics.

#### class D2DText: [header](/src/common/d2d_text.h) [source](/src/common/d2d_text.cpp)
Class for rendering text using DirectX.

#### class D2DWindow: [header](/src/common/d2d_window.h) [source](/src/common/d2d_window.cpp)
Base class for creating borderless windows, with DirectX enabled rendering pipeline.

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
