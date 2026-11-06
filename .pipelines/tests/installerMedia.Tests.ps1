# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

. (Join-Path $PSScriptRoot '..\..\installer\PowerToysSetupVNext\InstallerMedia.ps1')

function New-MediaTestFile {
    param([string]$Id, [string]$Content)

    $path = Join-Path $TestDrive "$Id.bin"
    [IO.File]::WriteAllText($path, $Content, (New-Object Text.UTF8Encoding($false)))
    [pscustomobject]@{
        Id = $Id
        Component = "${Id}Component"
        SourcePath = $path
        WxsPath = Join-Path $TestDrive 'Files.wxs'
    }
}

function Assert-MediaFailure {
    param([scriptblock]$Action)

    $failed = $false
    try { & $Action | Out-Null }
    catch { $failed = $true }
    $failed | Should Be $true
}

Describe 'Installer Smart Cabbing media' {
    It 'keeps duplicate payloads together across an otherwise overflowing cabinet boundary' {
        $files = @(
            New-MediaTestFile 'A' 'aaaa'
            New-MediaTestFile 'B' 'bb'
            New-MediaTestFile 'Z' 'bb'
        )
        $layout = Get-InstallerMediaLayout -Files $files -MaximumUncompressedSize 6

        $layout.Files.Count | Should Be 3
        ($layout.Files | Where-Object Id -EQ 'A').DiskId | Should Be 2
        ($layout.Files | Where-Object Id -EQ 'B').DiskId | Should Be 3
        ($layout.Files | Where-Object Id -EQ 'Z').DiskId | Should Be 3
        $layout.Media[0].Size | Should Be 4
        $layout.Media[1].Size | Should Be 4
    }

    It 'does not treat same-sized different contents as duplicates' {
        $files = @(
            New-MediaTestFile 'A' 'aa'
            New-MediaTestFile 'B' 'bb'
        )
        $layout = Get-InstallerMediaLayout -Files $files -MaximumUncompressedSize 2

        $layout.Files[0].Hash | Should Not Be $layout.Files[1].Hash
        $layout.Files[0].DiskId | Should Not Be $layout.Files[1].DiskId
    }

    It 'counts both destinations when they use one physical source' {
        $first = New-MediaTestFile 'A' 'aaa'
        $second = [pscustomobject]@{
            Id = 'B'
            Component = 'BComponent'
            SourcePath = $first.SourcePath
            WxsPath = $first.WxsPath
        }
        $layout = Get-InstallerMediaLayout -Files @($first, $second)

        $layout.Files.Count | Should Be 2
        $layout.Files[0].DiskId | Should Be $layout.Files[1].DiskId
        $layout.Media[0].Size | Should Be 6
    }

    It 'keeps an oversized content group in its own cabinet without splitting its copies' {
        $files = @(
            New-MediaTestFile 'A' 'aaaa'
            New-MediaTestFile 'B' 'aaaa'
            New-MediaTestFile 'C' 'cc'
        )
        $layout = Get-InstallerMediaLayout -Files $files -MaximumUncompressedSize 6

        $layout.Files[0].DiskId | Should Be $layout.Files[1].DiskId
        $layout.Files[2].DiskId | Should Not Be $layout.Files[1].DiskId
        $layout.Media[0].Size | Should Be 8
    }

    It 'handles empty inputs and retains zero-byte files' {
        $empty = Get-InstallerMediaLayout -Files @()
        $empty.Files.Count | Should Be 0
        $empty.Media.Count | Should Be 0

        $files = @(
            New-MediaTestFile 'A' ''
            New-MediaTestFile 'B' ''
        )
        $layout = Get-InstallerMediaLayout -Files $files
        $layout.Files.Count | Should Be 2
        $layout.Media.Count | Should Be 1
        $layout.Media[0].Size | Should Be 0
    }

    It 'produces the same assignments regardless of discovery order' {
        $files = @(
            New-MediaTestFile 'A' 'aaaa'
            New-MediaTestFile 'B' 'bb'
            New-MediaTestFile 'Z' 'bb'
        )
        $first = Get-InstallerMediaLayout -Files $files -MaximumUncompressedSize 6
        [array]::Reverse($files)
        $second = Get-InstallerMediaLayout -Files $files -MaximumUncompressedSize 6

        ($first | ConvertTo-Json -Depth 5 -Compress) | Should Be ($second | ConvertTo-Json -Depth 5 -Compress)
    }

    It 'rejects missing sources and duplicate MSI File identifiers' {
        $file = New-MediaTestFile 'A' 'a'
        Assert-MediaFailure { Get-InstallerMediaLayout -Files @($file, $file) }
        Remove-Item -LiteralPath $file.SourcePath
        Assert-MediaFailure { Get-InstallerMediaLayout -Files @($file) }
    }

    It 'collects the exact source and component emitted by the file harvester' {
        $scriptPath = Join-Path $PSScriptRoot '..\..\installer\PowerToysSetupVNext\generateAllFileComponents.ps1'
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
        foreach ($function in $ast.FindAll({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -in @('Generate-FileList', 'Generate-FileComponents')
        }, $false)) {
            . ([scriptblock]::Create($function.Extent.Text))
        }

        $fileListRoots = @{}
        $installerFiles = [Collections.Generic.List[object]]::new()
        $source = Join-Path $TestDrive 'harvest'
        $null = New-Item -ItemType Directory -Path $source
        Set-Content -LiteralPath "$source\shared.dll" -Value 'payload'
        $wxs = Join-Path $TestDrive 'Harvest.wxs'
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <?define TestFiles=?>
  <Fragment><DirectoryRef Id="INSTALLFOLDER">
    <!--TestFiles_Component_Def-->
  </DirectoryRef><ComponentGroup Id="Files">
  </ComponentGroup></Fragment>
</Wix>
'@ | Set-Content -LiteralPath $wxs -Encoding UTF8

        Generate-FileList -fileDepsJson '' -fileListName TestFiles -wxsFilePath $wxs -depsPath $source
        Generate-FileComponents -fileListName TestFiles -wxsFilePath $wxs

        $installerFiles.Count | Should Be 1
        $installerFiles[0].Id | Should Be 'TestFiles_File_shared.dll'
        $installerFiles[0].SourcePath | Should Be "$source\shared.dll"
        $installerFiles[0].Component | Should Be 'TestFiles_Component'
        $installerFiles[0].WxsPath | Should Be $wxs
    }

    It 'preserves both File rows, component ownership and unrelated authoring' {
        $files = @(
            New-MediaTestFile 'A' 'same'
            New-MediaTestFile 'B' 'same'
        )
        $productPath = Join-Path $TestDrive 'Product.wxs'
        $manifestPath = Join-Path $TestDrive 'obj\InstallerMedia.json'
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <DirectoryRef Id="INSTALLFOLDER">
      <Component Id="AComponent"><File Id="A" Source="root\shared.dll" /></Component>
      <Component Id="ManualComponent"><File Id="Manual" Source="manual.dll" /></Component>
    </DirectoryRef>
    <DirectoryRef Id="WinUI3AppsInstallFolder">
      <Component Id="BComponent"><File Id="B" Source="WinUI3Apps\shared.dll" /></Component>
    </DirectoryRef>
  </Fragment>
</Wix>
'@ | Set-Content -LiteralPath $files[0].WxsPath -Encoding UTF8
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="Test">
    <MajorUpgrade DowngradeErrorMessage="Existing policy" />
    <Media Id="1" Cabinet="cab1.cab" EmbedCab="yes" CompressionLevel="high" />
    <!--InstallerMedia-->
  </Package>
</Wix>
'@ | Set-Content -LiteralPath $productPath -Encoding UTF8

        Set-InstallerMedia -Files $files -ProductWxsPath $productPath -ManifestPath $manifestPath

        [xml]$actual = Get-Content -LiteralPath $files[0].WxsPath
        $nodes = @($actual.GetElementsByTagName('File'))
        $nodes.Count | Should Be 3
        ($nodes | Where-Object Id -EQ 'A').DiskId | Should Be '2'
        ($nodes | Where-Object Id -EQ 'B').DiskId | Should Be '2'
        ($nodes | Where-Object Id -EQ 'A').ParentNode.Id | Should Be 'AComponent'
        ($nodes | Where-Object Id -EQ 'B').ParentNode.Id | Should Be 'BComponent'
        ($nodes | Where-Object Id -EQ 'B').Source | Should Be 'WinUI3Apps\shared.dll'
        ($nodes | Where-Object Id -EQ 'Manual').HasAttribute('DiskId') | Should Be $false
        $actual.GetElementsByTagName('CopyFile').Count | Should Be 0

        [xml]$product = Get-Content -LiteralPath $productPath
        $product.Wix.Package.MajorUpgrade.DowngradeErrorMessage | Should Be 'Existing policy'
        @($product.Wix.Package.Media).Count | Should Be 2
        $product.Wix.Package.Media[1].EmbedCab | Should Be 'yes'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        @($manifest.Files).Count | Should Be 2
        $manifest.Files[0].Hash | Should Be (Get-FileHash -LiteralPath $files[0].SourcePath -Algorithm SHA256).Hash
    }
}
