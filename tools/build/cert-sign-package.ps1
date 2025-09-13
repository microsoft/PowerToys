param (
    [string]$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
    [string[]]$TargetPaths = "C:\PowerToys\ARM64\Release\WinUI3Apps\CmdPal\AppPackages\Microsoft.CmdPal.UI_0.0.1.0_arm64_Test\Microsoft.CmdPal.UI_0.0.1.0_arm64.msix"
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
    & signtool sign /sha1 $($cert.Thumbprint) /fd SHA256 /t http://timestamp.digicert.com "$filePath"
}