$version = ([xml](Get-Content ..\Version.props)).Project.PropertyGroup.Version

(Get-Content appxmanifest.xml) `
  -replace '(Name="[\.\w]+"\sVersion=")([\d\.]+)"', -join('${1}', $version, '.0"') `
  |  Out-File -Encoding utf8 appxmanifest.xml