// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// The resolved input/output contract emitted by <c>PowerScripts.Host.exe list --json</c>.
    /// </summary>
    public sealed class PowerScriptIo
    {
        public string Input { get; set; } = string.Empty;

        public string Output { get; set; } = string.Empty;
    }
}
