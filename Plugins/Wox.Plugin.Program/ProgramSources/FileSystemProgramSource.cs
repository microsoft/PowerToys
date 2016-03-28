using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public class FileSystemProgramSource : AbstractProgramSource
    {
        private string _baseDirectory;
        private int _maxDepth;
        private string[] _suffixes;

        public FileSystemProgramSource(string baseDirectory, int maxDepth, string[] suffixes)
        {
            _baseDirectory = baseDirectory;
            _maxDepth = maxDepth;
            _suffixes = suffixes;
        }

        public FileSystemProgramSource(string baseDirectory, string[] suffixes)
            : this(baseDirectory, -1, suffixes) {}

        public FileSystemProgramSource(ProgramSource source)
            : this(source.Location, source.MaxDepth, source.Suffixes)
        {
            BonusPoints = source.BonusPoints;
        }

        public override List<Program> LoadPrograms()
        {
            List<Program> list = new List<Program>();
            if (Directory.Exists(_baseDirectory))
            {
                GetAppFromDirectory(_baseDirectory, list);
                FileChangeWatcher.AddWatch(_baseDirectory, _suffixes);
            }
            return list;
        }

        private void GetAppFromDirectory(string path, List<Program> list)
        {
            GetAppFromDirectory(path, list, 0);
        }

        private void GetAppFromDirectory(string path, List<Program> list, int depth)
        {
            if(_maxDepth != -1 && depth > _maxDepth)
            {
                return;
            }
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (_suffixes.Any(o => file.EndsWith("." + o)))
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
                var woxPluginException = new WoxPluginException("Program", $"GetAppFromDirectory failed: {path}", e);
                Log.Error(woxPluginException);
            }
        }

        public override string ToString()
        {
            return typeof(FileSystemProgramSource).Name + ":" + _baseDirectory;
        }
    }
}
