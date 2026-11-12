# Manual selection checks

This suite is launched manually, outside CI test execution and the repository's UI automation suites. It loads the compiled CmdPal `ListItemsView` and `ListPage` in a native WinUI window to check real list/grid selection events, the dispatcher, scrolling, and Frame Back navigation. It does not install a package or launch or terminate another CmdPal instance.

Run on an interactive Windows desktop with the repository's Visual Studio and .NET prerequisites. The project is excluded from automatic MSBuild test execution because it requires a desktop.

From this project directory, build with the repository wrapper, then run VSTest after the build succeeds:

```powershell
& ..\..\..\..\..\tools\build\build.ps1 -Path . -Platform x64 -Configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
& "$vs\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
    ..\..\..\..\..\x64\Debug\WinUI3Apps\CmdPal\tests\manual\Microsoft.CmdPal.UI.ManualTests.dll `
    /TestCaseFilter:TestCategory=Manual /Logger:trx
```

The build embeds WinUI activation metadata in the private output copy of `testhost.exe` and copies the production XAML resources. Run the built DLL through VSTest so that host and its manifest are used.

Coverage includes consumed first-result resets followed by LoadMore, Home's late-result tracking, acknowledgement after direct selection, null-to-same-item reselection, a queued grid reset cancelled by user selection, and Back during deferred or interrupted publication.
