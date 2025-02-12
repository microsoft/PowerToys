[CmdletBinding()]
# todo: send in arch / conf, could send in actual path
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [AllowEmptyString()]
    [string]$targetDir = $PSScriptRoot + '/../extractedMsi/File'
)

$DirPath = $targetDir; #this file is in pipeline, we need root.
$items = Get-ChildItem -Path $DirPath -File -Include *.exe, *.dll, *.ttf, PTCustomActions -Recurse -Force -ErrorAction SilentlyContinue
$versionExceptions = @(
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
    "WinRT.Host.Shim.dll") -join '|';
$nullVersionExceptions = @(
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
    "Microsoft.Windows.Widgets.dll") -join '|';
$totalFailure = 0;

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
    else {
        $auth = Get-AuthenticodeSignature $_.FullName
        if ($auth.SignerCertificate -eq $null) {
            Write-Host "Not Signed: " + $_.FullName
            $totalFailure++;
        }
    }
}

if ($totalFailure -gt 0) {
    Write-Error "Some items had issues."
    exit 1
}

exit 0
