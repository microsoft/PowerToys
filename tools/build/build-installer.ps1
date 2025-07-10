Write-Host 'Make sure powertoys build is complete and available'

Write-Host '[CLEAN] installer (keep *.exe)'
git clean -xfd -e '*.exe' -- .\installer\ | Out-Null

MSBuild -t:restore .\installer\PowerToysSetup.sln -p:RestorePackagesConfig=true

MSBuild -m .\installer\PowerToysSetup.sln /t:PowerToysInstallerVNext /p:Configuration=Release /p:Platform="x64" /p:PerUser=true

MSBuild -m .\installer\PowerToysSetup.sln /t:PowerToysBootstrapperVNext /p:Configuration=Release /p:Platform="x64" /p:PerUser=true

Write-Host '[PIPELINE] Completed'