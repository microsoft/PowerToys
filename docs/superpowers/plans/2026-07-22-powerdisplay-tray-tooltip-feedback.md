# PowerDisplay Tray Tooltip Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Superseded after Task 2.** The Task 3 native-tooltip go/no-go failed on the modern overflow
> XAML Shell. Continue with `2026-07-22-powerdisplay-tray-feedback-overlay.md`. Task 1 and Task 2
> outputs remain valid prerequisites.

**Goal:** Show native, localized target-and-percentage feedback while brightness is adjusted by scrolling over the PowerDisplay tray icon.

**Architecture:** `MainViewModel` returns an immutable feedback payload built from the same planner results applied to monitor brightness. A pure formatter converts that payload and localized templates into bounded native tooltip text. `TrayIconService` updates `NOTIFYICONDATA.szTip` through `NIM_MODIFY`, best-effort forces the existing Shell tooltip with the EarTrumpet `TB_GETTOOLTIPS`/`TTM_POPUP` pattern, and restores the application name after two seconds.

**Tech Stack:** C# preview/.NET 10, WinUI 3, CommunityToolkit.Mvvm, Win32 `Shell_NotifyIcon`, toolbar/tooltip messages, System.Text.Json-compatible PowerDisplay models, MSTest, PowerToys build scripts.

## Global Constraints

- Work in `C:\Users\yuleng\source\repos\powerdisplay-wheel-design` on `yuleng/worktree/issue-49410-design`.
- Follow `docs/superpowers/specs/2026-07-22-powerdisplay-tray-tooltip-feedback-design.md`.
- Preserve the current version-0 notification callback and all scoped-hook/registration-recovery behavior.
- Use the native tray tooltip. Do not add a custom popup, toast, notification, animation, or setting.
- Wording is target-first: `Primary display · 55%`, `Primary displays · 35%, 70%`, or `All displays · 35%, 70%`.
- Show exact values for one through four physical targets. For five or more, show minimum-maximum and count.
- Preserve planner target order for exact lists.
- No valid target immediately restores or retains the normal `Power Display` tooltip; never show an error tooltip.
- Use the post-clamp values returned by the existing adjustment planner, including unchanged 0%/100% boundary targets.
- Every valid feedback update resets a two-second one-shot restore timer.
- Limit native tooltip text to 127 UTF-16 code units without a dangling high surrogate.
- Use localized placeholder templates with a neutral English fallback on `FormatException`.
- Update the tooltip only through `NIM_MODIFY`; feedback must never call `NIM_ADD`.
- Request immediate display best-effort using `Shell_TrayWnd` -> `TrayNotifyWnd` -> `SysPager` -> `ToolbarWindow32`, `TB_GETTOOLTIPS`, then `TTM_POPUP`.
- Send Explorer tooltip messages through `SendMessageTimeout(SMTO_ABORTIFHUNG, 100 ms)`, never an
  unbounded synchronous `SendMessage`.
- Missing Explorer toolbar/tooltip HWNDs are silent and do not alter registration state.
- A failed `NIM_MODIFY` resets stored tooltip text to `AppName`, marks registration stale, and enters existing self-healing recovery.
- Hide, registration loss, `TaskbarCreated`, and `Destroy()` cancel feedback and ensure future registration uses `AppName`.
- No tooltip work may run on `TrayIconMouseWheelListener.HookCallback`.
- Do not add third-party dependencies or modify Settings schema/UI, Runner, IPC, GPO, installer, CLI, or telemetry.
- Use x64 Debug for TDD, then validate PowerDisplay on x64 and ARM64.
- Run build essentials before the first targeted build for each architecture.
- Build tests before `vstest.console.exe`; do not use `dotnet test`.
- All source, comments, resources, documentation, and commit messages are English.
- Every implementation commit includes the trailers required by the active Copilot session.
- Go/no-go: if native update plus `TTM_POPUP` cannot show updated text during a real overflow-icon wheel interaction, stop and return to design; do not silently ship delayed-only feedback or switch to a custom OSD.

---

## File and Responsibility Map

| File | Responsibility |
| --- | --- |
| `src/modules/powerdisplay/PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs` | Immutable mode and post-adjustment value payload. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs` | Localized formatting-template payload. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs` | Pure localization-template formatting, count/range rules, and length limiting. |
| `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs` | Formatting behavior and defensive fallback tests. |
| `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs` | Return feedback from the same planner adjustments applied to monitors. |
| `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` | Pass nullable adjustment feedback to the tray service. |
| `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs` | Update, immediately request, restore, and invalidate native tooltip feedback. |
| `src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw` | English placeholder templates and localization comments. |

---

### Task 1: Add the Feedback Model and Pure Formatter

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs`

**Interfaces:**
- Produces: `TrayWheelAdjustmentFeedback(MouseWheelControlMode Mode, IReadOnlyList<int> BrightnessValues)`.
- Produces: `TrayWheelFeedbackTemplates`.
- Produces: `string? TrayWheelFeedbackFormatter.Format(TrayWheelAdjustmentFeedback, TrayWheelFeedbackTemplates, CultureInfo, int maxLength = 127)`.
- Consumed by: `MainViewModel`, `App`, and `TrayIconService`.

- [ ] **Step 1: Write the failing formatter tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackFormatterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static TrayWheelFeedbackTemplates Templates(
        string? primary = "Primary display \u00B7 {0}",
        string? primaryPlural = "Primary displays \u00B7 {0}",
        string? all = "All displays \u00B7 {0}",
        string? percentage = "{0}%",
        string? range = "{0}\u2013{1} ({2} displays)",
        string? separator = ", ")
        => new(primary, primaryPlural, all, percentage, range, separator);

    [TestMethod]
    public void Format_PrimarySingle_UsesSingularLabel()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            new[] { 55 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary display \u00B7 55%", result);
    }

    [TestMethod]
    public void Format_PrimaryMirrors_PreservesValueOrderAndUsesPluralLabel()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            new[] { 70, 35 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary displays \u00B7 70%, 35%", result);
    }

    [TestMethod]
    public void Format_AllDisplays_ListsUpToFourValues()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            new[] { 10, 30, 50, 70 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays \u00B7 10%, 30%, 50%, 70%", result);
    }

    [TestMethod]
    public void Format_PrimaryMoreThanFour_UsesRangeAndCount()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            new[] { 90, 20, 50, 60, 30 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("Primary displays \u00B7 20%\u201390% (5 displays)", result);
    }

    [TestMethod]
    public void Format_AllMoreThanFour_UsesRangeAndCount()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            new[] { 35, 70, 40, 90, 55, 60 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays \u00B7 35%\u201390% (6 displays)", result);
    }

    [TestMethod]
    public void Format_Boundaries_ShowZeroAndOneHundred()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            new[] { 0, 100 });

        var result = TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture);

        Assert.AreEqual("All displays \u00B7 0%, 100%", result);
    }

    [TestMethod]
    public void Format_EmptyValues_ReturnsNull()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            Array.Empty<int>());

        Assert.IsNull(TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture));
    }

    [TestMethod]
    public void Format_DisabledMode_ReturnsNull()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.Disabled,
            new[] { 55 });

        Assert.IsNull(TrayWheelFeedbackFormatter.Format(feedback, Templates(), Culture));
    }

    [TestMethod]
    public void Format_BrokenLocalizedTemplate_UsesNeutralEnglishTemplate()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            new[] { 55 });

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(primary: "Broken {1"),
            Culture);

        Assert.AreEqual("Primary display \u00B7 55%", result);
    }

    [TestMethod]
    public void Format_BrokenPercentageTemplate_UsesNeutralPercentage()
    {
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.AllDisplays,
            new[] { 25, 75 });

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(percentage: "{1}%"),
            Culture);

        Assert.AreEqual("All displays \u00B7 25%, 75%", result);
    }

    [TestMethod]
    public void Format_LimitsUtf16LengthWithoutDanglingHighSurrogate()
    {
        var longPrimary = new string('A', 126) + "\U0001F600{0}";
        var feedback = new TrayWheelAdjustmentFeedback(
            MouseWheelControlMode.PrimaryDisplay,
            new[] { 55 });

        var result = TrayWheelFeedbackFormatter.Format(
            feedback,
            Templates(primary: longPrimary),
            Culture,
            maxLength: 127);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length <= 127);
        Assert.IsFalse(char.IsHighSurrogate(result[^1]));
    }
}
```

- [ ] **Step 2: Build to verify RED**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
tools\build\build-essentials.cmd -Platform x64 -Configuration Debug
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"
```

Expected: non-zero exit code because the feedback model, templates, and formatter do not exist.

- [ ] **Step 3: Add the immutable feedback model**

Create `src/modules/powerdisplay/PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Models;

/// <summary>
/// Describes the post-adjustment values produced by one complete tray wheel action.
/// </summary>
/// <param name="Mode">The target scope used for the adjustment.</param>
/// <param name="BrightnessValues">Post-clamp brightness values in target enumeration order.</param>
public sealed record TrayWheelAdjustmentFeedback(
    MouseWheelControlMode Mode,
    IReadOnlyList<int> BrightnessValues);
```

- [ ] **Step 4: Add the localized-template payload**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Localized templates used to format tray wheel feedback.
/// </summary>
public sealed record TrayWheelFeedbackTemplates(
    string? PrimaryFormat,
    string? PrimaryPluralFormat,
    string? AllFormat,
    string? PercentageFormat,
    string? RangeFormat,
    string? ListSeparator);
```

- [ ] **Step 5: Implement the pure formatter**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Formats tray wheel adjustment feedback without UI or Shell dependencies.
/// </summary>
public static class TrayWheelFeedbackFormatter
{
    private const int ExactValueLimit = 4;
    private const string NeutralPrimaryFormat = "Primary display \u00B7 {0}";
    private const string NeutralPrimaryPluralFormat = "Primary displays \u00B7 {0}";
    private const string NeutralAllFormat = "All displays \u00B7 {0}";
    private const string NeutralPercentageFormat = "{0}%";
    private const string NeutralRangeFormat = "{0}\u2013{1} ({2} displays)";
    private const string NeutralListSeparator = ", ";

    /// <summary>
    /// Formats one feedback payload.
    /// </summary>
    public static string? Format(
        TrayWheelAdjustmentFeedback feedback,
        TrayWheelFeedbackTemplates templates,
        CultureInfo culture,
        int maxLength = 127)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (feedback.BrightnessValues is null || feedback.BrightnessValues.Count == 0)
        {
            return null;
        }

        var mode = feedback.Mode.Normalize();
        if (mode == MouseWheelControlMode.Disabled)
        {
            return null;
        }

        var percentages = new string[feedback.BrightnessValues.Count];
        var minimum = 100;
        var maximum = 0;
        for (var i = 0; i < feedback.BrightnessValues.Count; i++)
        {
            var value = Math.Clamp(feedback.BrightnessValues[i], 0, 100);
            percentages[i] = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                value);
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        string valuesText;
        if (percentages.Length <= ExactValueLimit)
        {
            valuesText = string.Join(
                string.IsNullOrEmpty(templates.ListSeparator)
                    ? NeutralListSeparator
                    : templates.ListSeparator,
                percentages);
        }
        else
        {
            var minimumText = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                minimum);
            var maximumText = SafeFormat(
                templates.PercentageFormat,
                NeutralPercentageFormat,
                culture,
                maximum);
            valuesText = SafeFormat(
                templates.RangeFormat,
                NeutralRangeFormat,
                culture,
                minimumText,
                maximumText,
                percentages.Length);
        }

        var isPrimary = mode == MouseWheelControlMode.PrimaryDisplay;
        var localizedOuter = isPrimary
            ? percentages.Length == 1
                ? templates.PrimaryFormat
                : templates.PrimaryPluralFormat
            : templates.AllFormat;
        var neutralOuter = isPrimary
            ? percentages.Length == 1
                ? NeutralPrimaryFormat
                : NeutralPrimaryPluralFormat
            : NeutralAllFormat;

        var result = SafeFormat(localizedOuter, neutralOuter, culture, valuesText);
        return LimitUtf16(result, maxLength);
    }

    private static string SafeFormat(
        string? localized,
        string neutral,
        CultureInfo culture,
        params object[] arguments)
    {
        if (!string.IsNullOrEmpty(localized))
        {
            try
            {
                return string.Format(culture, localized, arguments);
            }
            catch (FormatException)
            {
            }
        }

        return string.Format(CultureInfo.InvariantCulture, neutral, arguments);
    }

    private static string LimitUtf16(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = maxLength;
        if (char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }
}
```

- [ ] **Step 6: Build and run focused GREEN**

Run the test-project build, then:

```powershell
$repo = (git rev-parse --show-toplevel)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
$dll = "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll"
& $vstest $dll '/TestCaseFilter:FullyQualifiedName~TrayWheelFeedbackFormatterTests'
& $vstest $dll
```

Expected: 11/11 focused tests and the complete PowerDisplay.Lib.UnitTests suite pass.

- [ ] **Step 7: Commit the feedback contract and formatter**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs
git commit -m "Add tray wheel feedback formatter"
```

---

### Task 2: Return Applied Values and Coordinate Feedback

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs:22-85`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs:175-187`

**Interfaces:**
- Consumes: `TrayWheelAdjustmentFeedback`.
- Changes: `MainViewModel.AdjustBrightnessFromTrayWheel(int)` returns `TrayWheelAdjustmentFeedback?`.
- Produces: a nullable feedback return while preserving current brightness behavior.

- [ ] **Step 1: Return feedback from the existing planner results**

Change the method signature:

```csharp
public TrayWheelAdjustmentFeedback? AdjustBrightnessFromTrayWheel(int notches)
```

Replace every guard/no-target `return;` with:

```csharp
return null;
```

Replace the adjustment-application block with:

```csharp
_trayWheelNoTargetLogged = false;
var brightnessValues = new int[adjustments.Count];
for (var i = 0; i < adjustments.Count; i++)
{
    var adjustment = adjustments[i];
    brightnessValues[i] = adjustment.Brightness;

    foreach (var monitor in Monitors)
    {
        if (MonitorIdComparer.Equal(monitor.Id, adjustment.Id))
        {
            monitor.Brightness = adjustment.Brightness;
            break;
        }
    }
}

return new TrayWheelAdjustmentFeedback(mode, brightnessValues);
```

Do not recalculate values from monitor state after assigning them. The planner result is the
single source for both the setter and feedback.

- [ ] **Step 2: Preserve current behavior while compiling the new return type**

Replace the method-group subscription with:

```csharp
_trayIconService.MouseWheelScrolled += notches =>
{
    _ = mainWindow.ViewModel.AdjustBrightnessFromTrayWheel(notches);
};
```

Task 3 replaces this lambda with the production tooltip coordination after
`TrayIconService.UpdateAdjustmentFeedback` exists.

- [ ] **Step 3: Build PowerDisplay x64**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Expected: exit code 0 with no new warnings.

- [ ] **Step 4: Commit the feedback data flow**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
git commit -m "Return tray wheel adjustment feedback"
```

---

### Task 3: Deliver Native Tooltip Feedback

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs:175-187`
- Modify: `src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw:187-196`
- Verify: ignored `.superpowers/sdd/` feasibility and runtime scripts

**Interfaces:**
- Consumes: `TrayWheelAdjustmentFeedback`, `TrayWheelFeedbackTemplates`, and formatter.
- Produces: the production `UpdateAdjustmentFeedback` and connects App feedback coordination.
- Produces: immediate native tooltip update and two-second restoration.

- [ ] **Step 1: Run the native feasibility go/no-go probe**

Before product edits, use current-branch x64 bits and a persisted settings backup:

1. Locate the real PowerDisplay notification identity (`hWnd + uID 1001`) with the existing Task 8
   tray probes.
2. Open the overflow panel and locate the real `NotifyItemIcon` element.
3. Through an ignored PowerShell/Add-Type probe, call `NIM_MODIFY/NIF_TIP` with
   `Primary display · 55%`.
4. Walk `Shell_TrayWnd` -> `TrayNotifyWnd` -> `SysPager` -> `ToolbarWindow32`.
5. Send `TB_GETTOOLTIPS`, then `TTM_POPUP`.
6. Observe the visible tooltip through direct UIA or a screenshot.
7. Restore `Power Display`, settings bytes, original runner, Explorer, and brightness in `finally`.

Expected: the updated native text is visible during the hover without moving away and re-entering.

If the expected result is not observed, stop Task 3 with status BLOCKED, leave tracked source
unchanged, and return to design.

- [ ] **Step 2: Add localized templates**

Insert after `AppName` in `PowerDisplay/Strings/en-us/Resources.resw`:

```xml
  <data name="TrayWheelFeedbackPrimaryFormat" xml:space="preserve">
    <value>Primary display · {0}</value>
    <comment>{0} is the formatted brightness value, for example "55%".</comment>
  </data>
  <data name="TrayWheelFeedbackPrimaryPluralFormat" xml:space="preserve">
    <value>Primary displays · {0}</value>
    <comment>{0} is a list or range of brightness values, for example "35%, 70%".</comment>
  </data>
  <data name="TrayWheelFeedbackAllFormat" xml:space="preserve">
    <value>All displays · {0}</value>
    <comment>{0} is a list or range of brightness values, for example "35%, 70%".</comment>
  </data>
  <data name="TrayWheelFeedbackPercentageFormat" xml:space="preserve">
    <value>{0}%</value>
    <comment>{0} is an integer brightness percentage from 0 through 100.</comment>
  </data>
  <data name="TrayWheelFeedbackRangeFormat" xml:space="preserve">
    <value>{0}–{1} ({2} displays)</value>
    <comment>{0} is the minimum formatted percentage, {1} is the maximum formatted percentage, and {2} is the physical display count.</comment>
  </data>
  <data name="TrayWheelFeedbackListSeparator" xml:space="preserve">
    <value>, </value>
    <comment>Separator between brightness percentages in tray tooltip feedback.</comment>
  </data>
```

- [ ] **Step 3: Add feedback state and timer fields**

Add to `TrayIconService`:

```csharp
private const uint TbGetTooltips = PInvoke.WM_USER + 35;
private const uint TtmPopup = PInvoke.WM_USER + 34;
private const uint SmtoAbortIfHung = 0x0002;
private const uint TooltipMessageTimeoutMilliseconds = 100;
private const int MaxTrayTooltipLength = 127;
private static readonly TimeSpan AdjustmentFeedbackDuration = TimeSpan.FromSeconds(2);

private DispatcherQueueTimer? _feedbackTimer;
private bool _isAdjustmentFeedbackActive;
```

- [ ] **Step 4: Add production feedback handling**

Add:

```csharp
internal void UpdateAdjustmentFeedback(TrayWheelAdjustmentFeedback? feedback)
{
    if (feedback is null)
    {
        RestoreDefaultTooltip(updateShell: true);
        return;
    }

    var templates = new TrayWheelFeedbackTemplates(
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryPluralFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackAllFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPercentageFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackRangeFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackListSeparator"));
    var text = TrayWheelFeedbackFormatter.Format(
        feedback,
        templates,
        CultureInfo.CurrentCulture,
        MaxTrayTooltipLength);
    if (text is null)
    {
        RestoreDefaultTooltip(updateShell: true);
        return;
    }

    if (!TryModifyTrayTooltip(text))
    {
        return;
    }

    _isAdjustmentFeedbackActive = true;
    RestartFeedbackTimer();
    TryShowNativeTrayTooltip();
}
```

Add `using System.Globalization;`.

- [ ] **Step 5: Connect nullable feedback in `App`**

Replace the Task 2 discard lambda with:

```csharp
_trayIconService.MouseWheelScrolled += notches =>
{
    var feedback = mainWindow.ViewModel.AdjustBrightnessFromTrayWheel(notches);
    _trayIconService.UpdateAdjustmentFeedback(feedback);
};
```

Calling `UpdateAdjustmentFeedback(null)` is required: it clears a still-active previous percentage
when a later wheel action has no valid target.

- [ ] **Step 6: Implement modify and restore paths**

Add:

```csharp
private unsafe bool TryModifyTrayTooltip(string text)
{
    if (!_desiredTrayIconVisible ||
        !_isTrayIconRegistered ||
        _trayIconData is null)
    {
        return false;
    }

    var stored = (NOTIFYICONDATAW)_trayIconData;
    var modified = stored;
    modified.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
    modified.szTip = text;

    if (!Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_MODIFY, &modified))
    {
        StoreDefaultTooltip();
        MarkTrayIconRegistrationStale(resetBackoff: true, scheduleRecovery: true);
        return false;
    }

    stored.szTip = text;
    _trayIconData = stored;
    return true;
}

private void RestartFeedbackTimer()
{
    _feedbackTimer ??= _dispatcherQueue.CreateTimer();
    _feedbackTimer.IsRepeating = false;
    _feedbackTimer.Tick -= OnFeedbackTimerTick;
    _feedbackTimer.Tick += OnFeedbackTimerTick;
    _feedbackTimer.Stop();
    _feedbackTimer.Interval = AdjustmentFeedbackDuration;
    _feedbackTimer.Start();
}

private void OnFeedbackTimerTick(DispatcherQueueTimer sender, object args)
{
    sender.Stop();
    RestoreDefaultTooltip(updateShell: true);
}

private unsafe void RestoreDefaultTooltip(bool updateShell)
{
    _feedbackTimer?.Stop();
    if (!_isAdjustmentFeedbackActive)
    {
        return;
    }

    _isAdjustmentFeedbackActive = false;
    if (_trayIconData is null)
    {
        return;
    }

    var appName = GetString("AppName");
    var stored = (NOTIFYICONDATAW)_trayIconData;
    stored.szTip = appName;
    _trayIconData = stored;

    if (!updateShell || !_desiredTrayIconVisible || !_isTrayIconRegistered)
    {
        return;
    }

    var modified = stored;
    modified.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
    if (!Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_MODIFY, &modified))
    {
        MarkTrayIconRegistrationStale(resetBackoff: true, scheduleRecovery: true);
    }
}

private void StoreDefaultTooltip()
{
    _feedbackTimer?.Stop();
    _isAdjustmentFeedbackActive = false;
    if (_trayIconData is NOTIFYICONDATAW stored)
    {
        stored.szTip = GetString("AppName");
        _trayIconData = stored;
    }
}
```

Call `StoreDefaultTooltip()` before clearing identity in `Destroy()` and at the start of
`MarkTrayIconRegistrationStale`.

- [ ] **Step 7: Implement the EarTrumpet immediate popup request**

Add:

```csharp
private static void TryShowNativeTrayTooltip()
{
    var taskbar = FindWindowNative("Shell_TrayWnd", null);
    if (taskbar == 0)
    {
        return;
    }

    var notify = FindWindowExNative(taskbar, 0, "TrayNotifyWnd", null);
    var pager = notify == 0
        ? 0
        : FindWindowExNative(notify, 0, "SysPager", null);
    var toolbar = pager == 0
        ? 0
        : FindWindowExNative(pager, 0, "ToolbarWindow32", null);
    if (toolbar == 0)
    {
        return;
    }

    if (SendMessageTimeoutNative(
        toolbar,
        TbGetTooltips,
        0,
        0,
        SmtoAbortIfHung,
        TooltipMessageTimeoutMilliseconds,
        out var tooltipResult) == 0)
    {
        return;
    }

    var tooltip = (nint)tooltipResult;
    if (tooltip != 0)
    {
        _ = SendMessageTimeoutNative(
            tooltip,
            TtmPopup,
            0,
            0,
            SmtoAbortIfHung,
            TooltipMessageTimeoutMilliseconds,
            out _);
    }
}

[LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
private static partial nint FindWindowNative(string className, string? windowName);

[LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
private static partial nint FindWindowExNative(
    nint parent,
    nint childAfter,
    string className,
    string? windowName);

[LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
private static partial nint SendMessageTimeoutNative(
    nint window,
    uint message,
    nuint wParam,
    nint lParam,
    uint flags,
    uint timeoutMilliseconds,
    out nuint result);
```

No lookup failure logs are added.

- [ ] **Step 8: Build and run automated regression**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Run all PowerDisplay.Lib.UnitTests.

Expected: all tests and the x64 PowerDisplay build pass.

- [ ] **Step 9: Verify runtime go/no-go and lifecycle**

With persisted settings backups and current-branch process-path proof:

1. Overflow icon wheel immediately shows `Primary display · <value>%`.
2. A second notch changes the visible percentage without pointer leave/re-enter.
3. High-resolution `+60` alone does not update; the second `+60` does.
4. All mode shows both current display values.
5. After two seconds, native tooltip text returns to `Power Display`.
6. Hide/re-enable and Explorer restart begin with `Power Display`.
7. Existing brightness, targeting, click/menu, and registration recovery remain green.
8. Raw settings hashes, brightness, Explorer, and original runner paths are restored.

If immediate feedback cannot be observed on the real overflow icon, revert Task 3 tracked changes
and report BLOCKED for design reconsideration. Do not commit delayed-only behavior.

- [ ] **Step 10: Build ARM64 and commit**

Run ARM64 build essentials and PowerDisplay ARM64 Debug build.

Then:

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs `
    src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw
git commit -m "Show tray wheel adjustment tooltip"
```

---

### Task 4: Complete Verification and Update the Pull Request

**Files:**
- Verify current branch and ignored runtime reports.

**Interfaces:**
- Verifies Tasks 1-3 and the parent tray-wheel feature.

- [ ] **Step 1: Run complete automated validation**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll"

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"

tools\build\build-essentials.cmd -Platform arm64 -Configuration Debug
& "$repo\tools\build\build.ps1" `
    -Platform arm64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Expected: all tests and both architecture builds pass.

- [ ] **Step 2: Run final native-tooltip verification**

Use the PowerToys verification skill and current-branch bits. Record PASS/FAIL/BLOCKED evidence for:

- pinned icon if the environment supports pinning
- overflow icon
- immediate tooltip text after a real wheel notch
- high-resolution wheel accumulation
- Primary and All wording
- 0%/100%
- two-second restore
- no-target normal tooltip
- hide/re-enable and Explorer restart baseline text
- click/menu and brightness regression

The go/no-go criterion remains mandatory.

- [ ] **Step 3: Check final branch**

Run:

```powershell
git --no-pager diff --check origin/main...HEAD
git --no-pager diff --stat origin/main...HEAD
git status --short
```

Expected: no whitespace errors, only approved feature/design/test/resource changes, and a clean
worktree.

- [ ] **Step 4: Push the branch and update PR #49446**

Push:

```powershell
git push origin yuleng/worktree/issue-49410-design
```

Update the PR description validation section with formatter test count, x64/ARM64 builds, and native
tooltip runtime evidence. Do not create a second PR.
