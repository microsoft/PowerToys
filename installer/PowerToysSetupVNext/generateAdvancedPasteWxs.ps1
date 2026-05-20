[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$advancedPasteWxsFile,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$platform,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$configuration,
    [Parameter(Mandatory = $True, Position = 4)]
    [string]$nugetHeatPath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($platform -eq "x64") {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x64"
} else {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x86"
}

$heatExe = Join-Path $HeatPath "heat.exe"
if (-not (Test-Path $heatExe)) {
    Write-Error "heat.exe not found at '$heatExe'. Ensure the WixToolset.Heat package (5.0.2) is restored under '$nugetHeatPath'."
    exit 1
}

# AdvancedPaste loose-file build output (self-contained WinUI3 app).
# The wixproj sets the AdvancedPasteHarvestPath variable to this same path so
# the heat-generated <File Source="$(var.AdvancedPasteHarvestPath)\..." /> resolves at MSI build.
$SourceDir = Join-Path $scriptDir "..\..\$platform\$configuration\WinUI3Apps\AdvancedPaste"
$ComponentGroup = "AdvancedPasteHeatGenerated"
$DirectoryRef = "AdvancedPasteInstallFolder"
$Variable = "var.AdvancedPasteHarvestPath"

if (-not (Test-Path $SourceDir)) {
    Write-Error "AdvancedPaste build output not found at '$SourceDir'. Build the AdvancedPaste project before the installer."
    exit 1
}

& $heatExe dir "$SourceDir" -out "$advancedPasteWxsFile" -cg "$ComponentGroup" -dr "$DirectoryRef" -var "$Variable" -gg -srd -sreg -scom -nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "heat.exe failed harvesting AdvancedPaste with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Post-process the heat output to:
#  1. Inject the PowerToys Common.wxi include (needed for $(var.RegistryScope) below).
#  2. Replace each Component's KeyPath file with a RegistryValue under
#     HKxx\Software\Classes\powertoys\components so install/upgrade semantics match
#     the rest of the installer (and the file payload can be replaced cleanly on upgrade).
#  3. Add a single RemoveAdvancedPasteSubFolders component that cleans every
#     auto-created subdirectory on uninstall.
$fileWxs = Get-Content $advancedPasteWxsFile
$fileWxs = $fileWxs -replace " KeyPath=`"yes`" ", " "
# Force Language="0" (neutral) on every <File>. WiX auto-detects LCIDs from .mui
# file metadata, but gd-GB / mi-NZ / ug-CN locales produce non-standard Language Ids
# that fail ICE03 validation. Neutral (0) is always valid.
$fileWxs = $fileWxs -replace '(<File\b(?![^>]*\bLanguage=)[^/]*?)\s*/>', '$1 Language="0" />'

$newFileContent = ""
$componentId = "error"
$directories = @()

$fileWxs | ForEach-Object {
    $line = $_
    if ($line -match "<Wix xmlns=`".*`">") {
        $line += "`r`n`r`n    <?include `$(sys.CURRENTDIR)\Common.wxi?>`r`n"
    }
    if ($line -match "<Component Id=`"(.*)`" Directory") {
        $componentId = $matches[1]
    }
    if ($line -match "<Directory Id=`"(.*)`" Name=`".*`" />") {
        $directories += $matches[1]
    }
    if ($line -match "</Component>") {
        $line = @"
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
                </RegistryKey>
            </Component>
"@
    }
    $newFileContent += $line + "`r`n"
}

$removeFolderEntries = @"
`r`n            <Component Id="RemoveAdvancedPasteSubFolders" Guid="$((New-Guid).ToString().ToUpper())" Directory="AdvancedPasteInstallFolder" >
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="RemoveAdvancedPasteSubFolders" Value="" KeyPath="yes"/>
                </RegistryKey>`r`n
"@

$directories | ForEach-Object {
    $removeFolderEntries += @"
                <RemoveFolder Id="RemoveAP$($_)" Directory="$($_)" On="uninstall"/>

"@
}

$removeFolderEntries += @"
            </Component>
"@

$newFileContent = $newFileContent -replace "\s+(</ComponentGroup>)", "$removeFolderEntries`r`n        </ComponentGroup>"

Set-Content -Path $advancedPasteWxsFile -Value $newFileContent
