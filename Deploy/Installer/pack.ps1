$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..\..'

& $root\Deploy\Installer\InnoSetup\ISCC.exe $root\Deploy\Installer\Installer.iss
