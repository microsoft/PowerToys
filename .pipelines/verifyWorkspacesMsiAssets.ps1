# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$DecompiledMsi
)

$ErrorActionPreference = 'Stop'
foreach ($path in $DecompiledMsi) {
    # WiX nests each MSI File under its owning Component and target Directory.
    [xml]$wix = Get-Content -LiteralPath $path -Raw
    $ns = [Xml.XmlNamespaceManager]::new($wix.NameTable)
    $ns.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')
    $featureComponents = $wix.SelectNodes('//w:Feature/w:ComponentRef/@Id', $ns).Value
    foreach ($folder in @('Assets\Workspaces', 'WinUI3Apps\Assets\Workspaces')) {
        $segments = $folder.Split('\') | ForEach-Object { "w:Directory[@Name='$_']" }
        $directory = "//w:Directory[@Id='INSTALLFOLDER']/" + ($segments -join '/')
        foreach ($icon in @('DefaultIcon.ico', 'Workspaces.ico')) {
            $files = $wix.SelectNodes("$directory/w:Component/w:File[@Name='$icon']", $ns)
            if ($files.Count -ne 1) {
                throw "Expected one '$folder\$icon' in '$path', found $($files.Count)."
            }
            if ($files[0].ParentNode.GetAttribute('Id') -notin $featureComponents) {
                throw "No feature installs '$folder\$icon' in '$path'."
            }
        }
        $directoryId = $wix.SelectSingleNode($directory, $ns).GetAttribute('Id')
        $cleanup = "//w:RemoveFolder[@Directory='$directoryId' and (@On='uninstall' or @On='both')]"
        if ($null -eq $wix.SelectSingleNode($cleanup, $ns)) {
            throw "Missing uninstall cleanup for '$folder' in '$path'."
        }
    }
}
