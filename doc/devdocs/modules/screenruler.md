# Screen Ruler

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/screen-ruler)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Screen%20Ruler%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Screen%20Ruler%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Screen+Ruler%22)

## Overview

Screen Ruler (project name: MeasureTool or Measure 2) is a PowerToys module that allows users to measure pixel distances and detect color boundaries on the screen. The tool renders an overlay UI using DirectX and provides several measurement utilities.

## Features

- **Bounds Tool**: Loosely enclose an object with a left-button drag. On release, the selection fits the largest enclosed visual region, adapts to bounded background texture variation, excludes soft window shadows, and places the dimensions outside the completed selection when space permits. Hold Alt for the raw selection, or Shift to retain and add another measurement. Touch selections remain manual.
- **Spacing Tool**: Measure the length of a line with the same color with the same pixel value both horizontally and vertically. Use the mouse wheel to adjust color tolerance; the current value briefly appears above the pointer.
- **Horizontal Spacing**: Measure the line with the same color in the horizontal direction
- **Vertical Spacing**: Measure the line with the same color in the vertical direction
- **Guides**: Add, reposition, and remove monitor-spanning horizontal or vertical guides. While the toolbar is active, compact labels show the pixel distance from each monitor edge to the nearest guide and between consecutive guides. The clear-all action appears only while at least one guide exists.

## Architecture & Implementation

The Screen Ruler module consists of several components:

### MeasureToolModuleInterface

- **Dllmain.cpp**: Starts the Measure Tool process, toggles a resident guide host, manages settings, and handles graceful termination events.

### MeasureToolUI

- **App.xaml.cs**: Main entrance of the app. Initializes MeasureToolCore and listens for toolbar toggle and host termination events.
- **MainWindow.xaml.cs**: Sets properties and behaviors for the window, handles user interactions, and hides or restores the toolbar without discarding guides.
- **NativeMethods.cs**: Interacts with the Windows API to manipulate window properties, such as positioning and sizing.
- **Settings.cs**: Gets the default measure style from settings.

### PowerToys.MeasureToolCore

- **PowerToys.MeasureToolCore**: Handles initialization, state management, and starts the measure tool and bounds tool.
- **BGRATextureView.h**: Manages and interacts with BGRA textures in a Direct3D 11 context.
- **Measurement.cpp**: Defines a Measurement struct that represents a rectangular measurement area, including methods for converting and printing measurement details in various units.
- **Clipboard.cpp**: Copies measurement data to the clipboard.
- **D2DState.cpp**: Manages Direct2D rendering state and draws text boxes.
- **DxgiAPI.cpp**: Creates and manages Direct3D and Direct2D devices.
- **EdgeDetection.cpp**: Detects edges in a BGRA texture.
- **GuideModel.cpp**: Stores guide state and handles placement, hit testing, monitor transfer, snapping, and edge removal.
- **GuideOverlayUI.cpp**: Owns the per-monitor guide render and input windows and coordinates guide interactions.
- **GuideCompositionRenderer.cpp**: Uses retained Windows Composition visuals for guide lines, labels, and interaction feedback.
- **OverlayUI.cpp**: Creates and manages overlay windows for tools like MeasureTool and BoundsTool.
- **BoundsSnapModel.cpp**: Learns a bounded background tolerance from the selection perimeter, prevents gradual color changes from chaining indefinitely, separates perimeter-connected background from enclosed content, and refines the selected region with a stricter edge threshold.
- **BoundsToolOverlayUI.cpp**: UI implementation for bounds feature. Handles mouse and touch events to draw measurement rectangles on the screen and display their pixels.
- **MeasureToolOverlayUI.cpp**: UI implementation for measure feature. Draws measurement lines on the screen and displays their pixels.
- **ScreenCaptureSession.cpp**: Provides reusable Windows Graphics Capture sessions for measurement and guide snapping.
- **ScreenCapturing.cpp**: Captures the screen for spacing measurements and takes a fresh, overlay-excluded frame at the start of each Bounds drag.
- **PerGlyphOpacityTextRender.cpp**: Renders text with varying opacity on a Direct2D render target.

### Guide overlays and lifetime

Each monitor has a passive, capture-excluded render window containing a retained `Windows.UI.Composition` visual tree. These render windows never accept input. Separate transparent input windows expose only narrow regions around committed guides while the toolbar is visible; placement and dragging temporarily expand those regions. Active edit mode also renders per-axis distance labels, moving them to the opposite monitor edge when the toolbar would overlap them. Hiding the toolbar hides every input window and distance label while retaining the passive guide lines, so guides remain visible without blocking the desktop.

The Measure Tool UI process remains resident while guides exist. Invoking Screen Ruler again restores the same toolbar and guide collection. Guides are session-only: clearing them removes the overlays, and disabling Screen Ruler or exiting PowerToys terminates the host and discards them.

## Building & Debugging

### Building

1. Open PowerToys.slnx in Visual Studio
2. In the Solutions Configuration drop-down menu, select Release or Debug
3. From the Build menu, choose Build Solution
4. The executable app for Screen Ruler is named PowerToys.MeasureToolUI.exe

### Debugging

1. Right-click the project MeasureToolUI and click 'Set as Startup Project'
2. Right-click the project MeasureToolUI and click 'Debug'

## Known Issues

There are several open bugs for the Screen Ruler module, most of which are related to crashing issues. These can be found in the [PowerToys issues list](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue%20state%3Aopen%20Screen%20ruler%20type%3ABug).
