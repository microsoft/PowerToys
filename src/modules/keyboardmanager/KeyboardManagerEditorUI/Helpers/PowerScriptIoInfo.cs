// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>The resolved input/output data shapes a PowerScript declares (from the host's <c>io</c> block).</summary>
    public sealed class PowerScriptIoInfo
    {
        public string Input { get; init; } = string.Empty;

        public string Output { get; init; } = string.Empty;
    }
}
