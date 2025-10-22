[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$monacoWxsFile,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$platform,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$nugetHeatPath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($platform -eq "x64") {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x64"
} else {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x86"
}

# Validate heat.exe exists at the resolved path; fail fast if not found.
$heatExe = Join-Path $HeatPath "heat.exe"
if (-not (Test-Path $heatExe)) {
    Write-Error "heat.exe not found at '$heatExe'. Ensure the WixToolset.Heat package (5.0.2) is restored under '$nugetHeatPath'."
    exit 1
}

$SourceDir = Join-Path $scriptDir "..\..\src\Monaco\monacoSRC"  # Now relative to script location
$OutputFile = Join-Path $scriptDir "MonacoSRC.wxs"
$ComponentGroup = "MonacoSRCHeatGenerated"
$DirectoryRef = "MonacoPreviewHandlerMonacoSRCFolder"
$Variable = "var.MonacoSRCHarvestPath"

& $heatExe dir "$SourceDir" -out "$OutputFile" -cg "$ComponentGroup" -dr "$DirectoryRef" -var "$Variable" -gg -srd -nologo

$fileWxs = Get-Content $monacoWxsFile;

$fileWxs = $fileWxs -replace " KeyPath=`"yes`" ", " "

$newFileContent = ""

$componentId = "error"
$directories = @()

$fileWxs | ForEach-Object {
    $line = $_;
    if ($line -match "<Wix xmlns=`".*`">") {
        $line +=
@"
`r`n
    <?include `$(sys.CURRENTDIR)\Common.wxi?>`r`n
"@
    }
    if ($line -match "<Component Id=`"(.*)`" Directory") {
        $componentId = $matches[1]
    }
    if ($line -match "<Directory Id=`"(.*)`" Name=`".*`" />") {
        $directories += $matches[1]
    }
    if ($line -match "</Component>") {
        $line =
@"
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
                </RegistryKey>
            </Component>
"@
    }

    $newFileContent += $line + "`r`n";
}

$removeFolderEntries =
@"
`r`n            <Component Id="RemoveMonacoSRCFolders" Guid="$((New-Guid).ToString().ToUpper())" Directory="MonacoPreviewHandlerMonacoSRCFolder" >
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="RemoveMonacoSRCFolders" Value="" KeyPath="yes"/>
                </RegistryKey>`r`n
"@

$directories | ForEach-Object {

    $removeFolderEntries +=
@"
                <RemoveFolder Id="Remove$($_)" Directory="$($_)" On="uninstall"/>

"@
}

$removeFolderEntries +=
@"
            </Component>
"@



$newFileContent = $newFileContent -replace "\s+(</ComponentGroup>)", "$removeFolderEntries`r`n        </ComponentGroup>"

Set-Content -Path $monacoWxsFile -Value $newFileContent