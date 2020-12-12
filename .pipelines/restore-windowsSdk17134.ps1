# Not using this but keeping around in case we need it in the future.  
# It will install 17134 and can be modified to support iso's.

[CmdletBinding()]
param([Parameter(Mandatory=$true, Position=0)]
      [string]$buildNumber)

# Ensure the error action preference is set to the default for PowerShell3, 'Stop'
$ErrorActionPreference = 'Stop'

# Constants
$WindowsSDKOptions = @("OptionId.UWPCpp", "OptionId.DesktopCPPx64", "OptionId.DesktopCPPx86", "OptionId.DesktopCPPARM64", "OptionId.DesktopCPPARM", "OptionId.WindowsDesktopDebuggers")
$WindowsSDKRegPath = "HKLM:\Software\WOW6432Node\Microsoft\Windows Kits\Installed Roots"
$WindowsSDKRegRootKey = "KitsRoot10"
$WindowsSDKVersion = "10.0.$buildNumber.0"
$WindowsSDKInstalledRegPath = "$WindowsSDKRegPath\$WindowsSDKVersion\Installed Options"
$StrongNameRegPath = "HKLM:\SOFTWARE\Microsoft\StrongName\Verification"
$PublicKeyTokens = @("31bf3856ad364e35")

if ($buildNumber -notmatch "^\d{5,}$")
{
    Write-Host "ERROR: '$buildNumber' doesn't look like a windows build number"
    Write-Host
    Exit 1
}

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

function Disable-StrongName
{
    param ([string] $publicKeyToken = "*")

    reg ADD "HKLM\SOFTWARE\Microsoft\StrongName\Verification\*,$publicKeyToken" /f | Out-Null
    if ($env:PROCESSOR_ARCHITECTURE -eq "AMD64")
    {
        reg ADD "HKLM\SOFTWARE\Wow6432Node\Microsoft\StrongName\Verification\*,$publicKeyToken" /f | Out-Null
    }
}

function Test-Admin
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal $identity
    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-RegistryPathAndValue
{
    param (
        [parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $path,
        [parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $value)

    try
    {
        if (Test-Path $path)
        {
            Get-ItemProperty -Path $path | Select-Object -ExpandProperty $value -ErrorAction Stop | Out-Null
            return $true
        }
    }
    catch
    {
    }

    return $false
}

function Test-InstallWindowsSDK
{
    $retval = $true

    if (Test-RegistryPathAndValue -Path $WindowsSDKRegPath -Value $WindowsSDKRegRootKey)
    {
        # A Windows SDK is installed
        # Is an SDK of our version installed with the options we need?
        $allRequiredSdkOptionsInstalled = $true
        foreach($sdkOption in $WindowsSDKOptions)
        {
            if (!(Test-RegistryPathAndValue -Path $WindowsSDKInstalledRegPath -Value $sdkOption))
            {
                $allRequiredSdkOptionsInstalled = $false
            }
        }

        if($allRequiredSdkOptionsInstalled)
        {
            # It appears we have what we need. Double check the disk
            $sdkRoot = Get-ItemProperty -Path $WindowsSDKRegPath | Select-Object -ExpandProperty $WindowsSDKRegRootKey
            if ($sdkRoot)
            {
                if (Test-Path $sdkRoot)
                {
                    $refPath = Join-Path $sdkRoot "References\$WindowsSDKVersion"
                    if (Test-Path $refPath)
                    {
                        $umdPath = Join-Path $sdkRoot "UnionMetadata\$WindowsSDKVersion"
                        if (Test-Path $umdPath)
                        {
                            # Pretty sure we have what we need
                            $retval = $false
                        }
                    }
                }
            }
        }
    }

    return $retval
}

function Test-InstallStrongNameHijack
{
    foreach($publicKeyToken in $PublicKeyTokens)
    {
        $key = "$StrongNameRegPath\*,$publicKeyToken"
        if (!(Test-Path $key))
        {
            return $true
        }
    }

    return $false
}

Write-Host -NoNewline "Checking for installed Windows SDK $WindowsSDKVersion..."
$InstallWindowsSDK = Test-InstallWindowsSDK
if ($InstallWindowsSDK)
{
    Write-Host "Installation required"
}
else
{
    Write-Host "INSTALLED"
}

$StrongNameHijack = Test-InstallStrongNameHijack
Write-Host -NoNewline "Checking if StrongName bypass required..."

if ($StrongNameHijack)
{
    Write-Host "REQUIRED"
}
else
{
    Write-Host "Done"
}

if ($StrongNameHijack -or $InstallWindowsSDK)
{
    if (!(Test-Admin))
    {
        Write-Host
        throw "ERROR: Elevation required"
    }
}

if ($InstallWindowsSDK)
{
    # Static(ish) link for Windows SDK
    # Note: there is a delay from Windows SDK announcements to availability via the static link
    # $uri = "https://software-download.microsoft.com/download/sg/Windows_InsiderPreview_SDK_en-us_$($buildNumber)_1.iso";

    # https://developer.microsoft.com/en-us/windows/downloads/sdk-archive/
    $uri = "https://go.microsoft.com/fwlink/p/?linkid=870807"

    if ($env:TEMP -eq $null)
    {
        $env:TEMP = Join-Path $env:SystemDrive 'temp'
    }

    $winsdkTempDir = Join-Path (Join-Path $env:TEMP ([System.IO.Path]::GetRandomFileName())) "WindowsSDK"

    if (![System.IO.Directory]::Exists($winsdkTempDir))
    {
        [void][System.IO.Directory]::CreateDirectory($winsdkTempDir)
    }

    # $file = "winsdk_$buildNumber.iso"
    $file = "winsdk_$buildNumber.exe"

    Write-Host -NoNewline "Getting WinSDK from $uri"
    $downloadFile = Download-File $winsdkTempDir $uri $file

    Write-Host -NoNewline "File is at $downloadFile"
    $downloadFileItem = Get-Item $downloadFile

    # Check to make sure the file is at least 10 MB.
    # if ($downloadFileItem.Length -lt 10*1024*1024)
    # {
    #     Write-Host
    #     Write-Host "ERROR: Downloaded file doesn't look large enough to be an ISO. The requested version may not be on microsoft.com yet."
    #     Write-Host
    #     Exit 1
    # }

    # TODO Check if zip, exe, iso, etc.
    try
    {
        Write-Host -NoNewLine "Installing WinSDK..."

        Start-Process -Wait $downloadFileItem "/features $WindowsSDKOptions /q"
        Write-Host "Done installing"
    }
    finally
    {
        Write-Host "Done"
    }
}

if ($StrongNameHijack)
{
    Write-Host -NoNewline "Disabling StrongName for Windows SDK..."

    foreach($key in $PublicKeyTokens)
    {
        Disable-StrongName $key
    }

    Write-Host "Done"
}