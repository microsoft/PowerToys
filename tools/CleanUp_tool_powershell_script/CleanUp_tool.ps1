#CleanUp tool 1.0
#Copyright (C) 2022 Microsoft Corporation
#Tool to clean PowerToys settings inside AppData folder and registry

#Deleting json settings files in %AppData%/Local/Microsoft/PowerToys.

[String]$SettingsPath = $Env:LOCALAPPDATA + '\Microsoft\PowerToys'

if (Test-Path -Path $SettingsPath -PathType Any)
{
    Remove-Item -Path $SettingsPath -Recurse
}

#Deleting SuperFancyZones registry key

[String]$SuperFancyZones = "HKCU:\Software\SuperFancyZones"

if (Test-Path -Path $SuperFancyZones -PathType Any)
{
    Remove-Item -Path $SuperFancyZones -Recurse
}

#Deleting PowerRename registry key

[String]$PowerRename = "HKCU:\Software\Microsoft\PowerRename"

if (Test-Path -Path $PowerRename -PathType Any)
{
    Remove-Item -Path $PowerRename -Recurse
}

#Deleting ImageResizer registry key

[String]$ImageResizer = "HKCU:\Software\Microsoft\ImageResizer"

if (Test-Path -Path $ImageResizer -PathType Any)
{
    Remove-Item -Path $ImageResizer -Recurse
}

#Deleting DontShowThisDialogAgain registry key

[String]$DontShowThisDialogAgain = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{e16ea82f-6d94-4f30-bb02-d6d911588afd}"

if (Test-Path -Path $DontShowThisDialogAgain -PathType Any)
{
    Remove-Item -Path $DontShowThisDialogAgain
}
