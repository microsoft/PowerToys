using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program.ProgramSources
{
    public class FileSystemProgramSource : AbstractProgramSource
    {
        public string BaseDirectory;
        public List<string> Suffixes = new List<string>() { "lnk", "exe", "appref-ms" };

        public FileSystemProgramSource(string baseDirectory)
        {
            BaseDirectory = baseDirectory;
        }

        public FileSystemProgramSource(string baseDirectory, List<string> suffixes)
            : this(baseDirectory)
        {
            Suffixes = suffixes;
        }

        public FileSystemProgramSource(ProgramSource source)
            : this(source.Location)
        {
            this.BonusPoints = source.BonusPoints;
        }

        public override List<Program> LoadPrograms()
        {
            List<Program> list = new List<Program>();
            if (Directory.Exists(BaseDirectory))
            {
                GetAppFromDirectory(BaseDirectory, list);
            }
            return list;
        }

        private void GetAppFromDirectory(string path, List<Program> list)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (Suffixes.Any(o => file.EndsWith("." + o)))
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

        public override string ToString()
        {
            return typeof(FileSystemProgramSource).Name + ":" + this.BaseDirectory;
        }
    }
}
