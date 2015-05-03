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
        private int maxDepth;
        private string suffixes;

        public FileSystemProgramSource(string baseDirectory, int maxDepth, string suffixes)
        {
            this.baseDirectory = baseDirectory;
            this.maxDepth = maxDepth;
            this.suffixes = suffixes;
        }

        public FileSystemProgramSource(string baseDirectory)
            : this(baseDirectory, -1, "") {}

        public FileSystemProgramSource(ProgramSource source)
            : this(source.Location, source.MaxDepth, source.Suffixes)
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
            GetAppFromDirectory(path, list, 0);
        }

        private void GetAppFromDirectory(string path, List<Program> list, int depth)
        {
            if(maxDepth != -1 && depth > maxDepth)
            {
                return;
            }
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (ProgramStorage.Instance.ProgramSuffixes.Split(';').Any(o => file.EndsWith("." + o)) ||
                        suffixes.Split(';').Any(o => file.EndsWith("." + o)))
                    {
                        Program p = CreateEntry(file);
                        list.Add(p);
                    }
                }

                foreach (var subDirectory in Directory.GetDirectories(path))
                {
                    GetAppFromDirectory(subDirectory, list, depth + 1);
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
