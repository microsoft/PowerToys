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
    # PowerToys installs CoreFeature at the default MSI install level (1).
    $coreComponents = $wix.SelectNodes(
        "/w:Wix/w:Package/w:Feature[@Id='CoreFeature' and (not(@Level) or @Level=1)]/w:ComponentRef/@Id", $ns).Value
    foreach ($folder in @('Assets\Workspaces', 'WinUI3Apps\Assets\Workspaces')) {
        $segments = $folder.Split('\') | ForEach-Object { "w:Directory[@Name='$_']" }
        $directory = "//w:Directory[@Id='INSTALLFOLDER']/" + ($segments -join '/')
        foreach ($icon in @('DefaultIcon.ico', 'Workspaces.ico')) {
            $files = $wix.SelectNodes("$directory/w:Component/w:File[@Name='$icon']", $ns)
            if ($files.Count -ne 1) {
                throw "Expected one '$folder\$icon' in '$path', found $($files.Count)."
            }
            if ($files[0].ParentNode.GetAttribute('Id') -cnotin $coreComponents) {
                throw "CoreFeature does not install '$folder\$icon' in '$path'."
            }
        }
        $directoryId = $wix.SelectSingleNode($directory, $ns).GetAttribute('Id')
        $cleanup = $wix.SelectNodes("//w:Component/w:RemoveFolder[@Directory='$directoryId' and (@On='uninstall' or @On='both')]", $ns)
        if (-not ($cleanup | Where-Object { $_.ParentNode.GetAttribute('Id') -cin $coreComponents })) {
            throw "CoreFeature has no uninstall cleanup for '$folder' in '$path'."
        }
    }
}
