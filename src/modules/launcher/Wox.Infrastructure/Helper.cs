// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure
{
    public static class Helper
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IFileInfoFactory FileInfo = FileSystem.FileInfo;
        private static readonly IDirectory Directory = FileSystem.Directory;

        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T NonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            else
            {
                return obj;
            }
        }

        public static void RequireNonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
        }

        public static void ValidateDataDirectory(string bundledDataDirectory, string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            foreach (var bundledDataPath in Directory.GetFiles(bundledDataDirectory))
            {
                var data = Path.GetFileName(bundledDataPath);
                var dataPath = Path.Combine(dataDirectory, data.NonNull());
                if (!File.Exists(dataPath))
                {
                    File.Copy(bundledDataPath, dataPath);
                }
                else
                {
                    var time1 = FileInfo.FromFileName(bundledDataPath).LastWriteTimeUtc;
                    var time2 = FileInfo.FromFileName(dataPath).LastWriteTimeUtc;
                    if (time1 != time2)
                    {
                        File.Copy(bundledDataPath, dataPath, true);
                    }
                }
            }
        }

        public static void ValidateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string Formatted<T>(this T t)
        {
            var formatted = JsonSerializer.Serialize(t, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
                {
                    new JsonStringEnumConverter(),
                },
            });

            return formatted;
        }

        // Function to run as admin for context menu items
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static void RunAsAdmin(string path)
        {
            var info = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                Verb = "runas",
                UseShellExecute = true,
            };

            try
            {
                Process.Start(info);
            }
            catch (System.Exception ex)
            {
                Log.Exception($"Unable to Run {path} as admin : {ex.Message}", ex, MethodBase.GetCurrentMethod().DeclaringType);
            }
        }

        public static Process OpenInConsole(string path)
        {
            var processStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = path,
                FileName = "cmd.exe",
            };

            return Process.Start(processStartInfo);
        }

        public static bool OpenInShell(string path, string arguments = null, string workingDir = null, bool runAsAdmin = false, bool runWithHiddenWindow = false)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = path;
                process.StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? string.Empty : workingDir;
                process.StartInfo.Arguments = string.IsNullOrWhiteSpace(arguments) ? string.Empty : arguments;
                process.StartInfo.WindowStyle = runWithHiddenWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;

                if (runAsAdmin)
                {
                    process.StartInfo.Verb = "RunAs";
                }

                process.StartInfo.UseShellExecute = true;

                try
                {
                    process.Start();
                    return true;
                }
                catch (Win32Exception ex)
                {
                    Log.Exception($"Unable to open {path}: {ex.Message}", ex, MethodBase.GetCurrentMethod().DeclaringType);
                    return false;
                }
            }
        }
    }
}
