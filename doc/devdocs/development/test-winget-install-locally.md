## If for any reason, you'd like to test winget install scenario, you can follow this doc:

### Powertoys winget manifest definition:
[winget repository](https://github.com/microsoft/winget-pkgs/tree/master/manifests/m/Microsoft/PowerToys)

### How to test a winget installation locally:
1. Get artifacts from release CI pipeline  Pipelines - Runs for PowerToys Signed YAML Release Build, or you can build one yourself by execute the 
 'tools\build\build-installer.ps1' script

2. Get the artifact hash, this is required to define winget manifest
```powershell
cd /path/to/your/directory/contains/installer
Get-FileHash -Path ".\<Installer-name>.exe" -Algorithm SHA256
```
 3.  Host your installer.exe - Attention: staged github release artifacts or artifacts in release pipeline is not OK in this step
You can self-host it or you can upload to a publicly available endpoint  
**How to selfhost it** (A extremely simple way):
```powershell
python -m http.server 8000
```

4. Download a version folder from wingetpkgs like: [version 0.92.1](https://github.com/microsoft/winget-pkgs/tree/master/manifests/m/Microsoft/PowerToys/0.92.1)
and you get **a folder contains 3 yml files**
>note: Do not put any files other than these three in this folder

5. Modify the yml files based on your version and the self hosted artifact link, and modify the sha256 hash for the installer you'd like to use

6. Start winget install:
```powershell
#execute as admin
winget settings --enable LocalManifestFiles
winget install --manifest "<folder_path_of_manifest_files>" --architecture x64 --scope user
```