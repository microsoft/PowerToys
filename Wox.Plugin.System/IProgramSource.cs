using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.System
{
    public interface IProgramSource
    {
        List<Program> LoadPrograms();
        int BonusPoints { get; set; }
    }

    public abstract class AbstractProgramSource : IProgramSource
    {
        public abstract List<Program> LoadPrograms();

        public int BonusPoints
        {
            get; set;
        }

        protected Program CreateEntry(string file)
        {
            Program p = new Program()
            {
                Title = global::System.IO.Path.GetFileNameWithoutExtension(file),
                IcoPath = file,
                ExecutePath = file
            };

            return p;
        }
    }
}
