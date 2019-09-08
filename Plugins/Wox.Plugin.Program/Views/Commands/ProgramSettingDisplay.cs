using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Plugin.Program.Views.Models;

namespace Wox.Plugin.Program.Views.Commands
{
    internal static class ProgramSettingDisplay
    {
        internal static List<ProgramSource> LoadProgramSources(this List<Settings.ProgramSource> programSources)
        {
            var list = new List<ProgramSource>();

            programSources.ForEach(x => list.Add(new ProgramSource { Enabled = x.Enabled, Location = x.Location, Name = x.Name }));

            return list;
        }

        internal static void LoadAllApplications(this List<ProgramSource> listToUpdate)
        {
            Main._win32s                
                .ToList()
                .ForEach(t1 => ProgramSetting.ProgramSettingDisplayList.Add(new ProgramSource { Name = t1.Name, Location = t1.ParentDirectory, Enabled = t1.Enabled }));

            Main._uwps
                .ToList()
                .ForEach(t1 => ProgramSetting.ProgramSettingDisplayList.Add(new ProgramSource { Name = t1.DisplayName, Location = t1.Package.Location, Enabled = t1.Enabled }));
        }
    }
}
