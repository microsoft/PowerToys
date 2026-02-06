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
    }
}
