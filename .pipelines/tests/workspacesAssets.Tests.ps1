# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$installerSource = Join-Path $repoRoot 'installer\PowerToysSetupVNext'

function Invoke-WorkspacesAssetsHarvestFixture {
    param([string]$Platform)

    $fixtureRoot = Join-Path $TestDrive $Platform
    $outputRoot = Join-Path $fixtureRoot "$Platform\Release"
    $icons = Join-Path $outputRoot 'WinUI3Apps\Assets\Workspaces'
    $installer = Join-Path $fixtureRoot 'installer\PowerToysSetupVNext'
    New-Item -ItemType Directory -Path $icons, $installer -Force | Out-Null
    foreach ($name in @('DefaultIcon.ico', 'Workspaces.ico')) {
        Set-Content -LiteralPath (Join-Path $icons $name) -Value 'fixture icon'
    }
    Copy-Item -LiteralPath (Join-Path $installerSource 'Workspaces.wxs') -Destination $installer

    # Run the real harvest functions and Workspaces calls in isolation, without building any payload.
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $installerSource 'generateAllFileComponents.ps1'), [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) {
        throw 'The installer generation script could not be parsed.'
    }
    $functions = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -in @('Generate-FileList', 'Generate-FileComponents')
    }, $false) | ForEach-Object { $_.Extent.Text }
    $calls = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -in @('Generate-FileList', 'Generate-FileComponents') -and
            $node.Extent.Text -match '\\Workspaces\.wxs'
    }, $false) | ForEach-Object { $_.Extent.Text }
    $fixtureScript = Join-Path $installer 'harvest.ps1'
    Set-Content -LiteralPath $fixtureScript -Value (@('param([string]$platform)') + $functions + $calls)
    & $fixtureScript -platform $Platform

    [pscustomobject]@{
        Xml = [xml](Get-Content -LiteralPath (Join-Path $installer 'Workspaces.wxs') -Raw)
        Icons = $icons
        BinDir = $outputRoot + '\'
    }
}

Describe 'Workspaces installer assets' {
    It 'harvests canonical icons into WinUI and legacy asset directories on <Platform>' -TestCases @(
        @{ Platform = 'x64' }
        @{ Platform = 'ARM64' }
    ) {
        param($Platform)

        $fixture = Invoke-WorkspacesAssetsHarvestFixture -Platform $Platform
        $xml = $fixture.Xml
        $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $ns.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')
        $defines = @{}
        foreach ($definition in $xml.SelectNodes('//processing-instruction("define")')) {
            if ($definition.Value -match '^([^=]+)=(.*)$') {
                $defines[$matches[1]] = $matches[2].Replace('$(var.BinDir)', $fixture.BinDir)
            }
        }
        foreach ($parent in @('WinUI3AppsAssetsFolder', 'BaseApplicationsAssetsFolder')) {
            $folder = $xml.SelectSingleNode("//w:DirectoryRef[@Id='$parent']/w:Directory[@Name='Workspaces']", $ns)
            $folder | Should Not BeNullOrEmpty
            $components = $xml.SelectNodes("//w:DirectoryRef[@Id='$($folder.Id)']/w:Component", $ns)
            @($components).Count | Should Be 1
            $component = $components[0]
            @($component.File).Count | Should Be 2
            $filenames = @($component.File | ForEach-Object { [IO.Path]::GetFileName($_.Source) })
            foreach ($expected in @('DefaultIcon.ico', 'Workspaces.ico')) {
                ($filenames -contains $expected) | Should Be $true
            }
            foreach ($file in $component.File) {
                $source = $file.Source
                foreach ($name in $defines.Keys) {
                    $source = $source.Replace('$(var.' + $name + ')', $defines[$name])
                }
                $filename = [IO.Path]::GetFileName($source)
                [IO.Path]::GetFullPath($source) | Should Be (Join-Path $fixture.Icons $filename)
                Test-Path -LiteralPath $source | Should Be $true
            }
            $xml.SelectSingleNode("//w:ComponentGroup[@Id='WorkspacesComponentGroup']/w:ComponentRef[@Id='$($component.Id)']", $ns) | Should Not BeNullOrEmpty
            $xml.SelectSingleNode("//w:RemoveFolder[@Directory='$($folder.Id)' and @On='uninstall']", $ns) | Should Not BeNullOrEmpty
        }
    }
}
