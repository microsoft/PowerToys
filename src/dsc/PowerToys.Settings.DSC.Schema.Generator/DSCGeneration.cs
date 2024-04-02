// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using static PowerToys.Settings.DSC.Schema.Introspection;

namespace PowerToys.Settings.DSC.Schema;

internal sealed class DSCGeneration
{
    private static readonly string DoubleNewLine = Environment.NewLine + Environment.NewLine;

    private struct AdditionalPropertiesInfo
    {
        public string Name;

        public string Type;
    }

    private static readonly Dictionary<string, AdditionalPropertiesInfo> AdditionalPropertiesInfoPerModule = new Dictionary<string, AdditionalPropertiesInfo> { { "PowerLauncher", new AdditionalPropertiesInfo { Name = "Plugins", Type = "Hashtable[]" } } };

    private static string EmitEnumDefinition(Type type)
    {
        var values = string.Empty;

        int i = 0;
        foreach (var name in Enum.GetNames(type))
        {
            values += "    " + name;

            // Nullable enums seem to be not supported by winget, so the workaround is to always start with '1', because by default the values are initialized to zero. That allows us to use zero as a "lack of value" indicator.
            if (i == 0)
            {
                values += " = 1";
            }

            values += Environment.NewLine;
            i++;
        }

        return $$"""
        enum {{type.Name}} {
        {{values}}}
        """;
    }

    private struct PropertyEmitInfo
    {
        public string Name;
        public string Type;
        public string Initializer;
        public string EqualityOperator;
        public string DefaultValue;

        public PropertyEmitInfo(string name, Type property)
        {
            Name = name;

            bool intLike = Common.InferIsInt(property);
            bool boolLike = Common.InferIsBool(property);

            var rawType = "string";
            var isNullable = true;
            DefaultValue = "$null";
            EqualityOperator = "-ne";
            Initializer = "= $null";

            if (intLike)
            {
                rawType = "int";
                isNullable = false;
            }
            else if (boolLike)
            {
                rawType = "bool";
                isNullable = false;
            }
            else if (property.IsEnum)
            {
                rawType = property.Name;
                isNullable = true;
                Initializer = string.Empty;
                DefaultValue = "0";
            }

            // For strings
            else
            {
                EqualityOperator = "-notlike";
                DefaultValue = "''";
            }

            // We must make all our properties nullable to be able to detect which of them weren't supplied
            Type = isNullable ? rawType : $"Nullable[{rawType}]";
        }
    }

    private static string EmitPropertyDefinition(PropertyEmitInfo info)
    {
        return $$"""
    [DscProperty()] [{{info.Type}}]
    ${{info.Name}} {{info.Initializer}}
""";
    }

    private static string EmitPropertyApplyChangeStatements(string moduleName, PropertyEmitInfo info, string localPropertyName = null)
    {
        if (localPropertyName == null)
        {
            localPropertyName = info.Name;
        }

        return $$"""
                if ($this.{{localPropertyName}} {{info.EqualityOperator}} {{info.DefaultValue}}) {
                    $Changes.Value += "set {{moduleName}}.{{info.Name}} `"$($this.{{localPropertyName}})`""
                }
        """;
    }

    private static string EmitModuleDefinition(SettingsStructure module)
    {
        bool generalSettings = module.Name == "GeneralSettings";

        var properties = module.Properties
                .Where(property => !property.Value.IsIgnored)
                .Select(property => new PropertyEmitInfo(property.Key, property.Value.Type));

        var propertyDefinitionsBlock = string.Empty;
        var applyChangesBlock = string.Empty;

        foreach (var property in properties)
        {
            var definition = EmitPropertyDefinition(property);
            var applyChanges = EmitPropertyApplyChangeStatements(module.Name, property);

            propertyDefinitionsBlock += definition + DoubleNewLine;
            applyChangesBlock += applyChanges + DoubleNewLine;
        }

        bool hasAdditionalProperties = AdditionalPropertiesInfoPerModule.TryGetValue(module.Name, out var additionalPropertiesInfo);

        // Enabled property of each module is contained in General settings
        if (!generalSettings)
        {
            propertyDefinitionsBlock += $$"""
            [DscProperty(Key)] [Nullable[bool]]
            $Enabled = $null

        """;

            if (hasAdditionalProperties)
            {
                propertyDefinitionsBlock += $$"""

            [DscProperty()] [{{additionalPropertiesInfo.Type}}]
            ${{additionalPropertiesInfo.Name}} = @()


        """;
            }

            applyChangesBlock += EmitPropertyApplyChangeStatements("General.Enabled", new PropertyEmitInfo($"{module.Name}", typeof(bool)), "Enabled");
        }

        var additionalPropertiesCheckBlock = string.Empty;
        if (hasAdditionalProperties)
        {
            additionalPropertiesCheckBlock = $$"""
                if ($this.{{additionalPropertiesInfo.Name}}.Count -gt 0) {
                    $AdditionalPropertiesTmpPath = [System.IO.Path]::GetTempFileName()
                    $this.{{additionalPropertiesInfo.Name}} | ConvertTo-Json | Set-Content -Path $AdditionalPropertiesTmpPath
                    $Changes.Value += "setAdditional {{module.Name}} `"$AdditionalPropertiesTmpPath`""
                }
        """;
        }

        return $$"""
class {{module.Name}} {
{{propertyDefinitionsBlock}}    ApplyChanges([ref]$Changes) {
{{applyChangesBlock}}

{{additionalPropertiesCheckBlock}}
    }
}


""";
    }

    public static string EmitModuleFileContents(SettingsStructure[] moduleSettings, SettingsStructure generalSettings, string debugSettingsPath)
    {
        var enumsToEmit = new HashSet<Type>();

        var modulesBlock = string.Empty;
        var modulesResourcePropertiesBlock = string.Empty;
        var applyModulesChangesBlock = string.Empty;

        foreach (var module in moduleSettings.Append(generalSettings))
        {
            enumsToEmit.UnionWith(module.Properties
                .Where(property => property.Value.Type.IsEnum)
                .Select(property => property.Value.Type));

            modulesBlock += EmitModuleDefinition(module);

            applyModulesChangesBlock += $$"""
                $this.{{module.Name}}.ApplyChanges([ref]$ChangesToApply)
                
            """;

            modulesResourcePropertiesBlock += $$"""
                    [DscProperty()]
                    [{{module.Name}}]${{module.Name}} = [{{module.Name}}]::new()


                """;
        }

        var enumsBlock = string.Join(DoubleNewLine, enumsToEmit.Select(EmitEnumDefinition));
        var version = interop.CommonManaged.GetProductVersion().Replace("v", string.Empty);

        return $$"""
        #region enums
        enum PowerToysConfigureEnsure {
            Absent
            Present
        }
        
        {{enumsBlock}}
        #endregion enums

        #region DscResources
        {{modulesBlock}}
        [DscResource()]
        class PowerToysConfigure {
            [DscProperty(Key)] [PowerToysConfigureEnsure]
            $Ensure = [PowerToysConfigureEnsure]::Present

            [bool] $Debug = $false

        {{modulesResourcePropertiesBlock}}
            [string] GetPowerToysSettingsPath() {
                # Obtain PowerToys install location 
                if ($this.Debug -eq $true) {
                    $SettingsExePath = "{{debugSettingsPath}}"
                } else {
                    $installation = Get-CimInstance Win32_Product | Where-Object {$_.Name -eq "PowerToys (Preview)" -and $_.Version -eq "{{version}}"}
                
                    if ($installation) {
                        $SettingsExePath = Join-Path (Join-Path $installation.InstallLocation WinUI3Apps) PowerToys.Settings.exe
                        # Handle spaces in the path
                        $SettingsExePath = "`"$SettingsExePath`""
                    } else {
                        throw "PowerToys installation wasn't found."
                    }
                }

                return $SettingsExePath
            }

            [PowerToysConfigure] Get() {
                $CurrentState = [PowerToysConfigure]::new()
                $SettingsExePath = $this.GetPowerToysSettingsPath()
                $SettingsTmpFilePath = [System.IO.Path]::GetTempFileName()

                $SettingsToRequest = @{}
                foreach ($module in $CurrentState.PSObject.Properties) {
                    $moduleName = $module.Name
                    # Skip utility properties
                    if ($moduleName -eq "Ensure" -or $moduleName -eq "Debug") {
                        continue
                    }

                    $moduleProperties = $module.Value
                    $propertiesArray = @() 
                    foreach ($property in $moduleProperties.PSObject.Properties) {
                        $propertyName = $property.Name
                        # Skip Enabled properties - they should be requested from GeneralSettings
                        if ($propertyName -eq "Enabled") {
                            continue
                        }

                        $propertiesArray += $propertyName
                    }

                    $SettingsToRequest[$moduleName] = $propertiesArray
                }

                $settingsJson = $SettingsToRequest | ConvertTo-Json
                $settingsJson | Set-Content -Path $SettingsTmpFilePath

                Start-Process -FilePath $SettingsExePath -Wait -Args "get `"$SettingsTmpFilePath`""
                $SettingsValues = Get-Content -Path $SettingsTmpFilePath -Raw

                if ($this.Debug -eq $true) {
                    $TempFilePath = Join-Path -Path $env:TEMP -ChildPath "PowerToys.DSC.TestConfigure.txt"
                    Set-Content -Path "$TempFilePath" -Value ("Requested:`r`n" + $settingsJson + "`r`n" + "Got:`r`n" + $SettingsValues + "`r`n" + (Get-Date -Format "o")) -Force
                }

                $SettingsValues = $SettingsValues | ConvertFrom-Json
                foreach ($module in $SettingsValues.PSObject.Properties) {
                    $moduleName = $module.Name
                    $obtainedModuleSettings = $module.Value
                    $moduleRef = $CurrentState.$moduleName
                    foreach ($property in $obtainedModuleSettings.PSObject.Properties) {
                        $propertyName = $property.Name
                        $moduleRef.$propertyName = $property.Value
                    }
                }

                Remove-Item -Path $SettingsTmpFilePath

                return $CurrentState
            }

            [bool] Test() {
                # NB: we must always assume that the configuration isn't applied, because changing some settings produce external side-effects
                return $false 
            }

            [void] Set() {
                $SettingsExePath = $this.GetPowerToysSettingsPath()
                $ChangesToApply = @()

            {{applyModulesChangesBlock}}
                if ($this.Debug -eq $true) {
                    $tmp_info = $ChangesToApply
                    # $tmp_info = $this | ConvertTo-Json -Depth 10

                    $TempFilePath = Join-Path -Path $env:TEMP -ChildPath "PowerToys.DSC.TestConfigure.txt"
                    Set-Content -Path "$TempFilePath" -Value ($tmp_info + "`r`n" + (Get-Date -Format "o")) -Force
                } 

                # Stop any running PowerToys instances
                Stop-Process -Name "PowerToys.Settings" -PassThru | Wait-Process
                $PowerToysProcessStopped = Stop-Process -Name "PowerToys" -PassThru
                $PowerToysProcessStopped | Wait-Process

                foreach ($change in $ChangesToApply) {
                    Start-Process -FilePath $SettingsExePath -Wait -Args "$change"
                }

                # If the PowerToys was stopped, restart it.
                if ($PowerToysProcessStopped -ne $null) {
                    Start-Process -FilePath $SettingsExePath
                }
            }
        }
        #endregion DscResources
        """;
    }
}
