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
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: Generator.exe <PowerToys.Settings.UI.Lib.dll path> <module output path> <manifest output path>");
            return 1;
        }

        var dllPath = args[0];
        var moduleOutputPath = args[1];
        var manifestOutputPath = string.Empty;

        bool documentationMode = Path.GetExtension(moduleOutputPath) == ".md";
        bool sampleMode = Path.GetExtension(moduleOutputPath) == ".yaml";

        if (!documentationMode && !sampleMode)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: Generator.exe <PowerToys.Settings.UI.Lib.dll path> <module output path> <manifest output path>");
                return 1;
            }
            else
            {
                manifestOutputPath = args[2];
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(moduleOutputPath));

            var assembly = Assembly.LoadFrom(dllPath);
            var moduleSettings = Introspection.ParseModuleSettings(assembly);
            var generalSettings = Introspection.ParseGeneralSettings(assembly);
#if DEBUG
            PrintUniquePropertyTypes(moduleSettings);
#endif
            var outputFileContents = string.Empty;
            if (documentationMode)
            {
                outputFileContents = DocumentationGeneration.EmitDocumentationFileContents(moduleSettings, generalSettings);
            }
            else if (sampleMode)
            {
                outputFileContents = SampleGeneration.EmitSampleFileContents(moduleSettings, generalSettings);
            }
            else
            {
                var manifestFileContents = DSCGeneration.EmitManifestFileContents();
                File.WriteAllText(manifestOutputPath, manifestFileContents);
                var debugSettingsPath = Path.Combine(Directory.GetParent(dllPath).FullName, "PowerToys.Settings.exe");
                outputFileContents = DSCGeneration.EmitModuleFileContents(moduleSettings, generalSettings, debugSettingsPath);
            }

            File.WriteAllText(moduleOutputPath, outputFileContents);
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
