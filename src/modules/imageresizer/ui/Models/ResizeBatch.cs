#pragma warning disable IDE0073, SA1636
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073, SA1636

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Properties;
using ImageResizer.Services;
using Microsoft.Win32.SafeHandles;

namespace ImageResizer.Models
{
    public class ResizeBatch
    {
        private readonly IFileSystem _fileSystem = new FileSystem();
        private readonly List<ResizeError> _inputErrors = [];
        private readonly HashSet<string> _resolvedInputPaths = new(StringComparer.Ordinal);
        private static IAISuperResolutionService _aiSuperResolutionService;

        public string DestinationDirectory { get; set; }

        public ICollection<string> Files { get; } = new List<string>();

        internal IReadOnlyList<ResizeError> InputErrors => _inputErrors;

        public static void SetAiSuperResolutionService(IAISuperResolutionService service)
        {
            _aiSuperResolutionService = service;
        }

        public static void DisposeAiSuperResolutionService()
        {
            _aiSuperResolutionService?.Dispose();
            _aiSuperResolutionService = null;
        }

        private static readonly HashSet<string> ValidImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".dib", ".gif", ".jfif", ".jpe", ".jpeg", ".jpg",
            ".jxr", ".png", ".rle", ".tif", ".tiff", ".wdp",
        };

        /// <summary>
        /// Validates if a file path is a supported image format.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        /// <returns>True if the path is valid and points to a supported image file.</returns>
        private static bool IsValidImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!File.Exists(path))
            {
                return false;
            }

            var ext = Path.GetExtension(path);
            return ValidImageExtensions.Contains(ext);
        }

        /// <summary>
        /// Creates a ResizeBatch from CliOptions.
        /// </summary>
        /// <param name="standardInput">Standard input stream for reading additional file paths.</param>
        /// <param name="options">The parsed CLI options.</param>
        /// <returns>A ResizeBatch instance.</returns>
        public static ResizeBatch FromCliOptions(TextReader standardInput, CliOptions options)
            => FromCliOptionsCore(standardInput, options, reportInvalidInputs: false);

        /// <summary>
        /// Creates a batch for the public CLI and preserves a diagnostic for every rejected input.
        /// </summary>
        /// <param name="standardInput">Standard input stream for reading additional file paths.</param>
        /// <param name="options">The parsed CLI options.</param>
        /// <returns>A resize batch containing valid files and input diagnostics.</returns>
        internal static ResizeBatch FromCliOptionsWithDiagnostics(TextReader standardInput, CliOptions options)
            => FromCliOptionsCore(standardInput, options, reportInvalidInputs: true);

        private static ResizeBatch FromCliOptionsCore(TextReader standardInput, CliOptions options, bool reportInvalidInputs)
        {
            var batch = new ResizeBatch
            {
                DestinationDirectory = options.DestinationDirectory,
            };

            foreach (var file in options.Files)
            {
                if (reportInvalidInputs)
                {
                    AddStrictInput(batch, file);
                }
                else
                {
                    AddLenientInput(batch, file);
                }
            }

            if (string.IsNullOrEmpty(options.PipeName))
            {
                // NB: We read these from stdin since there are limits on the number of args you can have
                // Only read from stdin if it's redirected (piped input), not from interactive terminal
                string file;
                if (standardInput != null && (Console.IsInputRedirected || !ReferenceEquals(standardInput, Console.In)))
                {
                    while ((file = standardInput.ReadLine()) != null)
                    {
                        if (reportInvalidInputs)
                        {
                            AddStrictInput(batch, file);
                        }
                        else
                        {
                            AddLenientInput(batch, file);
                        }
                    }
                }
            }
            else
            {
                using (NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", options.PipeName, PipeDirection.In))
                {
                    // Connect to the pipe or wait until the pipe is available.
                    pipeClient.Connect();

                    using (StreamReader sr = new StreamReader(pipeClient, Encoding.Unicode))
                    {
                        string file;

                        // Read file paths from the named pipe
                        while ((file = sr.ReadLine()) != null)
                        {
                            if (reportInvalidInputs)
                            {
                                AddStrictInput(batch, file);
                            }
                            else if (IsValidImagePath(file))
                            {
                                // Preserve the legacy GUI/context-menu behavior for named-pipe input.
                                batch.Files.Add(file);
                            }
                        }
                    }
                }
            }

            return batch;
        }

        private static void AddLenientInput(ResizeBatch batch, string input)
        {
            // Keep the GUI and context-menu behavior unchanged: unsupported selections are ignored.
            var absolutePath = Path.IsPathRooted(input) ? input : Path.GetFullPath(input);
            if (IsValidImagePath(absolutePath))
            {
                batch.Files.Add(absolutePath);
            }
        }

        private static void AddStrictInput(ResizeBatch batch, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AddInputError(batch, input ?? string.Empty, Resources.CLI_ErrorFileNotFound);
                return;
            }

            try
            {
                if (ContainsWildcard(input))
                {
                    AddWildcardMatches(batch, input);
                }
                else
                {
                    AddResolvedInput(batch, input, Path.GetFullPath(input));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                AddInputError(
                    batch,
                    input,
                    string.Format(CultureInfo.InvariantCulture, Resources.CLI_ErrorInvalidInputPath, ex.Message));
            }
        }

        private static void AddWildcardMatches(ResizeBatch batch, string input)
        {
            var absolutePattern = Path.GetFullPath(input);
            var directory = Path.GetDirectoryName(absolutePattern);
            var pattern = Path.GetFileName(absolutePattern);

            if (string.IsNullOrEmpty(directory) || ContainsWildcard(directory))
            {
                AddInputError(batch, input, Resources.CLI_ErrorWildcardInDirectory);
                return;
            }

            List<string> matches = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];

            if (matches.Count == 0)
            {
                AddInputError(batch, input, Resources.CLI_ErrorNoWildcardMatches);
                return;
            }

            foreach (var match in matches)
            {
                AddResolvedInput(batch, match, match);
            }
        }

        private static void AddResolvedInput(ResizeBatch batch, string input, string absolutePath)
        {
            var normalizedPath = Path.GetFullPath(absolutePath);
            if (!File.Exists(normalizedPath))
            {
                AddInputError(batch, input, Resources.CLI_ErrorFileNotFound);
                return;
            }

            if (!ValidImageExtensions.Contains(Path.GetExtension(normalizedPath)))
            {
                AddInputError(batch, input, Resources.CLI_ErrorUnsupportedFileType);
                return;
            }

            if (batch.TryAddResolvedInput(normalizedPath))
            {
                batch.Files.Add(normalizedPath);
            }
        }

        private bool TryAddResolvedInput(string path)
            => _resolvedInputPaths.Add(GetCanonicalPathKey(path));

        private static string GetCanonicalPathKey(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var apiPath = AddExtendedPathPrefix(fullPath);
            var finalPath = TryGetFinalPath(apiPath);
            if (finalPath != null)
            {
                return finalPath;
            }

            var longPath = TryGetLongPath(fullPath) ?? TryGetLongPath(apiPath) ?? fullPath;

            return RemoveExtendedPathPrefix(longPath);
        }

        private static string AddExtendedPathPrefix(string path)
        {
            const string extendedPathPrefix = @"\\?\";
            const string devicePathPrefix = @"\\.\";

            if (path.StartsWith(extendedPathPrefix, StringComparison.Ordinal) ||
                path.StartsWith(devicePathPrefix, StringComparison.Ordinal))
            {
                return path;
            }

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return string.Concat(@"\\?\UNC\", path.AsSpan(2));
            }

            return path.Length >= 3 &&
                   char.IsAsciiLetter(path[0]) &&
                   path[1] == ':' &&
                   path[2] == '\\'
                ? extendedPathPrefix + path
                : path;
        }

        private static string TryGetLongPath(string path)
        {
            var buffer = new StringBuilder(path.Length + 1);
            while (true)
            {
                var length = GetLongPathNameW(path, buffer, (uint)buffer.Capacity);
                if (length == 0)
                {
                    return null;
                }

                if (length < buffer.Capacity)
                {
                    return buffer.ToString();
                }

                buffer.EnsureCapacity(checked((int)length));
            }
        }

        private static string TryGetFinalPath(string path)
        {
            const uint fileShareRead = 0x00000001;
            const uint fileShareWrite = 0x00000002;
            const uint fileShareDelete = 0x00000004;
            const uint openExisting = 3;
            const uint fileFlagOpenReparsePoint = 0x00200000;
            const uint volumeNameNt = 0x00000002;

            // Resolve parent-directory aliases and filesystem casing while preserving the
            // final directory entry (for example, a hard link or symbolic link) as distinct.
            using SafeFileHandle handle = CreateFileW(
                path,
                0,
                fileShareRead | fileShareWrite | fileShareDelete,
                IntPtr.Zero,
                openExisting,
                fileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return null;
            }

            var buffer = new StringBuilder(path.Length + 1);
            while (true)
            {
                var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, volumeNameNt);
                if (length == 0)
                {
                    return null;
                }

                if (length < buffer.Capacity)
                {
                    return buffer.ToString();
                }

                buffer.EnsureCapacity(checked((int)length));
            }
        }

        private static string RemoveExtendedPathPrefix(string path)
        {
            const string extendedUncPrefix = @"\\?\UNC\";
            const string extendedPathPrefix = @"\\?\";

            if (path.StartsWith(extendedUncPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(@"\\", path.AsSpan(extendedUncPrefix.Length));
            }

            if (path.Length >= extendedPathPrefix.Length + 3 &&
                path.StartsWith(extendedPathPrefix, StringComparison.OrdinalIgnoreCase) &&
                char.IsAsciiLetter(path[extendedPathPrefix.Length]) &&
                path[extendedPathPrefix.Length + 1] == ':' &&
                path[extendedPathPrefix.Length + 2] == '\\')
            {
                path = path.Substring(extendedPathPrefix.Length);
            }

            return path.Length >= 2 && path[1] == ':'
                ? char.ToUpperInvariant(path[0]) + path.Substring(1)
                : path;
        }

        private static void AddInputError(ResizeBatch batch, string input, string message)
            => batch._inputErrors.Add(new ResizeError(input, message));

        private static bool ContainsWildcard(string value)
        {
            var startIndex = value.StartsWith(@"\\?\", StringComparison.Ordinal) ? 4 : 0;
            return value.IndexOf('*', startIndex) >= 0 || value.IndexOf('?', startIndex) >= 0;
        }

        public static ResizeBatch FromCommandLine(TextReader standardInput, string[] args)
        {
            var options = CliOptions.Parse(args);
            return FromCliOptions(standardInput, options);
        }

        public Task<IEnumerable<ResizeError>> ProcessAsync(Action<int, double> reportProgress, CancellationToken cancellationToken)
        {
            // NOTE: Settings.Default is captured once before parallel processing.
            // Any changes to settings on disk during this batch will NOT be reflected until the next batch.
            // This improves performance and predictability by avoiding repeated mutex acquisition and behaviour change results in a batch.
            return ProcessAsync(reportProgress, Settings.Default, cancellationToken);
        }

        public async Task<IEnumerable<ResizeError>> ProcessAsync(Action<int, double> reportProgress, Settings settings, CancellationToken cancellationToken)
        {
            double total = Files.Count;
            int completed = 0;
            var processingErrors = new ConcurrentBag<ResizeError>();

            await Parallel.ForEachAsync(
                Files,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                },
                async (file, ct) =>
                {
                    try
                    {
                        await ExecuteAsync(file, settings);
                    }
                    catch (Exception ex)
                    {
                        processingErrors.Add(new ResizeError(_fileSystem.Path.GetFileName(file), FormatErrorMessage(ex)));
                    }

                    Interlocked.Increment(ref completed);
                    reportProgress(completed, total);
                });

            return _inputErrors.Concat(processingErrors);
        }

        internal static string FormatErrorMessage(Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(exception.Message))
            {
                return exception.Message;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                Resources.CLI_ErrorProcessingFallback,
                exception.GetType().Name,
                exception.HResult);
        }

        protected virtual async Task ExecuteAsync(string file, Settings settings)
        {
            var aiService = _aiSuperResolutionService ?? NoOpAiSuperResolutionService.Instance;
            await new ResizeOperation(file, DestinationDirectory, settings, aiService).ExecuteAsync();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(
            SafeFileHandle fileHandle,
            StringBuilder filePath,
            uint filePathLength,
            uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetLongPathNameW(
            string shortPath,
            StringBuilder longPath,
            uint bufferLength);
    }
}
