# [CmdletBinding()]
# param([Parameter(Mandatory=$false, Position=0)]
#       [string]$vsInstall = "enterprise")

# function Test-Admin
# {
#     $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
#     $principal = New-Object Security.Principal.WindowsPrincipal $identity
#     $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
# }

# if (!(Test-Admin))
# {
#     Write-Host
#     throw "ERROR: Elevation required"
# }
# else
# {
#     Write-Host "CI has admin"
# }

# try
# {
#     Write-Host "Installing VS with workloads needed..."

#     $vsPath = ${Env:ProgramFiles(x86)} + "\Microsoft Visual Studio\"
#     $vsInstallPath = $vsPath + "2019\" + $vsInstall 
#     $vsInstallerPath = $vsPath + "\Installer\vs_installer.exe"

#     Write-Host "Paths:"
#     Write-Host $vsPath
#     Write-Host $vsInstallPath
#     Write-Host $vsInstallerPath
    
#     # https://docs.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2019
#     # https://docs.microsoft.com/en-us/visualstudio/install/command-line-parameter-examples?view=vs-2019#using---wait
#     # https://docs.microsoft.com/en-us/visualstudio/install/workload-and-component-ids?view=vs-2019
#     # https://docs.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-enterprise?view=vs-2019&preserve-view=true

#     $process = Start-Process -FilePath $vsInstallerPath -ArgumentList "modify", "--installPath `"${vsInstallPath}`"", 
#     , "--quiet" -Wait -PassThru
#     Write-Host $process.ExitCode 
# }
# finally
# {
#     Write-Host "Done"
# }

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