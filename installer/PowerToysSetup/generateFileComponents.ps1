[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$fileListName,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$wxsFilePath,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$regroot
)

$wxsFile = Get-Content $wxsFilePath;

$wxsFile | ForEach-Object {
    if ($_ -match "(<?define $fileListName=)(.*)\?>") {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'fileList',
        Justification = 'variable is used in another scope')]

        $fileList = $matches[2] -split ';'
        return
    }
}

$componentId = "$($fileListName)_Component"

$componentDefs = "`r`n"
$componentDefs +=
@"
        <Component Id="$($componentId)" Win64="yes" Guid="$((New-Guid).ToString().ToUpper())">
          <RegistryKey Root="$($regroot)" Key="Software\Classes\powertoys\components">
            <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
          </RegistryKey>`r`n
"@

foreach ($file in $fileList) {
    $fileTmp = $file -replace "-", "_"
    $componentDefs +=
@"
          <File Id="$($fileListName)_File_$($fileTmp)" Source="`$(var.$($fileListName)Path)\$($file)" />`r`n
"@
}

$componentDefs +=
@"
        </Component>`r`n
"@

$wxsFile = $wxsFile -replace "\s+(<!--$($fileListName)_Component_Def-->)", $componentDefs

$componentRef =
@"
        <ComponentRef Id="$($componentId)" />
"@

$wxsFile = $wxsFile -replace "\s+(</ComponentGroup>)", "$componentRef`r`n    </ComponentGroup>"

Set-Content -Path $wxsFilePath -Value $wxsFile