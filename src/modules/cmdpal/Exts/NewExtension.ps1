Param(
    [string]$Name = "MyNewExtension",
    [string]$DisplayName = "My new command palette extension",
    [switch]$Help = $false,
    [switch]$Verbose = $false
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

Examples:

  .\extensions\NewExtension.ps1 -name MastodonExtension -DisplayName "Mastodon extension for cmdpal"
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
$TemplateFiles = git ls-files --full-name $TemplateRoot

# Create the new extension folder
New-Item -ItemType Directory -Path $ExtensionRoot -Force | Out-Null

# $TemplateFiles will be relative to the git root. That's something like
# src/modules/cmdpal/extensions/TemplateExtension/

$gitRoot = git rev-parse --show-toplevel

# Copy all the files from the template to the new extension
foreach($file in $TemplateFiles) {
  $RelativePath = (Join-Path $gitRoot $file) -replace [regex]::Escape($TemplateRoot), ""
  $SourcePath = Join-Path $TemplateRoot $RelativePath
  $DestinationPath = Join-Path $ExtensionRoot $RelativePath
  if ($Verbose) {
    Write-Host "Copying $SourcePath -> $DestinationPath" -ForegroundColor DarkGray
  }
  $DestinationFolder = Split-Path $DestinationPath -Parent
  if(-not (Test-Path $DestinationFolder)) {
    New-Item -ItemType Directory -Path $DestinationFolder -Force | Out-Null
  }
  Copy-Item -Path $SourcePath -Destination $DestinationPath -Force
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

# also rename files with TemplateExtension in the name
$Files = Get-ChildItem -Path $ExtensionRoot -Recurse -File -Filter "*TemplateExtension*"
foreach($file in $Files) {
  $NewName = $file.Name -replace "TemplateExtension", $Name
  $NewPath = Join-Path $file.DirectoryName $NewName
  Move-Item -Path $file.FullName -Destination $NewPath
}

Write-Host "Extension created in $ExtensionRoot" -ForegroundColor GREEN
Write-Host "Don't forget to add your new extension to the 'Sample plugins' in the WindowsCommandPalette solution."

$pathToSolution = Join-Path $gitRoot "src\modules\cmdpal\WindowsCommandPalette.sln"
Write-Host @"
You can open the solution with
    start $pathToSolution
and get started by editing the file
     $ExtensionRoot\src\${Name}Page.cs
"@

if ($Verbose) {
    $TotalTime = (Get-Date)-$StartTime
    $TotalMinutes = [math]::Floor($TotalTime.TotalMinutes)
    $TotalSeconds = [math]::Ceiling($TotalTime.TotalSeconds)
    Write-Host @"
Total Running Time:
$TotalMinutes minutes and $TotalSeconds seconds
"@ -ForegroundColor CYAN
}
