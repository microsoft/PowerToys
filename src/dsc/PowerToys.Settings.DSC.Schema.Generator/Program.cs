// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PowerToys.Settings.DSC.Schema;

internal sealed class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: Generator.exe <PowerToys.Settings.UI.Lib.dll path> <output path>");
            return 1;
        }

        var dllPath = args[0];
        var outputPath = args[1];

        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var moduleSettings = Introspection.ParseModuleSettings(assembly);
            var generalSettings = Introspection.ParseGeneralSettings(assembly);
#if DEBUG
            PrintUniquePropertyTypes(moduleSettings);
#endif
            var debugSettingsPath = Path.Combine(Directory.GetParent(dllPath).FullName, "PowerToys.Settings.exe");

            var schemaFileContents = Generation.EmitModuleFileContents(moduleSettings, generalSettings, debugSettingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, schemaFileContents);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static void PrintUniquePropertyTypes(Introspection.SettingsStructure[] moduleSettings)
    {
        Console.WriteLine("Detected the following module properties types:");
        var propertyTypes = new HashSet<Type>();
        foreach (var settings in moduleSettings)
        {
            Console.WriteLine($"{settings.Name}");
            foreach (var (_, property) in settings.Properties)
            {
                if (!property.IsIgnored)
                {
                    propertyTypes.Add(property.Type);
                }
            }
        }

        Console.WriteLine("\nDetected the following unique property types:");
        foreach (var type in propertyTypes)
        {
            Console.WriteLine($"{type}");
        }
    }
}
