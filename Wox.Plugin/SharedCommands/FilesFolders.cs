using System;
using System.IO;
using System.Windows;

namespace Wox.Plugin.SharedCommands
{
    public static class FilesFolders
    {
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
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Copying path {0} has failed, it will now be deleted for consistency", targetPath));
                RemoveFolder(targetPath);
#endif
            }

        }

        public static bool VerifyBothFolderFilesEqual(this string fromPath, string toPath)
        {
            try
            {
                var fromDir = new DirectoryInfo(fromPath);
                var toDir = new DirectoryInfo(toPath);

                if (fromDir.GetFiles("*", SearchOption.AllDirectories).Length != toDir.GetFiles("*", SearchOption.AllDirectories).Length)
                    return false;

                if (fromDir.GetDirectories("*", SearchOption.AllDirectories).Length != toDir.GetDirectories("*", SearchOption.AllDirectories).Length)
                    return false;

                return true;
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Unable to verify folders and files between {0} and {1}", fromPath, toPath));
                return false;
#endif
            }

        }

        public static void RemoveFolder(this string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Not able to delete folder {0}, please go to the location and manually delete it", path));
#endif
            }
        }
    }
}
