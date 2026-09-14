# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Read-FixtureDerElement {
    param([byte[]]$Bytes, [int]$Offset)
    if ($Offset -lt 0 -or $Offset + 2 -gt $Bytes.Length) {
        throw 'Truncated fixture ASN.1 header.'
    }
    $headerLength = 2
    [long]$length = $Bytes[$Offset + 1]
    if (($length -band 0x80) -ne 0) {
        $count = $length -band 0x7f
        if ($count -eq 0 -or $count -gt 4 -or $Offset + 2 + $count -gt $Bytes.Length) {
            throw 'Unsupported fixture ASN.1 length.'
        }
        $length = 0
        for ($index = 0; $index -lt $count; $index++) {
            $length = ($length -shl 8) -bor $Bytes[$Offset + 2 + $index]
        }
        $headerLength += $count
    }
    if ($length -gt [int]::MaxValue -or $Offset + $headerLength + $length -gt $Bytes.Length) {
        throw 'Fixture ASN.1 length exceeds its buffer.'
    }
    [pscustomobject]@{
        Tag = [int]$Bytes[$Offset]
        Offset = $Offset
        Content = $Offset + $headerLength
        End = $Offset + $headerLength + [int]$length
    }
}

function Convert-FixtureCmsEncoding {
    param([byte[]]$Bytes, [int]$FromTag, [int]$ToTag)
    $copy = [byte[]]$Bytes.Clone()
    $outer = Read-FixtureDerElement $copy 0
    $oid = Read-FixtureDerElement $copy $outer.Content
    $wrapper = Read-FixtureDerElement $copy $oid.End
    $signedData = Read-FixtureDerElement $copy $wrapper.Content
    $version = Read-FixtureDerElement $copy $signedData.Content
    $algorithms = Read-FixtureDerElement $copy $version.End
    $contentInfo = Read-FixtureDerElement $copy $algorithms.End
    $contentOid = Read-FixtureDerElement $copy $contentInfo.Content
    $contentWrapper = Read-FixtureDerElement $copy $contentOid.End
    $content = Read-FixtureDerElement $copy $contentWrapper.Content
    if ($outer.Tag -ne 0x30 -or $oid.Tag -ne 6 -or $wrapper.Tag -ne 0xa0 -or
        $signedData.Tag -ne 0x30 -or $version.Tag -ne 2 -or $algorithms.Tag -ne 0x31 -or
        $contentInfo.Tag -ne 0x30 -or $contentOid.Tag -ne 6 -or $contentWrapper.Tag -ne 0xa0 -or
        $content.Tag -ne $FromTag -or $content.End -ne $contentWrapper.End) {
        throw 'Unexpected fixture SignedData layout.'
    }
    # Legacy Authenticode hashes the contents of a SEQUENCE; CMS exposes those same bytes as an OCTET STRING.
    $copy[$content.Offset] = [byte]$ToTag
    if ($ToTag -eq 0x30) {
        if ($version.End - $version.Content -ne 1 -or $copy[$version.Content] -notin @(1, 3)) {
            throw 'Unexpected SignedData version encoding.'
        }
        # CMS uses version 3 for this content OID; the legacy PKCS#7 Authenticode envelope uses version 1.
        $copy[$version.Content] = 1
    }
    return ,$copy
}

function Set-FixtureCmsSignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )

    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    $bytes = [IO.File]::ReadAllBytes($Path)
    $stream = [IO.MemoryStream]::new($bytes, $false)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        $optionalHeader = $peOffset + 24
        $stream.Position = $optionalHeader
        if ($reader.ReadUInt16() -ne 0x20b) {
            throw 'This fixture helper accepts only its own x64 PE template.'
        }
        $securityDirectory = $optionalHeader + 112 + (8 * 4)
        $stream.Position = $securityDirectory
        $certificateOffset = $reader.ReadUInt32()
        $certificateSize = $reader.ReadUInt32()
        if ($certificateOffset -eq 0 -or $certificateOffset + $certificateSize -ne $bytes.Length) {
            throw 'The fixture certificate table must be the final region in the template.'
        }
        $stream.Position = $certificateOffset
        $entryLength = $reader.ReadUInt32()
        $revision = $reader.ReadUInt16()
        $type = $reader.ReadUInt16()
        if ($revision -ne 0x200 -or $type -ne 2 -or $entryLength -lt 8 -or $entryLength -gt $certificateSize) {
            throw 'The template does not have the expected PKCS#7 certificate entry.'
        }
        $encoded = $reader.ReadBytes([int]$entryLength - 8)
    }
    finally {
        $reader.Dispose()
    }

    $cms = [Security.Cryptography.Pkcs.SignedCms]::new()
    $cms.Decode((Convert-FixtureCmsEncoding $encoded 0x30 4))
    $cms.CheckSignature($true)
    if ($cms.ContentInfo.ContentType.Value -ne '1.3.6.1.4.1.311.2.1.4') {
        throw 'The template does not contain Authenticode indirect data.'
    }
    while ($cms.SignerInfos.Count -gt 0) {
        $cms.RemoveSignature(0)
    }
    foreach ($oldCertificate in @($cms.Certificates)) {
        $cms.RemoveCertificate($oldCertificate)
    }
    $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($Certificate)
    $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
    $signer.IncludeOption = [Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
    # Test-only: sign the unchanged Authenticode content without requiring a currently acceptable certificate.
    $cms.ComputeSignature($signer, $true)
    $cms.CheckSignature($true)
    $signature = Convert-FixtureCmsEncoding ($cms.Encode()) 4 0x30
    $entryLength = $signature.Length + 8
    $alignedLength = ($entryLength + 7) -band -8

    $output = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write($bytes, 0, [int]$certificateOffset)
        $writer.Write([uint32]$entryLength)
        $writer.Write([uint16]0x200)
        $writer.Write([uint16]2)
        $writer.Write($signature)
        for ($index = $entryLength; $index -lt $alignedLength; $index++) {
            $writer.Write([byte]0)
        }
        $output.Position = $securityDirectory + 4
        $writer.Write([uint32]$alignedLength)
        $writer.Flush()
        [IO.File]::WriteAllBytes($Path, $output.ToArray())
    }
    finally {
        $writer.Dispose()
    }
}
