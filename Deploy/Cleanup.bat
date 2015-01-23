cd /d %~dp0

REM Clean Plugins
cd ..\Output\Release\Plugins
del NLog.dll /s
del NLog.config /s
del Wox.Plugin.pdb /s
del Wox.Plugin.dll /s
del Wox.Core.dll /s
del Wox.Core.pdb /s
del ICSharpCode.SharpZipLib.dll /s
del NAppUpdate.Framework.dll /s
del Wox.Infrastructure.dll /s
del Wox.Infrastructure.pdb /s
del Newtonsoft.Json.dll /s
del WindowsInput.dll /s

REM Clean Wox
cd ..
del *.xml
