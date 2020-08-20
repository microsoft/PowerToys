#CleanUp tool 1.0
#Copyright (C) 2020 Microsoft Corporation
#Tool to clean PowerToys settings inside AppData folder and registry

$SettingsPath = $Env:LOCALAPPDATA + '\Microsoft\PowerToys'

#Deleting json settings files in %AppData%/Local/Microsoft/PowerToys.

if (Test-Path -Path $SettingsPath -PathType Any)
{
    Remove-Item –Path $SettingsPath –Recurse
}

$SuperFancyZones = "HKCU:\Software\SuperFancyZones"
$PowerRename = "HKCU:\Software\Microsoft\PowerRename"
$ImageResizer = "HKCU:\Software\Microsoft\ImageResizer"
$DontShowThisDialogAgain = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{e16ea82f-6d94-4f30-bb02-d6d911588afd}"

#Deleting SuperFancyZones registry key

if (Test-Path -Path $SuperFancyZones -PathType Any)
{
    Remove-Item –Path $SuperFancyZones –Recurse
}

#Deleting PowerRename registry key

if (Test-Path -Path $PowerRename -PathType Any)
{
    Remove-Item –Path $PowerRename –Recurse
}

#Deleting ImageResizer registry key

if (Test-Path -Path $ImageResizer -PathType Any)
{
    Remove-Item –Path $ImageResizer –Recurse
}

#Deleting DontShowThisDialogAgain registry key

if (Test-Path -Path $DontShowThisDialogAgain -PathType Any)
{
    Remove-Item –Path $DontShowThisDialogAgain
}
