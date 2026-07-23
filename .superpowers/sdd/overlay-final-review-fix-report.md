# Overlay Final Review Fix Report

## Finding dispositions

1. **Queued teardown announcement:** Fixed. `TrayWheelFeedbackWindow.Announce` now returns before creating or using an automation peer when the window is disposed or no longer visible.
2. **Feedback preparation boundary:** Fixed. `TrayIconService.UpdateAdjustmentFeedback` narrowly handles `COMException`, `InvalidOperationException`, `FormatException`, and `ArgumentException` around resource loading, formatting, and adjustment presentation. The first failure logs once for the `TrayIconService` lifetime; every handled failure stops the poll timer, clears the feedback session, and hides the overlay. Registration and hardware calls remain outside this boundary. Brightness remains first: `App.xaml.cs` calls `AdjustBrightnessFromTrayWheel` before `UpdateAdjustmentFeedback`.
3. **Visible placement gap:** Fixed. Placement now passes a zero gap. `FeedbackRoot` retains its 8-DIP padding, which supplies the visible Border-to-icon gap without double-counting shadow padding.
4. **Primary displays plural:** No change. `TrayWheelAdjustmentPlannerTests.Plan_PrimaryDisplay_SelectsEveryMirroredPhysicalTarget` proves two physical targets can share `\\.\DISPLAY1`; `TrayWheelFeedbackFormatterTests.Format_PrimaryMirrors_PreservesValueOrderAndUsesPluralLabel` proves the plural text for that reachable case.
5. **436-DIP root clamp:** No change. The 420-DIP visible Border maximum plus the 8-DIP root padding on each side equals the existing 436-DIP root clamp.

## Validation

| Command | Result |
|---|---|
| `tools\build\build.ps1 -Platform x64 -Configuration Debug -Path src\modules\powerdisplay\PowerDisplay.Lib.UnitTests /t:Rebuild` | Passed |
| `vstest.console.exe x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll /Platform:x64 /Framework:.NETCoreApp,Version=v10.0` | Passed: 242/242 |
| `tools\build\build.ps1 -Platform x64 -Configuration Debug -Path src\modules\powerdisplay\PowerDisplay` | Passed |
| `tools\build\build.ps1 -Platform ARM64 -Configuration Debug -Path src\modules\powerdisplay\PowerDisplay` | Passed; ARM64 essentials were already available |
| `git diff --check` | Passed |

## Runtime smoke

**BLOCKED (BLK-VISUAL-RENDER):** The interactive desktop is available, but `winapp` is not installed and the running installed `PowerToys.PowerDisplay` process predates the local build. Sideloading or replacing installed bits was not performed. Consequently, the local changes could not be validly driven to measure the rendered 8-DIP gap or to observe announcement/overlay teardown. No runtime state was changed.

## Commit

`HEAD` (this commit)

## Concerns

No product concerns identified. Runtime visual and shutdown smoke remains blocked pending a current-bits deployment with UI automation available.
