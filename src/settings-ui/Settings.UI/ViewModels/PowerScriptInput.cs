// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// The declared file-input contract for a script. Mirrors the <c>input</c> object emitted by
    /// <c>PowerScripts.Host.exe list --json</c>.
    /// </summary>
    public sealed class PowerScriptInput
    {
        public List<string> Extensions { get; set; } = new();
    }
}
