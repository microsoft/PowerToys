# Save current directory
$CurrentDir = Get-Location
$repoRoot = (Get-Item -Path $PSScriptRoot).Parent.Parent.FullName
# Go into the temporary directory
Set-Location -Path $env:TEMP
# Create a temporary directory for the monaco-editor
if (Test-Path -Path "monaco-editor") {
    Remove-Item -Path "monaco-editor" -Recurse -Force
}
New-Item -Path "monaco-editor" -ItemType Directory -Force
Set-Location -Path "monaco-editor"
# Install the monaco-editor package
npm i monaco-editor@0.52.2 --prefix
# Copy the minified files to the src/monaco/MonacoSRC directory
New-Item -Path $repoRoot\src\monaco\MonacoSRC -ItemType Directory -Force
Copy-Item -Path "$env:TEMP\monaco-editor\node_modules\monaco-editor\min" -Destination "$repoRoot\monaco\MonacoSRC\min" -Recurse -Force
# Delete the temporary directory
Set-Location -Path $CurrentDir
Remove-Item -Path "$env:TEMP\monaco-editor" -Recurse -Force
