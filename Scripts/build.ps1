function Build-Version {
    if ([string]::IsNullOrEmpty($env:APPVEYOR_BUILD_VERSION)) {
        $v = "1.2.0"
    } else {
        $v = $env:APPVEYOR_BUILD_VERSION
    }

    Write-Host "Build Version: $v"
    return $v
}

function Build-Path {
    if ([string]::IsNullOrEmpty($env:APPVEYOR_BUILD_FOLDER)) {
        $p = Convert-Path .
    } else {
        $p = $env:APPVEYOR_BUILD_FOLDER
    }

    Write-Host "Build Folder: $p"
    Set-Location $p

    return $p
}

function Validate-Directory ($output) {
    New-Item $output -ItemType Directory -Force
}

function Pack-Nuget ($path, $version, $output) {
    Write-Host "Begin build nuget library"

    $spec = "$path\Scripts\wox.plugin.nuspec"
    Write-Host "nuspec path: $spec"
    Write-Host "Output path: $output"

    Nuget pack $spec -Version $version -OutputDirectory $output

    Write-Host "End build nuget library"
}

function Zip-Release ($path, $version, $output) {
    Write-Host "Begin zip release"

    $input = "$path\Output\Release"
    Write-Host "Input path:  $input"
    $file = "$output\Wox-$version.zip"
    Write-Host "Filename: $file"

    [Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
    [System.IO.Compression.ZipFile]::CreateFromDirectory($input, $file)

    Write-Host "End zip release"
}

function Pack-Squirrel-Installer ($path, $version, $output) {
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\wox.nuspec"
    Write-Host "nuspec path: $spec"
    $input = "$path\Output\Release"
    Write-Host "Input path:  $input"
    Nuget pack $spec -Version $version -Properties Configuration=Release -BasePath $input -OutputDirectory  $output

    $nupkg = "$output\Wox.$version.nupkg"
    Write-Host "nupkg path: $nupkg"
    $icon = "$path\Wox\Resources\app.ico"
    # Squirrel.com: https://github.com/Squirrel/Squirrel.Windows/issues/369
    New-Alias Squirrel $path\packages\squirrel*\tools\Squirrel.exe -Force
    # why we need Write-Output: https://github.com/Squirrel/Squirrel.Windows/issues/489#issuecomment-156039327
    # directory of releaseDir in fucking squirrel can't be same as directory ($nupkg) in releasify
    $temp = "$output\Temp"

    Squirrel --releasify $nupkg --releaseDir $temp --setupIcon $iconPath --no-msi | Write-Output
    Move-Item $temp\* $output -Force
    Remove-Item $temp
    
    $file = "$output\Wox-$version.exe"
    Write-Host "Filename: $file"

    Move-Item "$output\Setup.exe" $file -Force

    Write-Host "End pack squirrel installer"
}

function Main {
    $v = Build-Version
    $p = Build-Path
    $o = "$p\Output\Packages"
    New-Alias Nuget $p\packages\NuGet.CommandLine.*\tools\NuGet.exe -Force

    Validate-Directory $o
    
    $isInCI = $env:APPVEYOR
    if ($isInCI) {
        Pack-Nuget $p $v $o
        Zip-Release $p $v $o
    }

    Pack-Squirrel-Installer $p $v $o

    Write-Host "List output directory"
    Get-ChildItem $o
}

Main