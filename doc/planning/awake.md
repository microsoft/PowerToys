---
last-update: 3-20-2022
---

# PowerToys Awake Changelog

## Builds

The build ID can be found in `Program.cs` in the `BuildId` variable - it is a unique identifier for the current builds that allows better diagnostics (we can look up the build ID from the logs) and offers a way to triage Awake-specific issues faster independent of the PowerToys version. The build ID does not carry any significance beyond that within the PowerToys code base.

The build ID moniker is made up of two components - a reference to a [Halo](https://en.wikipedia.org/wiki/Halo_(franchise)) character, and the date when the work on the specific build started in the format of `MMDDYYYY`.

| Build ID                                                  | Build Date       |
|:----------------------------------------------------------|:-----------------|
| [`ATRIOX_04132023`](#ATRIOX_04132023-april-13-2023)       | April 13, 2023   |
| [`LIBRARIAN_03202022`](#librarian_03202022-march-20-2022) | March 20, 2022   |
| `ARBITER_01312022`                                        | January 31, 2022 |

### `ATRIOX_04132023` (April 13, 2023)

- Moves from using `Task.Run` to spin up threads to actually using a blocking queue that properly sets thread parameters on the same thread.
- Moves back to using native Windows APIs through P/Invoke instead of using a package.
- Move away from custom logging and to built-in logging that is consistent with the rest of PowerToys.
- Updates `System.CommandLine` and `System.Reactive` to the latest preview versions of the package.

### `LIBRARIAN_03202022` (March 20, 2022)

- Changed the tray context menu to be following OS conventions instead of the style offered by Windows Forms. This introduces better support for DPI scaling and theming in the future.
- Custom times in the tray can now be configured in the `settings.json` file for awake, through the `tray_times` property. The property values are representative of a `Dictionary<string, int>` and can be in the form of `"YOUR_NAME": LENGTH_IN_SECONDS`:

```json
{
    "properties": {
        "awake_keep_display_on": true,
        "awake_mode": 2,
        "awake_hours": 0,
        "awake_minutes": 3,
        "tray_times": {
            "Custom length": 1800,
            "Another custom length": 3600
        }
    },
    "name": "Awake",
    "version": "1.0"
}
```

- Proper Awake background window closure was implemented to ensure that the process collects the correct handle instead of the empty one that was previously done through `System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow()`. This likely can help with the Awake process that is left hanging after PowerToys itself closes.
