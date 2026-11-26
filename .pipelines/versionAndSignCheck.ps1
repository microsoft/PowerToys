[CmdletBinding()]
# todo: send in arch / conf, could send in actual path
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [AllowEmptyString()]
    [string]$targetDir = $PSScriptRoot + '/../extractedMsi/File',
    [switch]$WinmdOnly
)

$DirPath = $targetDir; #this file is in pipeline, we need root.
$filePatterns = @('*.exe', '*.dll', '*.winmd', '*.ttf', 'PTCustomActions')
if ($WinmdOnly) {
    # MSIX payloads need the same metadata signature check without MSI version rules.
    $filePatterns = @('*.winmd')
}
$items = Get-ChildItem -Path $DirPath -File -Include $filePatterns -Recurse -Force -ErrorAction SilentlyContinue
$versionExceptions = @(
    "AdaptiveCards.Templating.dll",
    "Microsoft.Windows.ApplicationModel.DynamicDependency.Projection.dll",
    "Microsoft.Windows.ApplicationModel.Resources.Projection.dll",
    "Microsoft.Windows.ApplicationModel.WindowsAppRuntime.Projection.dll",
    "Microsoft.Windows.AppLifecycle.Projection.dll",
    "Microsoft.Windows.System.Power.Projection.dll",
    "Microsoft.Windows.Widgets.Providers.Projection.dll",
    "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll",
    "Microsoft.Xaml.Interactions.dll",
    "Microsoft.Xaml.Interactivity.dll",
    "hyjiacan.py4n.dll",
    "TraceReloggerLib.dll",
    "Microsoft.WindowsAppRuntime.Release.Net.dll",
    "Microsoft.Windows.Widgets.Projection.dll",
    "WinRT.Host.Shim.dll",
    "WyHash.dll",
    "Microsoft.Recognizers.Text.DataTypes.TimexExpression.dll",
    "ObjectModelCsProjection.dll",
    "RendererCsProjection.dll",
    "Microsoft.ML.OnnxRuntime.dll") -join '|';
$nullVersionExceptions = @(
    "SkiaSharp.Views.WinUI.Native.dll",
    "libSkiaSharp.dll",
    "codicon.ttf",
    "e_sqlite3.dll",
    "getfilesiginforedist.dll",
    "vcamp140_app.dll",
    "vcruntime140_app.dll",
    "vcruntime140_1_app.dll",
    "msvcp140_app.dll",
    "marshal.dll",
    "Microsoft.Toolkit.Win32.UI.XamlHost.dll",
    "Microsoft.UI.Composition.OSSupport.dll",
    "Microsoft.UI.Windowing.dll",
    "Microsoft.UI.Xaml.Internal.dll",
    "Microsoft.Windows.ApplicationModel.Resources.dll",
    "Microsoft.WindowsAppRuntime.dll",
    "Microsoft.WindowsAppRuntime.Bootstrap.dll",
    "MRM.dll",
    "PushNotificationsLongRunningTask.ProxyStub.dll",
    "WindowsAppSdk.AppxDeploymentExtensions.Desktop.dll",
    "System.Diagnostics.EventLog.Messages.dll",
    "Microsoft.Windows.Widgets.dll",
    "AdaptiveCards.ObjectModel.WinUI3.dll",
    "AdaptiveCards.Rendering.WinUI3.dll"
    ) -join '|';
$totalFailure = 0;

$monacoFontName = 'MonacoPreviewHandlerMonacoAssetsFiles_File_codicon.ttf'
# Raw font from the lockfile-verified monaco-editor 0.57.0 package; Windows does not verify its format with Authenticode.
$monacoFontSha256 = '77F902557231F95C4C71AE517D09A1A842753656F5F665086B3E5AD8095538DA'

Write-Host $DirPath;

if (-not (Test-Path $DirPath)) {
    Write-Error "Folder does not exist!"
}

Write-Host "Total items: " $items.Count

if ($items.Count -eq 0) {
    # no items means something bad happened.  We should fail ASAP
    exit 1;
}

$items | ForEach-Object {
    if ($_.Name -eq $monacoFontName) {
        $digest = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 -ErrorAction Stop
        if ($digest.Hash -ne $monacoFontSha256) {
            Write-Host "Monaco font SHA256 mismatch: $($_.FullName)"
            $totalFailure++
        }
        return
    }

    # WinRT metadata does not require a FileVersion resource, but still needs a valid signature.
    if ($_.Extension -ne '.winmd') {
        if ($_.VersionInfo.FileVersion -eq "0.0.0.0" -and $_.Name -notmatch $versionExceptions) {
            # These items are exceptions that actually have the 0.0.0.0 version.
            Write-Host "Version set to 0.0.0.0: " + $_.FullName
            $totalFailure++;
        }
        if ($_.VersionInfo.FileVersion -eq "1.0.0.0" -and $_.Name -notmatch $versionExceptions) {
            # These items are exceptions that actually have the 1.0.0.0 version.
            Write-Host "Version set to 1.0.0.0: " + $_.FullName
            $totalFailure++;
        }
        elseif ($_.VersionInfo.FileVersion -eq $null -and $_.Name -notmatch $nullVersionExceptions) {
            # These items are exceptions that actually a version not set.
            Write-Host "Version not set: " + $_.FullName
            $totalFailure++;
        }
    }

    $auth = Get-AuthenticodeSignature -LiteralPath $_.FullName
    if ($null -eq $auth.SignerCertificate -or $auth.Status -ne 'Valid') {
        Write-Host "Invalid signature ($($auth.Status)): $($_.FullName)"
        $totalFailure++;
    }
}

if ($totalFailure -gt 0) {
    Write-Error "Some items had issues."
    exit 1
}

exit 0
