// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Wox.Plugin.Logger;
using Wox.Plugin.Properties;

namespace Wox.Plugin.SharedCommands
{
    public static class FilesFolders
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static void Copy(this string sourcePath, string targetPath)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourcePath);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourcePath);
            }

            try
            {
                DirectoryInfo[] dirs = dir.GetDirectories();

                // If the destination directory doesn't exist, create it.
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    string temppath = Path.Combine(targetPath, file.Name);
                    file.CopyTo(temppath, false);
                }

                // Recursively copy subdirectories by calling itself on each subdirectory until there are no more to copy
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(targetPath, subdir.Name);
                    Copy(subdir.FullName, temppath);
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used. Due to #if debug vs release statement
            catch (Exception e)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                string error = $"Copying path {targetPath} has failed";
                Log.Exception(error, e, MethodBase.GetCurrentMethod().DeclaringType);
#if DEBUG
                throw;
#else
                // Using CurrentCulture since this is user facing
                System.Windows.MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.filesfolder_copy_failed, targetPath));
                RemoveFolder(targetPath);
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static bool VerifyBothFolderFilesEqual(this string fromPath, string toPath)
        {
            try
            {
                var fromDir = new DirectoryInfo(fromPath);
                var toDir = new DirectoryInfo(toPath);

                if (fromDir.GetFiles("*", SearchOption.AllDirectories).Length != toDir.GetFiles("*", SearchOption.AllDirectories).Length)
                {
                    return false;
                }

                if (fromDir.GetDirectories("*", SearchOption.AllDirectories).Length != toDir.GetDirectories("*", SearchOption.AllDirectories).Length)
                {
                    return false;
                }

                return true;
            }
#pragma warning disable CS0168 // Variable is declared but never used. Due to #if debug vs release statement
            catch (Exception e)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                string error = $"Unable to verify folders and files between {fromPath} and {toPath}";
                Log.Exception(error, e, MethodBase.GetCurrentMethod().DeclaringType);
#if DEBUG
                throw;
#else
                // Using CurrentCulture since this is user facing
                System.Windows.MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.filesfolder_verifybothfolderfilesequal_failed, fromPath, toPath));
                return false;
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static void RemoveFolder(this string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used. Due to #if debug vs release statement
            catch (Exception e)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                string error = $"Not able to delete folder {path}";
                Log.Exception(error, e, MethodBase.GetCurrentMethod().DeclaringType);
#if DEBUG
                throw;
#else
                // Using CurrentCulture since this is user facing
                System.Windows.MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.filesfolder_removefolder_failed, path));
#endif
            }
        }
    }
}
