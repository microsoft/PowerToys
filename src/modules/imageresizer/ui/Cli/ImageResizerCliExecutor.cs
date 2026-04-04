// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.Cli
{
    /// <summary>
    /// Executes Image Resizer CLI operations.
    /// Instance-based design for better testability and Single Responsibility Principle.
    /// </summary>
    public class ImageResizerCliExecutor
    {
        /// <summary>
        /// Runs the CLI executor with the provided command-line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Exit code.</returns>
        public int Run(string[] args)
        {
            var cliOptions = CliOptions.Parse(args);

            if (cliOptions.ParseErrors.Count > 0)
            {
                foreach (var error in cliOptions.ParseErrors)
                {
                    Console.Error.WriteLine(error);
                    CliLogger.Error($"Parse error: {error}");
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
                Console.WriteLine(Resources.CLI_NoInputFiles);
                CliOptions.PrintUsage();
                return 1;
            }

            return RunSilentModeAsync(cliOptions).GetAwaiter().GetResult();
        }

        private async Task<int> RunSilentModeAsync(CliOptions cliOptions)
        {
            var batch = ResizeBatch.FromCliOptions(Console.In, cliOptions);
            var settings = Settings.Default;
            CliSettingsApplier.Apply(cliOptions, settings);

            CliLogger.Info($"CLI mode: processing {batch.Files.Count} files");

            // Use accessible line-based progress if requested or detected
            bool useLineBasedProgress = cliOptions.ProgressLines ?? false;
            int lastReportedMilestone = -1;

            var results = await batch.ProcessAsync(
                (completed, total) =>
                {
                    var progress = (int)((completed / total) * 100);

                    if (useLineBasedProgress)
                    {
                        // Milestone-based progress (0%, 25%, 50%, 75%, 100%)
                        int milestone = (progress / 25) * 25;
                        if (milestone > lastReportedMilestone || completed == (int)total)
                        {
                            lastReportedMilestone = milestone;
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Resources.CLI_ProgressFormat, progress, completed, (int)total));
                        }
                    }
                    else
                    {
                        // Traditional carriage return mode
                        Console.Write(string.Format(CultureInfo.InvariantCulture, "\r{0}", string.Format(CultureInfo.InvariantCulture, Resources.CLI_ProgressFormat, progress, completed, (int)total)));
                    }
                },
                settings,
                CancellationToken.None);

            if (!useLineBasedProgress)
            {
                Console.WriteLine();
            }

            // ProcessAsync returns all results including successes; only report problems.
            var problemResults = results.Where(r => r is not SuccessResult).ToList();
            if (problemResults.Count > 0)
            {
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Resources.CLI_CompletedWithErrors, problemResults.Count));
                CliLogger.Error($"Processing completed with {problemResults.Count} problem(s)");
                foreach (var result in problemResults)
                {
                    var fileName = Path.GetFileName(result.FilePath);
                    var message = result switch
                    {
                        ErrorResult e => e.Exception.Message,
                        FileReplaceFailedResult r => r.Exception.Message,
                        FileRecycleFailedResult r => r.Exception.Message,
                        _ => string.Empty,
                    };
                    Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0}: {1}", fileName, message));
                    CliLogger.Error($"  {fileName}: {message}");
                }

                return 1;
            }

            CliLogger.Info("CLI batch completed successfully");
            Console.WriteLine(Resources.CLI_AllFilesProcessed);
            return 0;
        }
    }
}
