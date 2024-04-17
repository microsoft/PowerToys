# Helpers

The helpers project contains a set of helper functions and classes that are used by the File Actions menu and its plugins.

## Extensions

### `T GetOrArgumentNullException<T>(this T? value)`

Returns the value if it's not null, otherwise throws an `ArgumentNullException`.

### `bool IsImage(this string fileName)`

Returns `true` if the file name has an image extension, otherwise `false`. This is the same list ImageResizer uses.

## File action progress

This class generates a progress window for file actions.

### Constructor `FileActionProgressHelper(string actionName, int count, Action onCancel)`

#### `actionName`

The name of the action that is being performed.

#### `count`

The number of files that the action is being performed on.

#### `onCancel`

The action to perform when the user cancels the progress window.

### `void UpdateProgress(int current, string fileName)`

Updates the progress window with the current file and the current progress.

### `(awaitable) Task Conflict(string fileName, Action onReplace, Action onIgnore)`

Shows a window indicating that the file `fileName` already exists and that provides the options to replace or ignore the file.

## `HashEnums`

This class contains enums for the hash types and the verify/generate modes.

## `IconHelper`

### `BitmapIcon GetIconFromModuleName(string moduleName)`

Returns the icon of the specified module.

## `ResourceHelper`

### `string GetResource(string key)`

Returns the resource string with the specified key.

## ShortcutHelper

### `string GetFullPathFromShortcut(string shortcutPath)`

Returns the full path of the file that the shortcut points to.

## Telemetry events

The `Telemetry` folder contains the telemetry events that are sent by the File Actions menu. The events are described in the [Telemetry.md](Telemetry.md) file.

### `TelemetryHelper`

#### `void LogEvent<T>(T e, string[] selectedItems) where T : EventBase, IFileActionsMenuItemInvokedEvent`

Logs the event `e`. The `selectedItems` parameter is an array of the selected items.
