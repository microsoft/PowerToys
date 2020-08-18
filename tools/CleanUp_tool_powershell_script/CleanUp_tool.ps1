$SettingsPath = $Env:LOCALAPPDATA + '\Microsoft\PowerToys'

if (Test-Path -Path $SettingsPath -PathType Any)
{
    Remove-Item –Path $SettingsPath –Recurse
}

$SuperFancyZones = "HKCU:\Software\SuperFancyZones"
$PowerRename = "HKCU:\Software\Microsoft\PowerRename"
$ImageResizer = "HKCU:\Software\Microsoft\ImageResizer"
$DontShowThisDialogAgain = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain"

if (Test-Path -Path $SuperFancyZones -PathType Any)
{
    Remove-Item –Path $SuperFancyZones –Recurse
}

if (Test-Path -Path $PowerRename -PathType Any)
{
    Remove-Item –Path $PowerRename –Recurse
}

if (Test-Path -Path $ImageResizer -PathType Any)
{
    Remove-Item –Path $ImageResizer –Recurse
}

if (Test-Path -Path $DontShowThisDialogAgain -PathType Any)
{
    Remove-Item –Path $DontShowThisDialogAgain –Recurse
}
