[CmdletBinding()]
param([Parameter(Mandatory=$false, Position=0)]
      [string]$vsInstall = "enterprise")

try
{
    Write-Host "Installing VS installer..."

    $vsPath = ${Env:ProgramFiles(x86)} + "\Microsoft Visual Studio\"
    $vsInstallPath = $vsPath + "2019\" + $vsInstall 
    $vsInstallerPath = $vsPath + "\Installer\vs_installer.exe"
    Write-Host $vsPath
    
    # https://docs.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2019
    # https://docs.microsoft.com/en-us/visualstudio/install/command-line-parameter-examples?view=vs-2019#using---wait

    $process = Start-Process -FilePath $vsInstallerPath -ArgumentList "modify", "--installPath `"${vsInstallPath}`"", "--add Microsoft.VisualStudio.Workload.NativeDesktop", "--add  Microsoft.VisualStudio.Workload.ManagedDesktop", "--add Microsoft.VisualStudio.Workload.Universal", "--add Microsoft.VisualStudio.Component.Windows10SDK.17134", "--add Microsoft.VisualStudio.ComponentGroup.UWP.VC", "--add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre", "--add Microsoft.VisualStudio.Component.VC.ATL.Spectre", "--quiet" -Wait -PassThru
    Write-Host $process.ExitCode 
}
finally
{
    Write-Host "Done"
}


