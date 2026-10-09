// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public class ETLConverter
    {
        private const int TracerptConversionTimeout = 60000; // 60 seconds in milliseconds
        private const string ETLConversionOutputFormat = "xml"; // Assuming XML output format

        private readonly string etwDirPath;

        private readonly string tracerptPath;

        public ETLConverter(string etwDirPath, string tracerptPath)
        {
            this.etwDirPath = etwDirPath;
            this.tracerptPath = tracerptPath;
        }

        private bool ETLConversionsFailed { get; set; }

        public async Task ConvertDiagnosticsETLsAsync(CancellationToken cancellationToken = default)
        {
            var etlConversionTasks = new List<Task>();
            var directoryInfo = new DirectoryInfo(etwDirPath);

            foreach (var fileInfo in directoryInfo.GetFiles("*.etl", SearchOption.AllDirectories))
            {
                var task = Task.Run(() => ConvertETLAsync(fileInfo.FullName, cancellationToken), cancellationToken);
                etlConversionTasks.Add(task);
            }

            try
            {
                await Task.WhenAll(etlConversionTasks);
            }
            catch (Exception)
            {
                ETLConversionsFailed = true;
            }

            if (ETLConversionsFailed)
            {
                throw new InvalidOperationException("One or more ETL conversions failed.");
            }
        }

        private void ConvertETLAsync(string etlFilePathToConvert, CancellationToken cancellationToken)
        {
            var outputFilePath = Path.ChangeExtension(etlFilePathToConvert, $".{ETLConversionOutputFormat}");

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            var tracerPtArguments = $"\"{etlFilePathToConvert}\" -o \"{outputFilePath}\" -lr -y -of {ETLConversionOutputFormat}";

            var startInfo = new ProcessStartInfo
            {
                FileName = tracerptPath + "\\tracerpt.exe",
                Arguments = tracerPtArguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Logger.LogError("Failed to start tracerpt process.");
                }

                var processExited = process.WaitForExit(TracerptConversionTimeout);

                if (!processExited)
                {
                    process.Kill();
                    Logger.LogError("ETL conversion process timed out.");
                }

                var exitCode = process.ExitCode;
                if (exitCode != 0)
                {
                    Logger.LogError($"ETL conversion failed with exit code {exitCode}.");
                }
            }
        }
    }
}
