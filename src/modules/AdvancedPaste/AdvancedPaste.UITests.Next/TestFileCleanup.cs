// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.UITests;

internal static class TestFileCleanup
{
    internal static void Run(IEnumerable<string> generatedFiles, DirectoryInfo? testDirectory, bool testFailed, Action stopScope, Action<string> reportFailure)
    {
        var failures = new List<Exception>();
        try
        {
            foreach (var path in generatedFiles)
            {
                Attempt(
                    () =>
                    {
                        File.Delete(path);
                        var parent = Path.GetDirectoryName(path)!;
                        if (Path.GetFileName(parent).StartsWith("PowerToys_AdvancedPaste_", StringComparison.Ordinal) &&
                            Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                        {
                            Directory.Delete(parent);
                        }
                    },
                    path);
            }

            if (testDirectory is not null)
            {
                Attempt(() => testDirectory.Delete(recursive: true), testDirectory.FullName);
            }
        }
        finally
        {
            // A failed conversion or cleanup must not leave its scope available to the next test.
            if (testFailed || failures.Count > 0)
            {
                try
                {
                    stopScope();
                }
                catch (AggregateException exception)
                {
                    failures.Add(exception);
                    reportFailure($"Could not completely stop the failed paste test scope: {exception}");
                }
            }
        }

        if (!testFailed && failures.Count > 0)
        {
            throw new AggregateException("Temporary paste files could not be cleaned up.", failures);
        }

        void Attempt(Action cleanup, string path)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(exception);
                reportFailure($"Could not remove temporary paste artifact '{path}': {exception}");
            }
        }
    }
}
