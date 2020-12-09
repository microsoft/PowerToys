[CmdletBinding()]

function Download-File
{
    param ([string] $outDir,
           [string] $downloadUrl,
           [string] $downloadName)

    $downloadPath = Join-Path $outDir "$downloadName.download"
    $downloadDest = Join-Path $outDir $downloadName
    $downloadDestTemp = Join-Path $outDir "$downloadName.tmp"

    Write-Host -NoNewline "Downloading $downloadName..."

    $retries = 10
    $downloaded = $false
    while (-not $downloaded)
    {
        try
        {
            $webclient = new-object System.Net.WebClient
            $webclient.DownloadFile($downloadUrl, $downloadPath)
            $downloaded = $true
        }
        catch [System.Net.WebException]
        {
            Write-Host
            Write-Warning "Failed to fetch updated file from $downloadUrl : $($error[0])"
            if (!(Test-Path $downloadDest))
            {
                if ($retries -gt 0)
                {
                    Write-Host "$retries retries left, trying download again"
                    $retries--
                    start-sleep -Seconds 10
                }
                else
                {
                    throw "$downloadName was not found at $downloadDest"
                }
            }
            else
            {
                Write-Warning "$downloadName may be out of date"
            }
        }
    }

    Unblock-File $downloadPath

    $downloadDestTemp = $downloadPath;

    # Delete and rename to final dest
    Write-Host "testing $downloadDest"
    if (Test-Path $downloadDest)
    {
        Write-Host "Deleting: $downloadDest"
        Remove-Item $downloadDest -Force
    }

    Move-Item -Force $downloadDestTemp $downloadDest
    Write-Host "Done"

    return $downloadDest
}


function Test-Admin
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal $identity
    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$uri = "https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=enterprise"

if ($env:TEMP -eq $null)
{
    $env:TEMP = Join-Path $env:SystemDrive 'temp'
}

$vsTempDir = Join-Path (Join-Path $env:TEMP ([System.IO.Path]::GetRandomFileName())) "vs_installer"

if (![System.IO.Directory]::Exists($vsTempDir))
{
    [void][System.IO.Directory]::CreateDirectory($vsTempDir)
}

$file = "vs_enterprise.exe"

Write-Host -NoNewline "Getting VS installer from $uri"
$downloadFile = Download-File $vsTempDir $uri $file

Write-Host -NoNewline "File is at $downloadFile"
$downloadFileItem = Get-Item $downloadFile

# TODO Check if zip, exe, iso, etc.
try
{
    Write-Host -NoNewLine "Installing VS installer..."
    $vsInstall = "enterprise"
    $vsPath = ${Env:ProgramFiles(x86)} + "\Microsoft Visual Studio\2019\" + $vsInstall

    # https://docs.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2019
    # https://docs.microsoft.com/en-us/visualstudio/install/command-line-parameter-examples?view=vs-2019#using---wait
    
    $process = Start-Process -FilePath vs_installer.exe -ArgumentList "--modify", "--installPath `"${vsPath}`"", "--add Microsoft.VisualStudio.Workload.NativeDesktop", "--add  Microsoft.VisualStudio.Workload.ManagedDesktop", "--add Microsoft.VisualStudio.Workload.Universal", "--add Microsoft.VisualStudio.Component.Windows10SDK.17134", "--add Microsoft.VisualStudio.ComponentGroup.UWP.VC", "--add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre", "--add Microsoft.VisualStudio.Component.VC.ATL.Spectre", "--quiet" -Wait -PassThru
    Write-Host $process.ExitCode 
}
finally
{
    Write-Host "Done"
}


