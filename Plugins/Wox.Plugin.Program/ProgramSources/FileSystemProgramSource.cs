using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Log = Wox.Infrastructure.Logger.Log;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public class FileSystemProgramSource : AbstractProgramSource
    {
        private string baseDirectory;

        public FileSystemProgramSource(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
        }

        public FileSystemProgramSource(ProgramSource source)
            : this(source.Location)
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
                    if (ProgramStorage.Instance.ProgramSuffixes.Split(';').Any(o => file.EndsWith("." + o)))
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
            catch (Exception e)
            {
                Log.Warn(string.Format("GetAppFromDirectory failed: {0} - {1}", path, e.Message));
            }
        }

        public override string ToString()
        {
            return typeof(FileSystemProgramSource).Name + ":" + this.baseDirectory;
        }
    }
}
