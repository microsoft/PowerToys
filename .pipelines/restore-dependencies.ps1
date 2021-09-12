# not using this but keeping around in case we need it in the future.  
# good use case here could be to set up a new machine, we just point people at it.
# https://github.com/microsoft/PowerToys/tree/master/doc/devdocs#prerequisites-for-compiling-powertoys

# improvements if this script is used to replace the snippet
# Add in a param for passive versus quiet.  Could be a IsSettingUpDevComputer true/false flag
    # default it to true which would be passive flag for normal people, false would set to quiet

$VS_DOWNLOAD_LINK = "https://aka.ms/vs/16/release/vs_buildtools.exe"
$VS_INSTALL_ARGS = @("--nocache","--quiet","--wait", 
    "--add Microsoft.VisualStudio.Workload.NativeDesktop", 
    "--add  Microsoft.VisualStudio.Workload.ManagedDesktop", 
    "--add Microsoft.VisualStudio.Workload.Universal", 
    "--add Microsoft.VisualStudio.ComponentGroup.UWP.VC", 
    "--add Microsoft.VisualStudio.Component.Windows10SDK.17134",
    "--add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre", 
    "--add Microsoft.NetCore.Component.Runtime.3.1",
    "--add Microsoft.VisualStudio.Component.VC.ATL.Spectre")

curl.exe --retry 3 -kL $VS_DOWNLOAD_LINK --output vs_installer.exe
if ($LASTEXITCODE -ne 0) {
    echo "Download of the VS 2019 installer failed"
    exit 1
}

$process = Start-Process "${PWD}\vs_installer.exe" -ArgumentList $VS_INSTALL_ARGS -NoNewWindow -Wait -PassThru
Remove-Item -Path vs_installer.exe -Force
$exitCode = $process.ExitCode
if (($exitCode -ne 0) -and ($exitCode -ne 3010)) {
    echo "VS 2019 installer exited with code $exitCode, which should be one of [0, 3010]."
    exit 1
}