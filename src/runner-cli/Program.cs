// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace RunnerCLI;

public class Program
{
    private static SettingsUtils _settingsUtils = new();
    private static Option<string> moduleOption = new("--module", $"The module name: {DescriptiveModuleNames}")
    {
        IsRequired = true,
    };

    private static string DescriptiveModuleNames => string.Join(", ", GetSettingsConfigTypes().Keys.Order());

    public static async Task<int> Main(string[] args)
    {
        moduleOption.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>() ?? string.Empty;
            var validValues = GetSettingsConfigTypes().Keys.ToList();
            if (!validValues.Contains(value))
            {
                result.ErrorMessage = $"Invalid module name. Valid values are: {DescriptiveModuleNames}";
            }
        });

        var rootCommand = new RootCommand("MyApp Command-Line Interface");
        var dscCommand = new Command("dsc", "Manage DSC modules");

        AddGetCommand(dscCommand);
        AddSetCommand(dscCommand);

        rootCommand.AddCommand(dscCommand);
        return await rootCommand.InvokeAsync(args);
    }

    private static void AddGetCommand(Command dscCommand)
    {
        var getCommand = new Command("get", "Get module information")
        {
            moduleOption,
        };

        getCommand.SetHandler(async (context) =>
        {
            var module = context.ParseResult.GetValueForOption(moduleOption);
            var result = GetSettings(module!);
            Console.WriteLine(result);
            await Task.CompletedTask;
        });

        dscCommand.AddCommand(getCommand);
    }

    private static void AddSetCommand(Command dscCommand)
    {
        var writeOption = new Option<string>("--write", "JSON input to write") { IsRequired = true };
        var setCommand = new Command("set", "Set module information")
        {
            moduleOption,
            writeOption,
        };

        setCommand.SetHandler(async (context) =>
        {
            var module = context.ParseResult.GetValueForOption(moduleOption);
            var write = context.ParseResult.GetValueForOption(writeOption);

            SaveSettings(module!, write!);
            await Task.CompletedTask;
        });

        dscCommand.AddCommand(setCommand);
    }

    private static Dictionary<string, Type> GetSettingsConfigTypes()
    {
        return Assembly.GetAssembly(typeof(ISettingsConfig))!
            .GetTypes()
            .Where(t => typeof(ISettingsConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t);
    }

    private static string GetSettings(string moduleName)
    {
        if (GetSettingsConfigTypes().TryGetValue(moduleName, out var moduleType))
        {
            var method = typeof(Program).GetMethod(nameof(GetSettingsInternal), BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method!.MakeGenericMethod(moduleType);
            var result = genericMethod.Invoke(null, null);
            return result!.ToString()!;
        }
        else
        {
            throw new ArgumentException($"Module name '{moduleName}' is not recognized.");
        }
    }

    private static void SaveSettings(string moduleName, string settings)
    {
        if (GetSettingsConfigTypes().TryGetValue(moduleName, out var moduleType))
        {
            var method = typeof(Program).GetMethod(nameof(SaveSettingsInternal), BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method!.MakeGenericMethod(moduleType);
            genericMethod.Invoke(null, [settings]);
        }
        else
        {
            throw new ArgumentException($"Module name '{moduleName}' is not recognized.");
        }
    }

    private static string GetSettingsInternal<T>()
        where T : ISettingsConfig, new()
    {
        var setting = new T();
        return _settingsUtils.GetSettings<T>(setting.GetModuleName()).ToJsonString();
    }

    private static void SaveSettingsInternal<T>(string json)
        where T : ISettingsConfig, new()
    {
        var setting = new T();
        _settingsUtils.SaveSettings(json, setting.GetModuleName());
    }
}
