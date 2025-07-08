# Color Picker

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/color-picker)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Color%20Picker%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Color%20Picker%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Color+Picker%22)

## Overview
Color Picker is a system-wide color picking utility for Windows that allows users to pick colors from any screen and copy them to the clipboard in a configurable format.

## Implementation Details

### Color Capturing Mechanism
The Color Picker works by following these steps to capture the color at the current mouse position:

1. Obtain the position of the mouse
2. Create a 1x1 size rectangle at that position
3. Create a Bitmap class and use it to initiate a Graphics object
4. Create an image associated with the Graphics object by leveraging the CopyFromScreen function, which captures the pixel information from the specified location

### Core Color Picking Function
The following code snippet demonstrates the core functionality of how a color is picked from the screen:

```csharp
private static Color GetPixelColor(System.Windows.Point mousePosition)
{
    var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
    using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
    {
        var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        return bmp.GetPixel(0, 0);
    }
}
```

## Features
- Pick colors from any pixel on the screen
- View color information in various formats (RGB, HEX, HSL, etc.)
- Copy color values to clipboard in configurable formats
- Color history for quick access to previously selected colors
- Keyboard shortcuts for quick activation and operation

## User Experience
When activated, Color Picker displays a magnified view of the area around the cursor to allow for precise color selection. Once a color is selected, it can be copied to the clipboard in the user's preferred format for use in design tools, development environments, or other applications.
