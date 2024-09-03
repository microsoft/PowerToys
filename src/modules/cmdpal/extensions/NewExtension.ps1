Param(
    [string]$Name = "MyNewExtension",
    [string]$DisplayName = "My new command palette extension",
    [switch]$Help = $false
),

$StartTime = Get-Date

if ($Help) {
  Write-Host @"
Copyright (c) Microsoft Corporation.
Licensed under the MIT License.

Syntax:
      NewExtension.ps1 [options]

Description:
      Creates a new Command Palette extension.

Options:

  -Name <projectName>
      The new name for your project. This will be used to name the folder,
      classes, package, etc. This should not include spaces or special characters.
      Example: -Name MyCoolExtension

  -DisplayName <a friendly display name>
      A display name for your extension
      Example: -DisplayName "My really cool new extension"

  -Help
      Display this usage message.
"@
  Exit
}

if(-not $Name) {
  Write-Host "You must specify a name for your extension. Use -Name <projectName> to specify the name." -ForegroundColor RED
  Exit
}

$Name = $Name -replace " ", ""
$NewGuid = [guid]::NewGuid().ToString()


$ExtensionRoot = Join-Path $PSScriptRoot $Name
$TemplateRoot = Join-Path $PSScriptRoot "TemplateExtension"

if(Test-Path $ExtensionRoot) {
  Write-Host "The folder $Name already exists. Please specify a different name." -ForegroundColor RED
  Exit
}

Write-Host "Creating new extension $Name"

# Get all the folders and files tracked in git in the template
$TemplateFiles = git ls-files $TemplateRoot

# Create the new extension folder
New-Item -ItemType Directory -Path $ExtensionRoot -Force | Out-Null

# Copy all the files from the template to the new extension
foreach($file in $TemplateFiles) {
  $RelativePath = $file -replace "$TemplateRoot\\", ""
  $DestinationPath = Join-Path $ExtensionRoot $RelativePath
  $DestinationFolder = Split-Path $DestinationPath -Parent
  if(-not (Test-Path $DestinationFolder)) {
    New-Item -ItemType Directory -Path $DestinationFolder -Force | Out-Null
  }
  Copy-Item -Path $file -Destination $DestinationPath -Force
}

# Replace all the placeholders in the files
$Files = Get-ChildItem -Path $ExtensionRoot -Recurse -File
foreach($file in $Files) {
  $Content = Get-Content -Path $file.FullName
  $Content = $Content -replace "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", $NewGuid
  $Content = $Content -replace "TemplateDisplayName", $DisplayName
  $Content = $Content -replace "TemplateExtension", $Name
  Set-Content -Path $file.FullName -Value $Content
}

Write-Host "Extension created in $ExtensionRoot" -ForegroundColor GREEN

$TotalTime = (Get-Date)-$StartTime
$TotalMinutes = [math]::Floor($TotalTime.TotalMinutes)
$TotalSeconds = [math]::Ceiling($TotalTime.TotalSeconds)
Write-Host @"
Total Running Time:
$TotalMinutes minutes and $TotalSeconds seconds
"@ -ForegroundColor CYAN
