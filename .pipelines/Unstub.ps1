$RootPath = Split-Path $PSScriptRoot -Parent
$projFile = "$($RootPath)\src\settings-ui\Settings.UI\PowerToys.Settings.csproj"
$projFileContent = Get-Content $projFile -Encoding UTF8 -Raw

$xml = [xml]$projFileContent
$xml.PreserveWhitespace = $true

$propRef = $xml.SelectSingleNode("//ExperimentationLive")
$propRef.InnerText='True'

$xml.Save($projFile)