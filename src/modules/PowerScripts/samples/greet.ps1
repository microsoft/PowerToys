# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Greet (PowerShell) — demonstrates consumer-rendered PowerScript parameters. Metadata (including the
# parameters) lives in the sibling greet.ps1.tool.json descriptor (an MCP Tool). PowerScripts passes
# each chosen value as a -Name argument. Values arrive as strings, so the boolean parameter is
# compared against the literal 'true'.

param(
    [string]$greeting = "Hello",
    [string]$name = "World",
    [string]$shout = "false"
)

$message = "$greeting, $name!"
if ($shout -eq "true") {
    $message = $message.ToUpper()
}

# Show the result on top. A plain [MessageBox]::Show has no topmost option and can open behind the
# foreground window, so call user32 MessageBox directly. MB_TOPMOST alone is unreliable, so combine
# MB_SYSTEMMODAL (0x1000) | MB_SETFOREGROUND (0x10000) | MB_TOPMOST (0x40000) to force it on top.
Add-Type -Namespace PowerScripts -Name NativeUi -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);
'@
$MB_TOPMOST_FLAGS = 0x00051000
[PowerScripts.NativeUi]::MessageBox([System.IntPtr]::Zero, $message, "PowerScripts - greet", $MB_TOPMOST_FLAGS) | Out-Null

Write-Output $message
