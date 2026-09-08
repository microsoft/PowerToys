$CurrentDir = Get-Location
$repoRoot = (Get-Item -Path $PSScriptRoot).Parent.Parent.Parent.FullName
$tempRoot = Join-Path $env:TEMP "monaco-editor"
$pkgVersion = "0.52.2"
$tgzPath = Join-Path $tempRoot "monaco-editor-$pkgVersion.tgz"
$extractPath = Join-Path $tempRoot "extract"

Set-Location -Path $env:TEMP
if (Test-Path $tempRoot) {
	Remove-Item $tempRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $tempRoot, $extractPath | Out-Null

# Download package tarball from npm registry
Invoke-WebRequest -Uri "https://registry.npmjs.org/monaco-editor/-/monaco-editor-$pkgVersion.tgz" -OutFile $tgzPath

# Extract tgz (tar in modern Windows)
tar -xzf $tgzPath -C $extractPath

# npm tarballs unpack into "package/"
$srcMin = Join-Path $extractPath "package\min"
$dstRoot = Join-Path $repoRoot "src\monaco\MonacoSRC"
$dstMin = Join-Path $dstRoot "min"

New-Item -ItemType Directory -Path $dstRoot -Force | Out-Null
Copy-Item -Path $srcMin -Destination $dstMin -Recurse -Force

Set-Location -Path $CurrentDir
Remove-Item -Path $tempRoot -Recurse -Force