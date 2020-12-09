[CmdletBinding()]
param([Parameter(Mandatory=$false, Position=0)]
      [string]$vsInstall = "enterprise")

function Test-Admin
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal $identity
    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (!(Test-Admin))
{
    Write-Host
    throw "ERROR: Elevation required"
}
else
{
    Write-Host "CI has admin"
}

try
{
    Write-Host "Installing VS with workloads needed..."

    $vsPath = ${Env:ProgramFiles(x86)} + "\Microsoft Visual Studio\"
    $vsInstallPath = $vsPath + "2019\" + $vsInstall 
    $vsInstallerPath = $vsPath + "\Installer\vs_installer.exe"

    Write-Host "Paths:"
    Write-Host $vsPath
    Write-Host $vsInstallPath
    Write-Host $vsInstallerPath
    
    # https://docs.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2019
    # https://docs.microsoft.com/en-us/visualstudio/install/command-line-parameter-examples?view=vs-2019#using---wait

    $process = Start-Process -FilePath $vsInstallerPath -ArgumentList "modify", "--installPath `"${vsInstallPath}`"", "--add Microsoft.VisualStudio.Workload.NativeDesktop", "--add  Microsoft.VisualStudio.Workload.ManagedDesktop", "--add Microsoft.VisualStudio.Workload.Universal", "--add Microsoft.VisualStudio.ComponentGroup.UWP.VC", "--add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre", "--add Microsoft.VisualStudio.Component.VC.ATL.Spectre", "--quiet" -Wait -PassThru
    Write-Host $process.ExitCode 
}
finally
{
    Write-Host "Done"
}