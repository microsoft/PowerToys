param (
  [bool]$debug = 0
)

$PackagingLayoutFile = "PackagingLayout.xml"

if ($debug) {
  (Get-Content $PackagingLayoutFile) `
  -replace 'x64\\Release\\', 'x64\Debug\' `
  |  Out-File -Encoding utf8 "$env:temp\$PackagingLayoutFile"
  $PackagingLayoutFile = "$env:temp\$PackagingLayoutFile"
}
makeappx build /v /overwrite /f $PackagingLayoutFile /id "PowerToys-x64" /op bin\
