# Localization

## Localization on the pipeline (CDPX)
[The localization step](https://github.com/microsoft/PowerToys/blob/86d77103e9c69686c297490acb04775d43ef8b76/.pipelines/pipeline.user.windows.yml#L45-L52) is run on the pipeline before the solution is built. This step runs the [build-localization](https://github.com/microsoft/PowerToys/blob/master/.pipelines/build-localization.cmd) script, which generates resx files for all the projects with localization enabled using the `Localization.XLoc` package.

The [`Localization.XLoc`](https://github.com/microsoft/PowerToys/blob/86d77103e9c69686c297490acb04775d43ef8b76/.pipelines/build-localization.cmd#L24-L25) tool is run on the repo root, and it checks for all occurrences of `LocProject.json`. Each localized project has a `LocProject.json` file in the project root, which contains the location of the English resx file, list of languages for localization, and the output path where the localized resx files are to be copied to. In addition to this, some other parameters can be set, such as whether the language ID should be added as a folder in the file path or in the file name. When the CDPX pipeline is run, the localization team is notified of changes in the English resx files. For each project with localization enabled, a `loc` folder (see [this](https://github.com/microsoft/PowerToys/tree/master/src/modules/launcher/Microsoft.Launcher/loc) for example) is created in the same directory as the `LocProject.json` file, which language specific folders with `lcl` files with a folder path equivalent to where it is to be copied to. `lcl` files contain the English resources along with their translation for that language. These are described in more detail [here](#lcl-files). Once the `.resx` files are generated, they will be used during the `Build PowerToys` step for localized versions of the modules.

Since the localization script requires certain nuget packages, the [`restore-localization`](https://github.com/microsoft/PowerToys/blob/master/.pipelines/restore-localization.cmd) script is run before running `build-localization` to install all the required packages. This script must [run in the `restore` step](https://github.com/microsoft/PowerToys/blob/86d77103e9c69686c297490acb04775d43ef8b76/.pipelines/pipeline.user.windows.yml#L37-L39) of pipeline because [the host is network isolated](https://onebranch.visualstudio.com/Pipeline/_wiki/wikis/Pipeline.wiki/2066/Consuming-Packages-in-a-CDPx-Pipelinhttps://onebranch.visualstudio.com/Pipeline/_wiki/wikis/Pipeline.wiki/2066/Consuming-Packages-in-a-CDPx-Pipeline?anchor=overview) at the `build` step. The [Toolset package source](https://github.com/microsoft/PowerToys/blob/86d77103e9c69686c297490acb04775d43ef8b76/.pipelines/pipeline.user.windows.yml#L23) is used for this.

The process and variables that can be tweaked on the pipeline are described in more detail [here](https://onebranch.visualstudio.com/Pipeline/_wiki/wikis/Pipeline.wiki/290/Localization).

### UWP Special case
C# projects normally expect localized resource files with the language id in the file name as Resources.`langId`.resx, where `langId` is generally a two character code except for language with specific variants (like zh-Hans or pt-BR):

For example, `path\Resources.resx` for English and `path\Resources.fr.resx` for French.

UWP differs from this as it expects the resources to have the same Resources.resw file name, but they should be present in language specific folders, with the full language ID (such as fr-fr, zh-hans, pt-br, etc.)

For example, `path\en-us\Resources.resw` for English and `path\fr-fr\Resources.resw` for French.

Since the pipeline generates it in this format, [a script is run](https://github.com/microsoft/PowerToys/blob/86d77103e9c69686c297490acb04775d43ef8b76/.pipelines/build-localization.cmd#L29-L31) to move these resw files to the correct format expected by all UWP projects. Currently the only UWP project is [Microsoft.PowerToys.Settings.UI](https://github.com/microsoft/PowerToys/tree/master/src/core/Microsoft.PowerToys.Settings.UI). The script used for moving the resources can be [found here](https://github.com/microsoft/PowerToys/blob/master/tools/localization/move_uwp_resources.ps1). The equivalent full language IDs for each shortened language ID obtained from the pipeline has been hardcoded in the script.

## Enabling localization on a new project
To enable localization on a new project, the first step is to create a file `LocProject.json` in the project root.

For example, for a project in the folder `src\path` where the resx file is present in `resources\Resources.resx`, the LocProject.json file will contain the following:
```
{
    "Projects": [
        {
            "LanguageSet": "Azure_Languages",
            "LocItems": [
                {
                    "SourceFile": "src\\path\\resources\\Resources.resx",
                    "CopyOption": "LangIDOnName",
                    "OutputPath": "src\\path\\resources"
                }
            ]
        }
    ]
}
```
The rest of the steps depend on the project type and are covered in the sections below.

### C++

### C#
Since C# projects natively support `resx` files. updating language in resource csproj

### UWP

## Lcl Files
Lcl files contain all the resources that are present in the English resx file, along with a translation if it has been added.

For example, an entry for a resource in the lcl file looks like this:
```
<Item ItemId=";EditKeyboard_WindowName" ItemType="0;.resx" PsrId="211" Leaf="true">
    <Str Cat="Text">
        <Val><![CDATA[Remap keys]]></Val>
        <Tgt Cat="Text" Stat="Loc" Orig="New">
            <Val><![CDATA[Remapper des touches]]></Val>
        </Tgt>
    </Str>
    <Disp Icon="Str" />
</Item>
```
The `<Tgt>` element would not be present in the initial commits of the lcl files, as only the English version of the string would be present.

**Note:** The CDPX Localization system has a fail-safe check on the lcl files, where if the English string value which is present inside `<Val><![CDATA[*]]></Val>` does not match the value present in the English Resources.resx file then the translated value will not be copied to the localized resx file. This is present so that obsolete translations would not be loaded when the English resource has changed, and the English string will be used rather than the obsolete translation.

## Possible Issues in localization PRs (LEGO)
Since the LEGO PRs update some of the strings in LCL files at a time, there can be multiple PRs which modify the same files, leading to merge conflicts. In most cases this would show up on GitHub as a merge conflict, but sometimes a bad git merge may occur, and the file could end up with incorrect formatting, such as two `<Tgt>` elements for a single resource. These can be fixed by ensuring the elements follow the format described in [this section](#lcl-files). To catch such errors, the build farm should be run for every LEGO PR and if any error occurs in the localization step, we should check the corresponding resx/lcl files for conflicts.

## Enabling localized MSI for a new project
For C++ and UWP projects no additional files are generated with localization that need to be added to the MSI. For C++ projects all the resources are added to the dll/exe, while for UWP projects they are added to the `resources.pri` file (which is present even for an unlocalized project).
signing, and adding resources.dll to MSI
### C++ or UWP

### C#