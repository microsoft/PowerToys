// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;

namespace Microsoft.PowerToys.Settings.UI.OOBE.ViewModel
{
    public class OobeShellViewModel
    {
        public ObservableCollection<OobePowerToysModule> Modules { get; } = new();

        public OobeShellViewModel()
        {
            Modules = new ObservableCollection<OobePowerToysModule>(
            new (PowerToysModules Module, bool IsNew)[]
            {
            (PowerToysModules.Overview, false),
            (PowerToysModules.AdvancedPaste, true),
            (PowerToysModules.AlwaysOnTop, false),
            (PowerToysModules.Awake, false),
            (PowerToysModules.CmdNotFound, false),
            (PowerToysModules.CmdPal, true),
            (PowerToysModules.ColorPicker, false),
            (PowerToysModules.CropAndLock, false),
            (PowerToysModules.EnvironmentVariables, false),
            (PowerToysModules.FancyZones, false),
            (PowerToysModules.FileLocksmith, false),
            (PowerToysModules.FileExplorer, false),
            (PowerToysModules.ImageResizer, false),
            (PowerToysModules.KBM, false),
            (PowerToysModules.LightSwitch, true),
            (PowerToysModules.MouseUtils, false),
            (PowerToysModules.MouseWithoutBorders, false),
            (PowerToysModules.Peek, false),
            (PowerToysModules.PowerRename, false),
            (PowerToysModules.Run, false),
            (PowerToysModules.QuickAccent, false),
            (PowerToysModules.ShortcutGuide, false),
            (PowerToysModules.TextExtractor, false),
            (PowerToysModules.MeasureTool, false),
            (PowerToysModules.Hosts, false),
            (PowerToysModules.Workspaces, true),
            (PowerToysModules.RegistryPreview, false),
            (PowerToysModules.NewPlus, true),
            (PowerToysModules.ZoomIt, true),
            }
            .Select(x => new OobePowerToysModule
            {
                ModuleName = x.Module.ToString(),
                IsNew = x.IsNew,
            }));
        }

        public OobePowerToysModule GetModule(PowerToysModules module)
        {
            return Modules.First(m => m.ModuleName == module.ToString());
        }

        public OobePowerToysModule GetModuleFromTag(string tag)
        {
            if (!Enum.TryParse<PowerToysModules>(tag, ignoreCase: true, out var module))
            {
                throw new ArgumentException($"Invalid module tag: {tag}", nameof(tag));
            }

            return GetModule(module);
        }
    }
}
