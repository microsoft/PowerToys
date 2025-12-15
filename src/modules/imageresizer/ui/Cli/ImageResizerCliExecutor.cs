// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;

using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.Cli
{
    /// <summary>
    /// Centralizes the Image Resizer CLI execution logic for the dedicated CLI host.
    /// </summary>
    public static class ImageResizerCliExecutor
    {
        /// <summary>
        /// Entry point used by the dedicated CLI host.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Exit code.</returns>
        public static int RunStandalone(string[] args)
        {
            var cliOptions = CliOptions.Parse(args);

            if (cliOptions.ParseErrors.Count > 0)
            {
                foreach (var error in cliOptions.ParseErrors)
                {
                    Console.Error.WriteLine(error);
                }

                CliOptions.PrintUsage();
                return 1;
            }

            if (cliOptions.ShowHelp)
            {
                CliOptions.PrintUsage();
                return 0;
            }

            if (cliOptions.ShowConfig)
            {
                CliOptions.PrintConfig(Settings.Default);
                return 0;
            }

            if (cliOptions.Files.Count == 0 && string.IsNullOrEmpty(cliOptions.PipeName))
            {
                CliOptions.PrintUsage();
                return 1;
            }

            return RunSilentMode(cliOptions);
        }

        private static int RunSilentMode(CliOptions cliOptions)
        {
            var batch = ResizeBatch.FromCliOptions(Console.In, cliOptions);
            var settings = Settings.Default;
            ApplyCliOptionsToSettings(cliOptions, settings);

            Console.WriteLine($"Processing {batch.Files.Count} file(s)...");

            var errors = batch.Process(
                (completed, total) =>
                {
                    var progress = (int)((completed / total) * 100);
                    Console.Write($"\rProgress: {progress}% ({completed}/{(int)total})");
                },
                settings,
                CancellationToken.None);

            Console.WriteLine();

            var errorList = errors.ToList();
            if (errorList.Count > 0)
            {
                Console.Error.WriteLine($"Completed with {errorList.Count} error(s):");
                foreach (var error in errorList)
                {
                    Console.Error.WriteLine($"  {error.File}: {error.Error}");
                }

                return 1;
            }

            Console.WriteLine("All files processed successfully.");
            return 0;
        }

        /// <summary>
        /// Applies CLI options to the settings, overriding default values.
        /// </summary>
        /// <param name="cliOptions">The CLI options to apply.</param>
        /// <param name="settings">The settings to modify.</param>
        private static void ApplyCliOptionsToSettings(CliOptions cliOptions, Settings settings)
        {
            // If custom width/height specified, use custom size
            if (cliOptions.Width.HasValue || cliOptions.Height.HasValue)
            {
                if (cliOptions.Width.HasValue)
                {
                    settings.CustomSize.Width = cliOptions.Width.Value;
                }
                else
                {
                    // If only height specified, set width to 0 for auto-calculation in Fit mode
                    settings.CustomSize.Width = 0;
                }

                if (cliOptions.Height.HasValue)
                {
                    settings.CustomSize.Height = cliOptions.Height.Value;
                }
                else
                {
                    // If only width specified, set height to 0 for auto-calculation in Fit mode
                    settings.CustomSize.Height = 0;
                }

                if (cliOptions.Unit.HasValue)
                {
                    settings.CustomSize.Unit = cliOptions.Unit.Value;
                }

                if (cliOptions.Fit.HasValue)
                {
                    settings.CustomSize.Fit = cliOptions.Fit.Value;
                }

                // Select custom size (index = Sizes.Count)
                settings.SelectedSizeIndex = settings.Sizes.Count;
            }
            else if (cliOptions.SizeIndex.HasValue)
            {
                // Use preset size by index
                if (cliOptions.SizeIndex.Value >= 0 && cliOptions.SizeIndex.Value < settings.Sizes.Count)
                {
                    settings.SelectedSizeIndex = cliOptions.SizeIndex.Value;
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Invalid size index {cliOptions.SizeIndex.Value}. Using default.");
                }
            }

            // Apply other options
            if (cliOptions.ShrinkOnly.HasValue)
            {
                settings.ShrinkOnly = cliOptions.ShrinkOnly.Value;
            }

            if (cliOptions.Replace.HasValue)
            {
                settings.Replace = cliOptions.Replace.Value;
            }

            if (cliOptions.IgnoreOrientation.HasValue)
            {
                settings.IgnoreOrientation = cliOptions.IgnoreOrientation.Value;
            }

            if (cliOptions.RemoveMetadata.HasValue)
            {
                settings.RemoveMetadata = cliOptions.RemoveMetadata.Value;
            }

            if (cliOptions.JpegQualityLevel.HasValue)
            {
                settings.JpegQualityLevel = cliOptions.JpegQualityLevel.Value;
            }

            if (cliOptions.KeepDateModified.HasValue)
            {
                settings.KeepDateModified = cliOptions.KeepDateModified.Value;
            }

            if (!string.IsNullOrEmpty(cliOptions.FileName))
            {
                settings.FileName = cliOptions.FileName;
            }
        }
    }
}
