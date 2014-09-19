using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program.ProgramSources
{
    public class FileSystemProgramSource : AbstractProgramSource
    {
        private string baseDirectory;

        public FileSystemProgramSource(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
        }

        public FileSystemProgramSource(ProgramSource source):this(source.Location)
        {
            this.BonusPoints = source.BonusPoints;
        }

        public override List<Program> LoadPrograms()
        {
            List<Program> list = new List<Program>();
            if (Directory.Exists(baseDirectory))
            {
                GetAppFromDirectory(baseDirectory, list);
                FileChangeWatcher.AddWatch(baseDirectory);
            }
            return list;
        }

        private void GetAppFromDirectory(string path, List<Program> list)
        {
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (UserSettingStorage.Instance.ProgramSuffixes.Split(';').Any(o => file.EndsWith("." + o)))
                    {
                        Program p = CreateEntry(file);
                        list.Add(p);
                    }
                }

                foreach (var subDirectory in Directory.GetDirectories(path))
                {
                    GetAppFromDirectory(subDirectory, list);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.WriteLine(string.Format("Can't access to directory {0}", path), "WoxDebug");
            }
            catch (DirectoryNotFoundException e)
            {
                //no-operation
            }
        }

        public override string ToString()
        {
            return typeof(FileSystemProgramSource).Name + ":" + this.baseDirectory;
        }
    }
}
