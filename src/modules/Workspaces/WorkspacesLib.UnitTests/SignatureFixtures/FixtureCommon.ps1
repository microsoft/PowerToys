# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$FixtureExpectations = [ordered]@{
    'Unsigned.exe' = 'unsigned'
    'SelfSigned.exe' = 'certificate-untrusted'
    'TamperedSignature.exe' = 'invalid-signature'
    'ExpiredCertificate.exe' = 'certificate-untrusted'
    'WrongCertificateUsage.exe' = 'certificate-untrusted'
    'diagnostics\NotAnExecutable.exe' = 'verification-unavailable'
}

function Resolve-FixtureOutputDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $fullPath = [IO.Path]::GetFullPath($fullPath).TrimEnd('\')
    if ($fullPath -eq $RepoRoot.TrimEnd('\') -or
        $fullPath.StartsWith($RepoRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath -eq [IO.Path]::GetPathRoot($fullPath).TrimEnd('\')) {
        throw 'OutputDirectory must be a dedicated artifact directory outside the checkout, not a drive root.'
    }
    # Do not let an artifact junction redirect generation or cleanup into the source tree.
    $ancestor = $fullPath
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "OutputDirectory must use ordinary directories, not files or reparse points: $ancestor"
            }
        }
        $ancestor = Split-Path -Path $ancestor -Parent
    }
    return $fullPath
}
