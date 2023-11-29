Write-Output "Removing deps/boost_regex/examples dir"

$boostExamplesDir = $PSScriptRoot + '/../deps/boost_regex/example'

Remove-Item -Recurse -Force $boostExamplesDir

Write-Output "Removal Complete"
exit 0;