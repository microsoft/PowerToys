cd /D "%~dp0"

call makeappx build /v /overwrite /f PackagingLayout.xml /id "PowerToys-x64" /op bin\ || exit /b 1
