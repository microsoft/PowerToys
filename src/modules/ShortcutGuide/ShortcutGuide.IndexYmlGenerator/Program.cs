// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;

namespace ShortcutGuide.IndexYmlGenerator
{
    public static class Program
    {
        public static void Main()
        {
            Logger.InitializeLogger(@"\ShortcutGuide\IndexYmlGenerator\Logs");
            Logger.LogInfo("Shortcut Guide index file generation started.");

            try
            {
                ManifestIndexGenerator.CreateIndexYmlFile();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating Shortcut Guide index file: {ex.Message}", ex);

                // Informs the Shortcut Guide UI that the index generation failed.
                Environment.ExitCode = 1;
            }

            Logger.LogInfo("Shortcut Guide index file generation completed.");
        }
    }
}
