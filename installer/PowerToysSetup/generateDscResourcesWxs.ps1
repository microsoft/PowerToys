[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True)]
    [string]$dscWxsFile,
    [Parameter(Mandatory = $True)]
    [string]$Platform,
    [Parameter(Mandatory = $True)]
    [string]$Configuration
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildOutputDir = Join-Path $scriptDir "..\..\$Platform\$Configuration"

if (-not (Test-Path $buildOutputDir)) {
    Write-Error "Build output directory not found: '$buildOutputDir'"
    exit 1
}

$dscFiles = Get-ChildItem -Path $buildOutputDir -Filter "microsoft.powertoys.*.settings.dsc.resource.json" -File

if (-not $dscFiles) {
    Write-Warning "No DSC manifest files found in '$buildOutputDir'"
    $wxsContent = @"
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <?include `$(sys.CURRENTDIR)\Common.wxi?>
  <Fragment>
    <ComponentGroup Id="DscResourcesComponentGroup">
    </ComponentGroup>
  </Fragment>
</Wix>
"@
    Set-Content -Path $dscWxsFile -Value $wxsContent
    exit 0
}

Write-Host "Found $($dscFiles.Count) DSC manifest file(s)"

$wxsContent = @"
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <?include `$(sys.CURRENTDIR)\Common.wxi?>
  <Fragment>
    <DirectoryRef Id="DSCModulesReferenceFolder">
"@

$componentRefs = @()
foreach ($file in $dscFiles) {
    $componentId = "DscResource_" + ($file.BaseName -replace '[^A-Za-z0-9_]', '_')
    $fileId = $componentId + "_File"
    $guid = [System.Guid]::NewGuid().ToString().ToUpper()
    $componentRefs += $componentId
    
    $wxsContent += @"

      <Component Id="$componentId" Guid="{$guid}">
        <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
          <RegistryValue Type="string" Name="$componentId" Value="" KeyPath="yes"/>
        </RegistryKey>
        <File Id="$fileId" Source="`$(var.BinDir)$($file.Name)" Vital="no"/>
      </Component>
"@
}

$wxsContent += @"

    </DirectoryRef>
  </Fragment>
  <Fragment>
    <ComponentGroup Id="DscResourcesComponentGroup">
"@

foreach ($componentId in $componentRefs) {
    $wxsContent += @"

      <ComponentRef Id="$componentId"/>
"@
}

$wxsContent += @"

    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path $dscWxsFile -Value $wxsContent
Write-Host "Generated DSC resources WiX fragment: '$dscWxsFile'"
