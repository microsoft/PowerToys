taskkill /f /im explorer.exe

.\uninstall_msix.ps1
.\build_msix.ps1
.\sign_msix.ps1
.\install_msix.ps1

start $Env:windir\explorer.exe
