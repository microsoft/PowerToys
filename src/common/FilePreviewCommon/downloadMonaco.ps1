$CurrentDir = Get-Location
$repoRoot = (Get-Item -Path $PSScriptRoot).Parent.Parent.Parent.FullName
$dstRoot = Join-Path $repoRoot "src\Monaco\monacoSRC"

if (Test-Path $dstRoot) {
	return
}

$tempRoot = Join-Path $env:TEMP ("monaco-editor-" + [System.Guid]::NewGuid().ToString("N"))
$pkgVersion = "0.52.2"
$tgzPath = Join-Path $tempRoot "monaco-editor-$pkgVersion.tgz"
$extractPath = Join-Path $tempRoot "extract"

try {
	Set-Location -Path $env:TEMP
	New-Item -ItemType Directory -Path $tempRoot, $extractPath | Out-Null

	# Download package tarball from npm registry
	Invoke-WebRequest -Uri "https://registry.npmjs.org/monaco-editor/-/monaco-editor-$pkgVersion.tgz" -OutFile $tgzPath

	# Extract tgz (tar in modern Windows)
	tar -xzf $tgzPath -C $extractPath

	# npm tarballs unpack into "package/"
	$srcMin = Join-Path $extractPath "package\min"
	$dstMin = Join-Path $dstRoot "min"

	New-Item -ItemType Directory -Path $dstRoot -Force | Out-Null
	Copy-Item -Path $srcMin -Destination $dstMin -Recurse -Force
}
finally {
	Set-Location -Path $CurrentDir

	if (Test-Path $tempRoot) {
		Remove-Item -Path $tempRoot -Recurse -Force
	}
}