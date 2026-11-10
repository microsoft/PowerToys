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

function Import-MediaTestHarvester {
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
        . Import-MediaTestHarvester

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
    <!--/InstallerMedia-->
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

    It 'replaces generated media on retry without retaining obsolete cabinets' {
        $files = @(
            New-MediaTestFile 'A' 'aa'
            New-MediaTestFile 'B' 'bb'
        )
        $productPath = $files[0].WxsPath
        $manifestPath = Join-Path $TestDrive 'obj\InstallerMedia.json'
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="Test">
    <Media Id="1" Cabinet="cab1.cab" EmbedCab="yes" CompressionLevel="high" />
    <!--InstallerMedia-->
    <!--/InstallerMedia-->
  </Package>
  <Fragment><Component Id="AComponent"><File Id="A" /></Component><Component Id="BComponent"><File Id="B" /></Component></Fragment>
</Wix>
'@ | Set-Content -LiteralPath $productPath -Encoding UTF8

        Set-InstallerMedia -Files $files -ProductWxsPath $productPath -ManifestPath $manifestPath -MaximumUncompressedSize 2
        [xml]$first = Get-Content -LiteralPath $productPath
        @($first.Wix.Package.Media).Count | Should Be 3

        Set-InstallerMedia -Files $files -ProductWxsPath $productPath -ManifestPath $manifestPath -MaximumUncompressedSize 4
        [xml]$second = Get-Content -LiteralPath $productPath
        @($second.Wix.Package.Media).Count | Should Be 2
        ($second.Wix.Package.Media.Id -join ',') | Should Be '1,2'
        [xml]$authoring = Get-Content -LiteralPath $files[0].WxsPath
        @($authoring.GetElementsByTagName('File') | Where-Object DiskId -NE '2').Count | Should Be 0

        Set-InstallerMedia -Files @() -ProductWxsPath $productPath -ManifestPath $manifestPath
        [xml]$empty = Get-Content -LiteralPath $productPath
        @($empty.Wix.Package.Media).Count | Should Be 1
        $empty.Wix.Package.Media.Id | Should Be '1'
    }

    It 'restores the PowerDisplay template across consecutive local harvests' {
        . Import-MediaTestHarvester
        $installer = Join-Path $PSScriptRoot '..\..\installer'
        [xml]$nativeProject = Get-Content -LiteralPath "$installer\PowerToysSetupCustomActionsVNext\PowerToysSetupCustomActionsVNext.vcxproj"
        [xml]$msiProject = Get-Content -LiteralPath "$installer\PowerToysSetupVNext\PowerToysInstallerVNext.wixproj"
        $backup = @($nativeProject.Project.ItemDefinitionGroup.PreBuildEvent.Command -split '\r?\n' |
            Where-Object { $_ -match 'copy .*PowerDisplay\.wxs' }) -join "`r`n"
        $restore = @($msiProject.Project.PropertyGroup.PostBuildEvent -split '\r?\n' |
            Where-Object { $_ -match 'move .*PowerDisplay\.wxs' }) -join "`r`n"
        $projectDir = Join-Path $TestDrive 'installer\PowerToysSetupCustomActionsVNext'
        $wixDir = Join-Path $TestDrive 'installer\PowerToysSetupVNext'
        $buildDir = Join-Path $wixDir 'x64\Release\MachineSetup'
        $source = Join-Path $TestDrive 'assets'
        $null = New-Item -ItemType Directory -Path $projectDir, $buildDir, $source
        Set-Content -LiteralPath "$source\PowerDisplay.ico" -Value 'icon payload'
        Copy-Item -LiteralPath "$installer\PowerToysSetupVNext\PowerDisplay.wxs" -Destination $wixDir
        $wxs = Join-Path $wixDir 'PowerDisplay.wxs'
        $template = Get-Content -LiteralPath $wxs -Raw
        Set-Content -LiteralPath "$buildDir\backup.cmd" -Value $backup.Replace('$(ProjectDir)', "$projectDir\")
        Set-Content -LiteralPath "$buildDir\restore.cmd" -Value $restore

        Push-Location $buildDir
        try {
            foreach ($iteration in 1..2) {
                & $env:ComSpec /d /c "$buildDir\backup.cmd" | Out-Null
                $LASTEXITCODE | Should Be 0
                $fileListRoots = @{}
                $installerFiles = [Collections.Generic.List[object]]::new()
                Generate-FileList -fileDepsJson '' -fileListName PowerDisplayAssetsFiles -wxsFilePath $wxs -depsPath "$source\"
                Generate-FileComponents -fileListName PowerDisplayAssetsFiles -wxsFilePath $wxs
                $layout = Get-InstallerMediaLayout -Files $installerFiles.ToArray()
                $layout.Files.Count | Should Be 1
                $installerFiles[0].SourcePath | Should Be "$source\PowerDisplay.ico"

                & $env:ComSpec /d /c "$buildDir\restore.cmd" | Out-Null
                $LASTEXITCODE | Should Be 0
                (Get-Content -LiteralPath $wxs -Raw) | Should Be $template
            }
        }
        finally { Pop-Location }
    }

    It 'publishes local inputs before harvesting and leaves CI inputs unchanged' {
        $installer = Join-Path $PSScriptRoot '..\..\installer'
        [xml]$nativeProject = Get-Content -LiteralPath "$installer\PowerToysSetupCustomActionsVNext\PowerToysSetupCustomActionsVNext.vcxproj"
        [xml]$msiProject = Get-Content -LiteralPath "$installer\PowerToysSetupVNext\PowerToysInstallerVNext.wixproj"
        $nativeEvent = $nativeProject.SelectSingleNode('//*[local-name()="PreBuildEvent"]/*[local-name()="Command"]').InnerText
        $msiEvents = @($msiProject.Project.PropertyGroup | Where-Object { $_.PreBuildEvent } | ForEach-Object PreBuildEvent)
        $pipeline = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\steps-build-installer-vnext.yml') -Raw
        $supportStep = [regex]::Match($pipeline, '(?ms)^  - task: VSBuild@1\r?\n    displayName: Build Shared Support DLLs.*?(?=^  - |\z)').Value
        $pipelineFlag = [regex]::Match($supportStep, '(?m)^\s+IsPipeline:\s*[''"]?(\w+)').Groups[1].Value
        $cases = @(
            @{ CIBuild = 'false'; IsPipeline = ''; Expected = 'published' }
            @{ CIBuild = 'true'; IsPipeline = ''; Expected = 'published' }
            @{ CIBuild = 'true'; IsPipeline = $pipelineFlag; Expected = 'prepublished' }
        )
        foreach ($case in $cases) {
            foreach ($msiEvent in $msiEvents) {
                $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
                $null = New-Item -ItemType Directory -Path $fixture
                Set-Content -LiteralPath "$fixture\input.bin" -Value 'prepublished'
                Set-Content -LiteralPath "$fixture\publish.cmd" -Value '@echo published> "%~dp0input.bin"'
                Set-Content -LiteralPath "$fixture\harvest.cmd" -Value '@copy /Y "%~dp0input.bin" "%~dp0manifest.bin" >nul'

                # Exercise the real event ordering and conditions with only external work stubbed.
                $commands = "$nativeEvent`r`n$msiEvent"
                $commands = $commands -replace '(?m)^.*VsDevCmd\.bat.*$', 'rem developer environment'
                $commands = $commands -replace '(?m)^.*call cmd /C "copy .*$', 'rem template backup'
                $commands = $commands -replace '(?m)^.*call .*publish\.cmd.*$', "call `"$fixture\publish.cmd`""
                $commands = $commands -replace '(?m)^.*call .*generateAllFileComponents\.ps1.*$', "call `"$fixture\harvest.cmd`""
                $commands = $commands -replace '(?m)^.*call .*generateMonacoWxs\.ps1.*$', 'rem Monaco authoring'
                $commands = $commands.Replace('$(CIBuild)', $case.CIBuild).Replace('$(Platform)', 'x64')
                $commands = $commands.Replace('$(ProjectDir)', "$fixture\").Replace('$(SolutionDir)', "$fixture\")
                Set-Content -LiteralPath "$fixture\events.cmd" -Value "@echo off`r`nset IsPipeline=$($case.IsPipeline)`r`n$commands"
                & $env:ComSpec /d /c "$fixture\events.cmd" | Out-Null
                $LASTEXITCODE | Should Be 0

                $payload = Get-Content -LiteralPath "$fixture\input.bin" -Raw
                (Get-Content -LiteralPath "$fixture\manifest.bin" -Raw) | Should Be $payload
                $payload.Trim() | Should Be $case.Expected
            }
        }
    }

    It 'passes lowercase runtime platforms to every preview-handler publish' {
        $publish = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\installer\PowerToysSetupVNext\publish.cmd') -Raw
        foreach ($platform in @('ARM64', 'arm64', 'x64', 'X64')) {
            $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            $null = New-Item -ItemType Directory -Path $fixture
            Set-Content -LiteralPath "$fixture\compiler.cmd" -Value '@echo %*>> "%~dp0calls.txt"' -Encoding ASCII
            $commands = $publish -replace '(?m)^msbuild\b', "call `"$fixture\compiler.cmd`""
            Set-Content -LiteralPath "$fixture\publish.cmd" -Value $commands -Encoding ASCII
            & $env:ComSpec /d /c "$fixture\publish.cmd" $platform | Out-Null
            $LASTEXITCODE | Should Be 0

            $calls = Get-Content -LiteralPath "$fixture\calls.txt"
            $calls.Count | Should Be 4
            foreach ($call in $calls) {
                $actualPlatform = [regex]::Match($call, '-p:Platform="([^"]+)"').Groups[1].Value
                [string]::Equals($actualPlatform, $platform.ToLowerInvariant(), [StringComparison]::Ordinal) | Should Be $true
            }
        }
    }

    It 'replaces a previously generated file list instead of appending to it' {
        . Import-MediaTestHarvester
        $fileListRoots = @{}
        $installerFiles = [Collections.Generic.List[object]]::new()
        $source = Join-Path $TestDrive 'payload'
        $null = New-Item -ItemType Directory -Path $source
        Set-Content -LiteralPath "$source\a.dll" -Value 'a'
        $wxs = Join-Path $TestDrive 'Files.wxs'
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <?define TestFiles=?>
  <Fragment><DirectoryRef Id="INSTALLFOLDER">
    <!--TestFiles_Component_Def-->
  </DirectoryRef><ComponentGroup Id="Files">
  </ComponentGroup></Fragment>
</Wix>
'@ | Set-Content -LiteralPath $wxs -Encoding UTF8
        Generate-FileList -fileDepsJson '' -fileListName TestFiles -wxsFilePath $wxs -depsPath "$source\"
        Remove-Item -LiteralPath "$source\a.dll"
        Set-Content -LiteralPath "$source\b.dll" -Value 'b'
        Generate-FileList -fileDepsJson '' -fileListName TestFiles -wxsFilePath $wxs -depsPath "$source\"
        Generate-FileComponents -fileListName TestFiles -wxsFilePath $wxs

        $layout = Get-InstallerMediaLayout -Files $installerFiles.ToArray()
        $layout.Files.Count | Should Be 1
        $installerFiles[0].SourcePath | Should Be "$source\b.dll"
    }

    It 'restores authoring after a hash failure and permits a clean retry' {
        $generatorPath = Join-Path $PSScriptRoot '..\..\installer\PowerToysSetupVNext\generateAllFileComponents.ps1'
        $generator = Get-Content -LiteralPath $generatorPath -Raw
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($generator, [ref]$tokens, [ref]$errors)
        $firstHarvest = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Generate-FileList'
        }, $false)
        $fixture = Join-Path $TestDrive 'failed-harvest'
        $null = New-Item -ItemType Directory -Path "$fixture\payload"
        Copy-Item -LiteralPath (Join-Path (Split-Path $generatorPath -Parent) 'InstallerMedia.ps1') -Destination $fixture
        Set-Content -LiteralPath "$fixture\payload\a.dll" -Value 'payload'
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <?define TestFiles=?>
  <Fragment><DirectoryRef Id="INSTALLFOLDER">
    <!--TestFiles_Component_Def-->
  </DirectoryRef><ComponentGroup Id="Files">
  </ComponentGroup></Fragment>
</Wix>
'@ | Set-Content -LiteralPath "$fixture\Files.wxs" -Encoding UTF8
        @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="Test">
    <Media Id="1" Cabinet="cab1.cab" EmbedCab="yes" CompressionLevel="high" />
    <!--InstallerMedia-->
    <!--/InstallerMedia-->
  </Package>
</Wix>
'@ | Set-Content -LiteralPath "$fixture\Product.wxs" -Encoding UTF8
        $body = @'
Generate-FileList -fileDepsJson '' -fileListName TestFiles -wxsFilePath "$PSScriptRoot\Files.wxs" -depsPath "$PSScriptRoot\payload\"
Generate-FileComponents -fileListName TestFiles -wxsFilePath "$PSScriptRoot\Files.wxs"
Set-InstallerMedia -Files $installerFiles.ToArray() -ProductWxsPath "$PSScriptRoot\Product.wxs" -ManifestPath "$PSScriptRoot\obj\InstallerMedia.json"
'@
        $scriptPath = Join-Path $fixture 'harvest.ps1'
        Set-Content -LiteralPath $scriptPath -Value ($generator.Substring(0, $firstHarvest.Extent.StartOffset) + $body) -Encoding UTF8
        $before = (Get-FileHash -LiteralPath "$fixture\Files.wxs" -Algorithm SHA256).Hash
        $handle = [IO.File]::Open("$fixture\payload\a.dll", [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        try { Assert-MediaFailure { & $scriptPath -platform x64 } }
        finally { $handle.Dispose() }
        (Get-FileHash -LiteralPath "$fixture\Files.wxs" -Algorithm SHA256).Hash | Should Be $before

        & $scriptPath -platform x64
        $manifest = Get-Content -LiteralPath "$fixture\obj\InstallerMedia.json" -Raw | ConvertFrom-Json
        @($manifest.Files).Count | Should Be 1
        [xml]$actual = Get-Content -LiteralPath "$fixture\Files.wxs"
        $actual.GetElementsByTagName('ComponentRef').Count | Should Be 1
        $actual.GetElementsByTagName('File').Count | Should Be 1
    }
}
