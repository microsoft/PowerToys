---
last-update: 7-16-2024
---

# PowerToys Awake Changelog

## Builds

The build ID can be found in `Core\Constants.cs` in the `BuildId` variable - it is a unique identifier for the current builds that allows better diagnostics (we can look up the build ID from the logs) and offers a way to triage Awake-specific issues faster independent of the PowerToys version. The build ID does not carry any significance beyond that within the PowerToys code base.

The build ID moniker is made up of two components - a reference to a [Halo](https://en.wikipedia.org/wiki/Halo_(franchise)) character, and the date when the work on the specific build started in the format of `MMDDYYYY`.

| Build ID                                                           | Build Date        |
|:-------------------------------------------------------------------|:------------------|
| [`TILLSON_11272024`](#TILLSON_11272024-november-27-2024)           | November 27, 2024 |
| [`PROMETHEAN_09082024`](#PROMETHEAN_09082024-september-8-2024)     | September 8, 2024 |
| [`VISEGRADRELAY_08152024`](#VISEGRADRELAY_08152024-august-15-2024) | August 15, 2024   |
| [`DAISY023_04102024`](#DAISY023_04102024-april-10-2024)            | April 10, 2024    |
| [`ATRIOX_04132023`](#ATRIOX_04132023-april-13-2023)                | April 13, 2023    |
| [`LIBRARIAN_03202022`](#librarian_03202022-march-20-2022)          | March 20, 2022    |
| `ARBITER_01312022`                                                 | January 31, 2022  |

### `TILLSON_11272024` (November 27, 2024)

>[!NOTE]
>See pull request: [Awake - `TILLSON_11272024`](https://github.com/microsoft/PowerToys/pull/36049)

- [#35250](https://github.com/microsoft/PowerToys/issues/35250) Updates the icon retry policy, making sure that the icon consistently and correctly renders in the tray.
- [#35848](https://github.com/microsoft/PowerToys/issues/35848) Fixed a bug where custom tray time shortcuts for longer than 24 hours would be parsed as zero hours/zero minutes.
- [#34716](https://github.com/microsoft/PowerToys/issues/34716) Properly recover the state icon in the tray after an `explorer.exe` crash.
- Added configuration safeguards to make sure that invalid values for timed keep-awake times do not result in exceptions.
- Updated the tray initialization logic, making sure we wait for it to be properly created before setting icons.
- Expanded logging capabilities to track invoking functions.
- Added command validation logic to make sure that incorrect command line arguments display an error.
- Display state now shown in the tray tooltip.
- When timed mode is used, changing the display setting will no longer reset the timer.

### `PROMETHEAN_09082024` (September 8, 2024)

>[!NOTE]
>See pull request: [Awake - `PROMETHEAN_09082024`](https://github.com/microsoft/PowerToys/pull/34717)

- Updating the initialization logic to make sure that settings are respected for proper group policy and single-instance detection.
- [#34148](https://github.com/microsoft/PowerToys/issues/34148) Fixed a bug from the previous release that incorrectly synchronized threads for shell icon creation and initialized parent PID when it was not parented.

### `VISEGRADRELAY_08152024` (August 15, 2024)

>[!NOTE]
>See pull request: [Awake - `VISEGRADRELAY_08152024`](https://github.com/microsoft/PowerToys/pull/34316)

- [#34148](https://github.com/microsoft/PowerToys/issues/34148) Fixes the issue where the Awake icon is not displayed.
- [#17969](https://github.com/microsoft/PowerToys/issues/17969) Add the ability to bind the process target to the parent of the Awake launcher.
- PID binding now correctly ignores irrelevant parameters (e.g., expiration, interval) and only works for indefinite periods.
- Amending the native API surface to make sure that the Win32 error is set correctly.

### `DAISY023_04102024` (April 10, 2024)

>[!NOTE]
>See pull request: [Awake Update - `DAISY023_04102024`](https://github.com/microsoft/PowerToys/pull/32378)

- [#33630](https://github.com/microsoft/PowerToys/issues/33630) When in the UI and you select `0` as hours and `0` as minutes in `TIMED` awake mode, the UI becomes non-responsive whenever you try to get back to timed after it rolls back to `PASSIVE`.
- [#12714](https://github.com/microsoft/PowerToys/issues/12714) Adds the option to keep track of Awake state through tray tooltip.
- [#11996](https://github.com/microsoft/PowerToys/issues/11996) Adds custom icons support for mode changes in Awake.
- Removes the dependency on `System.Windows.Forms` and instead uses native Windows APIs to create the tray icon.
- Removes redundant/unused code that impacted application performance.
- Updates dependent packages to their latest versions (`Microsoft.Windows.CsWinRT` and `System.Reactive`).

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
