# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [Parameter(Mandatory = $true)]
    [string]$file
)

$item = Get-Item -LiteralPath $file -ErrorAction Stop
$message = @"
Name: $($item.Name)
Folder: $($item.DirectoryName)
Size: $($item.Length) bytes
"@

Add-Type -Namespace PowerScripts -Name NativeUi -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);
'@

[PowerScripts.NativeUi]::MessageBox(
    [System.IntPtr]::Zero,
    $message,
    "PowerScripts - inspect file",
    0x00051000) | Out-Null

Write-Output $item.FullName
