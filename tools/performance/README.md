# Performance tools

Scripts that measure how fast PowerToys starts and how much memory it uses. Use them to prove that a change makes startup faster (ReadyToRun, AOT, loading modules lazily, and so on) and to catch regressions.

| File | What it does |
|---|---|
| [`Measure-StartupPerformance.ps1`](Measure-StartupPerformance.ps1) | Starts the runner and the published .NET apps many times and reports median and P90 startup time and memory |
| [`Compare-StartupPerformance.ps1`](Compare-StartupPerformance.ps1) | Compares two sets of results and prints a Markdown table for a pull request |
| [`PowerToys.Performance.wprp`](PowerToys.Performance.wprp) | WPR profile for the runner's startup stage events, for traces you open in WPA |

## Measure startup

```powershell
.\tools\performance\Measure-StartupPerformance.ps1 -PowerToysRoot .\x64\Release -Label main
```

`-PowerToysRoot` is any folder that contains `PowerToys.exe`: a build output folder or an install folder. Use `-Scenario` to pick scenarios, and `-Iterations` (default 10) and `-WarmupIterations` (default 2) to set the number of samples. The script prints a summary table and writes every sample to a JSON file in `-OutputDirectory` (default `%TEMP%\PowerToys-Startup-Performance`).

Each sample starts a new process, after warm-up samples that load the files into the OS file cache. That makes the numbers "new process, warm disk cache" startup, which is what JIT, ReadyToRun, and AOT changes affect. A cold boot isn't simulated.

| Scenario | What it starts | Startup milestones (ms) |
|---|---|---|
| `Runner` | `PowerToys.exe` | `SettingsLoaded`, `TrayIconReady`, `ModulesLoaded`, `EnabledModulesStarted`, `Ready`: the runner's own stage times, in ms since process creation |
| `Settings` | Settings, through the running runner (`PowerToys.exe --open-settings=Dashboard`) | `WindowShownMs`: the window is shown. `ShellReadyMs`: the `DashboardNavItem` navigation item exists |
| `PowerToysRun` | `PowerToys.PowerLauncher.exe` on its own | `InputIdleMs`: the UI thread is idle, after the plugins are loaded |
| `FileLocksmith` | The File Locksmith UI, for one sample file | `WindowShownMs`, then `UiReadyMs`: the `ReloadBtn` button exists |
| `MarkdownPreview`, `MonacoPreview`, `SvgPreview` | The preview handler, with the same command line that File Explorer's shim uses, inside a host window | `WindowShownMs`: the handler's window is shown in the host. `WebViewShownMs`: WebView2 shows its window in the host. Navigation and painting after that are native WebView2 work |
| `SvgThumbnail` | The SVG thumbnail provider, with the shim's command line | `ExitMs`: the bitmap is written and the process has exited |

All scenarios except `SvgThumbnail` also report `WorkingSetMB` and `PrivateMB`, sampled `-SettleMilliseconds` (default 3000) after the last milestone. `Runner` also reports `TotalWorkingSetMB` for every process started from `-PowerToysRoot`.

Window times come from `EVENT_OBJECT_SHOW` events, so they're taken when Windows shows the window, not when a polling loop notices it. `ShellReadyMs` and `UiReadyMs` poll UI Automation, so they include up to about 15 ms of polling delay.

### To measure what ships

The installer ships published builds of Settings, PowerToys Run, File Locksmith, and the four .NET preview handlers and thumbnail providers. A normal build produces runnable output too, but publish settings such as ReadyToRun only apply when you publish. To measure those, publish the same projects that CI publishes (`csProjectsToPublish` in [`job-build-project.yml`](/.pipelines/v2/templates/job-build-project.yml)), with the same arguments, after the build:

```cmd
msbuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /t:Publish /graph /p:Configuration=Release /p:Platform=x64 /p:AppxBundle=Never /p:VCRTForwarders-IncludeDebugCRT=false /p:PowerToysRoot=%CD% /p:PublishProfile=InstallationPublishProfile.pubxml /p:TargetFramework=net10.0-windows10.0.26100.0
```

Use a lowercase platform (`x64` or `arm64`), because the publish profiles build the runtime identifier from it.

## Compare two builds

Machines drift, so alternate the runs between the builds, and pass all the result files to the compare script. It pools the samples of each side:

```powershell
$measure = '.\tools\performance\Measure-StartupPerformance.ps1'
& $measure -PowerToysRoot C:\builds\main -Label main -Iterations 5
& $measure -PowerToysRoot C:\builds\change -Label change -Iterations 5
& $measure -PowerToysRoot C:\builds\change -Label change -Iterations 5
& $measure -PowerToysRoot C:\builds\main -Label main -Iterations 5

.\tools\performance\Compare-StartupPerformance.ps1 -Baseline (Get-ChildItem $env:TEMP\PowerToys-Startup-Performance\main-*.json) -Candidate (Get-ChildItem $env:TEMP\PowerToys-Startup-Performance\change-*.json)
```

## Before you run it

- **It takes over PowerToys.** `Runner`, `Settings`, and `PowerToysRun` stop every running PowerToys runner first and start them again at the end. If PowerToys runs elevated, exit it first or run the script elevated. The other scenarios leave a running PowerToys alone.
- **It restores your settings.** Local and installed builds share `%LOCALAPPDATA%\Microsoft\PowerToys`, and a build of another version rewrites files there: version stamps, PowerToys Run's plugin data, default settings of modules. So for the three scenarios above, the script copies that folder (without logs) first and puts it back exactly at the end. It also writes the measured build's version to `last_version_run.json`, so "What's new" doesn't open during the run. `FileLocksmith` restores the `last-run.log` file it uses.
- **Compare like with like.** The results record the enabled modules. The runner's stage times and memory depend on them, so compare runs with the same settings.
- **Keep the machine quiet.** Close other apps, stay on AC power, and don't build at the same time.
- **Keep the warm-up.** The first start of a new build can take seconds longer, because antivirus scans new executables. Warm-up samples absorb that.
- **Local builds are version 0.0.1.** On Windows 11, File Locksmith, Image Resizer, and PowerRename then try to register their unsigned context menu package on every start. The attempt fails, but it adds time to `EnabledModulesStarted` that installed builds don't have.
- **`Runner` needs the stage logging.** It reads the stage line that the runner logs, so it can't measure builds from before that line was added. The other scenarios work with any build, including an installed one.

## Runner startup stage events

The runner marks five startup stages: `SettingsLoaded`, `TrayIconReady`, `ModulesLoaded`, `EnabledModulesStarted`, and `Ready` (right before the message loop). For each stage it writes a `Runner_StartupStage` event, with `Stage` and `MsSinceProcessStart` fields, to the `Microsoft.PowerToys.Performance` TraceLogging provider (`9d83a68b-e53f-5d64-0e80-e3a9faf69485`). That provider isn't in the telemetry provider group, so diagnostic data collection doesn't pick up the events, and they're written whether or not diagnostic data is turned on.

Once the runner is ready, it also writes all the stage times to its log (`%LOCALAPPDATA%\Microsoft\PowerToys\RunnerLogs`) as one line, which is what `Measure-StartupPerformance.ps1` reads, so it doesn't need elevation:

```text
[info] Startup stages (ms since process start): SettingsLoaded=47 TrayIconReady=56 ModulesLoaded=166 EnabledModulesStarted=433 Ready=434
```

To see the stages next to CPU and disk activity, record a trace from an elevated prompt and open it in WPA:

```cmd
wpr -start CPU -start tools\performance\PowerToys.Performance.wprp -filemode
:: start PowerToys
wpr -stop startup.etl
```
