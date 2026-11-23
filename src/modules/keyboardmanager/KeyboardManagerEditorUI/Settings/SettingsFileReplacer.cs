// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>
    /// Replaces a Keyboard Manager file with a fully written temporary file in one step, so a
    /// reader never sees a half-written file and a failed write leaves the old file intact.
    /// </summary>
    internal static class SettingsFileReplacer
    {
        private const int ErrorAccessDenied = 5;

        private const int ErrorSharingViolation = 32;

        private const int Attempts = 10;

        private const int RetryDelayMS = 50;

        /// <summary>
        /// Moves <paramref name="temporaryPath"/> over <paramref name="targetPath"/>. Replacing needs
        /// delete access to the target, which a reader that has it open for a moment (the engine,
        /// the runner, Settings) does not share. Windows then reports a sharing violation or, more
        /// often, access denied, so both are retried for a short while. A read-only target fails
        /// at once: waiting would not change it. Throws when the replace fails.
        /// </summary>
        internal static void Replace(string temporaryPath, string targetPath)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, targetPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (attempt < Attempts && IsTransient(ex, targetPath))
                {
                    Thread.Sleep(RetryDelayMS);
                }
            }
        }

        private static bool IsTransient(Exception ex, string targetPath)
        {
            int code = ex.HResult & 0xFFFF;
            if (ex is IOException && code == ErrorSharingViolation)
            {
                return true;
            }

            bool accessDenied = ex is UnauthorizedAccessException || (ex is IOException && code == ErrorAccessDenied);
            return accessDenied && !IsReadOnly(targetPath);
        }

        private static bool IsReadOnly(string path)
        {
            try
            {
                return File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
            }
            catch (Exception)
            {
                // The attributes could not be read either: treat it as permanent.
                return true;
            }
        }
    }
}
