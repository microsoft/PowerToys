// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    public sealed record PowerScriptsCatalogSnapshot(string? HostPath, IReadOnlyList<PowerScriptInfo> Scripts, string? ErrorMessage)
    {
        public bool Succeeded => ErrorMessage is null;
    }
}
