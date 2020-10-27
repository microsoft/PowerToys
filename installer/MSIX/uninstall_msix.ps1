Get-AppxPackage -Name '*PowerToys' | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
