# Introduction
The common lib, as the name suggests, contains code shared by multiple PowerToys components and modules.

# Classes and structures

#### class Animation: [header](./animation.h) [source](./animation.cpp)
Animation helper class with two easing-in animations: linear and exponential.

#### class AsyncMessageQueue: [header](./async_message_queue.h)
Header-only asynchronous message queue. Used by `TwoWayPipeMessageIPC`.

#### class TwoWayPipeMessageIPC: [header](./two_way_pipe_message_ipc.h)
Header-only asynchronous IPC messaging class. Used by the runner to communicate with the settings window.

#### class D2DSVG: [header](./d2d_svg.h) [source](./d2d_svg.cpp)
Class for loading, rendering and for some basic modifications of SVG graphics.

#### class D2DText: [header](./d2d_text.h) [source](./d2d_text.cpp)
Class for rendering text using DirectX.

#### class D2DWindow: [header](./d2d_window.h) [source](./d2d_window.cpp)
Base class for creating borderless windows, with DirectX enabled rendering pipeline.

#### class DPIAware: [header](./dpi_aware.h) [source](./dpi_aware.cpp)
Helper class for creating DPI-aware applications.

#### struct MonitorInfo: [header](./monitors.h) [source](./monitors.cpp)
Class for obtaining information about physical displays connected to the machine.

#### class Settings, class PowerToyValues, class CustomActionObject: [header](./settings_objects.h) [source](./settings_objects.cpp)
Classes used to define settings screens for the PowerToys modules.

#### class Tasklist: [header](./tasklist_positions.h) [source](./tasklist_positions.cpp)
Class that can detect the position of the windows buttons on the taskbar. It also detects which window will react to pressing `WinKey + number`.

#### struct WindowsColors: [header](./windows_colors.h) [source](./windows_colors.cpp)
Class for detecting the current Windows color scheme.

# Helpers

#### Common helpers: [header](./common.h) [source](./common.cpp)
Various helper functions.

#### Settings helpers: [header](./settings_helpers.h)
Helper methods for the settings.

#### Start visible helper: [header](./start_visible.h) [source](./start_visible.cpp)
Contains function to test if the Start menu is visible.
