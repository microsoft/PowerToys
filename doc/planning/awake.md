---
last-update: 3-20-2022
---

# PowerToys Awake Changelog

## Builds

The build ID can be found in [`NLog.config`](https://github.com/microsoft/PowerToys/blob/2e3a2b3f96f67c7dfc72963e5135662d3230b5fe/src/modules/awake/Awake/NLog.config#L5) - it is a unique identifier for the current builds that allows better diagnostics (we can look up the build ID from the logs) and offers a way to triage Awake-specific issues faster independent of the PowerToys version. The build ID does not carry any significance beyond that within the PowerToys code base.

| Build ID                                                  | Build Date       |
|:----------------------------------------------------------|:-----------------|
| [`LIBRARIAN_03202022`](#librarian_03202022-march-20-2022) | March 20, 2022   |
| `ARBITER_01312022`                                        | January 31, 2022 |

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
