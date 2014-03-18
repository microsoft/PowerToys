using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Wox.Plugin.System
{
    public class FileSystemProgramSource : AbstractProgramSource
    {
        public string BaseDirectory;
        public List<string> Suffixes = new List<string>() { "lnk", "exe" };

        public FileSystemProgramSource(string baseDirectory)
        {
            BaseDirectory = baseDirectory;
        }

        public FileSystemProgramSource(string baseDirectory, List<string> suffixes)
            : this(baseDirectory)
        {
            Suffixes = suffixes;
        }

        public FileSystemProgramSource(Wox.Infrastructure.UserSettings.ProgramSource source)
            : this(source.Location)
        {
            this.BonusPoints = source.BounsPoints;
        }

        public override List<Program> LoadPrograms()
        {
            List<Program> list = new List<Program>();
            GetAppFromDirectory(BaseDirectory, list);

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
    }
}
