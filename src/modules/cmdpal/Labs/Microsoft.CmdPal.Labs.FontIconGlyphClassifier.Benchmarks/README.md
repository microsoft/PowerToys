# FontIconGlyphClassifier Benchmarks

This project compares the native `Microsoft.Terminal.UI.FontIconGlyphClassifier`
against the managed `Microsoft.CmdPal.Common.Helpers.FontIconGlyphClassifier`
using BenchmarkDotNet.

It uses BenchmarkDotNet's in-process toolchain because the benchmark host
depends on native C++/WinRT projects that are built via MSBuild/DevShell rather
than the plain `dotnet` CLI.

Build:

```powershell
tools\build\build.ps1 -Platform x64 -Configuration Release -Path src\modules\cmdpal\Labs\Microsoft.CmdPal.Labs.FontIconGlyphClassifier.Benchmarks
```

Run:

```powershell
src\modules\cmdpal\Labs\Microsoft.CmdPal.Labs.FontIconGlyphClassifier.Benchmarks\bin\x64\Release\net9.0-windows10.0.26100.0\Microsoft.CmdPal.Labs.FontIconGlyphClassifier.Benchmarks.exe
```
