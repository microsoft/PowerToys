$expirationDate = {Get-Date}.Invoke().AddYears(5)
$pass = ConvertTo-SecureString -String "12345" -Force -AsPlainText
$thumbprint = (New-SelfSignedCertificate -notafter $expirationDate -Type CodeSigningCert -Subject "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" -FriendlyName "PowerToys Test Certificate" -KeyDescription "PowerToys Test Certificate" -KeyFriendlyName "PowerToys Test Key" -KeyUsage "DigitalSignature" -CertStoreLocation Cert:\LocalMachine\My).Thumbprint
Export-PfxCertificate -Cert cert:\LocalMachine\My\$thumbprint -FilePath PowerToys_TemporaryKey.pfx -Password $pass
Import-PfxCertificate -CertStoreLocation Cert:\LocalMachine\Root -FilePath PowerToys_TemporaryKey.pfx -Password $pass
