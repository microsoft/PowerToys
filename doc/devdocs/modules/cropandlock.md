# Crop and Lock

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/crop-and-lock)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-CropAndLock)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-CropAndLock)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-CropAndLock)

## Overview

The Crop and Lock module in PowerToys allows users to crop a current application into a smaller window or create a thumbnail. This utility enhances productivity by enabling users to focus on specific parts of an application window.

## Features

### Thumbnail Mode
Creates a window showing the selected area of the original window. Changes in the original window are reflected in the thumbnail.

### Reparent Mode
Creates a window that replaces the original window, showing only the selected area. The application is controlled through the cropped window.

## Code Structure

### Project Layout
The Crop and Lock module is part of the PowerToys solution. All the logic-related settings are in the main.cpp. The main implementations are in ThumbnailCropAndLockWindow and ReparentCropAndLockWindow. ChildWindow and OverlayWindow distinguish the two different modes of windows implementations.

### Key Files
- **ThumbnailCropAndLockWindow.cpp**: Defines the UI for the thumbnail mode.
- **OverlayWindow.cpp**: Thumbnail module type's window concrete implementation.
- **ReparentCropAndLockWindow.cpp**: Defines the UI for the reparent mode.
- **ChildWindow.cpp**: Reparent module type's window concrete implementation.

## Known Issues

- Cropping maximized or full-screen windows in "Reparent" mode might not work properly.
- Some UWP apps may not respond well to being cropped in "Reparent" mode.
- Applications with sub-windows or tabs can have compatibility issues in "Reparent" mode.

## Debug
1. build the entire project
2. launch the built Powertoys
3. select CropAndLock as the startup project in VS
4. In the debug button, choose "Attach to process". ![image](https://github.com/user-attachments/assets/a7624ec2-63f1-4720-9540-a916b0ada282)
5. Attach to CropAndLock.![image](https://github.com/user-attachments/assets/08aa0465-596c-4494-9daa-e96b234f9997)
