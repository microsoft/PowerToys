# [Build tools](/tools/build/)

These build tools help building PowerToys projects.

## [build-essentials.ps1](/tools/build/build-essentials.ps1)

A script that builds certain specified PowerToys projects. You can edit the `$ProjectsToBuild` variable to specify which projects to build.

## [convert-resx-to-rc.ps1](/tools/build/convert-resx-to-rc.ps1)

This script converts a .resx file to a .rc file, so it can be used in a C++ project. More information on localization can be found in the [localization guide](/doc/devdocs/localization.md).

## [convert-stringtable-to-resx.ps1](/tools/build/convert-stringtable-to-resx.ps1)

This script converts a stringtable to a .resx file, so it can be used in a C# project. More information about this script can be found in the [localization guide](/doc/devdocs/localization.md).

## [move-and-rename-resx.ps1](/tools/build/move-and-rename-resx.ps1)

This script is used by the pipeline to move the .resx files to the correct location, so that they can be localized into different languages.

## [move-uwp-resw.ps1](/tools/build/move-uwp-resw.ps1)

This script is used by the pipeline to move the .resw files to the correct location, so that they can be localized into different languages.

## [versionSetting.ps1](/tools/build/versionSetting.ps1)

Sets `version.props` file with the version number.

## [video_conference_make_cab.ps1](/tools/build/video_conference_make_cab.ps1)

This script creates a cab file for the Video Conference Mute driver.
