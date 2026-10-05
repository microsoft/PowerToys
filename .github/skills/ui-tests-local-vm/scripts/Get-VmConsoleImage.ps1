# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Saves the Hyper-V guest console as a PNG so it can be read without VMConnect.

.DESCRIPTION
A Hyper-V guest console is normally only visible through VMConnect, which an agent cannot read. The
hypervisor exposes the framebuffer through
Msvm_VirtualSystemManagementService.GetVirtualSystemThumbnailImage, which is enough to read boot
errors, Setup progress, and whether the expected desktop is on screen.

The thumbnail is RGB565; this converts it to PNG. Requires Hyper-V access on the host.

.EXAMPLE
pwsh ./Get-VmConsoleImage.ps1 -VmName PowerToysUiTest-Win11 -Path X:\evidence\console.png
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VmName,
    [Parameter(Mandatory)][string]$Path,
    [ValidateRange(64, 1920)][int]$Width = 1024,
    [ValidateRange(64, 1200)][int]$Height = 768
)

$ErrorActionPreference = 'Stop'

$namespace = 'root\virtualization\v2'
$service = Get-CimInstance -Namespace $namespace -ClassName Msvm_VirtualSystemManagementService
$system = Get-CimInstance -Namespace $namespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$VmName'"
if ($null -eq $system) {
    throw "Virtual machine '$VmName' was not found."
}
$settings = Get-CimAssociatedInstance -InputObject $system -ResultClassName Msvm_VirtualSystemSettingData |
    Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' } |
    Select-Object -First 1

$result = Invoke-CimMethod -InputObject $service -MethodName GetVirtualSystemThumbnailImage -Arguments @{
    TargetSystem = [ciminstance]$settings
    WidthPixels  = [uint16]$Width
    HeightPixels = [uint16]$Height
}
if ($result.ReturnValue -ne 0) {
    throw "GetVirtualSystemThumbnailImage failed with return value $($result.ReturnValue)."
}
$rgb565 = $result.ImageData
if ($null -eq $rgb565 -or $rgb565.Length -eq 0) {
    throw "The hypervisor returned an empty thumbnail for '$VmName'. The guest is probably off."
}

Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
try {
    $data = $bitmap.LockBits(
        [System.Drawing.Rectangle]::new(0, 0, $Width, $Height),
        [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $row = [byte[]]::new($data.Stride)
        for ($y = 0; $y -lt $Height; $y++) {
            for ($x = 0; $x -lt $Width; $x++) {
                $index = (($y * $Width) + $x) * 2
                if ($index + 1 -ge $rgb565.Length) { break }
                $pixel = [int]$rgb565[$index] -bor ([int]$rgb565[$index + 1] -shl 8)
                $offset = $x * 3
                # 24bpp bitmaps are stored blue, green, red.
                $row[$offset] = [byte]((($pixel -band 0x1F) * 255) / 31)
                $row[$offset + 1] = [byte](((($pixel -shr 5) -band 0x3F) * 255) / 63)
                $row[$offset + 2] = [byte](((($pixel -shr 11) -band 0x1F) * 255) / 31)
            }
            [Runtime.InteropServices.Marshal]::Copy(
                $row, 0, [IntPtr]($data.Scan0.ToInt64() + ($y * $data.Stride)), $data.Stride)
        }
    }
    finally {
        $bitmap.UnlockBits($data)
    }

    New-Item (Split-Path $Path -Parent) -ItemType Directory -Force | Out-Null
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $bitmap.Dispose()
}

[pscustomobject]@{
    VmName = $VmName
    Path = (Resolve-Path $Path).Path
    Width = $Width
    Height = $Height
    Bytes = (Get-Item $Path).Length
} | ConvertTo-Json
