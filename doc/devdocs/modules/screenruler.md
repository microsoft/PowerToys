# Screen Ruler

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/screen-ruler)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Screen%20Ruler%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Screen%20Ruler%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Screen+Ruler%22)

## Overview

Screen Ruler (project name: MeasureTool or Measure 2) is a PowerToys module that allows users to measure pixel distances and detect color boundaries on the screen. The tool renders an overlay UI using DirectX and provides several measurement utilities.

## Features

- **Bounce Utility**: Measure a rectangular zone by dragging with a left click
- **Spacing Tool**: Measure the length of a line with the same color with the same pixel value both horizontally and vertically
- **Horizontal Spacing**: Measure the line with the same color in the horizontal direction
- **Vertical Spacing**: Measure the line with the same color in the vertical direction

## Architecture & Implementation

The Screen Ruler module consists of several components:

### MeasureToolModuleInterface

- **Dllmain.cpp**: Provides functionality to start and stop the Measure Tool process based on hotkey events, manage settings, and handle events.

### MeasureToolUI

- **App.xaml.cs**: Main entrance of the app. Initializes MeasureToolCore and activates a new main window.
- **MainWindow.xaml.cs**: Sets properties and behaviors for the window, and handles user click interactions.
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
- **OverlayUI.cpp**: Creates and manages overlay windows for tools like MeasureTool and BoundsTool.
- **BoundsToolOverlayUI.cpp**: UI implementation for bounds feature. Handles mouse and touch events to draw measurement rectangles on the screen and display their pixels.
- **MeasureToolOverlayUI.cpp**: UI implementation for measure feature. Draws measurement lines on the screen and displays their pixels.
- **ScreenCapturing.cpp**: Continuously captures the screen, detects edges, and updates the measurement state for real-time drawing of measurement lines.
- **PerGlyphOpacityTextRender.cpp**: Renders text with varying opacity on a Direct2D render target.

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
