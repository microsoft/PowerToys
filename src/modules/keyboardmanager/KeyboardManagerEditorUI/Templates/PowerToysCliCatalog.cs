// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class PowerToysCliCatalog
    {
        public int SchemaVersion { get; init; }

        public List<CommandTemplateModule> Modules { get; init; } = new();
    }
}
