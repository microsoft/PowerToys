param (
    [string]$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
    [string[]]$TargetPaths = "C:\Program Files\PowerToys\WinUI3Apps\CmdPal\Microsoft.CmdPal.UI_0.0.1.0_x64.msix"
)

. "$PSScriptRoot\cert-management.ps1"
$cert = EnsureCertificate -certSubject $certSubject

if (-not $cert) {
    Write-Error "Failed to prepare certificate."
    exit 1
}

Write-Host "Certificate ready: $($cert.Thumbprint)"

if (-not $TargetPaths -or $TargetPaths.Count -eq 0) {
    Write-Error "No target files provided to sign."
    exit 1
}

foreach ($filePath in $TargetPaths) {
    if (-not (Test-Path $filePath)) {
        Write-Warning "Skipping: File does not exist - $filePath"
        continue
    }

    Write-Host "Signing: $filePath"
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign /sha1 $($cert.Thumbprint) /fd SHA256 /t http://timestamp.digicert.com "$filePath"
}