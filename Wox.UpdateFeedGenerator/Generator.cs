using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;

namespace Wox.UpdateFeedGenerator
{
    public class Generator
    {
        private string OutputDirectory;
        private string SourceDirectory;
        private string BaseURL = ConfigStorage.Instance.BaseURL;
        private string feedXMLPath;
        private bool checkVersion = ConfigStorage.Instance.CheckVersion;
        private bool checkSize = ConfigStorage.Instance.CheckSize;
        private bool checkDate = ConfigStorage.Instance.CheckDate;
        private bool checkHash = ConfigStorage.Instance.CheckHash;

        public Generator()
        {
            OutputDirectory = Path.GetFullPath(ConfigStorage.Instance.OutputDirectory);
            SourceDirectory = Path.GetFullPath(ConfigStorage.Instance.SourceDirectory);
            feedXMLPath = Path.Combine(ConfigStorage.Instance.OutputDirectory, ConfigStorage.Instance.FeedXMLName);
        }

        private List<FileInfoEx> ReadSourceFiles()
        {
            List<FileInfoEx> files = new List<FileInfoEx>();
            FileSystemEnumerator enumerator = new FileSystemEnumerator(SourceDirectory, "*.*", true);
            foreach (FileInfo fi in enumerator.Matches())
            {
                string file = fi.FullName;
                if ((IsIgnorable(file))) continue;
                FileInfoEx thisInfo = new FileInfoEx(file, SourceDirectory.Length);
                files.Add(thisInfo);
            }
            return files;
        }

        private bool IsIgnorable(string thisFile)
        {
            return false;
        }

        public void Build()
        {
            Console.WriteLine("Building Wox update feed");
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "utf-8", null);

            doc.AppendChild(dec);
            XmlElement feed = doc.CreateElement("Feed");
            feed.SetAttribute("BaseUrl", BaseURL.Trim());
            doc.AppendChild(feed);

            XmlElement tasks = doc.CreateElement("Tasks");

            foreach (FileInfoEx file in ReadSourceFiles())
            {
                Console.WriteLine("adding {0} to feed xml.", file.FileInfo.FullName);
                XmlElement task = doc.CreateElement("FileUpdateTask");
                task.SetAttribute("localPath", file.RelativeName);

                // generate FileUpdateTask metadata items
                task.SetAttribute("lastModified", file.FileInfo.LastWriteTime.ToFileTime().ToString(CultureInfo.InvariantCulture));
                task.SetAttribute("fileSize", file.FileInfo.Length.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(file.FileVersion)) task.SetAttribute("version", file.FileVersion);

                XmlElement conds = doc.CreateElement("Conditions");
                XmlElement cond;
                bool hasFirstCondition = false;

                //File Exists
                cond = doc.CreateElement("FileExistsCondition");
                cond.SetAttribute("type", "or");
                conds.AppendChild(cond);

                //Version
                if (checkVersion && !string.IsNullOrEmpty(file.FileVersion))
                {
                    cond = doc.CreateElement("FileVersionCondition");
                    cond.SetAttribute("what", "below");
                    cond.SetAttribute("version", file.FileVersion);
                    conds.AppendChild(cond);
                    hasFirstCondition = true;
                }

                //Size
                if (checkSize)
                {
                    cond = doc.CreateElement("FileSizeCondition");
                    cond.SetAttribute("type", hasFirstCondition ? "or-not" : "not");
                    cond.SetAttribute("what", "is");
                    cond.SetAttribute("size", file.FileInfo.Length.ToString(CultureInfo.InvariantCulture));
                    conds.AppendChild(cond);
                }

                //Date
                if (checkDate)
                {
                    cond = doc.CreateElement("FileDateCondition");
                    if (hasFirstCondition) cond.SetAttribute("type", "or");
                    cond.SetAttribute("what", "older");
                    // local timestamp, not UTC
                    cond.SetAttribute("timestamp", file.FileInfo.LastWriteTime.ToFileTime().ToString(CultureInfo.InvariantCulture));
                    conds.AppendChild(cond);
                }

                //Hash
                if (checkHash)
                {
                    cond = doc.CreateElement("FileChecksumCondition");
                    cond.SetAttribute("type", hasFirstCondition ? "or-not" : "not");
                    cond.SetAttribute("checksumType", "sha256");
                    cond.SetAttribute("checksum", file.Hash);
                    conds.AppendChild(cond);
                }

                task.AppendChild(conds);
                tasks.AppendChild(task);
                string destFile = Path.Combine(OutputDirectory, file.RelativeName);
                CopyFile(file.FileInfo.FullName, destFile);
            }
            feed.AppendChild(tasks);
            doc.Save(feedXMLPath);
        }

        private bool CopyFile(string sourceFile, string destFile)
        {
            // If the target folder doesn't exist, create the path to it
            var fi = new FileInfo(destFile);
            var d = Directory.GetParent(fi.FullName);
            if (!Directory.Exists(d.FullName)) CreateDirectoryPath(d.FullName);

            // Copy with delayed retry
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    if (File.Exists(destFile)) File.Delete(destFile);
                    File.Copy(sourceFile, destFile);
                    retries = 0; // success
                    return true;
                }
                catch (IOException)
                {
                    // Failed... let's try sleeping a bit (slow disk maybe)
                    if (retries-- > 0) Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException)
                {
                    // same handling as IOException
                    if (retries-- > 0) Thread.Sleep(200);
                }
            }
            return false;
        }

        private void CreateDirectoryPath(string directoryPath)
        {
            // Create the folder/path if it doesn't exist, with delayed retry
            int retries = 3;
            while (retries > 0 && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                if (retries-- < 3) Thread.Sleep(200);
            }
        }

    }
}
