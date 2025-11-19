// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using PowerToys.GPOWrapper;

namespace ManagedCommon
{
    public interface IPowerToysModule
    {
        public string Name { get; }

        public void Enable();

        public void Disable();

        public bool Enabled { get; }

        public GpoRuleConfigured GpoRuleConfigured { get; }

        public Dictionary<HotkeyEx, Action> Hotkeys { get => []; }

        public Dictionary<string, Action> CustomActions { get => []; }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
        {
        }
    }
}
