// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerScripts.Client;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Presentation adapter for the shared script discovery model.</summary>
    public sealed class PowerScriptInfo
    {
        public PowerScriptInfo(PowerScripts.Client.PowerScriptInfo script)
        {
            Script = script;
        }

        public PowerScripts.Client.PowerScriptInfo Script { get; }

        public string Id => Script.Id;

        public string Name => Script.Name;

        public string Description => Script.Description;

        public bool Trusted => Script.Trusted;

        public IReadOnlyList<PowerScriptParameter> Parameters => Script.Parameters;

        public override string ToString() => Name;
    }
}
