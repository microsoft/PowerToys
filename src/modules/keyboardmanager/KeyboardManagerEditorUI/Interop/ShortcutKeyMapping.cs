// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Interop
{
    // Condition for a single-key remap (dual-key / tap-alone). "Always" is the legacy unconditional
    // behavior; "Alone" applies the remap only when the source key is tapped by itself.
    public enum SingleKeyRemapCondition
    {
        Always = 0,
        Alone = 1,
    }

    public class ShortcutKeyMapping
    {
        public string OriginalKeys { get; set; } = string.Empty;

        public string TargetKeys { get; set; } = string.Empty;

        public string TargetApp { get; set; } = string.Empty;

        public ShortcutOperationType OperationType { get; set; }

        public string TargetText { get; set; } = string.Empty;

        public string ProgramPath { get; set; } = string.Empty;

        public string ProgramArgs { get; set; } = string.Empty;

        public string StartInDirectory { get; set; } = string.Empty;

        public ElevationLevel Elevation { get; set; } = ElevationLevel.NonElevated;

        public ProgramAlreadyRunningAction IfRunningAction { get; set; } = ProgramAlreadyRunningAction.ShowWindow;

        public StartWindowType Visibility { get; set; } = StartWindowType.Normal;

        public string UriToOpen { get; set; } = string.Empty;

        // Condition for single-key remaps (Always/Alone). Only meaningful for single-key key-to-key
        // or key-to-shortcut remaps; ignored for shortcuts and text remaps.
        public SingleKeyRemapCondition Condition { get; set; } = SingleKeyRemapCondition.Always;

        public enum ElevationLevel
        {
            NonElevated = 0,
            Elevated = 1,
            DifferentUser = 2,
        }

        public enum StartWindowType
        {
            Normal = 0,
            Hidden = 1,
            Minimized = 2,
            Maximized = 3,
        }

        public enum ProgramAlreadyRunningAction
        {
            ShowWindow = 0,
            StartAnother = 1,
            DoNothing = 2,
            Close = 3,
            EndTask = 4,
            CloseAndEndTask = 5,
        }

        public override bool Equals(object? obj)
        {
            if (obj is not ShortcutKeyMapping other)
            {
                return false;
            }

            return OriginalKeys == other.OriginalKeys &&
                   TargetKeys == other.TargetKeys &&
                   TargetApp == other.TargetApp &&
                   OperationType == other.OperationType &&
                   TargetText == other.TargetText &&
                   ProgramPath == other.ProgramPath &&
                   ProgramArgs == other.ProgramArgs &&
                   StartInDirectory == other.StartInDirectory &&
                   Elevation == other.Elevation &&
                   IfRunningAction == other.IfRunningAction &&
                   Visibility == other.Visibility &&
                   UriToOpen == other.UriToOpen &&
                   Condition == other.Condition;
        }

        public override int GetHashCode()
        {
            HashCode hash = default(HashCode);
            hash.Add(OriginalKeys);
            hash.Add(TargetKeys);
            hash.Add(TargetApp);
            hash.Add(OperationType);
            hash.Add(TargetText);
            hash.Add(ProgramPath);
            hash.Add(ProgramArgs);
            hash.Add(StartInDirectory);
            hash.Add(Elevation);
            hash.Add(IfRunningAction);
            hash.Add(Visibility);
            hash.Add(UriToOpen);
            hash.Add(Condition);
            return hash.ToHashCode();
        }
    }
}
