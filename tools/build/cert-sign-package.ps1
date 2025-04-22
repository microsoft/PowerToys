param (
    [string]$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
    [string[]]$TargetPaths
)

. "$PSScriptRoot\cert-management.ps1"
$cert = EnsureCertificate -certSubject $certSubject

if (-not $cert) {
    Write-Error "❌ Failed to prepare certificate."
    exit 1
}

Write-Host "✔️ Certificate ready: $($cert.Thumbprint)"

if (-not $TargetPaths -or $TargetPaths.Count -eq 0) {
    Write-Error "❌ No target files provided to sign."
    exit 1
}

foreach ($filePath in $TargetPaths) {
    if (-not (Test-Path $filePath)) {
        Write-Warning "⚠️ Skipping: File does not exist - $filePath"
        continue
    }

    Write-Host "🔏 Signing: $filePath"
    try {
        & signtool sign /sha1 $($cert.Thumbprint) /fd SHA256 /t http://timestamp.digicert.com "$filePath"
    }  
    catch {
        Write-Host "`n❌ Failed to sign: $filePath"
        Write-Host $_    
    }
}

Write-Host "`n✅ Signing process completed."