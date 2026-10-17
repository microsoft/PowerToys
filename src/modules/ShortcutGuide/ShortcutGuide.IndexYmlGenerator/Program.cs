// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace ShortcutGuide.IndexYmlGenerator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Logger.InitializeLogger(@"\ShortcutGuide\IndexYmlGenerator\Logs");
            Logger.LogInfo("Shortcut Guide index file generation started.");

            string path = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : ManifestIndexGenerator.DefaultManifestsPath;

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                var result = ManifestIndexGenerator.CreateIndexYmlFile(path);
                foreach (var (fileName, warning) in result.Warnings)
                {
                    Logger.LogWarning($"Skipping manifest '{fileName}': {warning}");
                }

                foreach (var (fileName, error) in result.Errors)
                {
                    Logger.LogError($"Error processing file '{fileName}'.", error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating Shortcut Guide index file: {ex.Message}", ex);

                // Inform the caller that the index generation failed.
                Environment.ExitCode = 1;
            }

            stopwatch.Stop();
            Logger.LogInfo($"Shortcut Guide index file generation completed in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }
}
