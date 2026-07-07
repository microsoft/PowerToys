# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Greet (PowerShell) — demonstrates prompted PowerScript parameters.
# PowerScripts passes each chosen value as a -Name argument. Values arrive as strings, so the
# boolean parameter is compared against the literal 'true'.

param(
    [string]$greeting = "Hello",
    [string]$name = "World",
    [string]$shout = "false"
)

$message = "$greeting, $name!"
if ($shout -eq "true") {
    $message = $message.ToUpper()
}

Add-Type -AssemblyName System.Windows.Forms | Out-Null
[System.Windows.Forms.MessageBox]::Show($message, "PowerScripts — greet") | Out-Null

Write-Output $message
